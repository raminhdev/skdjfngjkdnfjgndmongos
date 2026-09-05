using System.Collections.Concurrent;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Models.Storages
{
    public class SecurityStampStorage : ConcurrentDictionary<string, string>, ISelfSingletonDependency
    {
        public void UpdateSecurityStamp(string userType, string publicKey, string newSecurityStamp)
        {
            var storageKey = GetStorageKey(userType, publicKey);

            this[storageKey] = newSecurityStamp;
        }

        public string GetSecurityStamp(string userType, string publicKey)
        {
            var storageKey = GetStorageKey(userType, publicKey);
            TryGetValue(storageKey, out var stamp);
            return stamp;
        }

        private static string GetStorageKey(string userType, string publicKey) => $"SS:{userType}:{publicKey}";
    }
}
