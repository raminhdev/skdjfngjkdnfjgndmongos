namespace Monjo
{
    /// <summary>Base class for provider-independent Monjo errors (configuration, model, capability gaps).</summary>
    public class MonjoException(string message) : Exception(message);

    /// <summary>Thrown when the configured provider has no registration (missing package or missing <c>UseMonjo*</c> call).</summary>
    public sealed class MonjoProviderNotRegisteredException(string message, Exception? innerException = null)
        : MonjoException(message, innerException);

    /// <summary>Thrown when a requested capability is not supported by the provider (e.g. transactions on a standalone Mongo).</summary>
    public sealed class MonjoNotSupportedException(string message, Exception? innerException = null)
        : MonjoException(message, innerException);

    /// <summary>Thrown when SQLite reports a busy/locked database after the configured busy timeout.</summary>
    public sealed class MonjoBusyException(string message, Exception innerException)
        : MonjoException(message, innerException);
}
