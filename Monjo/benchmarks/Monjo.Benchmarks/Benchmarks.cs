using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Monjo;
using Monjo.MongoDB;
using Monjo.PostgreSQL;
using Monjo.SQLite;
using Utilities.MongoDatabase.Filter;

namespace Monjo.Benchmarks
{
    /// <summary>Benchmark entity: string id + a few representative columns (no soft-delete model → deletes are physical).</summary>
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
    /// Self-measuring design (no contamination between methods, no permanent table growth):
    ///  - GlobalSetup seeds a fixed 1,000-row dataset + a 1,300-row "delete pool" + one update
    ///    target row. Pool/target rows use Age ≤ 20 so the Age>20 filter-based benchmarks see
    ///    the identical 780 rows regardless of benchmark order.
    ///  - Update targets the stable update row (a real row exists on every call).
    ///  - Delete rotates through the pool; IterationCleanup (outside measurement) re-inserts the
    ///    row that was just deleted, so the table size is constant.
    ///  - Insert / BulkInsert use fresh ids; IterationCleanup hard-deletes exactly the rows the
    ///    last invocation inserted, so the table never permanently grows and later benchmarks
    ///    measure the same dataset.
    ///  - GlobalCleanup removes everything.
    ///
    /// Run:
    ///   dotnet run -c Release -- --filter "Full"
    ///   dotnet run -c Release -- --filter "GetById" --memory-measurement-mode Mean
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 250, iterationCount: 1000)]
    public class MonjoRepositoryBenchmarks
    {
        private const int SeedRows = 1000;
        private const int DeletePoolSize = 1300;   // > warmup + measured iterations (1250)
        private const int BulkSize = 100;

        private readonly IMonjoRepository<BenchPerson> _repo;
        private readonly BenchPerson _updateTarget = new();
        private readonly string[] _deletePool = new string[DeletePoolSize];
        private readonly string[] _bulkIds = new string[BulkSize];   // preallocated: zero per-invocation bookkeeping allocations
        private readonly MonjoQuery _filtered;
        private readonly MonjoQuery _sorted;
        private readonly MonjoQuery _paged;
        private int _deleteIndex;
        private string _lastDeleteId = null!;
        private string _lastInsertId = null!;
        private bool _lastInsertIsBulk;

        public MonjoRepositoryBenchmarks(string providerName)
        {
            _updateTarget.Id = "bench-update";
            _updateTarget.Name = "update-target";
            _updateTarget.Age = 1;                    // excluded from the Age>20 filter
            _updateTarget.Email = "update@example.com";
            _updateTarget.LastSeen = DateTime.UtcNow;

            for (var i = 0; i < DeletePoolSize; i++)
                _deletePool[i] = "bench-del-" + i;

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
            // Clean slate, then a fixed dataset:
            //  - 1,000 seed rows (ids 0000000000..0000000999), Age = i % 100 → 780 rows with Age > 20
            //  - 1,300 delete-pool rows (Age = 0: excluded from every filter benchmark)
            //  - 1 update-target row (Age = 1: excluded from every filter benchmark)
            await _repo.HardDeleteManyAsync(null);

            var batch = new List<BenchPerson>(SeedRows + DeletePoolSize + 1);
            for (var i = 0; i < SeedRows; i++)
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

            foreach (var poolId in _deletePool)
                batch.Add(new BenchPerson { Id = poolId, Name = "delete-pool", Age = 0 });

            batch.Add(_updateTarget);

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
            // Fresh id + fresh entity per call (the realistic pattern); the row is hard-deleted
            // in IterationCleanup so the table never permanently grows.
            _lastInsertId = Guid.NewGuid().ToString("N");
            _lastInsertIsBulk = false;
            var person = new BenchPerson
            {
                Id = _lastInsertId,
                Name = "insert",
                Age = 0,                               // excluded from the Age>20 filter
                Email = "insert@example.com",
                LastSeen = DateTime.UtcNow,
            };
            return _repo.InsertAsync(person);
        }

        [Benchmark(Description = "BulkInsert(100)")]
        public Task BulkInsert()
        {
            _lastInsertIsBulk = true;
            var batch = new List<BenchPerson>(BulkSize);
            for (var i = 0; i < BulkSize; i++)
            {
                _bulkIds[i] = Guid.NewGuid().ToString("N");
                batch.Add(new BenchPerson { Id = _bulkIds[i], Name = "bulk", Age = 0 });
            }
            return _repo.InsertManyAsync(batch);
        }

        [Benchmark(Description = "Update")]
        public Task Update()
        {
            // The stable update-target row exists for the whole run → every call is a real update.
            _updateTarget.Age = 1;
            return _repo.UpdateAsync(_updateTarget);
        }

        [Benchmark(Description = "Delete")]
        public Task Delete()
        {
            // Rotates through the delete pool; every call is a real (physical) delete of an
            // existing row. IterationCleanup re-inserts the deleted row.
            _lastDeleteId = _deletePool[_deleteIndex++ % DeletePoolSize];
            return _repo.DeleteAsync(_lastDeleteId);
        }

        [Benchmark(Description = "Count")]
        public Task<long> Count() => _repo.CountAsync(null);

        [Benchmark(Description = "Exists")]
        public Task<bool> Exists() => _repo.ExistsAsync(_filtered);

        /// <summary>
        /// Runs after every iteration, OUTSIDE the measured time/allocation window. Restores the
        /// dataset to its exact pre-invocation state so no benchmark leaves residue behind.
        /// </summary>
        [IterationCleanup]
        public async Task CleanupIterationAsync()
        {
            if (_lastInsertIsBulk)
            {
                for (var i = 0; i < BulkSize; i++)
                    await _repo.HardDeleteAsync(_bulkIds[i]);
            }
            else if (_lastInsertId is not null)
            {
                await _repo.HardDeleteAsync(_lastInsertId);
                _lastInsertId = null!;
            }

            if (_lastDeleteId is not null)
            {
                await _repo.InsertAsync(new BenchPerson { Id = _lastDeleteId, Name = "delete-pool", Age = 0 });
                _lastDeleteId = null!;
            }
        }

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
