using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Auth class that will create a Workload Identity based authentication for Dataverse Service Client using the specified Azure DevOps Service Connection.
    /// </summary>
    public class AzPipelineFederatedIdentityAuth
    {
        private AzurePipelinesCredential _pipelineCredential;
        private AzurePipelinesCredentialOptions _credentialOptions;
        private readonly bool _autoResolveAuthorityAndTenant;
        private Dictionary<Uri, List<string>> _scopesList;
        private Dictionary<Uri, AccessToken?> _cacheList;
        private ILogger _logger;
        private string _tenantId; 
        private string _clientId;
        private string _serviceConnectionId;
        private string _systemAccessTokenEnvVarName;

        /// <summary>
        /// Creates a new instance of the ServiceClient class using the AzDevOps Service Connection
        /// </summary>
        /// <param name="tenantId">TenantId for the service connection</param>
        /// <param name="clientId">ClientId for the service connection</param>
        /// <param name="serviceConnectionId">Service Connection Id of AzDevOps ServiceConnection configured for workload identity</param>
        /// <param name="connectionOptions">Dataverse ServiceClient Connection Options</param>
        /// <param name="configurationOptions">Dataverse ServiceClient Configuration Options. Default = null</param>
        /// <param name="systemAccessTokenEnvVarName">Environment Variable that has the current AzDevOps System Access Token.  Default=SYSTEM_ACCESSTOKEN</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static ServiceClient CreateServiceClient(
            string tenantId,
            string clientId,
            string serviceConnectionId,
            ConnectionOptions connectionOptions,
            ConfigurationOptions configurationOptions = null,
            string systemAccessTokenEnvVarName = "SYSTEM_ACCESSTOKEN")
        {
            if (connectionOptions == null)
            {
                throw new ArgumentException("ConnectionOptions are required");
            }
            if (connectionOptions.ServiceUri == null)
            {
                throw new ArgumentException("ConnectionOptions.ServiceUri is required");
            }
            connectionOptions.AuthenticationType = AuthenticationType.ExternalTokenManagement; // force the authentication type to be external token management.

            AzPipelineFederatedIdentityAuth azAuth = new AzPipelineFederatedIdentityAuth( tenantId, clientId, serviceConnectionId, systemAccessTokenEnvVarName, true, connectionOptions.Logger);
            connectionOptions.AccessTokenProviderFunctionAsync = azAuth.GetAccessToken;
            return new ServiceClient(connectionOptions, false, configurationOptions);
        }

        /// <summary>
        /// Creates an instance of the AzPipelineFederatedIdentityAuth class
        /// </summary>
        /// <param name="autoResolveAuthorityAndTenant">Should resolve Dataverse authority and resource from url.</param>
        /// <param name="serviceConnectionId">Service Connection Id of AzDevOps ServiceConnection configured for workload identity</param>
        /// <param name="clientId">ClientId for the service connection</param>
        /// <param name="tenantId">TenantId for the service connection</param>
        /// <param name="SystemAccessTokenEnvVarName">Environment Variable that has the current AzDevOps System Access Token.  Default=SYSTEM_ACCESSTOKEN</param>
        /// <param name="logger">ILogger instance</param>
        public AzPipelineFederatedIdentityAuth(string tenantId, string clientId, string serviceConnectionId, string SystemAccessTokenEnvVarName,  bool autoResolveAuthorityAndTenant, ILogger logger = null)
        {
            _tenantId = tenantId;
            _clientId = clientId;
            _serviceConnectionId = serviceConnectionId;
            _systemAccessTokenEnvVarName = SystemAccessTokenEnvVarName;
            _autoResolveAuthorityAndTenant = autoResolveAuthorityAndTenant;
            _logger = logger;
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
            if (_pipelineCredential == null)
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

            if (accessToken == null)
            {
                Stopwatch sw = Stopwatch.StartNew();
                _logger.LogDebug("Getting new access token for {0}", instanceUri);
                accessToken = await _pipelineCredential.GetTokenAsync(new TokenRequestContext(ResolveScopesList(instanceUri)), System.Threading.CancellationToken.None).ConfigureAwait(false);
                _logger.LogDebug("Access token retrieved in {0}ms", sw.ElapsedMilliseconds);
                sw.Stop();
                if (_cacheList.ContainsKey(instanceUri))
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
            _credentialOptions ??= new AzurePipelinesCredentialOptions();

            if (_autoResolveAuthorityAndTenant)
            {
                _logger.LogDebug("Resolving authority and tenant for {0}", instanceUrl);
                using var httpClient = new System.Net.Http.HttpClient();
                Auth.AuthorityResolver authorityResolver = new Auth.AuthorityResolver(httpClient);
                var authDetails = await authorityResolver.ProbeForExpectedAuthentication(instanceUrl).ConfigureAwait(false);
                resourceUri = authDetails.Resource;
                _credentialOptions.AuthorityHost = authDetails.Authority;
                //_credentialOptions.TenantId = authDetails.Authority.Segments[1].Replace("/", "");

                _logger.LogDebug("Authority and tenant resolved in {0}ms", sw.ElapsedMilliseconds);
                _logger.LogDebug("Initialize Creds - found authority with name " + (string.IsNullOrEmpty(authDetails.Authority.ToString()) ? "<Not Provided>" : authDetails.Authority.ToString()));
                _logger.LogDebug("Initialize Creds - found resource with name " + (string.IsNullOrEmpty(authDetails.Resource.ToString()) ? "<Not Provided>" : authDetails.Resource.ToString()));
                //_logger.LogDebug("Initialize Creds - found tenantId " + (string.IsNullOrEmpty(_credentialOptions.TenantId) ? "<Not Provided>" : _credentialOptions.TenantId));
            }

            _pipelineCredential = new AzurePipelinesCredential(_tenantId, _clientId, _serviceConnectionId, Environment.GetEnvironmentVariable(_systemAccessTokenEnvVarName), _credentialOptions);

            _logger.LogDebug("Credentials initialized in {0}ms", sw.ElapsedMilliseconds);
            sw.Start();

            return resourceUri;
        }

    }
}
