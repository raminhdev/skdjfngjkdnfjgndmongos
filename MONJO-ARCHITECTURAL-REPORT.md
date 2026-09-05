# MONJO ARCHITECTURAL REPORT

**Subject:** Redesign of `Utilities/MongoDatabase` into the multi-provider persistence
library **Monjo** (MongoDB, PostgreSQL, SQLite; extensible to SQL Server / MySQL).
**Repo state:** implementation + tests + benchmarks + wiring are in place on this branch.

> **Verification status (read first).** This environment has no .NET SDK and no NuGet
> access (verified: no reachable dotnet host, no package feeds beyond pypi/npm/github),
> so **no compilation, test run, or benchmark was executed here.** All correctness claims
> below are the product of static review of the code as written. Before merging, run:
> `dotnet build Monjo/Monjo.sln`, `dotnet test Monjo/tests/Monjo.Tests`,
> `dotnet build M1Mentor.Api.sln`. Benchmark numbers are therefore **not** included —
> the suite is provided to produce them in a normal environment.

---

## Part 1 — Executive summary

`Utilities/MongoDatabase` was a Mongo-specific persistence layer with a provider-agnostic
query model (`MonjoQuery`) bolted on for filtering/pagination. It forced Mongo types into
the whole application, made the database a hard assumption of every repository, and had
no path to any other engine.

Monjo keeps the developer experience that works (same `MonjoQuery`, same
`MonjoFilteredResult<T>`, same `BaseDocument`-derived entities, same repository lambda
APIs) and rebuilds the core around:

1. **One capability-oriented contract** (`IMonjoRepository<T>`) with zero Mongo types.
2. **Provider selection from configuration, resolved once at startup** by a centralized
   registry (FileStorage-resolver philosophy).
3. **Native execution per provider**: the Mongo provider drives `MongoDB.Driver`; the SQL
   providers drive Npgsql / Microsoft.Data.Sqlite with a shared, hand-rolled SQL engine
   (cached statement templates + parameterized translation + compiled row mappers). No
   EF Core, no Dapper, no LINQ providers.
4. **Backward compatibility as a first-class constraint**: the legacy API survives,
   verbatim in shape and namespaces, inside `Monjo.MongoDB`; the M1Mentor application was
   rewired so existing code compiles and behaves identically.

The result: the smallest architecture that gives real provider isolation, native
performance, and an extension point for new databases without touching the core.

## Part 2 — Current architecture (as-is)

- **Dependency graph:** `M1Mentor.Api → M1Mentor.Services → M1Mentor.Domain → Utilities`.
  `Utilities` carried `MongoDB.Driver 3.4.2` + Autofac.Extensions.DependencyInjection;
  net10.0, `Nullable disable`.
- **Configuration:** `"MonjoSettings": { "ConnectionString": "mongodb://localhost:27017",
  "DatabaseName": "M1MentorDB" }` bound via `RegisterSetting<MonjoSettings, IMonjoSettings>`.
- **BaseDocument:** `string Id` with `[BsonId]` + `[BsonRepresentation(ObjectId)]`
  (ObjectId storage preserved), audit fields (`CreatedBy/Info`, `CreatedMoment`,
  `ModifiedBy/Info`, `ModifiedMoment`, `DeletedBy/Info`, `DeletedMoment`, `IsDeleted`)
  stamped from `CurrentRequestContext`/`RequestUserInfo` at construction.
- **MonjoRepository<T:BaseDocument>:** full Mongo surface — `AsQueryable()` (pre-filtered
  `!IsDeleted`), lambda Find/Filter/Update/Replace/Delete (soft), `RealDelete*` (physical),
  pipelines, `IFindFluent`, index builder, `Count/Exists` with expression filters.
- **MonjoQuery / MonjoFilteredResult<T>:** the provider-agnostic query model; applied to
  `IQueryable<T>` via extension methods (`Apply(Where/Order/Page)`, `ExecuteAsync`) —
  i.e. filtering was translated to **Mongo LINQ**, not a generic query plan.
- **MonjoConnection / MonjoSettings:** client settings (Poll monitoring, TLS 1.2,
  `MaxConnecting=1`, timeouts), decimal→Decimal128 serializers; registered via
  `ISingletonDependency`.
- **Application usage:** five repositories inherit `MonjoRepository<T>`; three controller
  actions take `MonjoQuery` and return `MonjoFilteredResult<T>`; `FileReferenceService`
  exercises the full `AsQueryable().Apply(...).ExecuteAsync(...)` pipeline;
  `GridFsStorageProvider` uses `IMonjoConnection.Database` for GridFS.

## Part 3 — Problems with the current architecture

1. **Mongo is the architecture.** Every repository, contract, and the shared `Utilities`
   assembly depend on Mongo types; there is no way to use another database without
   rewriting the application.
2. **`MonjoQuery` was half-agnostic.** It only worked against Mongo LINQ
   (`IQueryable<T>`), so the "common query" was actually a Mongo query translator.
3. **No provider abstraction at all** — no connection/provider contract, no place where a
   different engine could plug in; `IMonjoConnection` exposed `IMongoClient`/
   `IMongoDatabase` directly (leakage into GridFS callers).
4. **No startup-time provider decision point**; the database was implicit in the assembly
   graph and the DI registrations.
5. **Inconsistent identity source** — audit fields read static request-context state at
   construction time (BaseDocument ctor), silently defaulting to "system" outside
   request scope; no explicit bridge point.
6. **Settings and connection coupled** (`MonjoConnection` registered through
   `ISingletonDependency` + `MonjoSettings`) with legacy naming, no validation, no
   fail-fast on misconfiguration.
7. **No schema/index management** — collections were assumed; no idempotent index
   creation; no story for SQL at all.
8. **Dead/typo'd surface** (e.g. `IFindFluetnExtentions`) kept alive only by habit.

## Part 4 — Target architecture

```
                 ┌────────────────────────────────────────────────┐
                 │                   Monjo.Core                   │
                 │ IMonjoRepository<T> · IMonjoConnection ·       │
                 │ IMonjoProvider · MonjoOptions · MonjoQuery (ns │
                 │ preserved) · MonjoFilteredResult<T> ·          │
                 │ MonjoEntityMetadata · MonjoActorContext ·      │
                 │ MonjoProviderRegistration · EntityReadinessGate│
                 │ MonjoTransaction · exceptions · attributes     │
                 └───────┬──────────────┬──────────────┬──────────┘
                         │              │              │
        ┌────────────────┴──┐   ┌───────┴────────┐  ┌──┴───────────────────┐
        │   Monjo.MongoDB   │   │    Monjo.Sql   │  │ (future: same shape  │
        │ MongoMonjoProvider│   │  (internal SQL │  │  for SQL Server /    │
        │ MongoMonjoConnect.│   │   engine)      │  │   MySQL)             │
        │ MongoMonjoReposi- │   └──┬──────────┬──┘  └──────────────────────┘
        │ tory + translators │    │          │
        │ MongoIndexManager  │ ┌─────────┐ ┌─┴────────────┐
        │ + preserved legacy │ │ Monjo.   │ │ Monjo.       │
        │   Utilities.Mongo- │ │PostgreSQL│ │ SQLite       │
        │   Database API     │ │ (Npgsql) │ │ (MS.Data.)   │
        └────────────────────┘ └──────────┘ └──────────────┘
```

Rules that shaped it:

- **Capability contract, not Mongo-shaped interface.** `IMonjoRepository<T>` contains only
  operations every provider can express natively (Part 8).
- **The query model stays WHAT.** Providers own HOW (native filter definitions /
  parameterized SQL). No engine emulates another.
- **Small over simple-with-ceremony.** Five packages, four core interfaces, no
  factories-per-thing, no deep inheritance, no reflection in hot paths (Part 5).
- **Compatibility is a package, not a compromise.** The legacy API lives in
  Monjo.MongoDB (Part 17) — it is not emulated through the new contract.

## Part 5 — Rationale for the key decisions

| Decision | Rationale (vs. alternatives) |
|---|---|
| No EF Core | A LINQ provider adds a runtime (expression trees, model building, change tracking) that the app would never use (no relationships, no tracking, no migrations), and would cap control over generated SQL and connection lifetime. |
| No Dapper | Dapper is a micro-ORM over parameterized SQL — exactly what the 200-line `Monjo.Sql` engine already does, minus the dependency, and with per-type cached templates Dapper can't express (its `Get` re-reflects unless manually cached). |
| No `Monjo.Abstractions` package | The abstractions are four small interfaces; a separate package only creates versioning friction between core and providers. |
| `Monjo.Sql` is internal (not NuGet) | It is shared engine code, not a product boundary; consumers get it transitively. Keeping it un-packable prevents half-configuration states. |
| Hand-rolled SQL translation | `MonjoQuery` is a small flat model (AND-of-OR-groups of 12 comparisons + order + page). Translating it to a parameterized fragment is a linear function; any SQL builder abstraction is ceremony on top of string composition. |
| Compiled expression row mappers | Same cost model Dapper uses for mapping, but built once per type from the cached metadata — no per-call reflection, no per-call parameter enumeration. |
| `AsyncLocal` ambient transaction/actor | The application already isolates request state this way (`CurrentRequestContext`); reusing the model avoids new ambient machinery and keeps concurrency isolation exact. |
| Gate-based one-time DDL | Idempotent `IF NOT EXISTS` DDL still costs a round-trip on first use per table; the owner-TCS gate makes concurrent first-uses wait on one execution and never re-run, with retry-on-failure. |

## Part 6 — Provider resolution flow

```
appsettings:  "Monjo": { "Database": { "Provider": "PostgreSQL" }, ... }
              (or legacy "MonjoSettings" — provider defaults to MongoDB)

AddMonjo(configuration)
  └─ MonjoOptions.Bind(...)               // once; normalizes provider name
  └─ singleton IMonjoProvider = sp =>
       MonjoProviderRegistration.GetFactory(sp)
         ├─ null            → MonjoProviderNotRegisteredException (lists the fix)
         └─ name mismatch   → MonjoProviderNotRegisteredException (configured vs registered)
  └─ singleton IMonjoConnection = provider.Connection

UseMonjo{MongoDB|PostgreSql|Sqlite}()
  └─ AddMonjoProviderFactory(name, factory)   // second registration → throws immediately
       (MongoDB additionally aliases MongoMonjoConnection as the legacy
        Utilities.MongoDatabase.Contracts.IMonjoConnection for old injection points)
```

Properties: configuration is read **once** (bound options are a registered singleton); the
provider object is constructed **once** (singleton); no database I/O at startup; a
misconfiguration fails at first resolution with an actionable message (or at registration
for duplicate providers). The app optionally resolves `IMonjoProvider` eagerly in
`Program.cs` to surface errors at boot.

## Part 7 — Connection and repository lifecycle

- **Provider** (singleton): owns the native client/pool. Mongo: one `IMongoClient` +
  `IMongoDatabase` + per-type cached collection handles. SQL: a `SqlDialect` that creates
  configured pooled connections on demand.
- **Connection** (singleton, `IMonjoConnection`): stateless dispatcher — per-type cached
  `IMonjoRepository<T>`, `BeginTransactionAsync`, `EnsureEntityReadyAsync<T>`.
- **Repository** (singleton per type): stateless; every operation opens an
  **operation context**:
  - outside a transaction: one pooled connection for the operation's lifetime
    (acquire → command → release; no physical connect in the hot path);
  - inside an ambient transaction: the transaction's dedicated connection is borrowed
    (no pool churn, no nested transactions).
- **Entity readiness** (once per provider+database+table per process): DDL — see Part 12.
- Nothing is disposed per request; the only per-operation resources are the pooled
  connection lease and command/reader handles (both `await using`).

## Part 8 — Query translation

`MonjoQuery` → provider, per operation:

**MongoDB** (`MongoQueryTranslator`, cached selectors per (type, column)):
- conditions → a single `Func<T,bool>` predicate (AND of OR-groups), built from cached
  member selectors + operand-converted constants; `Contains`/`NotContains` → `string
  .Contains` (driver renders a regex); null checks are typed constants;
- `BuildIdFilter` converts the id operand to the id column's CLR type (Guid/"N"-string/
  numeric) so `GetById(object)` works with any caller type;
- soft delete is combined with the user filter (`IsDeleted == false AND …`) on every read
  path; hard delete intentionally targets all rows;
- order → `SortDefinition<T>` (cached selectors); page → `Skip/Limit`.

**SQL** (`SqlQueryTranslator` + cached `SqlEntityMetadata`):
- WHERE fragment `" WHERE p0 > @p0 AND (p1 LIKE @p1 ESCAPE '\\' OR p2 IS NULL)"` —
  parameter names are positional (`p0…`), operand conversion is shared with Mongo
  (`MonjoOperandConversion`: enum by name, invariant-culture parse);
- the soft-delete predicate is combined with the user WHERE in **exactly one place**
  (`BuildWhereSql`) so no statement can double-WHERE or drop the filter (a bug class that
  was explicitly hunted and fixed in review);
- `ORDER BY` fragment from cached columns; `LIMIT/OFFSET` are bound parameters;
- the final statement = cached template (per type per provider) + fragments — e.g.
  `SELECT "Id","Name",… FROM "People" WHERE "IsDeleted" = 0 AND "Age" > @p0 ORDER BY
  "Age" ASC LIMIT @MonjoLimit OFFSET @MonjoOffset`;
- `UpdateColumnsAsync`/`DeleteManyAsync` compose `SET` clauses from the resolved columns at
  call time (partial updates are inherently per-call) but reuse the same where combination.

Column references accept property names, physical column names, or the legacy
`Type.Prefix.Name` form; unresolvable references fail fast naming the entity.

## Part 9 — MongoDB provider strategy

- **Native driver everywhere.** Reads are `Find` (+`Sort`/`Skip`/`Limit`) with the
  translated filter — server-side; `CountDocuments` for counts, `CountDocuments(Limit=1)`
  for exists; writes are `InsertOne/InsertMany` (native bulk), `ReplaceOne` (full update),
  `UpdateMany` (partial), `ReplaceOne(IsUpsert)` (upsert, id-only filter so soft-deleted
  rows are revived, not duplicated), `UpdateOne/UpdateMany` (soft delete),
  `DeleteOne/DeleteMany` (hard).
- **No data materialization before filtering** — the legacy `AsQueryable()` path is
  preserved for legacy code; the new common API never loads-then-filters.
- **Identifier generation:** `string`/`Guid` ids get an "N"-format Guid when null; ids
  with `[BsonId]` (the legacy `BaseDocument`) are left to the driver (ObjectId).
- **Transactions:** `StartSession` + `StartTransaction`; repository operations in the
  ambient scope run `WithSession`. Standalone server → `MonjoNotSupportedException` with
  explanation (the test suite skips on it).
- **Indexes:** `MongoIndexManager` lists existing indexes once and creates missing
  `[MonjoIndex]` ones by name (gate-gated, idempotent).
- **Compatibility layer:** the entire legacy `Utilities.MongoDatabase` API is preserved in
  package-local folders with original namespaces; `MonjoRepository<T>` derives from
  `MongoMonjoRepository<T>` so new common APIs are available without touching legacy ones;
  `MongoMonjoConnection` implements the legacy `IMonjoConnection` (Client/Database) for
  GridFS and collection-handle consumers.

## Part 10 — PostgreSQL provider strategy

- **Npgsql native pooling**; per-operation pooled connection; `TIMESTAMP WITH TIME ZONE`
  (UTC-normalized writes), `UUID` (native Guid), `BOOLEAN`, `NUMERIC(28,10)`, `BYTEA`.
- **Full async I/O** (`OpenAsync`/`Execute*Async` with token); `CommandTimeout` from
  options (default 30 s) applied to repository commands.
- **UPSERT** = `INSERT … ON CONFLICT ("Id") DO UPDATE SET …` with `@Up_`-prefixed update
  parameters (built once per type).
- **Exception mapping:** none beyond the base rethrow (driver details preserved); the
  dialect exposes a `TranslateException` hook for future needs.
- DDL via the shared engine: `CREATE TABLE IF NOT EXISTS` with typed columns
  (`NOT NULL` for value types and the id, `PRIMARY KEY` on the id) + `CREATE [UNIQUE]
  INDEX IF NOT EXISTS`.

## Part 11 — SQLite provider strategy

- **Microsoft.Data.Sqlite** with pooling; `WAL` journal mode set once per database
  (readers stay concurrent; single writer — the engine surfaces `busy`/`locked` as
  `MonjoBusyException` with configuration guidance).
- **Type affinity mapping:** INTEGER (bools as 0/1, numerics), REAL (double/float), TEXT
  (string, enum by name, decimal, **DateTime as UTC text**, Guid as "N" text), BLOB.
- **DateTime correctness:** writes are UTC-normalized; reads parse the stored text with
  `AssumeUniversal` (a `ReadsDateTimeAsText` dialect flag) because
  `Microsoft.Data.Sqlite.GetDateTime` would misinterpret stored UTC as local time — a
  round-trip bug explicitly handled in review.
- `DefaultTimeout` = busy timeout (option, default 5 s); command timeout = busy + 15 s.
- `:memory:` and file data sources both work through the same dialect (the readiness gate
  keys on the connection string, i.e. the actual database identity).

## Part 12 — Indexing strategy

- **Provider-neutral concept:** `[MonjoIndex("Col" | "A,B", unique, descending[], name)]`
  — the minimal intersection (composite, unique, direction, explicit name) expressible on
  Mongo + both SQL engines. Deliberately no sparse/partial/text/expression indexes in the
  common model; those stay on provider APIs (Mongo keeps `IMonjoIndexBuilder<T>`).
- **Created at initialization, never per request:** `EnsureEntityReadyAsync<T>` runs
  once per (provider, database, table) per process under the owner-TCS gate:
  SQL → `CREATE … INDEX IF NOT EXISTS` (default name `ix_<Table>_<Cols>`),
  Mongo → list indexes once, create missing by name.
- **Opt-in:** `MonjoOptions.AutoCreateIndexes` (default true) disables the whole step.
- Existing databases are respected: matching by name prevents duplication; the legacy
  application used no `CreateIndex` calls, so no index changes occur for it unless
  `[MonjoIndex]` is added.

## Part 13 — Transaction strategy

- **Provider-native, common surface:** `BeginTransactionAsync()` → `MonjoTransaction`
  (`CommitAsync`/`RollbackAsync`; `DisposeAsync` rolls back if still open; state machine
  prevents double completion).
- **Ambient enlistment** via `AsyncLocal<MonjoTransaction?>`: any repository operation
  started in the async scope uses the transaction's session (Mongo `WithSession`) or
  dedicated connection + `DbTransaction` (SQL) — no `using` ceremony at each call site,
  exact async isolation (no ambient leakage across parallel flows).
- **SQL transactions hold one dedicated pooled connection** for their lifetime: no pool
  churn per statement, no nested transactions (`InsertManyAsync` inside a transaction
  enlists instead of nesting).
- **Honest capability model:** MongoDB standalone cannot transact — the API says so with
  `MonjoNotSupportedException` instead of faking it. The legacy Mongo repository surface
  is not transaction-aware (documented; new code uses the common API inside
  transactions).

## Part 14 — DI and dependency strategy

- **MS DI abstractions only** in Core (`IServiceCollection`, `IConfiguration`); the
  application's Autofac container bridges via the existing
  `AutofacServiceProviderFactory` — no Monjo dependency on Autofac.
- Registrations: `AddMonjo` (options + provider pipeline) + one `UseMonjo*` (factory +
  provider/concrete + legacy-connection alias for Mongo). Everything is a **singleton**.
- **No scanning, no assembly walking** for Monjo itself; the app's existing Autofac scans
  continue to register its repositories (whose `IMonjoConnection` — legacy or core — is
  resolved through the MS DI bridge).
- **Dependency isolation:** a Postgres-only app references Core + Monjo.PostgreSQL and
  never sees MongoDB.Driver; a Mongo app never references Npgsql/Microsoft.Data.Sqlite.
  `Monjo.Sql` is transitively internal.
- **Legacy DI decoupling:** the obsolete `MonjoConnection` no longer implements
  `ISingletonDependency` (it is out of DI); the legacy `IMonjoConnection` is satisfied by
  the new singleton, so `GridFsStorageProvider` and repository constructors are untouched.
- **Audit identity:** `MonjoActorContext.SetProvider(...)` at startup; the M1Mentor app
  bridges `CurrentRequestContext.User` → `MonjoActor` (same default values as before:
  "system" / "system : system").

## Part 15 — Performance optimizations (and what was explicitly not done)

Done:
- **Zero per-request reflection**: metadata, SQL templates, Mongo selectors, and row
  mappers are built once per (type[, provider]) and cached (concurrent-dictionary
  lookups, no locks on hot paths).
- **Zero LINQ providers / expression building per request** (the Mongo filter predicate
  is a small cached-selector composition; SQL is string composition + parameters).
- **One round-trip where possible**: `FindManyAsync` never counts; `ExistsAsync` uses
  `LIMIT 1`/`Limit=1`; `GetById` is a direct id lookup.
- **No connection per query** (pooled leases; native Mongo client reuse); **no physical
  connect on the hot path**.
- **Bulk inserts**: one connection + one transaction + prepared statements (SQL) /
  native `InsertMany` (Mongo).
- **WAL + pooling + busy timeout** for SQLite; Npgsql pooling for PostgreSQL.
- **Parameterized LIMIT/OFFSET** (no re-parse, plan-stable SQL).
- **Value-type ambient context** (`MonjoActor` is a record struct; `AsyncLocal` reads).

Not done (and why):
- No query-plan caching beyond the type-level templates (the per-request translation is
  a few string allocations; caching it would add a dictionary + lifetime management for
  no measurable win — RULE 20: no optimizing on assumptions; the benchmark suite exists
  to verify).
- No command pipelining/batching APIs in v1 (the common contract's bulk insert is the
  one native bulk path both engines need).
- No materialized views / read models — out of scope for a persistence library.

## Part 16 — Memory & allocations on the hot path

Per SQL operation: the translated WHERE/ORDER fragments (short-lived strings), the
parameter list (small `List<SqlParameter>`), the connection lease, and — per row — the
entity object itself (unavoidable) via one compiled delegate. No per-row `Dictionary`,
no per-row reflection, no LINQ query objects, no per-call expression trees.
Per Mongo operation: one filter tree (small object graph the driver renders) + the
documents. Ambient reads (actor, transaction) allocate nothing. The `EntityReadinessGate`
post-warmup cost is one dictionary lookup + awaiting a completed task (no allocation).

## Part 17 — Backward-compatibility decisions

| Surface | Decision |
|---|---|
| `BaseDocument` | **Preserved untouched** (shape + BSON attributes + ObjectId storage) in Monjo.MongoDB; audit stamping routed through `MonjoActorContext` with identical defaults. |
| `MonjoRepository<T>` | **Preserved** (same members, same namespaces) — now derives from `MongoMonjoRepository<T>`, so it *gains* the common API. |
| `IMonjoRepository<T>` (legacy), `IMonjoIndexBuilder<T>`, `IMonjoConnection` (legacy) | **Preserved** in Monjo.MongoDB with original namespaces; the legacy connection is implemented by `MongoMonjoConnection` (same singleton as the core connection). |
| `MonjoQuery`/`MonjoCondition`/`MonjoPage`/`MonjoOrder`/`MonjoFilteredResult<T>` | **Preserved** — moved to Monjo.Core with **identical namespaces** (`Utilities.MongoDatabase.Filter`); `Page` stays nullable (no implicit paging). |
| `Apply/Execute` queryable extensions | **Preserved** (Mongo behavior unchanged, including its original quirks — no behavior changes were smuggled in). |
| `MonjoCollectionNameAttribute` | **Preserved**; now derives from `MonjoTableAttribute` so the metadata layer understands it. |
| `DateFieldFilter`, `ManualPaginationResult<T>` | **Preserved** (moved to Core, same namespaces, same member types). |
| `MonjoSettings`/`IMonjoSettings`/`MonjoConnection` | **[Obsolete]** (still compile; registered nowhere; message points to `MonjoOptions`/`UseMonjoMongoDB`). |
| Config section `"MonjoSettings"` | **Still read** (legacy fallback of `MonjoOptions.Bind`; provider defaults to MongoDB) — `appsettings.json` unchanged. |
| `IFindFluetnExtentions` | Typo preserved (public API). |
| Client settings (Poll, TLS1.2, MaxConnecting=1, timeouts, Decimal128) | **Reproduced** in `MongoMonjoConnection`/`MongoBsonDefaults`. |

**Proof that the removed abstractions were no longer needed** (RULE 19): `MonjoSettings`
and `MonjoConnection` were replaced by `MonjoOptions` + `MongoMonjoConnection` which
strictly subsume them (same values, plus fail-fast + normalization); `Utilities/MongoDatabase`
was not deleted — it was *moved into* Monjo.MongoDB with namespaces intact, so nothing in
the application changed reference.

## Part 18 — Breaking changes

For **existing Mongo applications (M1Mentor): none observable.** Compile-level notes:

1. `Utilities` no longer exposes `MongoDB.Driver` as a direct package reference (it
   arrives transitively via Monjo.MongoDB) — code in *other* assemblies that relied on the
   transitive reference from `Utilities` only is fine here (Domain/Services/Api all get
   the driver transitively through Domain→Monjo.MongoDB). If you add such a consumer,
   reference Monjo.MongoDB explicitly.
2. `MonjoSettings`/`IMonjoSettings`/`MonjoConnection` emit **obsolescence warnings** at
   compile time (build-breaking only if `TreatWarningsAsErrors`).
3. The removed `RegisterSetting<MonjoSettings, IMonjoSettings>` registrations mean
   `IMonjoSettings` is no longer resolvable from DI (nothing in the app resolved it).

For **new code**: the common API is the way forward; the legacy API is frozen (no new
members) and Mongo-only.

## Part 19 — Migration guide (summary)

Full detail: [`Monjo/docs/MIGRATION.md`](Monjo/docs/MIGRATION.md). The five steps
(already applied to M1Mentor in this branch):

1. Add `AddMonjo(configuration)` + `UseMonjo<Provider>()` in `Program.cs`; bridge
   `MonjoActorContext.SetProvider(...)`.
2. Point project references at Monjo (app projects: Core + the provider package).
3. Delete the `MonjoSettings`/`MonjoConnection` registrations.
4. Delete the moved sources from `Utilities` (they live in Monjo.MongoDB now).
5. Leave `appsettings.json` as-is (legacy section is still honored), or adopt the new
   `"Monjo"` section shape for explicitness.

## Part 20 — Test strategy

`Monjo/tests/Monjo.Tests` (xunit):

- **One semantic suite, three providers.** `MonjoProviderSuite` (abstract) asserts
  provider-identical behavior; concrete suites: `SqliteProviderTests` (always runs —
  temp-file DB per test instance), `MongoProviderTests` (skips unless
  `MONGO_CONNECTION_STRING` is set; own database per test, dropped after),
  `PostgreSqlProviderTests` (skips unless `MONJO_PG_CONNECTION_STRING` is set; own
  database per test, created/dropped).
- **Isolation contract:** every test gets a fresh database/file, so tests are order- and
  parallel-safe.
- Coverage: full-type round-trip (string/int/decimal/bool/enum/Guid/DateTime?/nullable
  string), all 12 comparison operators (numeric theory), string contains, null/empty
  comparisons, AND/OR grouping, ascending/descending sort, `QueryAsync` paging (total +
  page count + slice), `FindManyAsync` paging without count, `UpdateAsync` (full +
  Modified stamping), `UpdateColumnsAsync` (partial + stamping), `UpsertAsync`,
  soft-delete visibility + physical hard delete (verified against the physical store),
  POCO entities without soft-delete (physical delete), cancellation propagation,
  concurrent inserts/reads, transaction commit/rollback (skip on unsupported providers),
  declared-index creation idempotency (verified against `sqlite_master` /
  `pg_indexes` / Mongo index list).
- **Core unit tests** (no DB): options binding (new + legacy sections, precedence,
  provider-name normalization), provider resolution (success, missing factory, mismatch,
  duplicate registration), entity metadata (table/id/column resolution, dotted and
  case-insensitive references, ignore/rename attributes, readiness-gate retry semantics),
  `MonjoQuery` mapping, SQL translation unit tests (parameterized WHERE/ORDER/LIMIT text,
  empty plan, unknown-column failure, DDL shape).
- **Intentional provider differences** are asserted or documented rather than hidden:
  transaction availability (Mongo standalone skips), physical-delete semantics for
  POCOs, SQLite single-writer behavior.

## Part 21 — Benchmark suite (plan; results pending a buildable environment)

`Monjo/benchmarks/Monjo.Benchmarks` (BenchmarkDotNet 0.14, `MemoryDiagnoser` for
allocations): 1,000-row seed, then per provider (SQLite always; Mongo/PG when
`MONGO_CONNECTION_STRING` / `MONJO_PG_CONNECTION_STRING` are set):

| Benchmark | Operation |
|---|---|
| GetById | id lookup |
| FilteredQuery(count) | filtered count |
| SortedQuery | filter + order |
| PaginatedQuery | filter + order + page (count + slice) |
| Insert | single insert |
| BulkInsert(100) | 100-row bulk |
| Update | full update |
| Delete | soft delete by id |
| Count / Exists | unfiltered count / filtered exists |
| HardDelete(cleanup) | physical cleanup |

Run: `dotnet run -c Release --project Monjo/benchmarks/Monjo.Benchmarks`.
**No numbers are reported in this document** — the sandbox cannot build or execute;
fabricated figures would violate the "no optimizing on assumptions" rule. The suite is
the deliverable; run it on a machine with .NET 10 + the target databases and paste the
results into this section (throughput, latency, allocations, and note startup cost via
the readiness-gate first-use).

## Part 22 — Limitations and known constraints

1. **SQL providers, v1 column types:** string, bool, int, long, short, byte, double,
   float, decimal, DateTime, Guid, byte[], enum (as text), + nullable variants. Lists,
   nested objects, and arrays of scalars are not mappable (clear failure message).
   Mongo remains the provider of record for document-shaped data.
2. **No migrations framework.** `CREATE … IF NOT EXISTS` covers creation, not evolution
   (column additions/renames require manual DDL or a future `IMonjoMigrator`).
3. **SQLite concurrency** is single-writer by design (WAL); busy writers get
   `MonjoBusyException` with guidance — it is the right embedded choice, not a bug.
4. **Mongo transactions require a replica set.** Standalone: unsupported with a clear
   exception. Multi-database transactions are not modeled (out of scope).
5. **Legacy Mongo surface is not transaction-aware** (Part 13); new code should use the
   common API inside `BeginTransactionAsync`.
6. **Numeric SQL ids** (`int`/`long` `Id`) are supported for storage/lookup but **not**
   auto-generated on insert (explicit value required; `string`/`Guid` are generated).
7. **No query-result projections in the common contract** (the legacy Mongo surface
   still projects; a `FindManyAsync(selector)` overload is a likely v1.x addition).
8. **`MonjoQuery.Operand` is an `object`** (historically string from binding); conversion
   rules are centralized but the type system does not enforce them.
9. **Environment note:** this code has not been compiled in this sandbox (no .NET SDK /
   NuGet); see the verification status at the top and Part 23.
10. **Pre-existing (not introduced here):** the root `Dockerfile` references `iptv.*`
    projects that do not exist in this repository (leftover from a template); it was
    already non-functional before this refactor and was deliberately left untouched
    (unrelated-behavior rule). The `docker-compose.yml` settings
    (`MonjoSettings__ConnectionString` / `MonjoSettings__DatabaseName`) continue to work
    because `AddMonjo` honors the legacy section.

## Part 23 — Future provider strategy (SQL Server, MySQL, …)

The SQL path is the intended extension seam:

1. **New package** `Monjo.SqlServer` / `Monjo.MySql` (Core + Sql references + the ADO.NET
   provider package, e.g. Microsoft.Data.SqlClient / MySqlConnector).
2. **A `SqlDialect` subclass** answering five questions: identifier quoting (shared ANSI
   default), column type mapping (`GetSqlType`), boolean literals, Guid strategy
   (`SupportsNativeGuid`), DateTime read strategy (`ReadsDateTimeAsText`), connection
   factory, write conversion (`ToDbValue`), and optional exception mapping
   (`TranslateException` — e.g. SqlClient deadlock 1205 → `MonjoBusyException`).
3. **A provider class** deriving `SqlMonjoProvider` (name, dialect, command timeout,
   optional once-per-process pragmas) + a `UseMonjoSqlServer()` extension that registers
   the factory.
4. **Dialect-level SQL differences** are already isolated: upsert syntax currently
   assumes `ON CONFLICT … DO UPDATE` — the first non-Postgres SQL provider should extract
   the upsert template into the dialect (`BuildUpsertSql`), which is a one-method change
   in `Monjo.Sql`. (SQLite's `ON CONFLICT` is intentionally kept: it shares the
   PostgreSQL syntax by design.)

For a fundamentally different engine (Cosmos DB, DynamoDB): implement
`IMonjoProvider`/`IMonjoConnection` + `IMonjoRepository<T>` and a translator, exactly as
`Monjo.MongoDB` does — the core contract, options, registry, actor context, transaction
shape, readiness gate, and query model are all provider-neutral by construction.

Nothing in the core changes when adding either kind of provider: **the core has no
provider-specific code, and every provider-specific decision has exactly one home**
(dialect for SQL, translator/connection for Mongo-shaped engines).

---

## Appendix A — File map (Monjo)

- `src/Monjo.Core/` — IMonjoRepository/IMonjoConnection/IMonjoProvider, MonjoOptions
  (+Mongo/PostgreSql/Sqlite sub-options), MonjoActorContext, MonjoColumnUpdate,
  MonjoTransaction (+context), MonjoProviderRegistration, MonjoServiceCollectionExtensions
  (AddMonjo), MonjoIndexDefinition, EntityReadinessGate, MonjoOperandConversion,
  BaseEntity, Attributes (MonjoTable/Id/Column/Index/Ignore), Exceptions,
  Metadata (MonjoEntityMetadata + cache), Filter/ (MonjoQuery model, preserved
  namespaces), Extensions/ (Apply/Execute LINQ helpers, preserved), Shared/ (DateFieldFilter,
  ManualPaginationResult, preserved).
- `src/Monjo.Sql/` — SqlDialect, SqlValueConverters, SqlEntityMetadata (cached SQL
  templates), SqlRowMapper (compiled read/write delegates), SqlQueryTranslator
  (SqlQueryPlan), SqlOperationContext (+SqlTransactionBridge), SqlMonjoRepository
  (IMonjoRepository<T>), SqlMonjoConnection (+SqlMonjoProvider base).
- `src/Monjo.MongoDB/` — MongoBsonDefaults, MongoTransactionBridge, MongoMonjoConnection
  (+MongoMonjoProvider), MongoQueryTranslator, MongoMonjoRepository, MongoIndexManager,
  MonjoMongoDbServiceCollectionExtensions (UseMonjoMongoDB), `Utilities/` legacy tree
  (17 files, original namespaces).
- `src/Monjo.PostgreSQL/`, `src/Monjo.SQLite/` — dialect + provider + Use* extensions.
- `tests/Monjo.Tests/`, `benchmarks/Monjo.Benchmarks/`, `Monjo.sln`, `docs/`.

## Appendix B — M1Mentor wiring (this branch)

- `Program.cs`: `AddMonjo` + `UseMonjoMongoDB` + `MonjoActorContext.SetProvider` + eager
  `GetRequiredService<IMonjoProvider>()`.
- `Utilities.csproj`: − `MongoDB.Driver`, + Monjo.Core + Monjo.MongoDB references.
- `M1Mentor.Domain.csproj`: + Monjo.Core + Monjo.MongoDB references.
- `M1Mentor.Api.sln`: + the five Monjo src projects.
- Removed `RegisterSetting<MonjoSettings, IMonjoSettings>` (both sites); `Utilities/
  MongoDatabase/**` deleted (moved into Monjo.MongoDB); `appsettings.json` unchanged.
