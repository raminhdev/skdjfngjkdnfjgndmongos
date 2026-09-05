using System.Linq.Expressions;
using Utilities.MongoDatabase.Extensions;

namespace Utilities.MongoDatabase.Filter
{
    public class MonjoQuery
    {
        public MonjoPage Page { get; set; } = new MonjoPage();
        public IList<IList<MonjoCondition>> Where { get; set; }
        public IList<MonjoOrder> Order { get; set; }

        public MonjoQuery<TBase> WithBase<TBase>()
        {
            return new MonjoQuery<TBase>(this);
        }

        public MonjoQuery Map<TFrom, TTo>(Expression<Func<TFrom, object>> from,
            Expression<Func<TTo, object>> to)
        {
            var fromBase = typeof(TFrom).Name;

            var toBase = typeof(TTo).Name;

            Map($"{fromBase}.{from.Body.GetMemberName()}",
                $"{toBase}.{to.Body.GetMemberName()}");

            return this;
        }

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
