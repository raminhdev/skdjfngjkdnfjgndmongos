# Monjo

A small, provider-native persistence library for .NET: one shared repository contract,
executed natively by MongoDB, PostgreSQL, or SQLite. No ORM, no LINQ provider, no
reflection on hot paths.

- **Architecture:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **Migrating from `Utilities/MongoDatabase`:** [docs/MIGRATION.md](docs/MIGRATION.md)
- **Full architectural report:** [../MONJO-ARCHITECTURAL-REPORT.md](../MONJO-ARCHITECTURAL-REPORT.md)

## Quick start

```csharp
// Program.cs — provider chosen from configuration, resolved once at startup:
services.AddMonjo(configuration);      // section "Monjo" (or legacy "MonjoSettings")
services.UseMonjoPostgreSql();         // or UseMonjoMongoDB() / UseMonjoSqlite()

MonjoActorContext.SetProvider(() =>
{
    var user = CurrentRequestContext.User ?? new RequestUserInfo();
    return new MonjoActor(user.PublicKey, user.DisplayInfo);
});
```

```jsonc
// appsettings.json
"Monjo": {
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionString": "Host=localhost;Database=app;Username=app;Password=***",
  "DatabaseName": "app"
}
```

```csharp
public class PeopleRepository(IMonjoConnection connection)
{
    private readonly IMonjoRepository<Person> _repo =
        connection.CreateRepository<Person>();

    public Task<Person?> GetByIdAsync(string id)        => _repo.GetByIdAsync(id);
    public Task<IReadOnlyList<Person>> FindAsync(MonjoQuery q) => _repo.FindManyAsync(q); // no count
    public Task<MonjoFilteredResult<Person>> QueryAsync(MonjoQuery q) => _repo.QueryAsync(q);
    public Task<int> UpdateAgeAsync(MonjoQuery who, int age)
        => _repo.UpdateColumnsAsync(new MonjoColumnUpdate().Set("Age", age), who);
    public Task DeleteAsync(string id)                   => _repo.DeleteAsync(id);        // soft (if modeled)
}
```

## Packages

| Package | Contents |
|---|---|
| `Monjo.Core` | query model, result model, common contract, metadata, options, DI — no driver deps |
| `Monjo.MongoDB` | Mongo provider + the preserved legacy `Utilities.MongoDatabase` API |
| `Monjo.PostgreSQL` | Npgsql provider (transitively includes the internal SQL engine) |
| `Monjo.SQLite` | Microsoft.Data.Sqlite provider (transitively includes the internal SQL engine) |

Reference only the provider you use.

## Tests & benchmarks

```bash
dotnet test Monjo/tests/Monjo.Tests                    # SQLite always; Mongo/PG when env strings set
export MONGO_CONNECTION_STRING=mongodb://localhost:27017
export MONJO_PG_CONNECTION_STRING=Host=localhost;Username=...
dotnet run -c Release --project Monjo/benchmarks/Monjo.Benchmarks
```

> **Note:** this code was authored in an environment without a .NET SDK; build and test
> in a .NET 10 environment before release.
