namespace Monjo
{
    /// <summary>The identity performing writes (audit fields). A 24-byte value type — passing it allocates nothing.</summary>
    public readonly record struct MonjoActor(string PublicKey, string DisplayInfo)
    {
        public bool HasIdentity => !string.IsNullOrEmpty(PublicKey);
    }

    /// <summary>
    /// Ambient identity used to fill audit fields (<c>CreatedBy</c>/<c>ModifiedBy</c>/...).
    /// Set ONCE at startup with a delegate to the application's user context; a single delegate
    /// call per write, no DI lookups in hot paths, no allocations beyond the struct.
    /// </summary>
    /// <example>
    /// <code>
    /// MonjoActorContext.SetProvider(() =>
    /// {
    ///     var user = CurrentRequestContext.User ?? new RequestUserInfo();
    ///     return new MonjoActor(user.PublicKey, user.DisplayInfo);
    /// });
    /// </code>
    /// </example>
    public static class MonjoActorContext
    {
        private static Func<MonjoActor>? _provider;

        /// <summary>Installs (or removes, with <c>null</c>) the identity provider. Intended to be called once at startup.</summary>
        public static void SetProvider(Func<MonjoActor>? provider) => _provider = provider;

        /// <summary>The current actor; defaults to <c>(null, null)</c> when no provider is installed.</summary>
        public static MonjoActor Current
        {
            get
            {
                var provider = _provider;
                return provider is null ? default : provider();
            }
        }
    }
}
