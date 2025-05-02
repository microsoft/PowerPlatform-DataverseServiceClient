// Ignore Spelling: Dataverse

#region using
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Connector;
using Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises;
using Microsoft.PowerPlatform.Dataverse.Client.Builder;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Primary implementation of the API interface for Dataverse.
    /// </summary>
    public class ServiceClient : IOrganizationService, IOrganizationServiceAsync2, IDisposable
    {
        #region Vars


        /// <summary>
        /// Cached Object collection, used for pick lists and such.
        /// </summary>
        internal Dictionary<string, Dictionary<string, object>> _CachObject; //Cache object.

        /// <summary>
        /// List of Dataverse Language ID's
        /// </summary>
        internal List<int> _loadedLCIDList;

        /// <summary>
        /// Name of the cache object.
        /// </summary>
        internal string _cachObjecName = ".LookupCache";

        /// <summary>
        /// Logging object for the Dataverse Interface.
        /// </summary>
        internal DataverseTraceLogger _logEntry;

        /// <summary>
        /// Dataverse Web Service Connector layer
        /// </summary>
        internal ConnectionService _connectionSvc;

        /// <summary>
        /// Dynamic app utility
        /// </summary>
        internal DynamicEntityUtility _dynamicAppUtility = null;

        /// <summary>
        /// Configuration
        /// </summary>
        internal IOptions<ConfigurationOptions> _configuration = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>();

        /// <summary>
        /// Metadata Utility
        /// </summary>
        internal MetadataUtility _metadataUtlity = null;

        /// <summary>
        /// This is an internal Lock object,  used to sync communication with Dataverse.
        /// </summary>
        internal object _lockObject = new object();

        /// <summary>
        /// This is an internal lock object, used to sync clone operations.
        /// </summary>
        internal object _cloneLockObject = new object();

        /// <summary>
        /// BatchManager for Execute Multiple.
        /// </summary>
        internal BatchManager _batchManager = null;

        ///// <summary>
        ///// To cache the token
        ///// </summary>
        //private static CdsServiceClientTokenCache _CdsServiceClientTokenCache;

        private bool _disableConnectionLocking = false;

        /// <summary>
        /// SDK Version property backer.
        /// </summary>
        public string _sdkVersionProperty = null;

        /// <summary>
        /// Value used by the retry system while the code is running,
        /// this value can scale up and down based on throttling limits.
        /// </summary>
        private TimeSpan _retryPauseTimeRunning;

        /// <summary>
        /// Internal Organization Service Interface used for Testing
        /// </summary>
        internal IOrganizationService _testOrgSvcInterface { get; set; }

        /// <summary>
        /// ConnectionsOptions object used with connection presetup and staging for connect. 
        /// </summary>
        private ConnectionOptions _setupConnectionOptions = null;

        /// <summary>
        /// Connections Options holder for connection call. 
        /// </summary>
        private ConnectionOptions _connectionOptions = null; 

        #endregion

        #region Properties

        /// <summary>
        ///  Internal OnLineClient
        /// </summary>
        internal OrganizationWebProxyClientAsync OrganizationWebProxyClient
        {
            get
            {
                if (_connectionSvc != null)
                {
                    if (_connectionSvc.WebClient == null)
                    {
                        return null;
                    }
                    else
                        return _connectionSvc.WebClient;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        ///  Internal OnLineClient
        /// </summary>
        internal OrganizationServiceProxyAsync OnPremClient
        {
            get
            {
                if (_connectionSvc != null)
                {
                    if (_connectionSvc.OnPremClient == null)
                    {
                        return null;
                    }
                    else
                        return _connectionSvc.OnPremClient;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Enabled Log Capture in memory
        /// This capability enables logs that would normally be sent to your configured
        /// </summary>
        public static bool InMemoryLogCollectionEnabled { get; set; } = Utils.AppSettingsHelper.GetAppSetting<bool>("InMemoryLogCollectionEnabled", false);

        /// <summary>
        /// This is the number of minuets that logs will be retained before being purged from memory. Default is 5 min.
        /// This capability controls how long the log cache is kept in memory.
        /// </summary>
        public static TimeSpan InMemoryLogCollectionTimeOutMinutes { get; set; } = Utils.AppSettingsHelper.GetAppSettingTimeSpan("InMemoryLogCollectionTimeOutMinutes", Utils.AppSettingsHelper.TimeSpanFromKey.Minutes, TimeSpan.FromMinutes(5));

        /// <summary>
        /// Gets or sets max retry count.
        /// </summary>
        public int MaxRetryCount
        {
            get { return _configuration.Value.MaxRetryCount; }
            set { _configuration.Value.MaxRetryCount = value; }
        }

        /// <summary>
        /// Gets or sets retry pause time.
        /// </summary>
        public TimeSpan RetryPauseTime
        {
            get { return _configuration.Value.RetryPauseTime; }
            set { _configuration.Value.RetryPauseTime = value; }
        }

        /// <summary>
        /// if true the service is ready to accept requests.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// if true then Batch Operations are available.
        /// </summary>
        public bool IsBatchOperationsAvailable
        {
            get
            {
                if (_batchManager != null)
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// OAuth Authority.
        /// </summary>
        public string Authority
        {
            get
            {   //Restricting to only OAuth login
                if (_connectionSvc != null && (
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth ||
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.Certificate ||
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.ExternalTokenManagement ||
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.ClientSecret))
                    return _connectionSvc.Authority;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Logged in Office365 UserId using OAuth.
        /// </summary>
        public string OAuthUserId
        {
            get
            {   //Restricting to only OAuth login
                if (_connectionSvc != null && _connectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                    return _connectionSvc.UserId;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Gets or Sets the Max Connection Timeout for the connection.
        /// Default setting is 2 min,
        /// this property can also be set via app.config/app.settings with the property MaxConnectionTimeOutMinutes
        /// </summary>
        public static TimeSpan MaxConnectionTimeout
        {
            get
            {
                return ConnectionService.MaxConnectionTimeout;
            }
            set
            {
                ConnectionService.MaxConnectionTimeout = value;
            }
        }

        /// <summary>
        /// Authentication Type to use
        /// </summary>
        public AuthenticationType ActiveAuthenticationType
        {
            get
            {
                if (_connectionSvc != null)
                    return _connectionSvc.AuthenticationTypeInUse;
                else
                    return AuthenticationType.InvalidConnection;
            }
        }

        /// <summary>
        /// Returns the current access token in Use to connect to Dataverse.
        /// Note: this is only available when a token based authentication process is in use.
        /// </summary>
        public string CurrentAccessToken
        {
            get
            {
                if (_connectionSvc != null && (
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth ||
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.Certificate ||
                    _connectionSvc.AuthenticationTypeInUse == AuthenticationType.ClientSecret))
                {
                    if (_connectionSvc._authenticationResultContainer != null && !string.IsNullOrEmpty(_connectionSvc._resource) && !string.IsNullOrEmpty(_connectionSvc._clientId))
                    {
                        if (_connectionSvc._authenticationResultContainer.ExpiresOn.ToUniversalTime() < DateTime.UtcNow.AddMinutes(1))
                        {
                            // Force a refresh if the token is about to expire
                            return _connectionSvc.RefreshClientTokenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                        return _connectionSvc._authenticationResultContainer.AccessToken;
                    }
                    // if not configured, return empty string
                    return string.Empty; 
                }
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Defaults to True.
        /// <para>When true, this setting applies the default connection routing strategy to connections to Dataverse.</para>
        /// <para>This will 'prefer' a given node when interacting with Dataverse which improves overall connection performance.</para>
        /// <para>When set to false, each call to Dataverse will be routed to any given node supporting your organization. </para>
        /// <para>See https://docs.microsoft.com/en-us/powerapps/developer/data-platform/api-limits#remove-the-affinity-cookie for proper use.</para>
        /// </summary>
        public bool EnableAffinityCookie
        {
            get
            {
                if (_connectionSvc != null)
                    return _connectionSvc.EnableCookieRelay;
                else
                    return true;
            }
            set
            {
                if (_connectionSvc != null)
                    _connectionSvc.EnableCookieRelay = value;
            }
        }

        /// <summary>
        /// Pointer to Dataverse Service.
        /// </summary>
        internal IOrganizationService DataverseService
        {
            get
            {
                // Added to support testing of ServiceClient direct code.
                if (_testOrgSvcInterface != null)
                    return _testOrgSvcInterface;

                if (_connectionSvc != null)
                {
                    if (_connectionSvc.WebClient != null)
                        return _connectionSvc.WebClient;
                    else
                        return _connectionSvc.OnPremClient;
                }
                else return null;
            }
        }

        /// <summary>
        /// Pointer to Dataverse Service.
        /// </summary>
        internal IOrganizationServiceAsync DataverseServiceAsync
        {
            get
            {
                // Added to support testing of ServiceClient direct code.
                //if (_testOrgSvcInterface != null)
                //    return _testOrgSvcInterface;

                if (_connectionSvc != null)
                {
                    if (_connectionSvc.WebClient != null)
                        return _connectionSvc.WebClient;
                    else
                        return _connectionSvc.OnPremClient;
                }
                else return null;
            }
        }

        /// <summary>
        /// Current user Record.
        /// </summary>
        internal WhoAmIResponse SystemUser
        {
            get
            {
                if (_connectionSvc != null)
                {
                    if (_connectionSvc.CurrentUser != null)
                        return _connectionSvc.CurrentUser;
                    else
                    {
                        WhoAmIResponse resp = Task.Run(async () => await _connectionSvc.GetWhoAmIDetails(this).ConfigureAwait(false)).ConfigureAwait(false).GetAwaiter().GetResult();
                        _connectionSvc.CurrentUser = resp;
                        return resp;
                    }
                }
                else
                    return null;
            }
            set
            {
                _connectionSvc.CurrentUser = value;
            }
        }

        /// <summary>
        /// Returns the Last String Error that was created by the Dataverse Connection
        /// </summary>
        public string LastError { get { if (_logEntry != null) return _logEntry.LastError; else return string.Empty; } }

        /// <summary>
        /// Returns the Last Exception from Dataverse.
        /// </summary>
        public Exception LastException { get { if (_logEntry != null) return _logEntry.LastException; else return null; } }

        /// <summary>
        /// Returns the Actual URI used to connect to Dataverse.
        /// this URI could be influenced by user defined variables.
        /// </summary>
        public Uri ConnectedOrgUriActual { get { if (_connectionSvc != null) return _connectionSvc.ConnectOrgUriActual; else return null; } }

        /// <summary>
        /// Returns the friendly name of the connected Dataverse instance.
        /// </summary>
        public string ConnectedOrgFriendlyName { get { if (_connectionSvc != null) return _connectionSvc.ConnectedOrgFriendlyName; else return null; } }
        /// <summary>
        ///
        /// Returns the unique name for the org that has been connected.
        /// </summary>
        public string ConnectedOrgUniqueName { get { if (_connectionSvc != null) return _connectionSvc.CustomerOrganization; else return null; } }
        /// <summary>
        /// Returns the endpoint collection for the connected org.
        /// </summary>
        public EndpointCollection ConnectedOrgPublishedEndpoints { get { if (_connectionSvc != null) return _connectionSvc.ConnectedOrgPublishedEndpoints; else return null; } }

        /// <summary>
        /// OrganizationDetails for the currently connected environment.
        /// </summary>
        public OrganizationDetail OrganizationDetail { get { if (_connectionSvc != null) return _connectionSvc.ConnectedOrganizationDetail; else return null; } }

        /// <summary>
        /// This is the connection lock object that is used to control connection access for various threads. This should be used if you are using the Datavers queries via Linq to lock the connection
        /// </summary>
        internal object ConnectionLockObject { get { return _lockObject; } }

        /// <summary>
        /// Returns the Version Number of the connected Dataverse organization.
        /// If access before the Organization is connected, value returned will be null or 0.0
        /// </summary>
        public Version ConnectedOrgVersion { get { if (_connectionSvc != null) return _connectionSvc?.OrganizationVersion; else return new Version(0, 0); } }

        /// <summary>
        /// ID of the connected organization.
        /// </summary>
        public Guid ConnectedOrgId { get { if (_connectionSvc != null) return _connectionSvc.OrganizationId; else return Guid.Empty; } }

        /// <summary>
        /// Disabled internal cross thread safeties, this will gain much higher performance, however it places the requirements of thread safety on you, the developer.
        /// </summary>
        public bool DisableCrossThreadSafeties { get { return _disableConnectionLocking; } set { _disableConnectionLocking = value; } }

        /// <summary>
        /// Returns the access token from the attached function.
        /// This is set via the ServiceContructor that accepts a target url and a function to return an access token.
        /// </summary>
        internal Func<string, Task<string>> GetAccessToken { get; set; } = null;

        /// <summary>
        /// Returns any additional or custom headers that need to be added to the request to Dataverse. 
        /// </summary>
        internal Func<Task<Dictionary<string, string>>> GetCustomHeaders { get; set; } = null;

        /// <summary>
        /// Gets or Sets the current caller ID
        /// </summary>
        public Guid CallerId
        {
            get
            {
                if (OrganizationWebProxyClient != null)
                    return OrganizationWebProxyClient.CallerId;
                return Guid.Empty;
            }
            set
            {
                if (OrganizationWebProxyClient != null)
                    OrganizationWebProxyClient.CallerId = value;
            }
        }

        /// <summary>
        /// Gets or Sets the AAD Object ID of the caller.
        /// This is supported for Xrm 8.1 + only
        /// </summary>
        public Guid? CallerAADObjectId
        {
            get
            {
                if (_connectionSvc != null)
                {
                    return _connectionSvc.CallerAADObjectId;
                }
                return null;
            }
            set
            {
                if (Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(_connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AADCallerIDSupported))
                    _connectionSvc.CallerAADObjectId = value;
                else
                {
                    if (_connectionSvc?.OrganizationVersion != null)
                    {
                        _connectionSvc.CallerAADObjectId = null; // Null value as this is not supported for this version.
                        _logEntry.Log($"Setting CallerAADObject ID not supported in version {_connectionSvc?.OrganizationVersion}. Dataverse version {Utilities.FeatureVersionMinimums.AADCallerIDSupported} or higher is required.", TraceEventType.Warning);
                    }
                }
            }
        }

        /// <summary>
        /// This ID is used to support Dataverse Telemetry when trouble shooting SDK based errors.
        /// When Set by the caller, all Dataverse API Actions executed by this client will be tracked under a single session id for later troubleshooting.
        /// For example, you are able to group all actions in a given run of your client ( several creates / reads and such ) under a given tracking id that is shared on all requests.
        /// providing this ID when reporting a problem will aid in trouble shooting your issue.
        /// </summary>
        public Guid? SessionTrackingId
        {
            get
            {
                if (_connectionSvc != null)
                {
                    return _connectionSvc.SessionTrackingId;
                }
                return null;
            }

            set
            {
                if (_connectionSvc != null && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(ConnectedOrgVersion, Utilities.FeatureVersionMinimums.SessionTrackingSupported))
                    _connectionSvc.SessionTrackingId = value;
                else
                {
                    if (_connectionSvc?.OrganizationVersion != null)
                    {
                        _connectionSvc.SessionTrackingId = null; // Null value as this is not supported for this version.
                        _logEntry.Log($"Setting SessionTrackingId ID not supported in version {_connectionSvc?.OrganizationVersion}. Dataverse version {Utilities.FeatureVersionMinimums.SessionTrackingSupported} or greater is required.", TraceEventType.Warning);
                    }
                }
            }

        }

        /// <summary>
        /// This will force the Dataverse server to refresh the current metadata cache with current DB config.
        /// Note, that this is a performance impacting property.
        /// Use of this flag will slow down operations server side as the server is required to check for consistency of the platform metadata against disk on each API call executed.
        /// It is recommended to use this ONLY in conjunction with solution import or delete operations.
        /// </summary>
        public bool ForceServerMetadataCacheConsistency
        {
            get
            {
                if (_connectionSvc != null)
                {
                    return _connectionSvc.ForceServerCacheConsistency;
                }
                return false;
            }
            set
            {
                if (ConnectedOrgVersion == Version.Parse("9.0.0.0")) // Default setting found as this is a version number that is hard set during setup of connection. it is not possible to actually have an environment with this version number
                {
                    //force update version
                    _logEntry.Log($"Requested current version from Dataverse, found: {OrganizationDetail.OrganizationVersion}");
                }

                if (_connectionSvc != null && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(_connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.ForceConsistencySupported))
                    _connectionSvc.ForceServerCacheConsistency = value;
                else
                {
                    if (_connectionSvc?.OrganizationVersion != null)
                    {
                        _connectionSvc.ForceServerCacheConsistency = false; // Null value as this is not supported for this version.
                        _logEntry.Log($"Setting ForceServerMetadataCacheConsistency not supported in version {_connectionSvc?.OrganizationVersion}. Dataverse version {Utilities.FeatureVersionMinimums.ForceConsistencySupported} or higher is required." , TraceEventType.Warning);
                    }
                }
            }

        }

        /// <summary>
        /// Get the Client SDK version property
        /// </summary>
        public string SdkVersionProperty
        {
            get
            {
                if (string.IsNullOrEmpty(_sdkVersionProperty))
                {
                    _sdkVersionProperty = Environs.XrmSdkFileVersion;
                }
                return _sdkVersionProperty;
            }
        }

        /// <summary>
        /// Gets the Tenant Id of the current connection.
        /// </summary>
        public Guid TenantId
        {
            get
            {
                if (_connectionSvc != null)
                {
                    return _connectionSvc.TenantId;
                }
                else
                    return Guid.Empty;
            }
        }

        /// <summary>
        /// Gets the PowerPlatform Environment Id of the environment that is hosting this instance of Dataverse
        /// </summary>
        public string EnvironmentId
        {
            get
            {
                if (_connectionSvc != null)
                {
                    return _connectionSvc.EnvironmentId;
                }
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Use Dataverse Web API instead of Dataverse Object Model service where possible - Defaults to False.
        /// </summary>
        public bool UseWebApi
        {
            get => _configuration.Value.UseWebApi;
            set => _configuration.Value.UseWebApi = value;
        }

        /// <summary>
        /// Server Hint for the number of concurrent threads that would provide optimal processing. 
        /// </summary>
        public int RecommendedDegreesOfParallelism => _connectionSvc.RecommendedDegreesOfParallelism;

        #endregion

        #region Constructor and Setup methods

        /// <summary>
        /// Default / Non accessible constructor
        /// </summary>
        private ServiceClient()
        { }

        /// <summary>
        /// Internal constructor used for testing.
        /// </summary>
        /// <param name="orgSvc"></param>
        /// <param name="httpClient"></param>
        /// <param name="targetVersion"></param>
        /// <param name="baseConnectUrl"></param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        internal ServiceClient(IOrganizationService orgSvc, HttpClient httpClient, string baseConnectUrl, Version targetVersion = null, ILogger logger = null)
        {
            _testOrgSvcInterface = orgSvc;
            _logEntry = new DataverseTraceLogger(logger)
            {
                LogRetentionDuration = new TimeSpan(0, 10, 0),
                EnabledInMemoryLogCapture = true
            };
            _connectionSvc = new ConnectionService(orgSvc, baseConnectUrl, httpClient, logger);

            if (targetVersion != null)
                _connectionSvc.OrganizationVersion = targetVersion;

            _batchManager = new BatchManager(_logEntry);
            _metadataUtlity = new MetadataUtility(this);
            _dynamicAppUtility = new DynamicEntityUtility(this, _metadataUtlity);
            IsReady = true; 
        }

        /// <summary>
        /// ServiceClient to accept the connectionstring as a parameter
        /// </summary>
        /// <param name="dataverseConnectionString"></param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(string dataverseConnectionString, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(dataverseConnectionString))
                throw new ArgumentNullException("Dataverse ConnectionString", "Dataverse ConnectionString cannot be null or empty.");

            ConnectToService(dataverseConnectionString, logger);
        }

        /// <summary>
        /// Creates an instance of ServiceClient who's authentication is managed by the caller.
        /// This requires the caller to implement a function that will accept the InstanceURI as a string will return the access token as a string on demand when the ServiceClient requires it.
        /// This approach is recommended when working with WebApplications or applications that are required to implement an on Behalf of flow for user authentication.
        /// </summary>
        /// <param name="instanceUrl">URL of the Dataverse instance to connect too.</param>
        /// <param name="tokenProviderFunction">Function that will be called when the access token is require for interaction with Dataverse.  This function must accept a string (InstanceURI) and return a string (accesstoken) </param>
        /// <param name="useUniqueInstance">A value of "true" Forces the ServiceClient to create a new connection to the Dataverse instance vs reusing an existing connection, Defaults to true.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(Uri instanceUrl, Func<string, Task<string>> tokenProviderFunction, bool useUniqueInstance = true, ILogger logger = null)
        {
            GetAccessToken = tokenProviderFunction ??
                throw new DataverseConnectionException("tokenProviderFunction required for this constructor", new ArgumentNullException("tokenProviderFunction"));  // Set the function pointer or access.

            CreateServiceConnection(
                   null, AuthenticationType.ExternalTokenManagement, string.Empty, string.Empty, string.Empty, null,
                   string.Empty, null, string.Empty, string.Empty, string.Empty, true, useUniqueInstance, null,
                   string.Empty, null, PromptBehavior.Never, null, string.Empty, StoreName.My, null, instanceUrl, externalLogger: logger);
        }

        /// <summary>
        /// Log in with OAuth for online connections,
        /// <para>
        /// Utilizes the discovery system to resolve the correct endpoint to use given the provided server orgName, user name and password.
        /// </para>
        /// </summary>
        /// <param name="userId">User Id supplied</param>
        /// <param name="password">Password for login</param>
        /// <param name="regionGeo">Region where server is provisioned in for login</param>
        /// <param name="orgName">Name of the organization to connect</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="promptBehavior">The prompt Behavior.</param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        /// <param name="tokenCacheStorePath">(Optional)The token cache path where token cache file is placed. if string.empty, will use default cache file store, if null, will use in memory cache</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(string userId, SecureString password, string regionGeo, string orgName, bool useUniqueInstance, OrganizationDetail orgDetail,
                string clientId, Uri redirectUri, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false, string tokenCacheStorePath = null, ILogger logger = null)
        {
            CreateServiceConnection(
                    null, AuthenticationType.OAuth, string.Empty, string.Empty, orgName, null,
                    userId, password, string.Empty, regionGeo, string.Empty, true, useUniqueInstance, orgDetail,
                    clientId, redirectUri, promptBehavior, null, useDefaultCreds: useDefaultCreds, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath);
        }

        /// <summary>
        /// Log in with OAuth for online connections,
        /// <para>
        /// Will attempt to connect directly to the URL provided for the API endpoint.
        /// </para>
        /// </summary>
        /// <param name="userId">User Id supplied</param>
        /// <param name="password">Password for login</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="promptBehavior">The prompt Behavior.</param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        /// <param name="hostUri">API or Instance URI to access the Dataverse environment.</param>
        /// <param name="tokenCacheStorePath">(Optional)The token cache path where token cache file is placed. if string.empty, will use default cache file store, if null, will use in memory cache</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(string userId, SecureString password, Uri hostUri, bool useUniqueInstance,
                string clientId, Uri redirectUri, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false, string tokenCacheStorePath = null, ILogger logger = null)
        {
            CreateServiceConnection(
                    null, AuthenticationType.OAuth, string.Empty, string.Empty, null, null,
                    userId, password, string.Empty, null, string.Empty, true, useUniqueInstance, null,
                    clientId, redirectUri, promptBehavior, null, useDefaultCreds: useDefaultCreds, instanceUrl: hostUri, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath);
        }

        /// <summary>
        /// Log in with OAuth for On-Premises connections.
        /// </summary>
        /// <param name="userId">User Id supplied</param>
        /// <param name="password">Password for login</param>
        /// <param name="domain">Domain</param>
        /// <param name="hostName">Host name of the server that is hosting the Dataverse web service</param>
        /// <param name="port">Port number on the Dataverse Host Server ( usually 444 )</param>
        /// <param name="orgName">Organization name for the Dataverse Instance.</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="promptBehavior">The prompt Behavior.</param>
        /// <param name="tokenCacheStorePath">(Optional)The token cache path where token cache file is placed. if string.empty, will use default cache file store, if null, will use in memory cache</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(string userId, SecureString password, string domain, string hostName, string port, string orgName, bool useSsl, bool useUniqueInstance,
                OrganizationDetail orgDetail, string clientId, Uri redirectUri, PromptBehavior promptBehavior = PromptBehavior.Auto, string tokenCacheStorePath = null, ILogger logger = null)
        {
            CreateServiceConnection(
                    null, AuthenticationType.OAuth, hostName, port, orgName, null,
                    userId, password, domain, string.Empty, string.Empty, useSsl, useUniqueInstance, orgDetail,
                    clientId, redirectUri, promptBehavior, null, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath);
        }

        /// <summary>
        /// Log in with Certificate Auth On-Premises connections.
        /// </summary>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">URL of the Dataverse instance to connect too</param>
        /// <param name="orgName">Organization name for the Dataverse Instance.</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(X509Certificate2 certificate, StoreName certificateStoreName, string certificateThumbPrint, Uri instanceUrl, string orgName, bool useSsl, bool useUniqueInstance,
                OrganizationDetail orgDetail, string clientId, Uri redirectUri, ILogger logger = null)
        {
            if ((string.IsNullOrEmpty(clientId) || redirectUri == null))
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using Certificate Authentication you have to specify clientId and redirectUri.");
            }

            if (string.IsNullOrEmpty(certificateThumbPrint) && certificate == null)
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using Certificate Authentication you have to specify either a certificate thumbprint or provide a certificate to use.");
            }

            CreateServiceConnection(
                    null, AuthenticationType.Certificate, string.Empty, string.Empty, orgName, null,
                    string.Empty, null, string.Empty, string.Empty, string.Empty, useSsl, useUniqueInstance, orgDetail,
                    clientId, redirectUri, PromptBehavior.Never, null, certificateThumbPrint, certificateStoreName, certificate, instanceUrl, externalLogger: logger);
        }

        /// <summary>
        /// Log in with Certificate Auth OnLine connections.
        /// This requires the org API URI.
        /// </summary>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">API URL of the Dataverse instance to connect too</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(X509Certificate2 certificate, StoreName certificateStoreName, string certificateThumbPrint, Uri instanceUrl, bool useUniqueInstance, OrganizationDetail orgDetail,
                string clientId, Uri redirectUri, ILogger logger = null)
        {
            if ((string.IsNullOrEmpty(clientId)))
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using Certificate Authentication you have to specify clientId.");
            }

            if (string.IsNullOrEmpty(certificateThumbPrint) && certificate == null)
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using Certificate Authentication you have to specify either a certificate thumbprint or provide a certificate to use.");
            }

            CreateServiceConnection(
                    null, AuthenticationType.Certificate, string.Empty, string.Empty, string.Empty, null,
                    string.Empty, null, string.Empty, string.Empty, string.Empty, true, useUniqueInstance, orgDetail,
                    clientId, redirectUri, PromptBehavior.Never, null, certificateThumbPrint, certificateStoreName, certificate, instanceUrl, externalLogger: logger);
        }

        /// <summary>
        /// ClientID \ ClientSecret Based Authentication flow.
        /// </summary>
        /// <param name="instanceUrl">Direct URL of Dataverse instance to connect too.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="clientSecret">Client Secret for Client Id.</param>
        /// <param name="useUniqueInstance">Use unique instance or reuse current connection.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(Uri instanceUrl, string clientId, string clientSecret, bool useUniqueInstance, ILogger logger = null)
        {
            CreateServiceConnection(null,
                AuthenticationType.ClientSecret,
                string.Empty, string.Empty, string.Empty, null, string.Empty,
                MakeSecureString(clientSecret), string.Empty, string.Empty, string.Empty, true, useUniqueInstance,
                null, clientId, null, PromptBehavior.Never, null, null, instanceUrl: instanceUrl, externalLogger: logger);
        }

        /// <summary>
        /// ClientID \ ClientSecret Based Authentication flow, allowing for Secure Client ID passing.
        /// </summary>
        /// <param name="instanceUrl">Direct URL of Dataverse instance to connect too.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="clientSecret">Client Secret for Client Id.</param>
        /// <param name="useUniqueInstance">Use unique instance or reuse current connection.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        public ServiceClient(Uri instanceUrl, string clientId, SecureString clientSecret, bool useUniqueInstance, ILogger logger = null)
        {
            CreateServiceConnection(null,
                AuthenticationType.ClientSecret,
                string.Empty, string.Empty, string.Empty, null, string.Empty,
                clientSecret, string.Empty, string.Empty, string.Empty, true, useUniqueInstance,
                null, clientId, null, PromptBehavior.Never, null, null, instanceUrl: instanceUrl, externalLogger: logger);
        }

        /// <summary>
        /// Creating the ServiceClient Connection with a ConnectionOptions Object and ConfigurationOptions Object. This allows for deferred create of a Dataverse Service Client. 
        /// </summary>
        /// <param name="connectionOptions">Describes how the Connection should be created.</param>
        /// <param name="deferConnection">False by Default,  if True, stages the properties of the connection and returns.  You must call .Connect() to complete the connection. </param>
        /// <param name="serviceClientConfiguration">Described Configuration Options for the connection.</param>
        public ServiceClient(ConnectionOptions connectionOptions, bool deferConnection = false,  ConfigurationOptions serviceClientConfiguration = null)
        {
            // store the options and ready for connect call. 
            _setupConnectionOptions = connectionOptions;

            //_configuration
            if ( serviceClientConfiguration != null )
                _configuration.Value.UpdateOptions(serviceClientConfiguration);

            
            // External auth. 
            if (connectionOptions.AccessTokenProviderFunctionAsync != null)
            {
                connectionOptions.AuthenticationType = AuthenticationType.ExternalTokenManagement;
                GetAccessToken = connectionOptions.AccessTokenProviderFunctionAsync;
            }

            // Add custom header support. 
            if ( connectionOptions.RequestAdditionalHeadersAsync != null )
            {
                GetCustomHeaders = connectionOptions.RequestAdditionalHeadersAsync;
            }

            if (deferConnection)
            {
                _connectionOptions = connectionOptions;
                if (connectionOptions.Logger != null)
                    connectionOptions.Logger.LogInformation("Connection creation has been deferred at user request"); 
                return;     
            }

            // Form Connection string. 
            string connectionString = ConnectionStringConstants.CreateConnectionStringFromConnectionOptions(connectionOptions);
            ConnectToService(connectionString, connectionOptions.Logger);
        }

        /// <summary>
        /// Connects the Dataverse Service Client instance when staged with the Deferd Connection constructor. 
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            if (_connectionOptions != null && IsReady == false)
            {
                if (_connectionOptions.Logger != null)
                    _connectionOptions.Logger.LogInformation("Initiating connection to Dataverse");

                string connectionString = ConnectionStringConstants.CreateConnectionStringFromConnectionOptions(_connectionOptions);
                ConnectToService(connectionString, _connectionOptions.Logger);
                _connectionOptions = null;
                return true;
            }
            else
                return false; 
        }

        /// <summary>
        /// Parse the given connection string
        /// Connects to Dataverse using CreateWebServiceConnection
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        internal void ConnectToService(string connectionString, ILogger logger = null)
        {
            var parsedConnStr = DataverseConnectionStringProcessor.Parse(connectionString, logger);

            if (parsedConnStr.AuthenticationType == AuthenticationType.InvalidConnection)
                throw new ArgumentException("AuthType is invalid.  Please see Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType for supported authentication types.", "AuthType")
                { HelpLink = "https://docs.microsoft.com/powerapps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect" };

            var serviceUri = parsedConnStr.ServiceUri;

            var networkCredentials = parsedConnStr.ClientCredentials != null && parsedConnStr.ClientCredentials.Windows != null ?
                parsedConnStr.ClientCredentials.Windows.ClientCredential : System.Net.CredentialCache.DefaultNetworkCredentials;

            string orgName = parsedConnStr.Organization;

            if ((parsedConnStr.SkipDiscovery && parsedConnStr.ServiceUri != null) && string.IsNullOrEmpty(orgName))
                // Orgname is mandatory if skip discovery is not passed
                throw new ArgumentNullException("Dataverse Instance Name or URL name Required",
                        parsedConnStr.IsOnPremOauth ?
                        $"Unable to determine instance name to connect to from passed instance Uri. Uri does not match specification for OnPrem instances." :
                        $"Unable to determine instance name to connect to from passed instance Uri, Uri does not match known online deployments.");

            string homesRealm = parsedConnStr.HomeRealmUri != null ? parsedConnStr.HomeRealmUri.AbsoluteUri : string.Empty;

            string userId = parsedConnStr.UserId;
            string password = parsedConnStr.Password;
            string domainname = parsedConnStr.DomainName;
            string onlineRegion = parsedConnStr.Geo;
            string clientId = parsedConnStr.ClientId;
            string hostname = serviceUri.Host;
            string port = Convert.ToString(serviceUri.Port);

            Uri redirectUri = parsedConnStr.RedirectUri;

            bool useSsl = serviceUri.Scheme == "https" ? true : false;

            switch (parsedConnStr.AuthenticationType)
            {
                case AuthenticationType.OAuth:
                    hostname = parsedConnStr.IsOnPremOauth ? hostname : string.Empty; //
                    port = parsedConnStr.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) && redirectUri == null)
                    {
                        throw new ArgumentNullException("ClientId and Redirect Name", "ClientId or Redirect uri cannot be null or empty.");
                    }


                    CreateServiceConnection(null, parsedConnStr.AuthenticationType, hostname, port, orgName, networkCredentials, userId,
                                                MakeSecureString(password), domainname, onlineRegion, homesRealm, useSsl, parsedConnStr.UseUniqueConnectionInstance,
                                                    null, clientId, redirectUri, parsedConnStr.PromptBehavior, instanceUrl: parsedConnStr.SkipDiscovery ? parsedConnStr.ServiceUri : null,
                                                    useDefaultCreds: parsedConnStr.UseCurrentUser, externalLogger: logger, tokenCacheStorePath: parsedConnStr.TokenCacheStorePath);
                    break;
                case AuthenticationType.Certificate:
                    hostname = parsedConnStr.IsOnPremOauth ? hostname : string.Empty; //
                    port = parsedConnStr.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(parsedConnStr.CertThumbprint))
                    {
                        throw new ArgumentNullException("ClientId or Certificate Thumbprint must be populated for Certificate Auth Type.");
                    }

                    StoreName targetStoreName = StoreName.My;
                    if (!string.IsNullOrEmpty(parsedConnStr.CertStoreName))
                    {
                        Enum.TryParse<StoreName>(parsedConnStr.CertStoreName, out targetStoreName);
                    }

                    CreateServiceConnection(null, parsedConnStr.AuthenticationType, hostname, port, orgName, null, string.Empty,
                                                null, string.Empty, onlineRegion, string.Empty, useSsl, parsedConnStr.UseUniqueConnectionInstance,
                                                    null, clientId, redirectUri, PromptBehavior.Never, null, parsedConnStr.CertThumbprint, targetStoreName, instanceUrl: parsedConnStr.ServiceUri, externalLogger: logger);

                    break;
                case AuthenticationType.ClientSecret:
                    hostname = parsedConnStr.IsOnPremOauth ? hostname : string.Empty;
                    port = parsedConnStr.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) && string.IsNullOrEmpty(parsedConnStr.ClientSecret))
                    {
                        throw new ArgumentNullException("ClientId and ClientSecret must be populated for ClientSecret Auth Type.",
                            $"Client Id={(string.IsNullOrEmpty(clientId) ? "Not Specfied and Required." : clientId)} | Client Secret={(string.IsNullOrEmpty(parsedConnStr.ClientSecret) ? "Not Specfied and Required." : "Specfied")}");
                    }

                    CreateServiceConnection(null, parsedConnStr.AuthenticationType, hostname, port, orgName, null, string.Empty,
                                                 MakeSecureString(parsedConnStr.ClientSecret), string.Empty, onlineRegion, string.Empty, useSsl, parsedConnStr.UseUniqueConnectionInstance,
                                                    null, clientId, redirectUri, PromptBehavior.Never, null, null, instanceUrl: parsedConnStr.ServiceUri, externalLogger: logger);
                    break;
                case AuthenticationType.AD:
                    CreateServiceConnection(null, parsedConnStr.AuthenticationType, hostname, port, orgName, networkCredentials, userId,
                                                    MakeSecureString(password), domainname, string.Empty, string.Empty, useSsl, parsedConnStr.UseUniqueConnectionInstance, null, instanceUrl: parsedConnStr.SkipDiscovery ? parsedConnStr.ServiceUri : null, externalLogger: logger);

                    break;
                case AuthenticationType.ExternalTokenManagement:
                    CreateServiceConnection(null, parsedConnStr.AuthenticationType, string.Empty, string.Empty, string.Empty, null,
                                                    string.Empty, null, string.Empty, string.Empty, string.Empty, true, parsedConnStr.UseUniqueConnectionInstance, null,
                                                    string.Empty, null, PromptBehavior.Never, null, string.Empty, StoreName.My, null, parsedConnStr.ServiceUri, externalLogger: logger);
                    break;
            }
        }

        /// <summary>
        /// Uses the Organization Web proxy Client provided by the user
        /// </summary>
        /// <param name="externalOrgWebProxyClient">User Provided Organization Web Proxy Client</param>
        /// <param name="isCloned">when true, skips init</param>
        /// <param name="orginalAuthType">Auth type of source connection</param>
        /// <param name="sourceOrgVersion">source organization version</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        internal ServiceClient(OrganizationWebProxyClientAsync externalOrgWebProxyClient, bool isCloned = true, AuthenticationType orginalAuthType = AuthenticationType.OAuth, Version sourceOrgVersion = null, ILogger logger = null)
        {
            CreateServiceConnection(null, orginalAuthType, string.Empty, string.Empty, string.Empty, null, string.Empty,
                MakeSecureString(string.Empty), string.Empty, string.Empty, string.Empty, false, false, null, string.Empty, null,
                PromptBehavior.Auto, externalOrgWebProxyClient, isCloned: isCloned, incomingOrgVersion: sourceOrgVersion, externalLogger: logger);
        }


        /// <summary>
        /// Sets up the Dataverse Web Service Connection
        ///  For Connecting via AD
        /// </summary>
        /// <param name="externalOrgServiceProxy">if populated, is the org service to use to connect to Dataverse</param>
        /// <param name="requestedAuthType">Authentication Type requested</param>
        /// <param name="hostName">Host name of the server that is hosting the Dataverse web service</param>
        /// <param name="port">Port number on the Dataverse Host Server ( usually 5555 )</param>
        /// <param name="orgName">Organization name for the Dataverse Instance.</param>
        /// <param name="credential">Network Credential Object used to login with</param>
        /// <param name="userId">Live ID to connect with</param>
        /// <param name="password">Live ID Password to connect with</param>
        /// <param name="domain">Name of the Domain where the Dataverse is deployed</param>
        /// <param name="Geo">Region hosting the Dataverse online Server, can be NA, EMEA, APAC</param>
        /// <param name="claimsHomeRealm">HomeRealm Uri for the user</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="clientId">Registered Client Id on Azure</param>
        /// <param name="promptBehavior">Default Prompt Behavior</param>
        /// <param name="redirectUri">Registered redirect uri for ADAL to return</param>
        /// <param name="externalOrgWebProxyClient">OAuth related web proxy client</param>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">Actual URI of the Organization Instance</param>
        /// <param name="isCloned">When True, Indicates that the construction request is coming from a clone operation. </param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        /// <param name="incomingOrgVersion">Incoming Org Version, used as part of clone.</param>
        /// <param name="externalLogger">Logging provider <see cref="ILogger"/></param>
        /// <param name="tokenCacheStorePath">path for token file storage</param>
        internal void CreateServiceConnection(
            object externalOrgServiceProxy,
            AuthenticationType requestedAuthType,
            string hostName,
            string port,
            string orgName,
            System.Net.NetworkCredential credential,
            string userId,
            SecureString password,
            string domain,
            string Geo,
            string claimsHomeRealm,
            bool useSsl,
            bool useUniqueInstance,
            OrganizationDetail orgDetail,
            string clientId = "",
            Uri redirectUri = null,
            PromptBehavior promptBehavior = PromptBehavior.Auto,
            OrganizationWebProxyClientAsync externalOrgWebProxyClient = null,
            string certificateThumbPrint = "",
            StoreName certificateStoreName = StoreName.My,
            X509Certificate2 certificate = null,
            Uri instanceUrl = null,
            bool isCloned = false,
            bool useDefaultCreds = false,
            Version incomingOrgVersion = null,
            ILogger externalLogger = null,
            string tokenCacheStorePath = null
            )
        {

            _logEntry = new DataverseTraceLogger(externalLogger)
            {
                // Set initial properties
                EnabledInMemoryLogCapture = InMemoryLogCollectionEnabled,
                LogRetentionDuration = InMemoryLogCollectionTimeOutMinutes
            };

            _connectionSvc = null;

            // Handel Direct Set from Login control.
            if (instanceUrl == null && orgDetail != null)
            {
                if (orgDetail.FriendlyName.Equals("DIRECTSET", StringComparison.OrdinalIgnoreCase)
                    && orgDetail.OrganizationId.Equals(Guid.Empty)
                    && !string.IsNullOrEmpty(orgDetail.OrganizationVersion) && orgDetail.OrganizationVersion.Equals("0.0.0.0")
                    && orgDetail.Endpoints != null
                    && orgDetail.Endpoints.ContainsKey(EndpointType.OrganizationService))
                {
                    if (Uri.TryCreate(orgDetail.Endpoints[EndpointType.OrganizationService], UriKind.RelativeOrAbsolute, out instanceUrl))
                    {
                        orgDetail = null;
                        _logEntry.Log(string.Format("DIRECTSET URL detected via Login OrgDetails Property, Setting Connect URI to {0}", instanceUrl.ToString()));
                    }
                }
            }

            try
            {
                // Support for things like Excel that do not run from a local directory.
                Version fileVersion = new Version(SdkVersionProperty);
                if (!(Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(fileVersion, Utilities.FeatureVersionMinimums.DataverseVersionForThisAPI)))
                {
                    _logEntry.Log("!!WARNING!!! The version of the Dataverse product assemblies is less than the recommend version for this API; you must use version 5.0.9688.1533 or newer (Newer then the Oct-2011 service release)", TraceEventType.Warning);
                    _logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Dataverse Version found is {0}", SdkVersionProperty), TraceEventType.Warning);
                }
            }
            catch
            {
                _logEntry.Log("!!WARNING!!! Failed to determine the version of the Dataverse SDK Present", TraceEventType.Warning);
            }
            _metadataUtlity = new MetadataUtility(this);
            _dynamicAppUtility = new DynamicEntityUtility(this, _metadataUtlity);

            // doing a direct Connect,  use Connection Manager to do the connect.
            // if using an user provided connection,.
            if (externalOrgWebProxyClient != null)
            {
                _connectionSvc = new ConnectionService(externalOrgWebProxyClient, requestedAuthType, _logEntry, isClone: isCloned);
                if (isCloned && incomingOrgVersion != null)
                {
                    _connectionSvc.OrganizationVersion = incomingOrgVersion;
                }
            }
            else
            {
                if (requestedAuthType == AuthenticationType.ExternalTokenManagement)
                {
                    _connectionSvc = new ConnectionService(
                            requestedAuthType,
                            instanceUrl,
                            useUniqueInstance,
                            orgDetail, clientId,
                            redirectUri, certificateThumbPrint,
                            certificateStoreName, 
                            certificate, 
                            hostName, 
                            port, 
                            false, 
                            logSink: _logEntry, 
                            tokenCacheStorePath: tokenCacheStorePath);

                    if (GetAccessToken != null)
                        _connectionSvc.GetAccessTokenAsync = GetAccessToken;
                    else
                    {
                        // Should not get here,  however..
                        throw new DataverseConnectionException("tokenProviderFunction required for ExternalTokenManagement Auth type, You must use the appropriate constructor for this auth type.", new ArgumentNullException("tokenProviderFunction"));
                    }
                }
                else
                {
                    // check to see what sort of login this is.
                    if (requestedAuthType == AuthenticationType.OAuth)
                    {
                        if (!String.IsNullOrEmpty(hostName))
                            _connectionSvc = new ConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, clientId, redirectUri, promptBehavior, hostName, port, true, instanceToConnectToo: instanceUrl, logSink: _logEntry, useDefaultCreds: useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath);
                        else
                            _connectionSvc = new ConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, clientId, redirectUri, promptBehavior, hostName, port, false, instanceToConnectToo: instanceUrl, logSink: _logEntry, useDefaultCreds: useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath);
                    }
                    else if (requestedAuthType == AuthenticationType.Certificate)
                    {
                        _connectionSvc = new ConnectionService(requestedAuthType, instanceUrl, useUniqueInstance, orgDetail, clientId, redirectUri, certificateThumbPrint, certificateStoreName, certificate, hostName, port, !String.IsNullOrEmpty(hostName), logSink: _logEntry, tokenCacheStorePath: tokenCacheStorePath);
                    }
                    else if (requestedAuthType == AuthenticationType.ClientSecret)
                    {
                        if (!String.IsNullOrEmpty(hostName))
                            _connectionSvc = new ConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, clientId, redirectUri, promptBehavior, hostName, port, true, instanceToConnectToo: instanceUrl, logSink: _logEntry, useDefaultCreds: useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath);
                        else
                            _connectionSvc = new ConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, clientId, redirectUri, promptBehavior, hostName, port, false, instanceToConnectToo: instanceUrl, logSink: _logEntry, useDefaultCreds: useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath);
                    }
                    else if (requestedAuthType == AuthenticationType.AD)
                    {
                        // User is using AD or IFD
                        if (credential == null)
                            _connectionSvc = new ConnectionService(requestedAuthType, hostName, port, orgName, System.Net.CredentialCache.DefaultNetworkCredentials, useUniqueInstance, orgDetail, instanceToConnectToo: instanceUrl, logSink: _logEntry);
                        else
                            _connectionSvc = new ConnectionService(requestedAuthType, hostName, port, orgName, credential, useUniqueInstance, orgDetail, instanceToConnectToo: instanceUrl, logSink: _logEntry);
                    }
                }
            }

            if (_connectionSvc != null)
            {
                try
                {
                    if (GetCustomHeaders != null)
                        _connectionSvc.RequestAdditionalHeadersAsync = GetCustomHeaders; 
                    // Assign the log entry host to the ConnectionService engine
                    ConnectionService tempConnectService = null;
                    _connectionSvc.InternetProtocolToUse = useSsl ? "https" : "http";
                    if (!_connectionSvc.DoLogin(out tempConnectService))
                    {
                        _logEntry.Log("Unable to Login to Dataverse", TraceEventType.Error);
                        IsReady = false;
                        return;
                    }
                    else
                    {
                        if (tempConnectService != null)
                        {
                            _connectionSvc.Dispose();  // Clean up temp version and unassign assets.
                            _connectionSvc = tempConnectService;
                        }
                        _cachObjecName = _connectionSvc.ServiceCACHEName + ".LookupCache";

                        // Min supported version for batch operations.
                        if (_connectionSvc?.OrganizationVersion != null &&
                            Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(_connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.BatchOperations))
                            _batchManager = new BatchManager(_logEntry, IsClonedConnection:isCloned);
                        else
                            _logEntry.Log("Batch System disabled, Dataverse Server does not support this message call", TraceEventType.Information);

                        IsReady = true;

                    }
                }
                catch (Exception ex)
                {
                    if (_logEntry != null)
                        _logEntry.Dispose();

                    if (ex is AggregateException)
                        throw new DataverseConnectionException(ex.Message, ex);
                    else
                        throw new DataverseConnectionException("Failed to connect to Dataverse", ex);
                }
            }
        }

        #endregion

        #region Public General Interfaces

        /// <summary>
        /// Enabled only if InMemoryLogCollectionEnabled is true.
        /// Return all logs currently stored for the ServiceClient in queue.
        /// </summary>
        public IEnumerable<Tuple<DateTime, string>> GetAllLogs()
        {
            var source1 = _logEntry == null ? Enumerable.Empty<Tuple<DateTime, string>>() : _logEntry.Logs;
            var source2 = _connectionSvc == null ? Enumerable.Empty<Tuple<DateTime, string>>() : _connectionSvc.GetAllLogs();
            return source1.Union(source2);
        }

        /// <summary>
        /// Enabled only if InMemoryLogCollectionEnabled is true.
        /// Return all logs currently stored for the ServiceClient in queue in string list format with [UTCDateTime][LogEntry].
        /// </summary>
        public string[] GetAllLogsAsStringList()
        {
            return GetAllLogs().OrderBy(x => x.Item1).Select(x => $"[{x.Item1:yyyy-MM-dd HH:mm:ss:fffffff}]{x.Item2}").ToArray();
        }

        /// <summary>
        /// Clone, 'Clones" the current Dataverse ServiceClient with a new connection to Dataverse.
        /// Clone only works for connections creating using OAuth Protocol.
        /// </summary>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns>returns an active ServiceClient or null</returns>
        public ServiceClient Clone(ILogger logger = null)
        {
            return Clone(null, logger: logger);
        }

        /// <summary>
        /// Clone, 'Clones" the current Dataverse Service client with a new connection to Dataverse.
        /// Clone only works for connections creating using OAuth Protocol.
        /// </summary>
        /// <param name="strongTypeAsm">Strong Type Assembly to reference as part of the create of the clone.</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns></returns>
        public ServiceClient Clone(System.Reflection.Assembly strongTypeAsm, ILogger logger = null)
        {
            if (_connectionSvc == null || IsReady == false)
            {
                _logEntry.Log("You must have successfully created a connection to Dataverse before it can be cloned.", TraceEventType.Error);
                throw new DataverseOperationException("You must have successfully created a connection to Dataverse before it can be cloned.");
            }

            // On-Prem Auth flows are not supported for Clone right now. 
            if (_connectionSvc.AuthenticationTypeInUse == AuthenticationType.AD)
                throw new DataverseOperationException("On-Premises Connections are not supported for clone operations at this time.", new NotImplementedException("OnPrem Auth Flow are not implemented for clone operations"));

            _connectionSvc._isCloning = true; // set cloning behavior flag.
            _configuration.Value.UseWebApiLoginFlow = false; // override default settings for clone ops.  

            try
            {
                OrganizationWebProxyClientAsync proxy = null;
                if (_connectionSvc.ConnectOrgUriActual != null)
                {
                    if (strongTypeAsm == null)
                        proxy = new OrganizationWebProxyClientAsync(_connectionSvc.ConnectOrgUriActual, true);
                    else
                        proxy = new OrganizationWebProxyClientAsync(_connectionSvc.ConnectOrgUriActual, strongTypeAsm);
                }
                else
                {
                    var orgWebClient = _connectionSvc.WebClient;
                    if (orgWebClient != null)
                    {
                        if (strongTypeAsm == null)
                            proxy = new OrganizationWebProxyClientAsync(orgWebClient.Endpoint.Address.Uri, true);
                        else
                            proxy = new OrganizationWebProxyClientAsync(orgWebClient.Endpoint.Address.Uri, strongTypeAsm);
                    }
                    else
                    {
                        _logEntry.Log("Connection cannot be cloned.  There is currently no OAuth based connection active.");
                        return null;
                    }
                }
                if (proxy != null)
                {
                    try
                    {
                        if (_cloneLockObject == null)
                            _cloneLockObject = new object();

                        // Get Current Access Token.
                        // This will get the current access token
                        if (logger == null) logger = _logEntry._logger;
                        lock (_cloneLockObject)
                        {
                            proxy.HeaderToken = this.CurrentAccessToken;
                            var SvcClient = new ServiceClient(proxy, true, _connectionSvc.AuthenticationTypeInUse, _connectionSvc?.OrganizationVersion, logger: logger);
                            SvcClient._connectionSvc.SetClonedProperties(this);
                            SvcClient.CallerAADObjectId = CallerAADObjectId;
                            SvcClient.CallerId = CallerId;
                            SvcClient.MaxRetryCount = _configuration.Value.MaxRetryCount;
                            SvcClient.RetryPauseTime = _configuration.Value.RetryPauseTime;
                            SvcClient.GetAccessToken = GetAccessToken;
                            SvcClient.GetCustomHeaders = GetCustomHeaders;
                            return SvcClient;
                        }
                    }
                    catch (DataverseConnectionException)
                    {
                        // rethrow the Connection exception coming from the initial call.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logEntry.Log(ex);
                        throw new DataverseConnectionException("Failed to Clone Connection", ex);
                    }
                }
                else
                {
                    _logEntry.Log("Connection cannot be cloned.  There is currently no OAuth based connection active or it is mis-configured in the ServiceClient.");
                    return null;
                }
            }finally
            {
                _connectionSvc._isCloning = false; // set cloning behavior flag.
                _configuration.Value.UseWebApiLoginFlow = false; // override default settings for clone ops.  
            }
        }

        /// <summary>
        /// Creates a ServiceClient Request builder that allows you to customize a specific request sent to dataverse.  This should be used only for a single request and then released. 
        /// </summary>
        /// <returns>Service Request builder that is used to create and submit a single request.</returns>
        public ServiceClientRequestBuilder CreateRequestBuilder()
        {
            return new ServiceClientRequestBuilder(this);
        }

        #region Dataverse DiscoveryServerMethods

        /// <summary>
        /// Discovers the organizations against an On-Premises deployment.
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service URI.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="clientId">The client Id.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior.</param>
        /// <param name="authority">The authority provider for OAuth tokens. Unique if any already known.</param>
        /// <param name="useDefaultCreds">(Optional) if specified, tries to use the current user</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns>A collection of organizations</returns>
        public static async Task<DiscoverOrganizationsResult> DiscoverOnPremiseOrganizationsAsync(Uri discoveryServiceUri, ClientCredentials clientCredentials, string clientId, Uri redirectUri, string authority, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false, string tokenCacheStorePath = null, ILogger logger = null)
        {
            return await ConnectionService.DiscoverOrganizationsAsync(discoveryServiceUri, clientCredentials, clientId, redirectUri, promptBehavior, isOnPrem: true, authority, useDefaultCreds: useDefaultCreds, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Discovers the organizations, used for OAuth.
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service URI.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="clientId">The client Id.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior.</param>
        /// <param name="isOnPrem">The deployment type: OnPrem or Online.</param>
        /// <param name="authority">The authority provider for OAuth tokens. Unique if any already known.</param>
        /// <param name="useDefaultCreds">(Optional) if specified, tries to use the current user</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns>A collection of organizations</returns>
        public static async Task<DiscoverOrganizationsResult> DiscoverOnlineOrganizationsAsync(Uri discoveryServiceUri, ClientCredentials clientCredentials, string clientId, Uri redirectUri, bool isOnPrem, string authority, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false, string tokenCacheStorePath = null, ILogger logger = null)
        {
            return await ConnectionService.DiscoverOrganizationsAsync(discoveryServiceUri, clientCredentials, clientId, redirectUri, promptBehavior, isOnPrem, authority, useGlobalDisco: true, useDefaultCreds: useDefaultCreds, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
        }

        /// <summary>
        ///  Discovers Organizations Using the global discovery service.
        ///  <para>Provides a User ID / Password flow for authentication to the online discovery system.
        ///  You can also provide the discovery instance you wish to use, or not pass it.  If you do not specify a discovery region, the commercial global region is used</para>
        /// </summary>
        /// <param name="userId">User ID to login with</param>
        /// <param name="password">Password to use to login with</param>
        /// <param name="discoServer">(Optional) URI of the discovery server</param>
        /// <param name="clientId">The client Id.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior.</param>
        /// <param name="isOnPrem">The deployment type: OnPrem or Online.</param>
        /// <param name="authority">The authority provider for OAuth tokens. Unique if any already known.</param>
        /// <param name="useDefaultCreds">(Optional) if specified, tries to use the current user</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns>A collection of organizations</returns>
        public static async Task<DiscoverOrganizationsResult> DiscoverOnlineOrganizationsAsync(string userId, string password, string clientId, Uri redirectUri, bool isOnPrem, string authority, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false, Model.DiscoveryServer discoServer = null, string tokenCacheStorePath = null, ILogger logger = null)
        {
            Uri discoveryUriToUse = null;
            if (discoServer != null && discoServer.RequiresRegionalDiscovery)
            {
                // use the specified regional discovery server.
                discoveryUriToUse = discoServer.RegionalGlobalDiscoveryServer;
            }
            else
            {
                // default commercial cloud discovery server
                discoveryUriToUse = new Uri(ConnectionService.GlobalDiscoveryAllInstancesUri);
            }

            // create credentials.
            ClientCredentials clientCredentials = new ClientCredentials();
            clientCredentials.UserName.UserName = userId;
            clientCredentials.UserName.Password = password;

            return await ConnectionService.DiscoverOrganizationsAsync(discoveryUriToUse, clientCredentials, clientId, redirectUri, promptBehavior, isOnPrem, authority, useGlobalDisco: true, useDefaultCreds: useDefaultCreds, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Discovers Organizations Using the global discovery service and an external source for access tokens
        /// </summary>
        /// <param name="discoveryServiceUri">Global discovery base URI to use to connect too,  if null will utilize the commercial Global Discovery Server.</param>
        /// <param name="tokenProviderFunction">Function that will provide access token to the discovery call.</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns></returns>
        public static async Task<OrganizationDetailCollection> DiscoverOnlineOrganizationsAsync(Func<string, Task<string>> tokenProviderFunction, Uri discoveryServiceUri = null, string tokenCacheStorePath = null, ILogger logger = null)
        {
            if (discoveryServiceUri == null)
                discoveryServiceUri = new Uri(ConnectionService.GlobalDiscoveryAllInstancesUri); // use commercial GD

            return await ConnectionService.DiscoverGlobalOrganizationsAsync(discoveryServiceUri, tokenProviderFunction, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Discovers Organizations Using the global discovery service and an external source for access tokens
        /// </summary>
        /// <param name="discoveryServiceUri">Global discovery base URI to use to connect too,  if null will utilize the commercial Global Discovery Server.</param>
        /// <param name="tokenProviderFunction">Function that will provide access token to the discovery call.</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns></returns>
        public static async Task<OrganizationDetailCollection> DiscoverOnlineOrganizationsAsync(Func<string, Task<string>> tokenProviderFunction, Uri discoveryServiceUri = null, string tokenCacheStorePath = null, ILogger logger = null, CancellationToken cancellationToken = default)
        {
            if (discoveryServiceUri == null)
                discoveryServiceUri = new Uri(ConnectionService.GlobalDiscoveryAllInstancesUri); // use commercial GD

            return await ConnectionService.DiscoverGlobalOrganizationsAsync(discoveryServiceUri, tokenProviderFunction, externalLogger: logger, tokenCacheStorePath: tokenCacheStorePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #endregion

        #region OAuth Token Cache

        /// <summary>
        /// Clear the persistent and in-memory store cache
        /// </summary>
        /// <param name="tokenCachePath"></param>
        /// <returns></returns>
        public static bool RemoveOAuthTokenCache(string tokenCachePath = "")
        {
            throw new NotImplementedException();
            //If tokenCachePath is not supplied it will take from the constructor  of token cache and delete the file.
            //if (_CdsServiceClientTokenCache == null)
            //    _CdsServiceClientTokenCache = new CdsServiceClientTokenCache(tokenCachePath);
            //return _CdsServiceClientTokenCache.Clear(tokenCachePath);
            //TODO: Update for new Token cache providers.
            //return false;
        }

        #endregion

        #region XRM Commands and handlers
        #region Public Access to direct commands.

        /// <summary>
        /// Executes a web request against Xrm WebAPI.
        /// </summary>
        /// <param name="queryString">Here you would pass the path and query parameters that you wish to pass onto the WebAPI.
        /// The format used here is as follows:
        ///   {APIURI}/api/data/v{instance version}/querystring.
        /// For example,
        ///     if you wanted to get data back from an account,  you would pass the following:
        ///         accounts(id)
        ///         which creates:  get - https://myinstance.crm.dynamics.com/api/data/v9.0/accounts(id)
        ///     if you were creating an account, you would pass the following:
        ///         accounts
        ///         which creates:  post - https://myinstance.crm.dynamics.com/api/data/v9.0/accounts - body contains the data.
        ///         </param>
        /// <param name="method">Method to use for the request</param>
        /// <param name="body">Content your passing to the request</param>
        /// <param name="customHeaders">Headers in addition to the default headers added by for Executing a web request</param>
        /// <param name="contentType">Content Type attach to the request.  this defaults to application/json if not set.</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns></returns>
        public HttpResponseMessage ExecuteWebRequest(HttpMethod method, string queryString, string body, Dictionary<string, List<string>> customHeaders, string contentType = default, CancellationToken cancellationToken = default)
        {
            _logEntry.ResetLastError();  // Reset Last Error
            ValidateConnectionLive();
            if (DataverseService == null)
            {
                _logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            if (string.IsNullOrEmpty(queryString) && string.IsNullOrEmpty(body))
            {
                _logEntry.Log("Execute Web Request failed, queryString and body cannot be null", TraceEventType.Error);
                return null;
            }

            if (Uri.TryCreate(queryString, UriKind.Absolute, out var urlPath))
            {
                // Was able to create a URL here... Need to make sure that we strip out everything up to the last segment.
                string baseQueryString = urlPath.Segments.Last();
                if (!string.IsNullOrEmpty(urlPath.Query))
                    queryString = baseQueryString + urlPath.Query;
                else
                    queryString = baseQueryString;
            }

            var result = _connectionSvc.Command_WebExecuteAsync(queryString, body, method, customHeaders, contentType, string.Empty, CallerId, _disableConnectionLocking, MaxRetryCount, RetryPauseTime, cancellationToken: cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            if (result == null)
                throw LastException;
            else
                return result;
        }

        /// <summary>
        /// Executes a web request against Xrm WebAPI Async.
        /// </summary>
        /// <param name="queryString">Here you would pass the path and query parameters that you wish to pass onto the WebAPI.
        /// The format used here is as follows:
        ///   {APIURI}/api/data/v{instance version}/querystring.
        /// For example,
        ///     if you wanted to get data back from an account,  you would pass the following:
        ///         accounts(id)
        ///         which creates:  get - https://myinstance.crm.dynamics.com/api/data/v9.0/accounts(id)
        ///     if you were creating an account, you would pass the following:
        ///         accounts
        ///         which creates:  post - https://myinstance.crm.dynamics.com/api/data/v9.0/accounts - body contains the data.
        ///         </param>
        /// <param name="method">Method to use for the request</param>
        /// <param name="body">Content your passing to the request</param>
        /// <param name="customHeaders">Headers in addition to the default headers added by for Executing a web request</param>
        /// <param name="contentType">Content Type attach to the request.  this defaults to application/json if not set.</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> ExecuteWebRequestAsync(HttpMethod method, string queryString, string body, Dictionary<string, List<string>> customHeaders, string contentType = default, CancellationToken cancellationToken = default)
        {
            _logEntry.ResetLastError();  // Reset Last Error
            ValidateConnectionLive();
            if (DataverseService == null)
            {
                _logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            if (string.IsNullOrEmpty(queryString) && string.IsNullOrEmpty(body))
            {
                _logEntry.Log("Execute Web Request failed, queryString and body cannot be null", TraceEventType.Error);
                return null;
            }

            if (Uri.TryCreate(queryString, UriKind.Absolute, out var urlPath))
            {
                // Was able to create a URL here... Need to make sure that we strip out everything up to the last segment.
                string baseQueryString = urlPath.Segments.Last();
                if (!string.IsNullOrEmpty(urlPath.Query))
                    queryString = baseQueryString + urlPath.Query;
                else
                    queryString = baseQueryString;
            }

            var result = await _connectionSvc.Command_WebExecuteAsync(queryString, body, method, customHeaders, contentType, string.Empty, CallerId, _disableConnectionLocking, MaxRetryCount, RetryPauseTime, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result == null)
                throw LastException;
            else
                return result;
        }

        /// <summary>
        /// Executes a Dataverse Organization Request (thread safe) and returns the organization response object. Also adds metrics for logging support.
        /// </summary>
        /// <param name="req">Organization Request  to run</param>
        /// <param name="logMessageTag">Message identifying what this request in logging.</param>
        /// <param name="useWebAPI">When True, uses the webAPI to execute the organization Request.  This works for only Create at this time.</param>
        /// <returns>Result of request or null.</returns>
        public OrganizationResponse ExecuteOrganizationRequest(OrganizationRequest req, string logMessageTag = "User Defined", bool useWebAPI = false)
        {
            return ExecuteOrganizationRequestImpl(req, logMessageTag, useWebAPI, false);
        }

        /// <summary>
        /// Executes a Dataverse Organization Request (In Async mode) and returns the organization response object. Also adds metrics for logging support.
        /// </summary>
        /// <param name="req">Organization Request  to run</param>
        /// <param name="logMessageTag">Message identifying what this request in logging.</param>
        /// <param name="useWebAPI">When True, uses the webAPI to execute the organization Request.  This works for only Create at this time.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Result of request or null.</returns>
        public async Task<OrganizationResponse> ExecuteOrganizationRequestAsync(OrganizationRequest req, string logMessageTag = "User Defined", bool useWebAPI = false , CancellationToken cancellationToken = default)
        {
            return await ExecuteOrganizationRequestAsyncImpl(req, cancellationToken, logMessageTag, useWebAPI, false).ConfigureAwait(false);
        }

        #endregion

        #region Internal

        internal OrganizationResponse ExecuteOrganizationRequestImpl(OrganizationRequest req, string logMessageTag = "User Defined", bool useWebAPI = false, bool bypassPluginExecution = false)
        {
            _logEntry.ResetLastError();  // Reset Last Error
            ValidateConnectionLive();
            if (req != null)
            {
                useWebAPI = Utilities.IsRequestValidForTranslationToWebAPI(req);
                if (!useWebAPI)
                {
                    return Command_Execute(req, logMessageTag, bypassPluginExecution);
                }
                else
                {
                    // use Web API.
                    return _connectionSvc.Command_WebAPIProcess_ExecuteAsync(req, logMessageTag, bypassPluginExecution, _metadataUtlity, CallerId, _disableConnectionLocking, MaxRetryCount, RetryPauseTime, new CancellationToken()).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
            else
            {
                _logEntry.Log("Execute Organization Request failed, Organization Request cannot be null", TraceEventType.Error);
                return null;
            }
        }

        private async Task<OrganizationResponse> ExecuteOrganizationRequestAsyncImpl(OrganizationRequest req, CancellationToken cancellationToken, string logMessageTag = "User Defined", bool useWebAPI = false, bool bypassPluginExecution = false)
        {
            _logEntry.ResetLastError();  // Reset Last Error
            ValidateConnectionLive();
            cancellationToken.ThrowIfCancellationRequested();
            if (req != null)
            {
                useWebAPI = Utilities.IsRequestValidForTranslationToWebAPI(req);
                if (!useWebAPI)
                {
                    return await Command_ExecuteAsync(req, logMessageTag, cancellationToken, bypassPluginExecution).ConfigureAwait(false);
                }
                else
                {
                    // use Web API.
                    return await _connectionSvc.Command_WebAPIProcess_ExecuteAsync(req, logMessageTag, bypassPluginExecution, _metadataUtlity, CallerId, _disableConnectionLocking, MaxRetryCount, RetryPauseTime, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                _logEntry.Log("Execute Organization Request failed, Organization Request cannot be null", TraceEventType.Error);
                return null;
            }
        }

        /// <summary>
        /// Executes a Dataverse Create Request and returns the organization response object.
        /// Uses an Async pattern to allow for the thread to be backgrounded.
        /// </summary>
        /// <param name="req">Request to run</param>
        /// <param name="errorStringCheck">Formatted Error string</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Result of create request or null.</returns>
        internal async Task<OrganizationResponse> Command_ExecuteAsync(OrganizationRequest req, string errorStringCheck, CancellationToken cancellationToken, bool bypassPluginExecution = false)
        {
            ValidateConnectionLive();
            if (DataverseServiceAsync != null)
            {
                // if created based on Async Client.
                return await Command_ExecuteAsyncImpl(req, errorStringCheck, cancellationToken, bypassPluginExecution).ConfigureAwait(false);
            }
            else
            {
                // if not use task.run().
                return await Task.Run(() => Command_Execute(req, errorStringCheck, bypassPluginExecution), cancellationToken).ConfigureAwait(false);
            }

        }

        /// <summary>
        /// Executes a Dataverse Create Request and returns the organization response object.
        /// </summary>
        /// <param name="req">Request to run</param>
        /// <param name="errorStringCheck">Formatted Error string</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Result of create request or null.</returns>
        internal async Task<OrganizationResponse> Command_ExecuteAsyncImpl(OrganizationRequest req, string errorStringCheck, System.Threading.CancellationToken cancellationToken, bool bypassPluginExecution = false)
        {
            ValidateConnectionLive();
            Guid requestTrackingId = Guid.NewGuid();
            OrganizationResponse resp = null;
            Stopwatch logDt = Stopwatch.StartNew();
            TimeSpan LockWait = TimeSpan.Zero;
            int retryCount = 0;
            bool retry = false;

            do
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _retryPauseTimeRunning = _configuration.Value.RetryPauseTime;
                    retry = false;
                    if (!_disableConnectionLocking)
                        if (_lockObject == null)
                            _lockObject = new object();

                    if (_connectionSvc != null && _connectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                        _connectionSvc.CalledbyExecuteRequest = true;
                    OrganizationResponse rsp = null;

                    // Check to see if a Tracking ID has allready been assigned,
                    if (!req.RequestId.HasValue || (req.RequestId.HasValue && req.RequestId.Value == Guid.Empty))
                    {
                        // Assign Tracking ID
                        req.RequestId = requestTrackingId;
                    }
                    else
                    {
                        // assign request Id to the tracking id.
                        requestTrackingId = req.RequestId.Value;
                    }

                    // if request should bypass plugin exec.
                    if (bypassPluginExecution && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(_connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowBypassCustomPlugin))
                        req.Parameters[Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION] = true;

                    _logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - {0}{1}: {3}RequestID={2}",
                        req.RequestName,
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestTrackingId.ToString(),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : ""
                        ), TraceEventType.Verbose);

                    logDt.Stop();
                    logDt = Stopwatch.StartNew();
                    _ = await _connectionSvc.RefreshClientTokenAsync().ConfigureAwait(false);
                    rsp = await DataverseServiceAsync.ExecuteAsync(req).ConfigureAwait(false);

                    logDt.Stop();
                    _logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - {0}{2}: {5}RequestID={3} {4}: duration={1}",
                        req.RequestName,
                        logDt.Elapsed.ToString(),
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestTrackingId.ToString(),
                        LockWait == TimeSpan.Zero ? string.Empty : string.Format(": LockWaitDuration={0} ", LockWait.ToString()),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : ""
                        ), TraceEventType.Verbose);
                    resp = rsp;
                }
                catch (Exception ex)
                {
                    bool isThrottled = false;
                    retry = ShouldRetry(req, ex, retryCount, out isThrottled) || !cancellationToken.IsCancellationRequested;
                    if (retry)
                    {
                        retryCount = await Utilities.RetryRequest(req, requestTrackingId, LockWait, logDt, _logEntry, SessionTrackingId, _disableConnectionLocking, _retryPauseTimeRunning, ex, errorStringCheck, retryCount, isThrottled, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logEntry.LogRetry(retryCount, req, _retryPauseTimeRunning, true, isThrottled: isThrottled);
                        _logEntry.LogException(req, ex, errorStringCheck);
                        //keep it in end so that LastError could be a better message.
                        _logEntry.LogFailure(req, requestTrackingId, SessionTrackingId, _disableConnectionLocking, LockWait, logDt, ex, errorStringCheck, true);

                        // Callers which cancel should expect to handle a OperationCanceledException
                        if (ex is OperationCanceledException)
                            throw;
                        else
                            cancellationToken.ThrowIfCancellationRequested();
                    }
                    resp = null;
                }
                finally
                {
                    logDt.Stop();
                }
            } while (retry);

            return resp;
        }

        /// <summary>
        /// Executes a Dataverse Create Request and returns the organization response object.
        /// </summary>
        /// <param name="req">Request to run</param>
        /// <param name="errorStringCheck">Formatted Error string</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <returns>Result of create request or null.</returns>
        internal OrganizationResponse Command_Execute(OrganizationRequest req, string errorStringCheck, bool bypassPluginExecution = false)
        {
            ValidateConnectionLive();
            Guid requestTrackingId = Guid.NewGuid();
            OrganizationResponse resp = null;
            Stopwatch logDt = Stopwatch.StartNew();
            TimeSpan LockWait = TimeSpan.Zero;
            int retryCount = 0;
            bool retry = false;

            do
            {
                try
                {
                    _retryPauseTimeRunning = _configuration.Value.RetryPauseTime; // Set the default time for each loop.
                    retry = false;
                    if (!_disableConnectionLocking)
                        if (_lockObject == null)
                            _lockObject = new object();

                    if (_connectionSvc != null && _connectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                        _connectionSvc.CalledbyExecuteRequest = true;
                    OrganizationResponse rsp = null;

                    // Check to see if a Tracking ID has allready been assigned,
                    if (!req.RequestId.HasValue || (req.RequestId.HasValue && req.RequestId.Value == Guid.Empty))
                    {
                        // Assign Tracking ID
                        req.RequestId = requestTrackingId;
                    }
                    else
                    {
                        // assign request Id to the tracking id.
                        requestTrackingId = req.RequestId.Value;
                    }

                    // if request should bypass plugin exec.
                    if (bypassPluginExecution && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(_connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowBypassCustomPlugin))
                        req.Parameters[Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION] = true;

                    //RequestId Logging line for telemetry 
                    string requestIdLogSegement = _logEntry.GetFormatedRequestSessionIdString(requestTrackingId, SessionTrackingId);

                    _logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - {0}{1}: {2}",
                        req.RequestName,
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestIdLogSegement
                        ), TraceEventType.Verbose);

                    logDt.Stop();
                    logDt = Stopwatch.StartNew();
                    _connectionSvc.RefreshClientTokenAsync().Wait(); // Refresh the token if needed.. 
                    if (!_disableConnectionLocking) // Allow Developer to override Cross Thread Safeties
                        lock (_lockObject)
                        {
                            if (logDt.Elapsed > TimeSpan.FromMilliseconds(0000010))
                                LockWait = logDt.Elapsed;
                            logDt.Stop();
                            logDt = Stopwatch.StartNew();
                            rsp = DataverseService.Execute(req);
                        }
                    else
                        rsp = DataverseService.Execute(req);

                    logDt.Stop();
                    _logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - {0}{2}: {3} {4}: duration={1}",
                        req.RequestName,
                        logDt.Elapsed.ToString(),
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestIdLogSegement,
                        LockWait == TimeSpan.Zero ? string.Empty : string.Format(": LockWaitDuration={0} ", LockWait.ToString())
                        ), TraceEventType.Verbose);
                    resp = rsp;
                }
                catch (Exception ex)
                {
                    bool isThrottled = false;
                    retry = ShouldRetry(req, ex, retryCount, out isThrottled);
                    if (retry)
                    {
                        Task.Run(async () =>
                        {
                            retryCount = await Utilities.RetryRequest(req, requestTrackingId, LockWait, logDt, _logEntry, SessionTrackingId, _disableConnectionLocking, _retryPauseTimeRunning, ex, errorStringCheck, retryCount, isThrottled).ConfigureAwait(false);
                        }).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _logEntry.LogRetry(retryCount, req, _retryPauseTimeRunning, true, isThrottled: isThrottled);
                        _logEntry.LogException(req, ex, errorStringCheck);
                        //keep it in end so that LastError could be a better message.
                        _logEntry.LogFailure(req, requestTrackingId, SessionTrackingId, _disableConnectionLocking, LockWait, logDt, ex, errorStringCheck, true);
                    }
                    resp = null;
                }
                finally
                {
                    logDt.Stop();
                }
            } while (retry);

            return resp;
        }

        /// <summary>
        /// retry request or not
        /// </summary>
        /// <param name="req">req</param>
        /// <param name="ex">exception</param>
        /// <param name="retryCount">retry count</param>
        /// <param name="isThrottlingRetry">when true, indicates that the retry was caused by a throttle tripping.</param>
        /// <returns></returns>
        private bool ShouldRetry(OrganizationRequest req, Exception ex, int retryCount, out bool isThrottlingRetry)
        {
            isThrottlingRetry = false;
            if (retryCount >= _configuration.Value.MaxRetryCount)
                return false;
            else if (ex is OperationCanceledException)
                return false;
            else if (((string.Equals(req.RequestName.ToLower(), "retrieve"))
                && ((Utilities.ShouldAutoRetryRetrieveByEntityName(((Microsoft.Xrm.Sdk.EntityReference)req.Parameters["Target"]).LogicalName))))
                || (string.Equals(req.RequestName.ToLower(), "retrievemultiple")
                && (
                        ((((RetrieveMultipleRequest)req).Query is FetchExpression) && Utilities.ShouldAutoRetryRetrieveByEntityName(((FetchExpression)((RetrieveMultipleRequest)req).Query).Query))
                    || ((((RetrieveMultipleRequest)req).Query is QueryExpression) && Utilities.ShouldAutoRetryRetrieveByEntityName(((QueryExpression)((RetrieveMultipleRequest)req).Query).EntityName))
                    )))
                return true;
            else if ((ex.HResult == -2147204784 || ex.HResult == -2146233087) && ex.Message.Contains("SQL"))
                return true;
            else if (ex.Message.ToLowerInvariant().Contains("(502) bad gateway"))
                return true;
            else if (ex.Message.ToLowerInvariant().Contains("(503) service unavailable"))
            {
                _retryPauseTimeRunning = _configuration.Value.RetryPauseTime;
                isThrottlingRetry = true;
                return true;
            }
            else if (ex is FaultException<OrganizationServiceFault>)
            {
                var OrgEx = (FaultException<OrganizationServiceFault>)ex;
                if (OrgEx.Detail.ErrorCode == ErrorCodes.ThrottlingBurstRequestLimitExceededError ||
                    OrgEx.Detail.ErrorCode == ErrorCodes.ThrottlingTimeExceededError ||
                    OrgEx.Detail.ErrorCode == ErrorCodes.ThrottlingConcurrencyLimitExceededError)
                {
                    // Use Retry-After delay when specified
                    if (OrgEx.Detail.ErrorDetails.TryGetValue("Retry-After", out var retryAfter) && retryAfter is TimeSpan retryAsTimeSpan)
                    {
                        _retryPauseTimeRunning = retryAsTimeSpan;
                    }
                    else
                    {
                        // else use exponential back off delay
                        _retryPauseTimeRunning = _configuration.Value.RetryPauseTime.Add(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    }
                    isThrottlingRetry = true;
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        #endregion
        #endregion

        /// <summary>
        ///  Makes a secure string
        /// </summary>
        /// <param name="pass"></param>
        /// <returns></returns>
        public static SecureString MakeSecureString(string pass)
        {
            SecureString _pass = new SecureString();
            if (!string.IsNullOrEmpty(pass))
            {
                foreach (char c in pass)
                {
                    _pass.AppendChar(c);
                }
                _pass.MakeReadOnly(); // Lock it down.
                return _pass;
            }
            return null;
        }
        
        /// <summary>
        /// Validates that a connection is live and connected. Throws an exception if the connection is not active. 
        /// </summary>
        /// <returns></returns>
        internal void ValidateConnectionLive()
        {
            if (isDisposed)
            {
                // This client has been disposed and no property of it can be trusted.  Thrown Exception should bubble to caller.
                throw new ObjectDisposedException("This instance of the ServiceClient has been disposed and cannot be used. You must create a new ServiceClient instance.");
            }

            if (!this.IsReady)
            {
                if (this._connectionOptions != null)
                {
                    var failureExecpt = new DataverseConnectionException("ServiceClient is Staged for Connection but not Connected. You must call .Connect() on this client before using it.");
                    _logEntry?.Log(failureExecpt);
                    throw failureExecpt;
                }
                else
                {
                    var failureExecpt = new DataverseConnectionException("ServiceClient is not connected to Dataverse. Please recreate this ServiceClient.");
                    _logEntry?.Log(failureExecpt);
                    throw failureExecpt;
                }
            }
        }

        #region IOrganzation Service Proxy - Proxy object
        /// <summary>
        /// Issues an Associate Request to Dataverse.
        /// </summary>
        /// <param name="entityName">Entity Name to associate to</param>
        /// <param name="entityId">ID if Entity to associate to</param>
        /// <param name="relationship">Relationship Name</param>
        /// <param name="relatedEntities">Entities to associate</param>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            AssociateResponse resp = (AssociateResponse)ExecuteOrganizationRequestImpl(new AssociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }, "Associate To Dataverse via IOrganizationService");
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Issues a Create request to Dataverse
        /// </summary>
        /// <param name="entity">Entity to create</param>
        /// <returns>ID of newly created entity</returns>
        public Guid Create(Entity entity)
        {
            // Relay to Update request.
            CreateResponse resp = (CreateResponse)ExecuteOrganizationRequestImpl(
                new CreateRequest()
                {
                    Target = entity
                }
                , "Create To Dataverse via IOrganizationService"
                , useWebAPI: true);
            if (resp == null)
                throw LastException;

            return resp.id;
        }

        /// <summary>
        /// Issues a Delete request to Dataverse
        /// </summary>
        /// <param name="entityName">Entity name to delete</param>
        /// <param name="id">ID if entity to delete</param>
        public void Delete(string entityName, Guid id)
        {
            DeleteResponse resp = (DeleteResponse)ExecuteOrganizationRequestImpl(
                new DeleteRequest()
                {
                    Target = new EntityReference(entityName, id)
                }
                , "Delete Request to Dataverse via IOrganizationService"
                , useWebAPI: true);
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Issues a Disassociate Request to Dataverse.
        /// </summary>
        /// <param name="entityName">Entity Name to disassociate from</param>
        /// <param name="entityId">ID if Entity to disassociate from</param>
        /// <param name="relationship">Relationship Name</param>
        /// <param name="relatedEntities">Entities to disassociate</param>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            DisassociateResponse resp = (DisassociateResponse)ExecuteOrganizationRequestImpl(new DisassociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }, "Disassociate To Dataverse via IOrganizationService");
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Executes a general organization request
        /// </summary>
        /// <param name="request">Request object</param>
        /// <returns>Response object</returns>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            OrganizationResponse resp = ExecuteOrganizationRequestImpl(request, string.Format("Execute ({0}) request to Dataverse from IOrganizationService", request.RequestName), useWebAPI: true);
            if (resp == null)
                throw LastException;
            return resp;
        }

        /// <summary>
        /// Issues a Retrieve Request to Dataverse
        /// </summary>
        /// <param name="entityName">Entity name to request</param>
        /// <param name="id">ID of the entity to request</param>
        /// <param name="columnSet">ColumnSet to request</param>
        /// <returns>Entity object</returns>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            RetrieveResponse resp = (RetrieveResponse)ExecuteOrganizationRequestImpl(
                new RetrieveRequest()
                {
                    ColumnSet = columnSet,
                    Target = new EntityReference(entityName, id)
                }
                , "Retrieve Request to Dataverse via IOrganizationService");
            if (resp == null)
                throw LastException;

            return resp.Entity;
        }

        /// <summary>
        /// Issues a RetrieveMultiple Request to Dataverse
        /// </summary>
        /// <param name="query">Query to Request</param>
        /// <returns>EntityCollection Result</returns>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            RetrieveMultipleResponse resp = (RetrieveMultipleResponse)ExecuteOrganizationRequestImpl(new RetrieveMultipleRequest() { Query = query }, "RetrieveMultiple to Dataverse via IOrganizationService");
            if (resp == null)
                throw LastException;

            return resp.EntityCollection;
        }

        /// <summary>
        /// Issues an update to Dataverse.
        /// </summary>
        /// <param name="entity">Entity to update into Dataverse</param>
        public void Update(Entity entity)
        {
            // Relay to Update request.
            UpdateResponse resp = (UpdateResponse)ExecuteOrganizationRequestImpl(
                new UpdateRequest()
                {
                    Target = entity
                }
                , "UpdateRequest To Dataverse via IOrganizationService"
                , useWebAPI: true);

            if (resp == null)
                throw LastException;
        }

        #endregion

        #region IOrganzationServiceAsync helper - Proxy object
        /// <summary>
        /// Associate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await AssociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <returns>The ID of the created record</returns>
        public async Task<Guid> CreateAsync(Entity entity)
        {
            return await CreateAsync(entity, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        public async Task DeleteAsync(string entityName, Guid id)
        {
            await DeleteAsync(entityName, id, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        /// <summary>
        /// Disassociate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await DisassociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        /// <summary>
        /// Perform an action in an organization specified by the request.
        /// </summary>
        /// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
        /// <returns>Results from processing the request</returns>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return await ExecuteAsync(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="columnSet">Column Set collection to return with the request</param>
        /// <returns>Selected Entity</returns>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return await RetrieveAsync(entityName, id, columnSet, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <param name="query"></param>
        /// <returns>Returns an EntityCollection Object containing the results of the query</returns>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return await RetrieveMultipleAsync(query, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to update</param>
        public async Task UpdateAsync(Entity entity)
        {
            await UpdateAsync(entity, CancellationToken.None).ConfigureAwait(false);
            return;
        }


        #endregion

        #region IOrganzationServiceAsync2 helper - Proxy object

        /// <summary>
        /// Associate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            AssociateResponse resp = (AssociateResponse)await ExecuteOrganizationRequestAsyncImpl(new AssociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }
            , cancellationToken
            , "Associate To Dataverse via IOrganizationService").ConfigureAwait(false);
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>The ID of the created record</returns>
        public async Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken)
        {
            // Relay to Update request.
            CreateResponse resp = (CreateResponse)await ExecuteOrganizationRequestAsyncImpl(
                new CreateRequest()
                {
                    Target = entity
                }
                , cancellationToken
                , "Create To Dataverse via IOrganizationService"
                , useWebAPI: true).ConfigureAwait(false);
            if (resp == null)
                throw LastException;

            return resp.id;
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Returns the newly created record</returns>
        public async Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken)
        {
            CreateResponse resp = (CreateResponse)await ExecuteOrganizationRequestAsyncImpl(
            new CreateRequest()
            {
                Target = entity
            }
            , cancellationToken
            , "Create To Dataverse via IOrganizationService"
            , useWebAPI: true).ConfigureAwait(false);
            
            if (resp == null) throw LastException;

            // Get the Response and query the entity with all fields. 
            return await RetrieveAsync(entity.LogicalName, resp.id, new ColumnSet(true)).ConfigureAwait(false); 
        }
        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <returns>Returns the newly created record</returns>
        public async Task<Entity> CreateAndReturnAsync(Entity entity)
        {
            return await CreateAndReturnAsync(entity, CancellationToken.None).ConfigureAwait(false);    
        }

        /// <summary>
        /// Delete instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public async Task DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken)
        {
            DeleteResponse resp = (DeleteResponse)await ExecuteOrganizationRequestAsyncImpl(
               new DeleteRequest()
               {
                   Target = new EntityReference(entityName, id)
               }
               , cancellationToken
               , "Delete Request to Dataverse via IOrganizationService"
               , useWebAPI: true).ConfigureAwait(false);
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Disassociate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            DisassociateResponse resp = (DisassociateResponse)await ExecuteOrganizationRequestAsyncImpl(new DisassociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }
            , cancellationToken
            , "Disassociate To Dataverse via IOrganizationService").ConfigureAwait(false);
            if (resp == null)
                throw LastException;
        }

        /// <summary>
        /// Perform an action in an organization specified by the request.
        /// </summary>
        /// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Results from processing the request</returns>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, CancellationToken cancellationToken)
        {
            OrganizationResponse resp = await ExecuteOrganizationRequestAsyncImpl(request
                , cancellationToken
                , string.Format("Execute ({0}) request to Dataverse from IOrganizationService", request.RequestName)
                , useWebAPI: true).ConfigureAwait(false);
            if (resp == null)
                throw LastException;
            return resp;
        }

        /// <summary>
        /// Retrieves instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="columnSet">Column Set collection to return with the request</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Selected Entity</returns>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, CancellationToken cancellationToken)
        {
            RetrieveResponse resp = (RetrieveResponse)await ExecuteOrganizationRequestAsyncImpl(
            new RetrieveRequest()
            {
                ColumnSet = columnSet,
                Target = new EntityReference(entityName, id)
            }
            , cancellationToken
            , "Retrieve Request to Dataverse via IOrganizationService").ConfigureAwait(false);
            if (resp == null)
                throw LastException;

            return resp.Entity;
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <param name="query"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Returns an EntityCollection Object containing the results of the query</returns>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken)
        {
            RetrieveMultipleResponse resp = (RetrieveMultipleResponse)await ExecuteOrganizationRequestAsyncImpl(new RetrieveMultipleRequest() { Query = query }, cancellationToken, "RetrieveMultiple to Dataverse via IOrganizationService").ConfigureAwait(false);
            if (resp == null)
                throw LastException;

            return resp.EntityCollection;
        }

        /// <summary>
        /// Updates an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to update</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public async Task UpdateAsync(Entity entity, CancellationToken cancellationToken)
        {
            // Relay to Update request.
            UpdateResponse resp = (UpdateResponse)await ExecuteOrganizationRequestAsyncImpl(
                new UpdateRequest()
                {
                    Target = entity
                }
                , cancellationToken
                , "UpdateRequest To Dataverse via IOrganizationService"
                , useWebAPI: true).ConfigureAwait(false);

            if (resp == null)
                throw LastException;
        }

        #endregion

        #region IDisposable Support
        private bool isDisposed = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    //if (_CdsServiceClientTokenCache != null)
                    //    _CdsServiceClientTokenCache.Dispose();


                    if (_logEntry != null)
                    {
                        _logEntry.Dispose();
                    }

                    if (_connectionSvc != null)
                    {
                        _connectionSvc.Dispose();
                    }

                    _connectionSvc = null;
                    IsReady = false;

                }
                isDisposed = true;
            }
        }


        /// <summary>
        /// Disposed the resources used by the ServiceClient.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
