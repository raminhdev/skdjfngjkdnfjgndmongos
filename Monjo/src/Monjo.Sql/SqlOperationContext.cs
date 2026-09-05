using System.Data.Common;

namespace Monjo.Sql
{
    /// <summary>
    /// The connection/transaction a single repository operation runs on.
    /// Outside a transaction: acquires a pooled connection for the operation's lifetime (one
    /// acquire, one release — never a physical connect per call).
    /// Inside an ambient transaction: borrows the transaction's dedicated connection (no pool
    /// churn, all statements share the transaction).
    /// </summary>
    public sealed class SqlOperationContext : IAsyncDisposable
    {
        private readonly DbConnection? _ownedConnection;

        public DbConnection Connection { get; }
        public DbTransaction? Transaction { get; }

        /// <summary>
        /// The dialect that owns this connection's storage representations. Bound parameter
        /// values are converted through it (see <see cref="AddParameter"/>) so a filter on a
        /// Guid, decimal, or enum column matches the stored representation exactly.
        /// </summary>
        public SqlDialect Dialect { get; }

        internal SqlOperationContext(DbConnection? ownedConnection, DbConnection connection, DbTransaction? transaction, SqlDialect dialect)
        {
            _ownedConnection = ownedConnection;
            Connection = connection;
            Transaction = transaction;
            Dialect = dialect;
        }

        internal static ValueTask<SqlOperationContext> OpenAsync(SqlMonjoConnection connection, CancellationToken cancellationToken)
        {
            var transaction = MonjoTransactionContext.Current;
            if (transaction?.Native is SqlTransactionBridge bridge)
                return ValueTask.FromResult(new SqlOperationContext(null, bridge.Connection, bridge.Transaction, bridge.Dialect));

            var owned = connection.CreateConnection();
            return OpenOwnedAsync(owned, connection.Dialect, cancellationToken);
        }

        private static async ValueTask<SqlOperationContext> OpenOwnedAsync(DbConnection owned, SqlDialect dialect, CancellationToken cancellationToken)
        {
            await owned.OpenAsync(cancellationToken).ConfigureAwait(false);
            return new SqlOperationContext(owned, owned, null, dialect);
        }

        public DbCommand CreateCommand(string commandText)
        {
            var command = Connection.CreateCommand();
            command.CommandText = commandText;
            if (Transaction is { } transaction)
                command.Transaction = transaction;
            return command;
        }

        /// <summary>
        /// Binds one parameter, converting the value to the stored representation
        /// (<c>enum → its name</c>, <c>DateTime → normalized UTC</c>, and the dialect conversion
        /// — SQLite: <c>Guid → "N" text</c>, <c>decimal → the sortable encoding</c>). The same
        /// conversion the row writers use, so bound values always match stored values.
        /// </summary>
        public void AddParameter(DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value is null
                ? DBNull.Value
                : Dialect.ToDbValue(SqlValueConverters.ToDb(value));
            command.Parameters.Add(parameter);
        }

        /// <summary>Releases the owned connection back to the pool; borrowed (transactional) connections are left alone.</summary>
        public async ValueTask DisposeAsync()
        {
            if (_ownedConnection is { } owned)
                await owned.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Provider-native bridge for a SQL transaction: the dedicated connection + native transaction.</summary>
    public sealed class SqlTransactionBridge
    {
        public SqlTransactionBridge(DbConnection connection, DbTransaction transaction, SqlDialect dialect)
        {
            Connection = connection;
            Transaction = transaction;
            Dialect = dialect;
        }

        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }
        public SqlDialect Dialect { get; }
    }
}
