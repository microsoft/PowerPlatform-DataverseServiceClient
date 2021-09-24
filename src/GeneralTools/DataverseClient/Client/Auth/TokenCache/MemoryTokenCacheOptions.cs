using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache
{
    internal class MemoryTokenCacheOptions
    {
        /// By default, the sliding expiration is set for 14 days
        public MemoryTokenCacheOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14);
        }

        /// <summary>
        /// Gets or sets the value of the duration after which the cache entry will expire unless it's used
        /// This is the duration the tokens are kept in memory cache.
        /// In production, a higher value, up-to 90 days is recommended.
        /// </summary>
        /// <value>
        /// The AbsoluteExpirationRelativeToNow value.
        /// </value>
        public TimeSpan AbsoluteExpirationRelativeToNow
        {
            get;
            set;
        }
    }
}
