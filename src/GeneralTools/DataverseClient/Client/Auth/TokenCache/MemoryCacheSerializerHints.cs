using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Adapted from Microsoft.Identity.Web/TokenCacheProviders/CacheSerializerHints.cs for general use.
// https://github.com/AzureAD/microsoft-identity-web/blob/master/src/Microsoft.Identity.Web/TokenCacheProviders/CacheSerializerHints.cs

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache
{
    /// <summary>
    /// Set of properties that the token cache serialization implementations might use to optimize the cache.
    /// </summary>
    internal class MemoryCacheSerializerHints
    {
        /// <summary>
        /// CancellationToken enabling cooperative cancellation between threads, thread pool, or Task objects.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Suggested cache expiry based on the in-coming token. Use to optimize cache eviction
        /// with the app token cache.
        /// </summary>
        public DateTimeOffset? SuggestedCacheExpiry { get; set; }
    }
}
