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

        internal SqlOperationContext(DbConnection? ownedConnection, DbConnection connection, DbTransaction? transaction)
        {
            _ownedConnection = ownedConnection;
            Connection = connection;
            Transaction = transaction;
        }

        internal static ValueTask<SqlOperationContext> OpenAsync(SqlMonjoConnection connection, CancellationToken cancellationToken)
        {
            var transaction = MonjoTransactionContext.Current;
            if (transaction?.Native is SqlTransactionBridge bridge)
                return ValueTask.FromResult(new SqlOperationContext(null, bridge.Connection, bridge.Transaction));

            var owned = connection.CreateConnection();
            return OpenOwnedAsync(owned, cancellationToken);
        }

        private static async ValueTask<SqlOperationContext> OpenOwnedAsync(DbConnection owned, CancellationToken cancellationToken)
        {
            await owned.OpenAsync(cancellationToken).ConfigureAwait(false);
            return new SqlOperationContext(owned, owned, null);
        }

        public DbCommand CreateCommand(string commandText)
        {
            var command = Connection.CreateCommand();
            command.CommandText = commandText;
            if (Transaction is { } transaction)
                command.Transaction = transaction;
            return command;
        }

        public void AddParameter(DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
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
        public SqlTransactionBridge(DbConnection connection, DbTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }
    }
}
