using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache
{
    /// <summary>
    /// File backed storage of MSAL tokens for Public Client only.
    /// </summary>
    internal class FileBackedTokenCache
    {
        private StorageCreationProperties _storageProps = null;

        public FileBackedTokenCache(FileBackedTokenCacheHints cacheOptions)
        {
            StorageCreationPropertiesBuilder _builder = null;

            _builder = new StorageCreationPropertiesBuilder(
                cacheOptions.cacheFileName, cacheOptions.cacheFileDirectory)
                .WithLinuxKeyring(cacheOptions.linuxSchemaName, cacheOptions.linuxCollection, cacheOptions.linuxLabel, cacheOptions.linuxAttr1, cacheOptions.linuxAttr2)
                .WithMacKeyChain(cacheOptions.macKeyChainServiceName, cacheOptions.macKeyChainServiceAccount);

            _storageProps = _builder.Build();
        }

        /// <summary>
        /// Initialize and configure the file token cache
        /// </summary>
        /// <param name="tokenCache"></param>
        /// <returns></returns>
        public async Task Initialize(ITokenCache tokenCache)
        {
            if (tokenCache == null)
            {
                throw new ArgumentNullException(nameof(tokenCache));
            }

            if (tokenCache == null)
            {
                throw new ArgumentNullException(nameof(_storageProps));
            }

            var cacheHelper = await MsalCacheHelper.CreateAsync(_storageProps).ConfigureAwait(false);
            cacheHelper.RegisterCache(tokenCache);
        }
    }
}
