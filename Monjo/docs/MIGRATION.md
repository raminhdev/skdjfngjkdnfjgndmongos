# Migrating from `Utilities/MongoDatabase` to Monjo

The refactor moves the persistence code from `Utilities/MongoDatabase` into the Monjo
packages **without changing existing application code paths**. Existing Mongo
applications keep compiling, running against the same collections, and behaving the same.

> Verification note: written without an in-sandbox .NET SDK; compile and run the test
> suite (`dotnet test Monjo/tests/Monjo.Tests`) in a .NET 10 environment to confirm.

## What happened where

| Legacy location | New location |
|---|---|
| `Utilities/MongoDatabase/Filter/*` (MonjoQuery, MonjoCondition, MonjoPage, MonjoOrder, MonjoFilteredResult) | `Monjo.Core` — **same namespaces** (`Utilities.MongoDatabase.Filter`) |
| `Utilities/MongoDatabase/Extensions/*` (Apply/Execute LINQ helpers) | `Monjo.Core` (shared, provider-agnostic) + `Monjo.MongoDB/Utilities/MongoDatabase/Extensions` (Mongo queryable extensions) |
| `Utilities/MongoDatabase/Documents/BaseDocument` | `Monjo.MongoDB/Utilities/MongoDatabase/Documents` — **unchanged shape/BSON**; audit fields now filled from `Monjo.MonjoActorContext` |
| `Utilities/MongoDatabase/MonjoRepository<T>` | `Monjo.MongoDB/Utilities/MongoDatabase` — now derives from `MongoMonjoRepository<T>` (the common `IMonjoRepository<T>` surface is inherited) |
| `Utilities/MongoDatabase/Contracts/*` (IMonjoConnection, IMonjoRepository, IMonjoIndexBuilder, IMonjoSettings) | `Monjo.MongoDB/Utilities/MongoDatabase/Contracts` — `IMonjoConnection` implemented by `MongoMonjoConnection`; `IMonjoSettings`/`MonjoSettings` marked `[Obsolete]` |
| `Utilities/MongoDatabase/MonjoConnection` | `[Obsolete]` in Monjo.MongoDB (no longer registered in DI; `MongoMonjoConnection` replaces it) |
| `Utilities/Attributes/MonjoCollectionNameAttribute` | `Monjo.MongoDB/Utilities/Attributes` — now derives from `Monjo.MonjoTableAttribute` so every provider understands it |
| `Utilities/Models/Updates/DateFieldFilter` | `Monjo.Core/Shared` (namespace `Utilities.Models.Updates` preserved) |
| `Utilities/Utilities/PaginationHelper.ManualPaginationResult<T>` | `Monjo.Core/Shared` (namespace `Utilities.Utilities` preserved) |

## Application changes (M1Mentor, already applied in this repo)

1. **`Program.cs`** — replace the legacy settings registration with Monjo registration:

   ```csharp
   services.AddMonjo(builder.Configuration);   // binds MonjoOptions ("Monjo" or legacy "MonjoSettings" section)
   services.UseMonjoMongoDB();                 // the provider package in use

   // Bridge the request user into Monjo's audit fields (once at startup):
   MonjoActorContext.SetProvider(() =>
   {
       var user = CurrentRequestContext.User ?? new RequestUserInfo();
       return new MonjoActor(user.PublicKey, user.DisplayInfo);
   });
   ```

   `AddMonjo` reads the existing `MonjoSettings` section (`ConnectionString`,
   `DatabaseName`) with the provider defaulting to `MongoDB`, so **`appsettings.json` is
   unchanged**.

2. **Project references** — `Utilities` no longer references `MongoDB.Driver` directly; it
   references `Monjo.Core` + `Monjo.MongoDB` (the driver flows transitively, which is what
   `GridFsStorageProvider` needs). `M1Mentor.Domain` references `Monjo.Core` +
   `Monjo.MongoDB` (entities inherit `BaseDocument`, repositories derive `MonjoRepository<T>`).

3. **Removed registrations** — `RegisterSetting<MonjoSettings, IMonjoSettings>(...)` in
   `Utilities/Configuration/ServiceCollectionExtensions.cs` and
   `M1Mentor.Api/Utilities/Configurations/ControllerServiceCollectionExtensions.cs`
   (replaced by `AddMonjo`'s binding).

4. **Deleted** — the entire `Utilities/MongoDatabase` folder (moved into Monjo.MongoDB) and
   the moved `DateFieldFilter`/`ManualPaginationResult` copies (Core keeps them under the
   same namespaces).

## Behavioral equivalences preserved

- `BaseDocument` keeps `[BsonId]` + `[BsonRepresentation(ObjectId)]` on `Id` — existing
  ObjectIds and stored documents are untouched.
- Audit fields default to `"system"` / `"system : system"` when no user context is
  installed — identical to the previous `RequestUserInfo` defaults.
- `AsQueryable()` still pre-filters `!IsDeleted`; lambda Find/Update/Delete/Replace
  operations are unchanged; `MonjoQuery`/`ExecuteAsync` controller APIs are unchanged.
- Soft delete vs `RealDelete*` semantics unchanged.
- Mongo client settings preserved: Poll server-monitoring mode (default), TLS 1.2
  enforcement, `MaxConnecting = 1`, 30 s connect/server-selection timeouts,
  decimal → Decimal128 serializers.

## What changed under the hood (Mongo)

- The connection is `MongoMonjoConnection` (same singleton; also the legacy
  `IMonjoConnection`, so `IMonjoConnection` constructor injection in repositories and
  `GridFSBucket(connection.Database)` keep working).
- `MonjoRepository<T>` now derives from `MongoMonjoRepository<T>`: identical legacy
  behavior, plus the common `IMonjoRepository<T>` methods available for new code.
- `[MonjoIndex]` attributes are honored: declared indexes are created once per process at
  first use (idempotent). Existing indexes are matched by name and never duplicated.
- Standalone Mongo: `IMonjoConnection.BeginTransactionAsync` throws
  `MonjoNotSupportedException` (transactions need a replica set).

## Switching the provider (new apps)

```jsonc
"Monjo": {
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionString": "Host=...;Database=...;Username=...;Password=...",
  "DatabaseName": "app",
  "PostgreSql": { "MaxPoolSize": 100 }
}
```

```csharp
services.AddMonjo(configuration);
services.UseMonjoPostgreSql();   // or UseMonjoSqlite()
```

Entity requirements per provider:

- **MongoDB**: same as today (any BSON-mappable property).
- **PostgreSQL / SQLite**: properties must be `string, bool, int, long, short, byte,
  double, float, decimal, DateTime, Guid, byte[]`, an enum (stored as text), or a nullable
  of those. Complex properties (lists, nested objects) are not supported by the SQL
  providers in v1 (the error names the offending properties).
- Tables are created when missing (`AutoCreateSchema`, default true) and declared indexes
  when missing (`AutoCreateIndexes`, default true), once per process.

## API deprecations (legacy Mongo surface)

- `MonjoSettings` / `IMonjoSettings` / `MonjoConnection` — `[Obsolete]`; use
  `MonjoOptions` + `UseMonjoMongoDB()`.
- The legacy `IMonjoConnection` (Client/Database) is kept **not** obsolete: it is the
  compatibility surface (GridFS, collection handles).
