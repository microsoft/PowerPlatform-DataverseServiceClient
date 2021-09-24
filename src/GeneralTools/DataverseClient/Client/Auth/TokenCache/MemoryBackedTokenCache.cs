using Microsoft.Identity.Client;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;

// Adapted from Microsoft.Identity.Web/TokenCacheProviders In Memory Token cacheProvider files for use by clients.
// https://github.com/AzureAD/microsoft-identity-web/blob/master/src/Microsoft.Identity.Web/TokenCacheProviders/MsalAbstractTokenCacheProvider.cs
// and
// https://github.com/AzureAD/microsoft-identity-web/blob/master/src/Microsoft.Identity.Web/TokenCacheProviders/InMemory/MsalMemoryTokenCacheProvider.cs
namespace Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache
{

    /// <summary>
    /// DV Client On-board memory cache system for tokens.
    /// </summary>
    internal class MemoryBackedTokenCache
    {
        /// <summary>
        /// .NET Core Memory cache.
        /// </summary>
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// MSAL memory token cache options.
        /// </summary>
        private readonly MemoryTokenCacheOptions _tokenCacheOptions;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MemoryBackedTokenCache(MemoryTokenCacheOptions tokenCacheOptions)
        {
            _tokenCacheOptions = tokenCacheOptions;
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Initializes the token cache serialization.
        /// </summary>
        /// <param name="tokenCache">Token cache to serialize/deserialize.</param>
        public void Initialize(ITokenCache tokenCache)
        {
            if (tokenCache == null)
            {
                throw new ArgumentNullException(nameof(tokenCache));
            }

            tokenCache.SetBeforeAccessAsync(OnBeforeAccessAsync);
            tokenCache.SetAfterAccessAsync(OnAfterAccessAsync);
        }

        /// <summary>
        /// Clean up token cache.
        /// </summary>
        public void ClearCache()
        {
            _memoryCache.Dispose();
        }

        private async Task OnBeforeAccessAsync(TokenCacheNotificationArgs args)
        {
            if (!string.IsNullOrEmpty(args.SuggestedCacheKey))
            {
                byte[] tokenCacheBytes = await ReadCacheBytesAsync(args.SuggestedCacheKey).ConfigureAwait(false);

                //args.TokenCache.DeserializeMsalV3(UnprotectBytes(tokenCacheBytes), shouldClearExistingCache: true);
                args.TokenCache.DeserializeMsalV3(tokenCacheBytes, shouldClearExistingCache: true);
            }
        }


        private async Task OnAfterAccessAsync(TokenCacheNotificationArgs args)
        {
            // The access operation resulted in a cache update.
            if (args.HasStateChanged)
            {
                MemoryCacheSerializerHints cacheSerializerHints = CreateHintsFromArgs(args);

                if (args.HasTokens)
                {
                    await WriteCacheBytesAsync(args.SuggestedCacheKey, args.TokenCache.SerializeMsalV3(), cacheSerializerHints).ConfigureAwait(false);
                }
                else
                {
                    // No token in the cache. we can remove the cache entry
                    await RemoveKeyAsync(args.SuggestedCacheKey).ConfigureAwait(false);
                }
            }
        }


        #region Utilities

        #endregion

        /// <summary>
        /// Removes a token cache identified by its key, from the serialization
        /// cache.
        /// </summary>
        /// <param name="cacheKey">token cache key.</param>
        /// <returns>A <see cref="Task"/> that completes when key removal has completed.</returns>
        protected Task RemoveKeyAsync(string cacheKey)
        {
            _memoryCache.Remove(cacheKey);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads a blob from the serialization cache (identified by its key).
        /// </summary>
        /// <param name="cacheKey">Token cache key.</param>
        /// <returns>Read Bytes.</returns>
        protected Task<byte[]> ReadCacheBytesAsync(string cacheKey)
        {
            byte[] tokenCacheBytes = (byte[])_memoryCache.Get(cacheKey);
            return Task.FromResult(tokenCacheBytes);
        }

        /// <summary>
        /// Writes a token cache blob to the serialization cache (identified by its key).
        /// </summary>
        /// <param name="cacheKey">Token cache key.</param>
        /// <param name="bytes">Bytes to write.</param>
        /// <param name="cacheSerializerHints">Hints for the cache serialization implementation optimization.</param>
        /// <returns>A <see cref="Task"/> that completes when a write operation has completed.</returns>
        protected Task WriteCacheBytesAsync(
            string cacheKey,
            byte[] bytes,
            MemoryCacheSerializerHints cacheSerializerHints)
        {
            TimeSpan? cacheExpiry = null;
            if (cacheSerializerHints != null && cacheSerializerHints?.SuggestedCacheExpiry != null)
            {
                cacheExpiry = cacheSerializerHints.SuggestedCacheExpiry.Value.UtcDateTime - DateTime.UtcNow;
                if (cacheExpiry < TimeSpan.FromTicks(0))
                {
                    System.Diagnostics.Trace.WriteLine($"<<<<<<< Bad Calculation detected. Suggested ExpireTime {cacheSerializerHints.SuggestedCacheExpiry.Value.UtcDateTime} - UTC {DateTime.UtcNow} = {cacheExpiry} - Reseting to 1 hrs.");
                    // CacheExpirey is set in the past, reset to current
                    cacheExpiry = TimeSpan.FromHours(1);
                }
            }

            MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = cacheExpiry ?? _tokenCacheOptions.AbsoluteExpirationRelativeToNow,
                Size = bytes?.Length,
            };


            _memoryCache.Set(cacheKey, bytes, memoryCacheEntryOptions);
            return Task.CompletedTask;
        }

        private static MemoryCacheSerializerHints CreateHintsFromArgs(TokenCacheNotificationArgs args) => new MemoryCacheSerializerHints { CancellationToken = args.CancellationToken, SuggestedCacheExpiry = args.SuggestedCacheExpiry };

    }
}
