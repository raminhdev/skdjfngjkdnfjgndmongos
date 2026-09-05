using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Monjo;
using Monjo.MongoDB;
using Monjo.PostgreSQL;
using Monjo.SQLite;
using Utilities.MongoDatabase.Filter;

namespace Monjo.Benchmarks
{
    /// <summary>Benchmark entity: string id + a few representative columns.</summary>
    [MonjoTable("BenchPeople")]
    public class BenchPerson
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string? Email { get; set; }
        public DateTime? LastSeen { get; set; }
    }

    /// <summary>
    /// Critical-path benchmarks: GetById, filtered/sorted/paginated query, insert, bulk insert,
    /// update, delete, count, exists — with MemoryDiagnoser to report allocations.
    /// SQLite always runs; MongoDB / PostgreSQL run when MONGO_CONNECTION_STRING /
    /// MONJO_PG_CONNECTION_STRING are set.
    ///
    /// Run:
    ///   dotnet run -c Release -- --filter "Full"
    ///   dotnet run -c Release -- --filter "GetById" --memory-measurement-mode Mean
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 250, iterationCount: 1000)]
    public class MonjoRepositoryBenchmarks
    {
        private IMonjoRepository<BenchPerson> _repo = null!;
        private readonly string _providerName;
        private readonly BenchPerson _person = new();
        private readonly MonjoQuery _filtered;
        private readonly MonjoQuery _sorted;
        private readonly MonjoQuery _paged;

        public MonjoRepositoryBenchmarks(string providerName)
        {
            _providerName = providerName;

            var options = providerName switch
            {
                "SQLite" => new MonjoOptions
                {
                    Provider = "SQLite",
                    ConnectionString = Path.Combine(Path.GetTempPath(), "monjo-bench.sqlite"),
                    DatabaseName = "bench",
                },
                "MongoDB" => new MonjoOptions
                {
                    Provider = "MongoDB",
                    ConnectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"),
                    DatabaseName = "monjo_bench",
                },
                "PostgreSQL" => new MonjoOptions
                {
                    Provider = "PostgreSQL",
                    ConnectionString = Environment.GetEnvironmentVariable("MONJO_PG_CONNECTION_STRING"),
                    DatabaseName = "monjo_bench",
                },
                _ => throw new ArgumentException(providerName),
            };

            _repo = (options.Provider == "SQLite" ? new MonjoSqliteProvider(options)
                    : options.Provider == "MongoDB" ? new MongoMonjoProvider(options)
                    : new MonjoPostgreSqlProvider(options))
                .Connection.CreateRepository<BenchPerson>();

            _person.Id = "bench-1";
            _person.Name = "bench-name";
            _person.Age = 42;
            _person.Email = "bench@example.com";
            _person.LastSeen = DateTime.UtcNow;

            _filtered = new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "20" }]],
            };
            _sorted = new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "20" }]],
                Order = [new MonjoOrder { Column = "Name" }],
            };
            _paged = new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "20" }]],
                Order = [new MonjoOrder { Column = "Name" }],
                Page = new MonjoPage { Index = 1, Size = 50 },
            };
        }

        [GlobalSetup]
        public async Task SetupAsync()
        {
            // clean + seed 1,000 rows
            await _repo.HardDeleteManyAsync(null);
            var batch = new List<BenchPerson>(1000);
            for (var i = 0; i < 1000; i++)
            {
                batch.Add(new BenchPerson
                {
                    Id = i.ToString("D10"),
                    Name = $"name-{i:D5}",
                    Age = i % 100,
                    Email = $"user{i}@example.com",
                    LastSeen = DateTime.UtcNow.AddHours(-i),
                });
            }
            for (var i = 0; i < batch.Count; i += 200)
                await _repo.InsertManyAsync(batch.GetRange(i, Math.Min(200, batch.Count - i)));
        }

        [Benchmark(Description = "GetById")]
        public Task<BenchPerson?> GetById() => _repo.GetByIdAsync("0000000042");

        [Benchmark(Description = "FilteredQuery(count)")]
        public Task<long> FilteredCount() => _repo.CountAsync(_filtered);

        [Benchmark(Description = "SortedQuery")]
        public Task<IReadOnlyList<BenchPerson>> SortedQuery() => _repo.FindManyAsync(_sorted);

        [Benchmark(Description = "PaginatedQuery")]
        public Task<MonjoFilteredResult<BenchPerson>> PaginatedQuery() => _repo.QueryAsync(_paged);

        [Benchmark(Description = "Insert")]
        public Task<BenchPerson> Insert()
        {
            _person.Id = Guid.NewGuid().ToString("N");
            return _repo.InsertAsync(_person);
        }

        [Benchmark(Description = "BulkInsert(100)")]
        public Task BulkInsert()
        {
            var batch = new List<BenchPerson>(100);
            for (var i = 0; i < 100; i++)
                batch.Add(new BenchPerson { Id = Guid.NewGuid().ToString("N"), Name = "bulk", Age = i % 100 });
            return _repo.InsertManyAsync(batch);
        }

        [Benchmark(Description = "Update")]
        public Task Update()
        {
            _person.Age = _person.Age + 1;
            return _repo.UpdateAsync(_person);
        }

        [Benchmark(Description = "Delete")]
        public Task Delete() => _repo.DeleteAsync(_person.Id);

        [Benchmark(Description = "Count")]
        public Task<long> Count() => _repo.CountAsync(null);

        [Benchmark(Description = "Exists")]
        public Task<bool> Exists() => _repo.ExistsAsync(_filtered);

        [Benchmark(Description = "HardDelete(cleanup)")]
        public Task HardDeleteCleanup() => _repo.HardDeleteManyAsync(null);

        [GlobalCleanup]
        public Task CleanupAsync() => _repo.HardDeleteManyAsync(null);

        public static void Main(string[] args)
        {
            var providers = new List<(string Name, Func<string, MonjoRepositoryBenchmarks> Factory)>
            {
                ("SQLite", n => new MonjoRepositoryBenchmarks(n)),
            };
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")))
                providers.Add(("MongoDB", n => new MonjoRepositoryBenchmarks(n)));
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONJO_PG_CONNECTION_STRING")))
                providers.Add(("PostgreSQL", n => new MonjoRepositoryBenchmarks(n)));

            var benchmark = new BenchmarkRunner(args);
            foreach (var (name, factory) in providers)
                _ = benchmark.RunAll(factory(name), "monjo-" + name.ToLowerInvariant());
        }
    }
}
