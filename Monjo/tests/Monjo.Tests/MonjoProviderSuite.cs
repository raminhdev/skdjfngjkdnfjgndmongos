using Monjo;
using Utilities.MongoDatabase.Filter;
using Xunit;

namespace Monjo.Tests
{
    /// <summary>
    /// The provider-semantic test suite: every provider (SQLite always; MongoDB and PostgreSQL
    /// when a connection string is supplied via environment) must pass the SAME tests, which is
    /// how cross-provider semantic equivalence is verified.
    /// Each test gets its own isolated database (file/database name derived per test instance).
    /// </summary>
    public abstract class MonjoProviderSuite : IAsyncLifetime
    {
        protected abstract string ProviderName { get; }
        protected abstract Task<IMonjoProvider> CreateAsync();

        protected IMonjoProvider Provider { get; private set; }
        protected IMonjoRepository<TestPerson> Repo { get; private set; }
        protected IMonjoRepository<TestCounter> Counters { get; private set; }
        protected string RunId { get; } = Guid.NewGuid().ToString("N")[..8];

        public async Task InitializeAsync()
        {
            Provider = await CreateAsync();
            Repo = Provider.Connection.CreateRepository<TestPerson>();
            Counters = Provider.Connection.CreateRepository<TestCounter>();
        }

        public virtual Task DisposeAsync() => Task.CompletedTask;

        protected static MonjoQuery Query(
            Func<MonjoCondition> condition = null,
            Func<MonjoOrder> order = null,
            int? index = null, int? size = null)
        {
            var query = new MonjoQuery();
            if (condition is not null)
                query.Where = [[condition()]];
            if (order is not null)
                query.Order = [order()];
            if (index is int || size is int)
                query.Page = new MonjoPage { Index = index ?? 1, Size = size ?? 50 };
            return query;
        }

        protected static TestPerson Person(string suffix, int age = 30, string name = null)
            => new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name ?? $"p-{suffix}-{Guid.NewGuid():N}",
                Nickname = null,
                Age = age,
                Balance = 12.5m,
                IsActive = true,
                State = PersonState.Active,
                ReferenceId = Guid.NewGuid(),
                LastSeen = null,
            };

        // ------------------------------------------------------------------ CRUD

        [Fact]
        public async Task InsertAndGetByIdRoundTripsAllColumnTypes()
        {
            var person = Person(RunId);
            person.Nickname = "nick";
            person.LastSeen = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            await Repo.InsertAsync(person);
            var loaded = await Repo.GetByIdAsync(person.Id);

            Assert.NotNull(loaded);
            Assert.Equal(person.Id, loaded.Id);
            Assert.Equal(person.Name, loaded.Name);
            Assert.Equal("nick", loaded.Nickname);
            Assert.Equal(person.Age, loaded.Age);
            Assert.Equal(person.Balance, loaded.Balance);
            Assert.True(loaded.IsActive);
            Assert.Equal(PersonState.Active, loaded.State);
            Assert.Equal(person.ReferenceId, loaded.ReferenceId);
            Assert.NotNull(loaded.LastSeen);
            Assert.Equal(person.LastSeen.Value.ToUniversalTime(), loaded.LastSeen.Value.ToUniversalTime());
            Assert.False(loaded.IsDeleted);
            Assert.NotNull(loaded.CreatedMoment);
        }

        [Fact]
        public async Task GetByIdReturnsNullWhenMissing()
            => Assert.Null(await Repo.GetByIdAsync(Guid.NewGuid().ToString("N")));

        [Fact]
        public async Task InsertManyAndCount()
        {
            var batch = Enumerable.Range(0, 10).Select(_ => Person(RunId)).ToList();
            await Repo.InsertManyAsync(batch);
            Assert.Equal(10, await Repo.CountAsync(Query(c => new MonjoCondition
            {
                Column = "Name", Comparison = ComparisonMethods.Contains, Operand = RunId
            })));
        }

        // ------------------------------------------------------------------ filtering

        [Theory]
        [InlineData(30, 1, "Equal")]
        [InlineData(30, 0, "NotEqual")]
        [InlineData(29, 1, "GreaterThan")]
        [InlineData(30, 1, "GreaterThanOrEqual")]
        [InlineData(31, 1, "LessThan")]
        [InlineData(30, 1, "LessThanOrEqual")]
        public async Task NumericComparisonsWork(int operand, int expected, string comparison)
        {
            await Repo.InsertAsync(Person(RunId));

            var count = await Repo.CountAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = (ComparisonMethods)Enum.Parse(typeof(ComparisonMethods), comparison), Operand = operand.ToString() }));

            Assert.Equal(expected, count);
        }

        [Fact]
        public async Task StringContainsComparisonsWork()
        {
            var person = await Repo.InsertAsync(Person(RunId, name: "contains-me-xyz"));

            Assert.True(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Name", Comparison = ComparisonMethods.Contains, Operand = "contains" })));
            Assert.False(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Name", Comparison = ComparisonMethods.NotContains, Operand = "contains" })));
            Assert.True(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Name", Comparison = ComparisonMethods.NotContains, Operand = "zzz" })));
        }

        [Fact]
        public async Task NullComparisonsWork()
        {
            var withNull = Person(RunId);
            var withoutNull = Person(RunId, name: "nick");
            withoutNull.Nickname = "has-nick";
            await Repo.InsertManyAsync([withNull, withoutNull]);

            Assert.True(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Nickname", Comparison = ComparisonMethods.IsNull, Operand = null })));
            Assert.True(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Nickname", Comparison = ComparisonMethods.IsNotNull, Operand = null })));
            Assert.False(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Nickname", Comparison = ComparisonMethods.IsEmpty, Operand = null })));
            Assert.True(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Nickname", Comparison = ComparisonMethods.IsNotEmpty, Operand = null })));
        }

        [Fact]
        public async Task AndOrGroupingWorks()
        {
            await Repo.InsertAsync(Person(RunId, age: 20));
            await Repo.InsertAsync(Person(RunId, age: 40));

            // (Age == 20 OR Age == 40) AND State == 'Active'
            var query = new MonjoQuery
            {
                Where =
                [
                    [
                        new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "20" },
                        new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "40" }
                    ],
                    [new MonjoCondition { Column = "State", Comparison = ComparisonMethods.Equal, Operand = "Active" }]
                ]
            };
            Assert.Equal(2, await Repo.CountAsync(query));

            // (Age == 99 OR Age == 40) AND State == 'Active'
            query.Where[0][0].Operand = "99";
            Assert.Equal(1, await Repo.CountAsync(query));
        }

        // ------------------------------------------------------------------ sorting / pagination

        [Fact]
        public async Task SortingWorksAscendingAndDescending()
        {
            await Repo.InsertManyAsync([
                Person(RunId, age: 30),
                Person(RunId, age: 40),
                Person(RunId, age: 20),
            ]);

            var asc = await Repo.FindManyAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "0" },
                o => new MonjoOrder { Column = "Age" }));
            Assert.Equal([20, 30, 40], asc.Select(p => p.Age));

            var desc = await Repo.FindManyAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "0" },
                o => new MonjoOrder { Column = "Age", Descending = true }));
            Assert.Equal([40, 30, 20], desc.Select(p => p.Age));
        }

        [Fact]
        public async Task PaginatedQueryReturnsCountAndSlice()
        {
            for (var i = 0; i < 5; i++)
                await Repo.InsertAsync(Person(RunId));

            var page1 = await Repo.QueryAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "30" },
                o => new MonjoOrder { Column = "CreatedMoment" },
                index: 1, size: 2));

            Assert.Equal(5, page1.TotalCount);
            Assert.Equal(3, page1.PageCount);
            Assert.Equal(2, page1.Data.Count);

            var page3 = await Repo.QueryAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "30" },
                o => new MonjoOrder { Column = "CreatedMoment" },
                index: 3, size: 2));
            Assert.Equal(1, page3.Data.Count);
        }

        [Fact]
        public async Task FindManyAppliesPageWithoutCounting()
        {
            for (var i = 0; i < 7; i++)
                await Repo.InsertAsync(Person(RunId));

            var slice = await Repo.FindManyAsync(Query(
                c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "30" },
                null, index: 2, size: 3));
            Assert.Equal(3, slice.Count);
        }

        // ------------------------------------------------------------------ count / exists

        [Fact]
        public async Task CountAndExistsReflectFilters()
        {
            Assert.Equal(0, await Repo.CountAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "999" })));
            Assert.False(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "999" })));
        }

        // ------------------------------------------------------------------ updates

        [Fact]
        public async Task UpdateReplacesAndStampsModified()
        {
            var person = await Repo.InsertAsync(Person(RunId));
            var before = person.ModifiedMoment;
            await Task.Delay(20);

            person.Name = person.Name + "-v2";
            person.Age = 44;
            await Repo.UpdateAsync(person);

            var loaded = await Repo.GetByIdAsync(person.Id);
            Assert.Equal(person.Name, loaded.Name);
            Assert.Equal(44, loaded.Age);
            Assert.NotNull(loaded.ModifiedMoment);
            if (before is not null)
                Assert.True(loaded.ModifiedMoment >= before);
        }

        [Fact]
        public async Task UpdateColumnsOnlyTouchesGivenColumns()
        {
            var person = await Repo.InsertAsync(Person(RunId));
            var originalName = person.Name;

            await Repo.UpdateColumnsAsync(
                new MonjoColumnUpdate().Set("Age", 99),
                Query(c => new MonjoCondition { Column = "Id", Comparison = ComparisonMethods.Equal, Operand = person.Id }));

            var loaded = await Repo.GetByIdAsync(person.Id);
            Assert.Equal(99, loaded.Age);
            Assert.Equal(originalName, loaded.Name);
            Assert.NotNull(loaded.ModifiedMoment);
        }

        [Fact]
        public async Task UpsertInsertsThenUpdates()
        {
            var person = Person(RunId);
            await Repo.UpsertAsync(person);
            Assert.NotNull(await Repo.GetByIdAsync(person.Id));

            person.Age = 55;
            await Repo.UpsertAsync(person);

            var loaded = await Repo.GetByIdAsync(person.Id);
            Assert.Equal(55, loaded.Age);
            Assert.Equal(1, await Repo.CountAsync(Query(c => new MonjoCondition { Column = "Id", Comparison = ComparisonMethods.Equal, Operand = person.Id })));
        }

        // ------------------------------------------------------------------ soft delete

        [Fact]
        public async Task SoftDeleteHidesFromReads()
        {
            var person = await Repo.InsertAsync(Person(RunId));
            await Repo.DeleteAsync(person.Id);

            Assert.Null(await Repo.GetByIdAsync(person.Id));
            Assert.False(await Repo.ExistsAsync(Query(c => new MonjoCondition { Column = "Id", Comparison = ComparisonMethods.Equal, Operand = person.Id })));

            // Physically still present: hard delete by id must remove it (twice = idempotent).
            await Repo.HardDeleteAsync(person.Id);
            await Repo.HardDeleteAsync(person.Id);
        }

        [Fact]
        public async Task SoftDeleteManyByFilter()
        {
            await Repo.InsertManyAsync(Enumerable.Range(0, 4).Select(_ => Person(RunId, age: 60)));
            await Repo.DeleteManyAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "60" }));
            Assert.Equal(0, await Repo.CountAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "60" })));
        }

        [Fact]
        public async Task HardDeleteManyRemovesPhysically()
        {
            await Repo.InsertManyAsync(Enumerable.Range(0, 3).Select(_ => Person(RunId, age: 77)));
            await Repo.HardDeleteManyAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "77" }));
            Assert.Equal(0, await CountPhysicalAsync());
        }

        protected abstract Task<long> CountPhysicalAsync();

        // ------------------------------------------------------------------ POCO (no soft delete model)

        [Fact]
        public async Task PocoEntityWithoutSoftDeleteModel()
        {
            var counter = new TestCounter { Id = Guid.NewGuid().ToString("N"), Value = 5 };
            await Counters.InsertAsync(counter);
            Assert.NotNull(await Counters.GetByIdAsync(counter.Id));

            // Delete is physical for POCOs (no IsDeleted column).
            await Counters.DeleteAsync(counter.Id);
            Assert.Null(await Counters.GetByIdAsync(counter.Id));
        }

        // ------------------------------------------------------------------ cancellation

        [Fact]
        public async Task CancellationPropagates()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repo.GetByIdAsync(Guid.NewGuid().ToString("N"), cts.Token));
        }

        // ------------------------------------------------------------------ concurrency

        [Fact]
        public async Task ConcurrentInsertsAreSafe()
        {
            var batch = Enumerable.Range(0, 20).Select(_ => Person(RunId)).ToList();
            await Task.WhenAll(batch.Select(p => Repo.InsertAsync(p)));
            Assert.Equal(20, await Repo.CountAsync(Query(c => new MonjoCondition { Column = "Name", Comparison = ComparisonMethods.Contains, Operand = RunId })));
        }

        [Fact]
        public async Task ConcurrentReadsAreSafe()
        {
            await Repo.InsertAsync(Person(RunId));
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Repo.CountAsync(Query(c => new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "30" })));
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.Equal(1, r));
        }

        // ------------------------------------------------------------------ transactions

        private async Task<MonjoTransaction> BeginTxOrSkipAsync()
        {
            try
            {
                return await Provider.BeginTransactionAsync();
            }
            catch (MonjoNotSupportedException e)
            {
                throw new Xunit.Sdk.SkipException(e.Message);
            }
        }

        [Fact]
        public async Task TransactionCommitMakesChangesVisible()
        {
            await using var tx = await BeginTxOrSkipAsync();
            var person = Person(RunId);
            await Repo.InsertAsync(person);

            Assert.NotNull(await Repo.GetByIdAsync(person.Id)); // visible within the transaction
            await tx.CommitAsync();
            Assert.NotNull(await Repo.GetByIdAsync(person.Id)); // still visible after commit
        }

        [Fact]
        public async Task TransactionRollbackDiscardsChanges()
        {
            await using var tx = await BeginTxOrSkipAsync();
            var person = Person(RunId);
            await Repo.InsertAsync(person);

            await tx.RollbackAsync();
            Assert.Null(await Repo.GetByIdAsync(person.Id));
        }

        // ------------------------------------------------------------------ indexes

        [Fact]
        public async Task DeclaredIndexesAreCreatedOnce()
        {
            await Repo.GetByIdAsync(Guid.NewGuid().ToString("N")); // triggers EnsureEntityReady
            await AssertIndexesCreatedAsync();
            await AssertIndexesCreatedAsync(); // idempotent
        }

        protected abstract Task AssertIndexesCreatedAsync();
    }
}
