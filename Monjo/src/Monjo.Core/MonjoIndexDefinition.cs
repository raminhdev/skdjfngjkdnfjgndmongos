namespace Monjo
{
    /// <summary>One indexed column with direction.</summary>
    public readonly record struct MonjoIndexColumn(string Property, bool Descending);

    /// <summary>
    /// Provider-neutral index definition — the minimal concept that is genuinely expressible on
    /// all supported providers (composite, unique, direction). Provider-specific index features
    /// (sparse, partial, text, expression indexes, ...) remain on the provider-specific APIs
    /// (e.g. Mongo's <c>IMonjoIndexBuilder&lt;T&gt;</c>); they are deliberately NOT part of this
    /// common model.
    /// </summary>
    public sealed record MonjoIndexDefinition(
        string Name,
        IReadOnlyList<MonjoIndexColumn> Columns,
        bool Unique = false)
    {
        /// <summary>Builts a definition from a <see cref="MonjoIndexAttribute"/> (cached per attribute instance by the metadata cache).</summary>
        public static MonjoIndexDefinition FromAttribute(MonjoIndexAttribute attribute, string tableName)
        {
            var parts = attribute.Columns
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var columns = new MonjoIndexColumn[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                columns[i] = new MonjoIndexColumn(
                    parts[i],
                    attribute.Descending is { Length: > i } d && d[i]);

            var name = attribute.Name
                ?? "ix_" + tableName + "_" + string.Join("_", parts);

            return new MonjoIndexDefinition(name, columns, attribute.Unique);
        }
    }
}
