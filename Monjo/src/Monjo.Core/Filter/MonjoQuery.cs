using System.Linq.Expressions;
using Utilities.MongoDatabase.Extensions;

namespace Utilities.MongoDatabase.Filter
{
    /// <summary>
    /// The common query model shared by all Monjo providers. It describes WHAT the caller wants
    /// (filter groups, ordering, page) — never HOW the database executes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Where"/> is a list of AND-ed groups; each group is a list of OR-ed
    /// <see cref="MonjoCondition"/>s. A condition column may be prefixed with the (result) type
    /// name, e.g. <c>"UserFilteredResult.PublicKey"</c> — the prefix is resolved/ignored by the
    /// provider translator.
    /// </para>
    /// <para>
    /// Providers translate this model natively: the MongoDB provider maps it onto Mongo
    /// filter/sort definitions, the SQL providers map it onto parameterized SQL. No expression
    /// trees are built by the model itself; per-request allocations are limited to the small
    /// filter/SQL structures the translator needs.
    /// </para>
    /// </remarks>
    public class MonjoQuery
    {
        /// <summary>
        /// Page bounds (1-based Index, Size rows). The pre-existing default ({1, 50}) is kept so
        /// queries that do not specify a page behave exactly as before (first 50 rows); set
        /// <c>Page = null</c> to opt out of paging entirely.
        /// </summary>
        public MonjoPage Page { get; set; } = new MonjoPage();
        public IList<IList<MonjoCondition>> Where { get; set; }
        public IList<MonjoOrder> Order { get; set; }

        /// <summary>Prepares this query for a different result type by mapping bare column names to <c>TBase.column</c>.</summary>
        public MonjoQuery<TBase> WithBase<TBase>()
        {
            return new MonjoQuery<TBase>(this);
        }

        /// <summary>Maps a column referenced by <paramref name="from"/> onto the column referenced by <paramref name="to"/>.</summary>
        public MonjoQuery Map<TFrom, TTo>(Expression<Func<TFrom, object>> from,
            Expression<Func<TTo, object>> to)
        {
            var fromBase = typeof(TFrom).Name;

            var toBase = typeof(TTo).Name;

            Map($"{fromBase}.{from.Body.GetMemberName()}",
                $"{toBase}.{to.Body.GetMemberName()}");

            return this;
        }

        /// <summary>Maps a raw column name onto another raw column name (filter and order clauses).</summary>
        public MonjoQuery Map(string from, string to)
        {
            if (Where != null)
                foreach (var conditions in Where)
                    foreach (var condition in conditions)
                        if (condition.Column == from)
                            condition.Column = to;

            if (Order != null)
                foreach (var order in Order)
                    if (order.Column == from)
                        order.Column = to;

            return this;
        }
    }

    /// <summary>A <see cref="MonjoQuery"/> whose bare column names are mapped onto <typeparamref name="TBase"/> at construction time.</summary>
    public class MonjoQuery<TBase> : MonjoQuery
    {
        public MonjoQuery(MonjoQuery monjoQuery)
        {
            Page = monjoQuery.Page;
            Where = monjoQuery.Where;
            Order = monjoQuery.Order;

            mappAllToBase();
        }

        private void mappAllToBase()
        {
            var columns = Where?.SelectMany(conditions =>
                                conditions.Where(condition => !condition.Column.Contains("."))
                                .Select(condition => condition.Column)
                            ).ToList() ?? new List<string>();

            columns.AddRange(Order?.Where(order => !order.Column.Contains("."))
                                        .Select(order => order.Column)
                                        .ToList() ?? []);

            foreach (var column in columns.Distinct())
                Map(column, $"{typeof(TBase).Name}.{column}");
        }

        public MonjoQuery<TBase> Map<TTo>(Expression<Func<TBase, object>> from,
            Expression<Func<TTo, object>> to)
        {
            return (MonjoQuery<TBase>)base.Map(from, to);
        }
    }
}
