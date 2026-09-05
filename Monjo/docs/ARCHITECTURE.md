# Monjo Architecture

Monjo is a small, provider-native persistence library. One shared capability-oriented
repository contract; each provider (MongoDB, PostgreSQL, SQLite) executes it with its own
native client. No ORM, no LINQ-to-SQL, no expression-tree building on hot paths.

> Verification note: this code was written in an environment without a .NET SDK or NuGet
> access. It has **not** been compiled or tested here; run `dotnet build` / `dotnet test`
> in an environment with the .NET 10 SDK before release.

---

## 1. Package layout

```
Monjo/
├─ src/
│  ├─ Monjo.Core          provider-agnostic: query model, result model, common repository
│  │                       contract, entity metadata, options, provider registry, DI wiring
│  │                       (deps: MS.Extensions.*.Abstractions only)
│  ├─ Monjo.Sql           internal SQL engine shared by the two SQL providers
│  │                       (deps: Core only; System.Data.Common; not published as a package)
│  ├─ Monjo.MongoDB       MongoDB.Driver 3.x provider + the preserved legacy
│  │                       Utilities.MongoDatabase API (deps: Core + MongoDB.Driver)
│  ├─ Monjo.PostgreSQL    Npgsql provider (deps: Core, Sql, Npgsql)
│  └─ Monjo.SQLite        Microsoft.Data.Sqlite provider (deps: Core, Sql, Microsoft.Data.Sqlite)
├─ tests/Monjo.Tests      cross-provider semantic suite (xunit)
└─ benchmarks/Monjo.Benchmarks   BenchmarkDotNet suite
```

A consumer references **only** the provider packages it uses. A Postgres-only app never
loads MongoDB.Driver. Monjo.Sql is internal plumbing (transitive, not NuGet-packable).

Deliberate choices:

- **No `Monjo.Abstractions` package.** The abstractions are small (4 interfaces) and live in
  Core; splitting them would force two package versions per provider for no benefit.
- **No Abstractions/Entities/Contracts ceremony.** This is a utility library, not a domain
  boundary.
- **No EF Core / Dapper.** Both would add a runtime (LINQ provider or SQL builder +
  micro-ORM) where direct parameterized commands and the native drivers are simpler and
  faster, and give Monjo full control of the generated SQL and the connection lifecycle.

## 2. The common contract

`Monjo.IMonjoRepository<T>` (Core):

| Operation | Notes |
|---|---|
| `GetByIdAsync` | identifier lookup; soft-deleted rows excluded |
| `FindOneAsync` | first match, page ignored, exactly one row fetched |
| `FindManyAsync` | match + optional page, **no count query** (one round-trip) |
| `QueryAsync` | full `MonjoQuery` → `MonjoFilteredResult<T>` with `TotalCount`/`PageCount` (two round-trips — the only common API that counts) |
| `CountAsync` / `ExistsAsync` | server-side count / `LIMIT 1` existence probe |
| `InsertAsync` / `InsertManyAsync` | identifier generated for `string`/`Guid` ids when null; a numeric id of `0` is rejected with `MonjoException` (see below); `[BsonId]` ids are driver-generated on Mongo |
| `UpdateAsync` | full-row replace of an existing row; stamps `Modified*` |
| `UpdateColumnsAsync` | partial update of named columns; stamps `Modified*` unless set |
| `UpsertAsync` | insert when id missing, full update otherwise (revives soft-deleted rows) |
| `DeleteAsync` / `DeleteManyAsync` | soft delete when the entity model has `IsDeleted`, else physical |
| `HardDeleteAsync` / `HardDeleteManyAsync` | physical delete, always |

Provider-specific APIs stay in provider namespaces:

- Mongo: the legacy `Utilities.MongoDatabase.IMonjoRepository<T>` (pipelines, `FilterDefinition`,
  cursors, `FindOneAndUpdate`, GridFS access via the legacy `IMonjoConnection.Database`) —
  preserved unchanged in Monjo.MongoDB.
- SQL: no extra surface in v1; the common contract is the whole surface.

### Identifier generation rule (all providers)

Monjo never invents numeric identifiers (none of the providers can generate them):

- `string` / `Guid` id, value `null` → a new id is generated (`Guid` / "N"-format string) on
  every provider; on Mongo a `[BsonId]` id (e.g. the legacy `BaseDocument.ObjectId`) is left
  for the driver to auto-generate.
- numeric id (`int`/`long`/`short`/`byte`), value `null` → `MonjoException` (deterministic,
  no silent behavior).
- numeric id, value `0` → `MonjoException`. A `0` cannot be distinguished from "unset", and
  silently inserting rows under id `0` would corrupt the identifier space; failing
  deterministically with a clear message is the intended behavior.

## 3. Provider resolution (startup, once)

```csharp
services.AddMonjo(configuration);   // binds MonjoOptions (section "Monjo", legacy "MonjoSettings")
services.UseMonjoMongoDB();         // exactly one of UseMonjoMongoDB/UseMonjoPostgreSql/UseMonjoSqlite
```

Flow:

1. `AddMonjo` binds `MonjoOptions` **once** (configuration is read a single time; no
   per-request config access). Provider name is normalized (`postgres` → `PostgreSQL`, ...).
2. The provider package registers a **named factory** in a process-wide registry
   (`MonjoProviderRegistration`). Registering two providers throws immediately: a process
   uses exactly one Monjo provider.
3. `IMonjoProvider` is a **singleton** resolved lazily once: the factory is looked up,
   validated against the configured provider name (mismatch → `MonjoProviderNotRegisteredException`
   with an actionable message), and the provider is constructed. No database call happens
   during registration or construction — clients connect lazily on first use.
4. `IMonjoConnection` is the same singleton connection; every repository shares it.

This mirrors the application's FileStorage resolver philosophy: configuration decides,
registration is explicit, selection is once-at-startup, never per request.

## 4. Query model and translation

`MonjoQuery` (namespace `Utilities.MongoDatabase.Filter`, preserved) describes WHAT:

```
Where : IList<IList<MonjoCondition>>   // AND of OR-groups; 12 comparison operators
Order : IList<MonjoOrder>              // column + direction
Page  : MonjoPage?                     // 1-based Index + Size; null = no paging
```

Providers translate it natively at execution time:

- **Mongo** (`MongoQueryTranslator`): builds a small `Func<T,bool>` predicate (compiled by the
  driver's LINQ translator) + sort definition. Column selectors are cached per (type, column).
- **SQL** (`SqlQueryTranslator`): builds a parameterized `WHERE`/`ORDER BY` fragment plus
  parameter list. The final statement is cached SQL text (built once per type per provider)
  + the translated fragment.

Translation is **not** cached per request as a compiled object — it is a linear, allocation-
minimal translation of a small model, which is cheaper than any cache lookup + lifecycle.
Everything expensive (column resolution, SQL templates, selector lambdas, row mappers) is
cached per type, built once per process.

### Operand conversion

Condition operands are usually strings (JSON model binding). `MonjoOperandConversion`
converts them identically for every provider: enum by name, invariant-culture numeric/date
parsing. Unknown columns fail fast with the entity/table named.

### Cross-provider semantic differences (read this before picking a provider)

The common contract is provider-identical in **semantics** (same filters, same soft-delete
rules, same result shape), but each provider's storage engine imposes a few differences that
can matter:

| Area | MongoDB | PostgreSQL | SQLite |
|---|---|---|---|
| `Contains`/`NotContains` | `$regex` — **case-sensitive** | `LIKE` — **case-sensitive** | `LIKE` — **case-insensitive for ASCII** (SQLite default; no portable per-query override is used) |
| `decimal` storage | BSON Decimal128 (exact) | native `NUMERIC(57,28)` (exact) | **fixed-width sortable TEXT encoding** (58 chars: sign bit + 29 int digits + 28 frac digits, 9's-complement for negatives) — exact, and equality/`<`/`>`/`ORDER BY` are numerically correct |
| `Guid` storage | BSON Guid (binary) | native `uuid` | TEXT in "N" format — equality fine, but `ORDER BY` is lexicographic, not binary |
| `DateTime` precision | full .NET tick precision (BSON Int64 ticks) | `timestamptz` — **truncated to microseconds** | UTC TEXT — full .NET precision |
| `bool` storage | BSON bool | `BOOLEAN` | INTEGER `0`/`1` |
| `enum` storage | Int32 (driver default) | TEXT (member name) | TEXT (member name) |
| `null` vs missing | a missing BSON field and an explicit `null` both match `IsNull` | column is `NULL` | column is `NULL` |
| `UpdateColumnsAsync` return | `ModifiedCount` — documents actually changed (a no-op `$set` may count 0) | rows **matched** by the WHERE clause (a no-op update still counts) | rows **matched** by the WHERE clause (a no-op update still counts) |
| Transactions | require a **replica set**; standalone → `MonjoNotSupportedException` | always available | always available (WAL; a single writer) |
| Identifier sort order | ObjectId = time-ordered; Guid = 12-byte binary | `uuid` = binary | Guid "N" text = lexicographic |
| Table/collection naming | unqualified collection name | **unqualified table names** — resolved by the connection's `search_path` (public schema unless configured otherwise) | file is the database; table names are case-folded per SQLite rules (quoted, so exact) |

Where a difference is unavoidable (case sensitivity of `Contains` on SQLite, `Guid` sort
order, `UpdateColumnsAsync` counting, microsecond truncation on PostgreSQL), Monjo
**documents it instead of hiding it** — code that depends on one of these must be written
against the target provider, or normalized in the application layer.

## 5. Entity metadata

`MonjoEntityMetadata` (Core) — built lazily once per type, cached process-wide, zero
reflection on hot paths:

- table/collection name: `[MonjoTable]` (or the legacy `[MonjoCollectionName]`, which now
  derives from it), else the CLR type name;
- identifier: `[MonjoId]`, else a property named `Id` of `string`/`Guid`/`int`/`long`;
- columns: every public readable property except `[MonjoIgnore]`; `[MonjoColumn("name")]`
  renames;
- audit fields by convention: `CreatedBy(Info)`, `CreatedMoment`, `ModifiedBy(Info)`,
  `ModifiedMoment`, `DeletedBy(Info)`, `DeletedMoment`, `IsDeleted` (presence of `IsDeleted`
  enables soft delete);
- indexes: `[MonjoIndex("A,B", unique: true, descending: new[] {...})]` — composite,
  unique, direction — the intersection expressible on every provider. Provider-specific
  index features remain on provider APIs (e.g. Mongo `IMonjoIndexBuilder<T>`).

The SQL engine adds `SqlEntityMetadata` (per type **per provider**): column ordinals, SQL
types, and pre-built SQL templates (SELECT/INSERT/UPDATE/UPSERT/DELETE/COUNT/EXISTS/DDL).
The Mongo engine adds a per-type selector cache.

`BaseEntity` (Core) is the common entity base (same property names as the legacy
`BaseDocument`); `BaseDocument` (Monjo.MongoDB) is the legacy Mongo base, preserved with
its BSON attributes (`[BsonId]`, `[BsonRepresentation(ObjectId)]`) so existing collections
are read exactly as before.

Audit identity: `MonjoActorContext.SetProvider(() => ...)` is called **once at startup**;
each write performs one delegate call + a value-type `MonjoActor` — no DI lookups, no
allocations in the write path. Applications bridge their request context (the M1Mentor
app bridges `CurrentRequestContext.User`).

## 6. Connection model

- **MongoDB**: one reused `IMongoClient` (thread-safe by driver design); cached
  `IMongoCollection<T>` handles. No connections per query (the driver manages them).
- **PostgreSQL**: Npgsql pooling. Each operation acquires/releases **one pooled
  connection**; a transaction borrows one dedicated pooled connection for its lifetime.
- **SQLite**: Microsoft.Data.Sqlite pooling + WAL journal mode (set once per process per
  database; the `PRAGMA journal_mode=WAL` statement returns a result row, so it is executed
  as a reader and the row consumed — never via `ExecuteNonQueryAsync`) + busy timeout.
  Readers stay concurrent; writers are serialized by SQLite (documented behavior for
  embedded use). `busy`/`locked` map to `MonjoBusyException` with guidance.

No physical connect in the request hot path for any provider.

## 7. Transactions

`IMonjoConnection.BeginTransactionAsync()` → `MonjoTransaction` (commit / rollback /
dispose-rolls-back-if-open). While open, the transaction is **ambient** through
`AsyncLocal` (same isolation model the application already uses for request context);
repository operations started in the async scope enlist automatically:

- Mongo: operations run on the transaction's `IClientSessionHandle` (`WithSession`).
  Requires a replica set; on a standalone server `BeginTransactionAsync` throws
  `MonjoNotSupportedException` with an explanatory message (tests skip on it).
- SQL: the transaction holds one dedicated pooled connection; repository commands attach
  the native `DbTransaction`.

The legacy Mongo surface (`Utilities.MongoDatabase.IMonjoRepository<T>`) is not
transaction-aware — it uses the plain collection handle. Documented limitation.

## 8. One-time entity readiness (schema + indexes)

`EnsureEntityReadyAsync<T>()` is gated by `EntityReadinessGate` — an owner-TCS gate keyed
by provider + database identity + table: the work runs **exactly once per key per process**,
concurrent callers join the owner's task, and a failure removes the gate entry so the next
call retries.

Allocation profile: the per-type gate key (string) and work delegate are cached on the
connection, once per entity type (the first call builds them). After that, every call's
fast path is a `ConcurrentDictionary.TryGetValue` on the per-type cache + a
`TryGetValue` + `IsCompleted` check in the gate — **zero allocations**, no per-call
lambda, closure or key-string construction. The gate itself allocates one small entry per
key per process (the first time), never again.

Work performed: SQL → `CREATE TABLE IF NOT EXISTS` + `CREATE [UNIQUE] INDEX IF NOT EXISTS`
for each declared index (gate key includes the concrete database: the file path for
SQLite, the database name for PostgreSQL). Mongo → list existing indexes once, create
missing declared ones by name.

The repository calls this automatically on first use; the app can also call it eagerly at
startup if it wants DDL failures there.

## 9. Pagination

`MonjoPage` keeps the pre-existing 1-based `Index` + `Size` shape. A `null` page means
"no paging" (the pre-existing default). Counting is **never automatic**: `FindManyAsync`
issues one query; only `QueryAsync` (whose contract includes `TotalCount`) issues the
count as well. The SQL plan carries `LIMIT`/`OFFSET` as parameters (no re-parsing);
Mongo uses `Skip`/`Limit`. Keyset/cursor pagination can be added later as an option on the
query model without changing providers' execution paths.

## 10. Errors and cancellation

- Cancellation tokens are threaded through every operation and propagated to the driver;
  cancellation is never swallowed (a cancelled operation throws `OperationCanceledException`).
- Provider-native exceptions are mapped to provider-independent ones **only where a
  mapping is genuinely provider-neutral** (SQLite busy/locked → `MonjoBusyException`);
  everything else is rethrown unchanged so driver details are preserved.
- Structural problems fail fast: unknown column in a condition, unmappable entity
  property (SQL), missing/duplicate provider registration, provider mismatch — all with
  messages naming the entity/column/provider and the fix.

## 11. Hot-path budget (per operation)

- 0 reflection, 0 LINQ-to-provider, 0 per-request expression compilation.
- Readiness gate: after the first call for a type, 2 dictionary lookups + 1 IsCompleted
  check — **0 allocations** (see §8).
- SQL: cached statement text + translated fragment (small strings), N parameters,
  one pooled connection, one compiled row-mapper delegate per row. Parameter values are
  converted through `SqlValueConverters.ToDb` + the dialect (`ToDbValue`) at bind time, so
  bound values always use the stored representation (enum → name, SQLite Guid → "N",
  SQLite decimal → the sortable encoding).
- Mongo: cached selectors + small filter tree, driver-native cursor.
- Ambient context reads (actor, transaction) are `AsyncLocal` reads — no locks anywhere
  on hot paths.

## 12. Extending to a new provider (e.g. SQL Server / MySQL)

1. New project `Monjo.SqlServer` (or `Monjo.MySql`) referencing Core + Sql.
2. Implement a `SqlDialect` subclass: `GetSqlType`, literals, `SupportsNativeGuid`,
   `ReadsDateTimeAsText`, `ReadsDecimalAsText` + `EncodeDecimal`/`DecodeDecimal` (only if
   decimals are stored as text), `CreateConnection`, `ToDbValue`, optional exception mapping.
3. A provider class deriving `SqlMonjoProvider` (name + dialect + optional pragmas), a
   `UseMonjoSqlServer()` extension registering the factory. Done — the shared SQL engine
   (translation, row mapping, transactions, readiness gating) is reused unchanged.

A fundamentally different engine (e.g. Cosmos) would implement `IMonjoProvider`/
`IMonjoConnection`/`IMonjoRepository<T>` and its own translator, like Monjo.MongoDB does.
