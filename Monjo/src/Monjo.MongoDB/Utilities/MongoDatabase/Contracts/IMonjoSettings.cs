namespace Utilities.MongoDatabase.Contracts
{
    /// <summary>Legacy settings contract. Use <c>Monjo.MonjoOptions</c> (via <c>services.AddMonjo(configuration)</c>) for new code.</summary>
    [System.Obsolete("Use Monjo.MonjoOptions and services.AddMonjo(configuration). Kept for compatibility.")]
    public interface IMonjoSettings
    {
        string ConnectionString { get; set; }
        string DatabaseName { get; set; }
    }
}
