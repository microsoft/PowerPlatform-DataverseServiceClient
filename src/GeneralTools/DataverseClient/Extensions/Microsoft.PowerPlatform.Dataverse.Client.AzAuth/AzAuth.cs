using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Base auth class to create an authentication client for Az Authentication
    /// This module will provide a means to create an Dataverse Service Client using Az Authentication
    /// </summary>
    public class AzAuth
    {

        private DefaultAzureCredential _defaultAzureCredential;
        private DefaultAzureCredentialOptions _credentialOptions;
        private readonly bool _autoResolveAuthorityAndTenant;
        private Dictionary<Uri, List<string>> _scopesList;
        private Dictionary<Uri, AccessToken?> _cacheList;
        private ILogger _logger;


        /// <summary>
        /// Creates a new instance of the ServiceClient class
        /// </summary>
        /// <param name="instanceUrl"></param>
        /// <param name="autoResolveAuthorityAndTenant"></param>
        /// <param name="credentialOptions"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static ServiceClient CreateServiceClient(string instanceUrl, bool autoResolveAuthorityAndTenant = true, ILogger logger = null, DefaultAzureCredentialOptions credentialOptions = null)
        {
            if (!Uri.IsWellFormedUriString(instanceUrl, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException("Invalid instance URL");
            }
            AzAuth azAuth = new AzAuth(autoResolveAuthorityAndTenant, credentialOptions , logger);
            return new ServiceClient(new Uri(instanceUrl), tokenProviderFunction: azAuth.GetAccessToken, logger: logger);
        }

        /// <summary>
        /// Build this based on connection and configuration options. 
        /// </summary>
        /// <param name="autoResolveAuthorityAndTenant"></param>
        /// <param name="connectionOptions"></param>
        /// <param name="configurationOptions"></param>
        /// <param name="credentialOptions"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static ServiceClient CreateServiceClient(ConnectionOptions connectionOptions, ConfigurationOptions configurationOptions = null, bool autoResolveAuthorityAndTenant = true, DefaultAzureCredentialOptions credentialOptions = null)
        {
            if ( connectionOptions == null )
            {
                throw new ArgumentException("ConnectionOptions are required");
            }
            if (connectionOptions.ServiceUri == null)
            {
                throw new ArgumentException("ConnectionOptions.ServiceUri is required");
            }
            connectionOptions.AuthenticationType = AuthenticationType.ExternalTokenManagement; // force the authentication type to be external token management.

            AzAuth azAuth = new AzAuth(autoResolveAuthorityAndTenant, credentialOptions, connectionOptions.Logger);
            connectionOptions.AccessTokenProviderFunctionAsync = azAuth.GetAccessToken;
            return new ServiceClient(connectionOptions, false, configurationOptions); 
        }



        /// <summary>
        /// Creates a new instance of the AzAuth class
        /// </summary>
        /// <param name="autoResolveAuthorityAndTenant"></param>
        /// <param name="credentialOptions"></param>
        /// <param name="logger"></param>
        public AzAuth(bool autoResolveAuthorityAndTenant, DefaultAzureCredentialOptions credentialOptions = null, ILogger logger = null)
        {
            _credentialOptions = credentialOptions;
            _autoResolveAuthorityAndTenant = autoResolveAuthorityAndTenant;
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Returns the current access token for the connected ServiceClient instance
        /// </summary>
        /// <param name="instanceUrl"></param>
        /// <returns></returns>
        public async Task<string> GetAccessToken(string instanceUrl)
        {
            if (!Uri.IsWellFormedUriString(instanceUrl, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException("Invalid instance URL");
            }
            AccessToken? accessToken = null; 
            Uri instanceUri = new Uri(instanceUrl);
            if (_defaultAzureCredential == null)
            {
                Uri resourceUri = await InitializeCredentials(instanceUri).ConfigureAwait(false);
                ResolveScopesList(instanceUri, resourceUri);
            }

            // Get or create existing token. 
            _cacheList ??= new Dictionary<Uri, AccessToken?>();
            if (_cacheList.ContainsKey(instanceUri))
            {
                accessToken = _cacheList[instanceUri];
                if (accessToken.HasValue && accessToken.Value.ExpiresOn < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)))
                {
                    accessToken = null; // flush the access token if it is about to expire. 
                    _cacheList.Remove(instanceUri);
                }
            }

            if ( accessToken == null)
            {
                Stopwatch sw = Stopwatch.StartNew();
                _logger.LogDebug("Getting new access token for {0}", instanceUri);
                accessToken = await _defaultAzureCredential.GetTokenAsync(new Azure.Core.TokenRequestContext(ResolveScopesList(instanceUri))).ConfigureAwait(false);
                _logger.LogDebug("Access token retrieved in {0}ms", sw.ElapsedMilliseconds);
                sw.Stop();
                if(_cacheList.ContainsKey(instanceUri))
                {
                    _cacheList[instanceUri] = accessToken;
                }
                else
                {
                    _cacheList.Add(instanceUri, accessToken);
                }
            }

            if (accessToken == null)
            {
                throw new Exception("Failed to retrieve access token");
            }

            return accessToken.Value.Token;
        }

        /// <summary>
        /// gets or creates the scope list for the current instance.
        /// </summary>
        /// <param name="instanceUrl"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private string[] ResolveScopesList(Uri instanceUrl, Uri resource = null)
        {
            _scopesList ??= new Dictionary<Uri, List<string>>();

            if (_scopesList.TryGetValue(instanceUrl, out List<string> foundList))
                return foundList.ToArray();

            if (resource == null)
                throw new ArgumentNullException("Resource URI is required");

            _scopesList.Add(instanceUrl, new List<string> { $"{resource}.default" });
            return _scopesList[instanceUrl].ToArray();
        }

        /// <summary>
        /// Initialize the credentials for the current instance
        /// </summary>
        /// <param name="instanceUrl"></param>
        /// <returns></returns>
        private async Task<Uri> InitializeCredentials(Uri instanceUrl)
        {
            _logger.LogDebug("Initializing credentials for {0}", instanceUrl);
            Stopwatch sw = Stopwatch.StartNew();
           
            Uri resourceUri = null;
            _credentialOptions ??= new DefaultAzureCredentialOptions();

            if (_autoResolveAuthorityAndTenant)
            {
                _logger.LogDebug("Resolving authority and tenant for {0}", instanceUrl);
                using var httpClient = new System.Net.Http.HttpClient();
                Auth.AuthorityResolver authorityResolver = new Auth.AuthorityResolver(httpClient);
                var authDetails = await authorityResolver.ProbeForExpectedAuthentication(instanceUrl).ConfigureAwait(false);
                resourceUri = authDetails.Resource;
                _credentialOptions.AuthorityHost = authDetails.Authority;
                _credentialOptions.TenantId = authDetails.Authority.Segments[1].Replace("/", "");

                _logger.LogDebug("Authority and tenant resolved in {0}ms", sw.ElapsedMilliseconds);
                _logger.LogDebug("Initialize Creds - found authority with name " + (string.IsNullOrEmpty(authDetails.Authority.ToString()) ? "<Not Provided>" : authDetails.Authority.ToString()));
                _logger.LogDebug("Initialize Creds - found resource with name " + (string.IsNullOrEmpty(authDetails.Resource.ToString()) ? "<Not Provided>" : authDetails.Resource.ToString()));
                _logger.LogDebug("Initialize Creds - found tenantId " + (string.IsNullOrEmpty(_credentialOptions.TenantId) ? "<Not Provided>" : _credentialOptions.TenantId));
            }
            _defaultAzureCredential = new DefaultAzureCredential(_credentialOptions);

            _logger.LogDebug("Credentials initialized in {0}ms", sw.ElapsedMilliseconds);
            sw.Start();

            return resourceUri;
        }
        
    }
}
