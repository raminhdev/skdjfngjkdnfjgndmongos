using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monjo;
using Monjo.Metadata;
using Monjo.PostgreSQL;
using Monjo.SQLite;
using Monjo.Sql;
using Utilities.MongoDatabase.Filter;
using Xunit;

namespace Monjo.Tests
{
    // ------------------------------------------------------------------ configuration

    public class MonjoOptionsTests
    {
        private static IConfiguration Config(params (string, string)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(
                values.ToDictionary(v => v.Item1, v => v.Item2)).Build();

        [Fact]
        public void BindsNewSectionShape()
        {
            var options = MonjoOptions.Bind(Config(
                ("Monjo:Database:Provider", "postgres"),
                ("Monjo:ConnectionString", "Host=localhost"),
                ("Monjo:DatabaseName", "app"),
                ("Monjo:PostgreSql:MaxPoolSize", "42")));

            Assert.Equal("PostgreSQL", options.Provider);
            Assert.Equal("Host=localhost", options.ConnectionString);
            Assert.Equal("app", options.DatabaseName);
            Assert.Equal(42, options.PostgreSql.MaxPoolSize);
        }

        [Fact]
        public void BindsLegacyMonjoSettingsSectionWithMongoFallback()
        {
            var options = MonjoOptions.Bind(Config(
                ("MonjoSettings:ConnectionString", "mongodb://localhost:27017"),
                ("MonjoSettings:DatabaseName", "M1MentorDB")));

            Assert.Equal("MongoDB", options.Provider);
            Assert.Equal("mongodb://localhost:27017", options.ConnectionString);
            Assert.Equal("M1MentorDB", options.DatabaseName);
        }

        [Fact]
        public void NewSectionWinsOverLegacy()
        {
            var options = MonjoOptions.Bind(Config(
                ("Monjo:Database:Provider", "sqlite"),
                ("Monjo:ConnectionString", "test.db"),
                ("MonjoSettings:ConnectionString", "mongodb://localhost:27017")));

            Assert.Equal("SQLite", options.Provider);
            Assert.Equal("test.db", options.ConnectionString);
        }

        [Theory]
        [InlineData("mongodb", "MongoDB")]
        [InlineData("mongo", "MongoDB")]
        [InlineData("PostgreSQL", "PostgreSQL")]
        [InlineData("pg", "PostgreSQL")]
        [InlineData("sqlite3", "SQLite")]
        [InlineData("MySQL", "MySQL")] // unknown: passed through so resolution can produce a precise error
        public void NormalizesProviderNames(string input, string expected)
            => Assert.Equal(expected, MonjoOptions.NormalizeProviderName(input));
    }

    // ------------------------------------------------------------------ provider resolution (DI)

    public class ProviderResolutionTests
    {
        [Fact]
        public void ResolvesConfiguredProviderOnce()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monjo:Database:Provider"] = "SQLite",
                ["Monjo:ConnectionString"] = Path.Combine(Path.GetTempPath(), $"monjo-res-{Guid.NewGuid():N}.db"),
            }).Build();

            services.AddMonjo(configuration);
            services.UseMonjoSqlite();

            using var provider = services.BuildServiceProvider();
            var resolved = provider.GetRequiredService<IMonjoProvider>();
            Assert.Equal("SQLite", resolved.Name);
            // same singleton instance for the connection:
            Assert.Same(resolved.Connection, provider.GetRequiredService<IMonjoConnection>());
        }

        [Fact]
        public void MissingProviderRegistrationFailsFastWithActionableError()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monjo:Database:Provider"] = "PostgreSQL",
                ["Monjo:ConnectionString"] = "Host=localhost",
            }).Build();

            services.AddMonjo(configuration);
            // No UseMonjo*() call.

            using var provider = services.BuildServiceProvider();
            var ex = Assert.Throws<MonjoProviderNotRegisteredException>(() => provider.GetRequiredService<IMonjoProvider>());
            Assert.Contains("PostgreSQL", ex.Message);
        }

        [Fact]
        public void ProviderMismatchFailsFast()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monjo:Database:Provider"] = "PostgreSQL",
                ["Monjo:ConnectionString"] = "Host=localhost",
            }).Build();

            services.AddMonjo(configuration);
            services.UseMonjoSqlite(); // config says PostgreSQL

            using var provider = services.BuildServiceProvider();
            var ex = Assert.Throws<MonjoProviderNotRegisteredException>(() => provider.GetRequiredService<IMonjoProvider>());
            Assert.Contains("PostgreSQL", ex.Message);
            Assert.Contains("SQLite", ex.Message);
        }

        [Fact]
        public void DuplicateProviderRegistrationFailsAtRegistrationTime()
        {
            var services = new ServiceCollection();
            services.AddMonjoProviderFactory("SQLite", (_, o) => throw new NotSupportedException());
            Assert.Throws<MonjoException>(() => services.AddMonjoProviderFactory("PostgreSQL", (_, o) => throw new NotSupportedException()));
        }

        [Fact]
        public void LegacyMongoConnectionSurfaceIsRegisteredByUseMonjoMongoDB()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monjo:ConnectionString"] = "mongodb://localhost:27017",
                ["Monjo:DatabaseName"] = "x",
            }).Build();

            services.AddMonjo(configuration);
            global::Monjo.MongoDB.MonjoMongoDbServiceCollectionExtensions.UseMonjoMongoDB(services);

            using var provider = services.BuildServiceProvider();
            var legacy = provider.GetRequiredService<Utilities.MongoDatabase.Contracts.IMonjoConnection>();
            var core = provider.GetRequiredService<IMonjoConnection>();
            Assert.Same(legacy, core); // one connection, two contracts
        }
    }

    // ------------------------------------------------------------------ metadata

    public class EntityMetadataTests
    {
        [Fact]
        public void ResolvesTableNameIdAndColumns()
        {
            var meta = MonjoEntityMetadata.Get<TestPerson>();
            Assert.Equal("People", meta.TableName);
            Assert.NotNull(meta.Id);
            Assert.Equal("Id", meta.Id!.Property.Name);
            Assert.True(meta.HasSoftDelete);
            Assert.Equal(2, meta.Indexes.Count);
        }

        [Fact]
        public void ResolvesDottedColumnReferences()
        {
            var meta = MonjoEntityMetadata.Get<TestPerson>();
            Assert.Equal("Age", meta.ResolveColumn("TestPerson.Age"));
            Assert.Equal("Age", meta.ResolveColumn("age"));
            Assert.Equal("Age", meta.ResolveColumn("Age"));
            Assert.Null(meta.ResolveColumn("NoSuchColumn"));
        }

        [Fact]
        public void RespectsMonjoColumnAndMonjoIgnoreAttributes()
        {
            var meta = MonjoEntityMetadata.Get<AttributeTestEntity>();
            Assert.Contains(meta.Columns, c => c.Property.Name == "Code" && c.ColumnName == "code_column");
            Assert.DoesNotContain(meta.Columns, c => c.Property.Name == "Ignored");
        }

        [Fact]
        public void GateRunsWorkOnceAndRetriesAfterFailure()
        {
            var runs = 0;
            var key = "gate-test-" + Guid.NewGuid();

            var failures = 0;
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    EntityReadinessGate.EnsureAsync(key, _ =>
                    {
                        Interlocked.Increment(ref runs);
                        if (Interlocked.CompareExchange(ref failures, 1, 0) == 0)
                            throw new Exception("boom");
                        return Task.CompletedTask;
                    }).GetAwaiter().GetResult();
                }
                catch (Exception) when (i == 0)
                {
                    // first run fails; gate must allow retry
                }
            }

            Assert.True(runs >= 2);
        }
    }

    [MonjoTable("AttrEntities")]
    public class AttributeTestEntity
    {
        public string Id { get; set; }
        [MonjoColumn("code_column")] public string Code { get; set; }
        [MonjoIgnore] public string Ignored { get; set; }
    }

    // ------------------------------------------------------------------ MonjoQuery model

    public class MonjoQueryModelTests
    {
        [Fact]
        public void WithBaseMapsBareColumns()
        {
            var query = new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.Equal, Operand = "30" }]],
                Order = [new MonjoOrder { Column = "Age" }],
            };

            var typed = query.WithBase<TestPerson>();
            Assert.Equal("TestPerson.Age", typed.Where[0][0].Column);
            Assert.Equal("TestPerson.Age", typed.Order[0].Column);
        }

        [Fact]
        public void MapRenamesColumnsInWhereAndOrder()
        {
            var query = new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "A", Comparison = ComparisonMethods.Equal, Operand = "1" }]],
                Order = [new MonjoOrder { Column = "A" }],
            };
            query.Map("A", "B");

            Assert.Equal("B", query.Where[0][0].Column);
            Assert.Equal("B", query.Order[0].Column);
        }
    }

    // ------------------------------------------------------------------ SQL translation (unit, no DB)

    public class SqlTranslationTests
    {
        private static readonly MonjoSqliteProvider Provider =
            new(new MonjoOptions { Provider = "SQLite", ConnectionString = "t.db", DatabaseName = "t" });

        private static SqlEntityMetadata Meta()
            => SqlEntityMetadata.Build(Provider, typeof(TestPerson));

        [Fact]
        public void TranslatesWhereOrderAndPageToParameterizedSql()
        {
            var meta = Meta();
            var plan = SqlQueryTranslator.Translate(new MonjoQuery
            {
                Where =
                [
                    [new MonjoCondition { Column = "Age", Comparison = ComparisonMethods.GreaterThan, Operand = "30" }],
                    [
                        new MonjoCondition { Column = "Name", Comparison = ComparisonMethods.Contains, Operand = "ab" },
                        new MonjoCondition { Column = "Nickname", Comparison = ComparisonMethods.IsNull, Operand = null }
                    ]
                ],
                Order =
                [
                    new MonjoOrder { Column = "Age" },
                    new MonjoOrder { Column = "Name", Descending = true }
                ],
                Page = new MonjoPage { Index = 2, Size = 10 },
            }, meta);

            Assert.Equal(
                " WHERE \"Age\" > @p0 AND (\"Name\" LIKE @p1 ESCAPE '\\' OR \"Nickname\" IS NULL)",
                plan.WhereSql);
            Assert.Equal(" ORDER BY \"Age\" ASC, \"Name\" DESC", plan.OrderSql);
            Assert.Equal(10, plan.Limit);
            Assert.Equal(10, plan.Offset);
            Assert.Equal(2, plan.Parameters.Count);
            Assert.Equal("p0", plan.Parameters[0].Name);
            Assert.Equal(30, plan.Parameters[0].Value);
            Assert.Equal("%ab%", plan.Parameters[1].Value);
        }

        [Fact]
        public void EmptyQueryProducesEmptyPlan()
        {
            var meta = Meta();
            var plan = SqlQueryTranslator.Translate(null, meta);
            Assert.Equal(string.Empty, plan.WhereSql);
            Assert.Equal(string.Empty, plan.OrderSql);
            Assert.Null(plan.Limit);
        }

        [Fact]
        public void UnknownColumnFailsFast()
        {
            var meta = Meta();
            Assert.Throws<MonjoException>(() => SqlQueryTranslator.Translate(new MonjoQuery
            {
                Where = [[new MonjoCondition { Column = "Nope", Comparison = ComparisonMethods.Equal, Operand = "1" }]],
            }, meta));
        }

        [Fact]
        public void BuildSelectIncludesLimitOffsetParameters()
        {
            var meta = Meta();
            var plan = SqlQueryTranslator.Translate(new MonjoQuery
            {
                Page = new MonjoPage { Index = 1, Size = 5 },
            }, meta);

            var sql = meta.BuildSelect(plan);
            Assert.StartsWith("SELECT ", sql);
            Assert.EndsWith(" LIMIT @MonjoLimit OFFSET @MonjoOffset", sql);
            Assert.Contains("FROM \"People\"", sql);
        }

        [Fact]
        public void SchemaDdlIsIdempotentShape()
        {
            var meta = Meta();
            Assert.StartsWith("CREATE TABLE IF NOT EXISTS \"People\"", meta.CreateSchemaSql);
            Assert.Contains("\"Id\" TEXT NOT NULL PRIMARY KEY", meta.CreateSchemaSql);
            Assert.Contains("\"IsDeleted\" INTEGER NOT NULL", meta.CreateSchemaSql);
        }
    }
}
