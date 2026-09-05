using System.Data.Common;
using System.Globalization;
using Monjo.Metadata;
using Utilities.MongoDatabase.Filter;

namespace Monjo.Sql
{
    /// <summary>
    /// The SQL implementation of <see cref="IMonjoRepository{T}"/> (PostgreSQL + SQLite).
    /// Hot path per operation: cached SQL text + one pooled connection acquire + one
    /// parameterized command + a compiled row mapper. True async I/O, cancellation propagated,
    /// no reflection, no LINQ, no per-request compiled delegates.
    /// </summary>
    public class SqlMonjoRepository<T> : IMonjoRepository<T> where T : class
    {
        private readonly SqlMonjoProvider _provider;
        private readonly SqlMonjoConnection _connection;
        private readonly SqlEntityMetadata _meta;
        private readonly SqlRowMapper<T> _mapper;

        public string TableName => _meta.Core.TableName;

        internal SqlMonjoRepository(SqlMonjoProvider provider, SqlMonjoConnection connection)
        {
            _provider = provider;
            _connection = connection;
            _meta = provider.GetMetadata<T>();
            _mapper = SqlRowMapper<T>.Create(_meta, provider.Dialect);
        }

        // ------------------------------------------------------------------ reads

        public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = context.CreateCommand(_meta.GetByIdSql);
                context.AddParameter(command, "@Id", _meta.ConvertId(id));

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    return null;
                return _mapper.Read(reader);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<T?> FindOneAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            // Page is ignored by FindOne: exactly one row is fetched.
            try
            {
                var plan = TranslateNoPage(query);
                var sql = _meta.BuildSelect(plan) + " LIMIT 1";
                return await ExecuteSingleAsync(sql, plan, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<IReadOnlyList<T>> FindManyAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
                var plan = SqlQueryTranslator.Translate(query, _meta);
                return await ExecuteListAsync(_meta.BuildSelect(plan), plan, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<MonjoFilteredResult<T>> QueryAsync(MonjoQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                // The result contract includes TotalCount, so the count runs; the data query keeps
                // where/order and adds LIMIT/OFFSET. Two round-trips, both fully server-side.
                var countPlan = SqlQueryTranslator.Translate(NoPage(query), _meta);
                var totalCount = await ExecuteCountAsync(countPlan, cancellationToken).ConfigureAwait(false);

                var dataPlan = SqlQueryTranslator.Translate(query, _meta);
                var data = await ExecuteListAsync(_meta.BuildSelect(dataPlan), dataPlan, cancellationToken).ConfigureAwait(false);

                var pageSize = query.Page is { Size: > 0 } ? query.Page.Size : (data.Count > 0 ? data.Count : 1);
                var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

                return new MonjoFilteredResult<T>
                {
                    TotalCount = totalCount,
                    PageCount = pageCount,
                    Data = data
                };
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<long> CountAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
                var plan = SqlQueryTranslator.Translate(NoPage(query), _meta);
                return await ExecuteCountAsync(plan, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<bool> ExistsAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                var plan = SqlQueryTranslator.Translate(NoPage(query), _meta);
                var sql = _meta.ExistsSql +
                          SqlEntityMetadataExtensions.BuildWhereSql(_meta.SoftDeleteFilterSql, plan.WhereSql) +
                          " LIMIT 1";

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = CreateCommand(context, sql, plan);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        // ------------------------------------------------------------------ writes

        public async Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
                EnsureId(entity);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = context.CreateCommand(_meta.InsertSql);
                _mapper.ConfigureInsert(command, entity);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return entity;
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task InsertManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                var batch = entities.ToList();
                if (batch.Count == 0)
                    return;

                foreach (var entity in batch)
                    EnsureId(entity);

                // One connection, one transaction, N prepared statements: the efficient bulk
                // pattern for both providers (no per-row connection churn). When called inside
                // an ambient transaction the statements enlist in it instead (no nesting).
                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                var ownsTransaction = context.Transaction is null;
                var transaction = ownsTransaction ? context.Connection.BeginTransaction() : null;

                try
                {
                    for (var i = 0; i < batch.Count; i++)
                    {
                        await using var command = context.CreateCommand(_meta.InsertSql);
                        if (transaction is { } tx)
                            command.Transaction = tx;
                        _mapper.ConfigureInsert(command, batch[i]);
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (transaction is { } commitTx)
                        await commitTx.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction is { } rollbackTx)
                    {
                        try { rollbackTx.Rollback(); } finally { rollbackTx.Dispose(); }
                    }
                    throw;
                }
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
                StampModified(entity);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = context.CreateCommand(_meta.UpdateSql);
                _mapper.ConfigureUpdate(command, entity);
                context.AddParameter(command, "@Id", GetIdValue(entity));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task<int> UpdateColumnsAsync(MonjoColumnUpdate update, MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (update.IsEmpty)
                return 0;

            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                // Stamp audit columns unless the caller already set them.
                var meta = _meta.Core;
                var actor = MonjoActorContext.Current;
                if (meta.ModifiedMoment is { } mm && !update.Contains(mm.ColumnName))
                    update.Set(mm.ColumnName, DateTime.UtcNow);
                if (meta.ModifiedBy is { } mb && !update.Contains(mb.ColumnName))
                    update.Set(mb.ColumnName, actor.PublicKey ?? "system");
                if (meta.ModifiedByInfo is { } mbi && !update.Contains(mbi.ColumnName))
                    update.Set(mbi.ColumnName, actor.DisplayInfo ?? "system : system");

                var plan = SqlQueryTranslator.Translate(NoPage(filter), _meta);

                var setClauses = new List<string>(update.Count);
                var extraParameters = new List<SqlParameter>(update.Count);
                foreach (var entry in update.Values)
                {
                    var column = _meta.FindColumn(entry.Key)
                        ?? throw new MonjoException($"Unknown column or property '{entry.Key}' for table '{_meta.Core.TableName}'.");
                    var name = "c" + setClauses.Count;
                    setClauses.Add(column.Quoted + " = @" + name);
                    extraParameters.Add(new SqlParameter(name, SqlValueConverters.ConvertOperand(entry.Value, column.Core.NonNullableType)));
                }

                var sql = $"UPDATE {_meta.TableQuoted} SET {string.Join(", ", setClauses)}" +
                          SqlEntityMetadataExtensions.BuildWhereSql(_meta.SoftDeleteFilterSql, plan.WhereSql);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = CreateCommand(context, sql, plan, extraParameters);
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task UpsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
                EnsureId(entity);
                StampModified(entity);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = context.CreateCommand(_meta.UpsertSql);
                _mapper.ConfigureInsert(command, entity);
                _mapper.ConfigureUpsertUpdate(command, entity);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        // ------------------------------------------------------------------ deletes

        public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);

                if (_meta.SoftDeleteByIdSql.Length == 0)
                {
                    // Entity has no soft-delete model: physical delete.
                    await using var command = context.CreateCommand(_meta.HardDeleteByIdSql);
                    context.AddParameter(command, "@Id", _meta.ConvertId(id));
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                var actor = MonjoActorContext.Current;
                await using var command = context.CreateCommand(_meta.SoftDeleteByIdSql);
                context.AddParameter(command, "@Id", _meta.ConvertId(id));
                if (_meta.Core.DeletedMoment is not null)
                    context.AddParameter(command, "@DeletedMoment", DateTime.UtcNow);
                if (_meta.Core.DeletedBy is not null)
                    context.AddParameter(command, "@DeletedBy", actor.PublicKey ?? "system");
                if (_meta.Core.DeletedByInfo is not null)
                    context.AddParameter(command, "@DeletedByInfo", actor.DisplayInfo ?? "system : system");

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task DeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                if (_meta.SoftDeleteByIdSql.Length == 0)
                {
                    await HardDeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var plan = SqlQueryTranslator.Translate(NoPage(filter), _meta);
                var actor = MonjoActorContext.Current;

                var setClauses = new List<string>
                {
                    $"{SqlDialect.Quote(_meta.Core.IsDeleted!.ColumnName)} = {_provider.Dialect.TrueLiteral}"
                };
                if (_meta.Core.DeletedMoment is { } dm) setClauses.Add($"{SqlDialect.Quote(dm.ColumnName)} = @DeletedMoment");
                if (_meta.Core.DeletedBy is { } db) setClauses.Add($"{SqlDialect.Quote(db.ColumnName)} = @DeletedBy");
                if (_meta.Core.DeletedByInfo is { } dbi) setClauses.Add($"{SqlDialect.Quote(dbi.ColumnName)} = @DeletedByInfo");

                var sql = $"UPDATE {_meta.TableQuoted} SET {string.Join(", ", setClauses)}" +
                          SqlEntityMetadataExtensions.BuildWhereSql(_meta.SoftDeleteFilterSql, plan.WhereSql);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = CreateCommand(context, sql, plan);
                if (_meta.Core.DeletedMoment is not null)
                    context.AddParameter(command, "@DeletedMoment", DateTime.UtcNow);
                if (_meta.Core.DeletedBy is not null)
                    context.AddParameter(command, "@DeletedBy", actor.PublicKey ?? "system");
                if (_meta.Core.DeletedByInfo is not null)
                    context.AddParameter(command, "@DeletedByInfo", actor.DisplayInfo ?? "system : system");

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task HardDeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = context.CreateCommand(_meta.HardDeleteByIdSql);
                context.AddParameter(command, "@Id", _meta.ConvertId(id));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        public async Task HardDeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

                // Hard delete targets every row (including soft-deleted ones) matching the filter.
                var plan = SqlQueryTranslator.Translate(NoPage(filter), _meta);
                var sql = $"DELETE FROM {_meta.TableQuoted}" + plan.WhereSql;

                await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
                await using var command = CreateCommand(context, sql, plan);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw Translate(e);
            }
        }

        // ------------------------------------------------------------------ infrastructure

        /// <summary>Maps provider-native exceptions onto Monjo's provider-independent ones (identity rethrow when unmapped).</summary>
        private Exception Translate(Exception exception)
            => _provider.Dialect.TranslateException(exception) ?? exception;

        /// <summary>Strips order/page: only the WHERE clause is relevant for count/exists/update/delete/find-one.</summary>
        private static MonjoQuery? NoPage(MonjoQuery? query)
            => query is null ? null : new MonjoQuery { Where = query.Where, Order = null, Page = null };

        private SqlQueryPlan TranslateNoPage(MonjoQuery? query)
            => SqlQueryTranslator.Translate(NoPage(query), _meta);

        private DbCommand CreateCommand(SqlOperationContext context, string sql, SqlQueryPlan plan, List<SqlParameter>? extraParameters = null)
        {
            var command = context.CreateCommand(sql);
            command.CommandTimeout = _provider.CommandTimeoutSeconds;

            foreach (var parameter in plan.Parameters)
                context.AddParameter(command, "@" + parameter.Name, parameter.Value);

            if (plan.Limit is { } limit)
            {
                context.AddParameter(command, "@MonjoLimit", limit);
                context.AddParameter(command, "@MonjoOffset", plan.Offset);
            }

            if (extraParameters is not null)
                foreach (var parameter in extraParameters)
                    context.AddParameter(command, "@" + parameter.Name, parameter.Value);

            return command;
        }

        private async Task<long> ExecuteCountAsync(SqlQueryPlan plan, CancellationToken cancellationToken)
        {
            var sql = _meta.CountSql + SqlEntityMetadataExtensions.BuildWhereSql(_meta.SoftDeleteFilterSql, plan.WhereSql);
            await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
            await using var command = CreateCommand(context, sql, plan);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        private async Task<IReadOnlyList<T>> ExecuteListAsync(string sql, SqlQueryPlan plan, CancellationToken cancellationToken)
        {
            var list = new List<T>();
            await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
            await using var command = CreateCommand(context, sql, plan);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                list.Add(_mapper.Read(reader));
            return list;
        }

        private async Task<T?> ExecuteSingleAsync(string sql, SqlQueryPlan plan, CancellationToken cancellationToken)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            await using var context = await SqlOperationContext.OpenAsync(_connection, cancellationToken).ConfigureAwait(false);
            await using var command = CreateCommand(context, sql, plan);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;
            return _mapper.Read(reader);
        }

        private void EnsureId(T entity)
        {
            var idMetadata = _meta.Core.Id!;
            var current = idMetadata.Property.GetValue(entity);
            if (current is null)
            {
                var idType = idMetadata.NonNullableType;
                object generated = idType == typeof(Guid)
                    ? Guid.NewGuid()
                    : idType == typeof(string)
                        ? Guid.NewGuid().ToString("N")
                        : throw new MonjoException(
                            $"'{typeof(T).Name}.Id' is null and the SQL providers do not generate " +
                            $"{idType.Name} identifiers. Set an explicit Id before inserting " +
                            "(string and Guid identifiers are generated automatically).");
                idMetadata.Property.SetValue(entity, generated);
            }
            else if (current is 0 or (short)0 or (byte)0 or (long)0)
            {
                // A numeric 0 cannot be distinguished from "unset", and the SQL providers do not
                // generate numeric identifiers — inserting 0 would silently create rows under a
                // bogus id. Fail deterministically with a clear message instead.
                throw new MonjoException(
                    $"'{typeof(T).Name}.Id' is 0 and the SQL providers do not generate numeric identifiers. " +
                    "Set an explicit non-zero Id before inserting (string and Guid identifiers are generated automatically).");
            }
        }

        private object GetIdValue(T entity)
            => _meta.ConvertId(_meta.Core.Id!.Property.GetValue(entity)!);

        private void StampModified(T entity)
        {
            var meta = _meta.Core;
            var actor = MonjoActorContext.Current;
            if (meta.ModifiedMoment is { } mm) mm.Property.SetValue(entity, DateTime.UtcNow);
            if (meta.ModifiedBy is { } mb) mb.Property.SetValue(entity, actor.PublicKey ?? "system");
            if (meta.ModifiedByInfo is { } mbi) mbi.Property.SetValue(entity, actor.DisplayInfo ?? "system : system");
        }
    }

    public static class SqlEntityMetadataExtensions
    {
        /// <summary>
        /// Combines the entity's soft-delete predicate and the translated user predicate into a
        /// single WHERE clause (the one and only place where both meet).
        /// <paramref name="whereSql"/> is <c>""</c> or <c>" WHERE &lt;predicates&gt;"</c>.
        /// </summary>
        internal static string BuildWhereSql(string softDeleteFilter, string whereSql)
        {
            var userPredicate = whereSql.Length >= 7 && whereSql.StartsWith(" WHERE ")
                ? whereSql.AsSpan(7).Trim().ToString()
                : string.Empty;

            string predicate;
            if (softDeleteFilter.Length > 0 && userPredicate.Length > 0)
                predicate = softDeleteFilter + " AND " + userPredicate;
            else if (softDeleteFilter.Length > 0)
                predicate = softDeleteFilter;
            else
                predicate = userPredicate;

            return predicate.Length > 0 ? " WHERE " + predicate : string.Empty;
        }

        /// <summary>Base SELECT with soft-delete + where/order/limit/offset applied (limit/offset are parameters).</summary>
        public static string BuildSelect(this SqlEntityMetadata meta, SqlQueryPlan plan)
        {
            var sql = "SELECT " + meta.SelectColumnsSql + " FROM " + meta.TableQuoted +
                      BuildWhereSql(meta.SoftDeleteFilterSql, plan.WhereSql) + plan.OrderSql;
            if (plan.Limit is not null)
                sql += " LIMIT @MonjoLimit OFFSET @MonjoOffset";
            return sql;
        }
    }
}
