using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Xml;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Xrm.Sdk.WebServiceClient;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Rest;
using Microsoft.PowerPlatform.Cds.Client.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerPlatform.Cds.Client
{
    /// <summary>
    /// Primary implementation of the API interface for CDS. 
    /// </summary>
    public sealed class CdsServiceClient : IOrganizationService, IDisposable
    {
        #region Vars


        /// <summary>
        /// Cached Object collection, used for pick lists and such. 
        /// </summary>
        private Dictionary<string, Dictionary<string, object>> _CachObject; //Cache object. 

        /// <summary>
        /// List of CDS Language ID's 
        /// </summary>
        private List<int> CdsLoadedLCIDList;

        /// <summary>
        /// Name of the cache object. 
        /// </summary>
        private string CACHOBJECNAME = ".LookupCache";

        /// <summary>
        /// Logging object for the CDS Interface. 
        /// </summary>
        internal CdsTraceLogger logEntry;

        /// <summary>
        /// Enabled Log Capture in memory
        /// This capability enables logs that would normally be sent to your configured
        /// </summary>
        public static bool InMemoryLogCollectionEnabled { get; set; } = Utils.AppSettingsHelper.GetAppSetting<bool>("InMemoryLogCollectionEnabled", false);

        /// <summary>
        /// This is the number of minuets that logs will be retained before being purged from memory. Default is 5 min.
        /// This capability controls how long the log cache is kept in memory. 
        /// </summary>
        public static TimeSpan InMemoryLogCollectionTimeOutMinutes { get; set; } = Utils.AppSettingsHelper.GetAppSettingTimeSpan("InMemoryLogCollectionTimeOutMinutes", Utils.AppSettingsHelper.TimeSpanFromKey.Minutes, new TimeSpan(0, 0, 5, 0));


        /// <summary>
        /// CDS Web Service Connector layer
        /// </summary>
        internal CdsConnectionService CdsConnectionSvc;

        /// <summary>
        /// Dynamic app utility
        /// </summary>
        private DynamicEntityUtility dynamicAppUtility = null;

        /// <summary>
        /// Metadata Utility
        /// </summary>
        private MetadataUtility metadataUtlity = null;

        /// <summary>
        /// This is an internal Lock object,  used to sync communication with CDS. 
        /// </summary>
        internal object _lockObject = new object();

        /// <summary>
        /// BatchManager for Execute Multiple. 
        /// </summary>
        private BatchManager _BatchManager = null;

        /// <summary>
        /// To cache the token
        /// </summary>
        private static CdsServiceClientTokenCache _CdsServiceClientTokenCache;

        private bool _disableConnectionLocking = false;

        /// <summary>
        /// MinVersion that supports AAD Caller ID. 
        /// </summary>
        private Version _minAADCallerIDSupportedVersion = new Version("8.1.0.0");

        /// <summary>
        /// MinVersion that supports Session ID Telemetry Tracking. 
        /// </summary>
        private Version _minSessionTrackingSupportedVersion = new Version("9.0.2.0");

        /// <summary>
        /// MinVersion that supports Forcing Cache Sync. 
        /// </summary>
        private Version _minForceConsistencySupportedVersion = new Version("9.1.0.0");

        /// <summary>
        /// Minimum version supported by the Web API 
        /// </summary>
        private Version _minWebAPISupportedVersion = new Version("8.0.0.0");

        /// <summary>
        /// SDK Version property backer. 
        /// </summary>
        public string _sdkVersionProperty = null;

        /// <summary>
        /// Number of retries for an execute operation
        /// </summary>
        private int _maxRetryCount = Utils.AppSettingsHelper.GetAppSetting("ApiOperationRetryCountOverride", 10);

        /// <summary>
        /// Amount of time to wait between retries 
        /// </summary>
        private TimeSpan _retryPauseTime = Utils.AppSettingsHelper.GetAppSetting("ApiOperationRetryDelayOverride", new TimeSpan(0, 0, 0, 5));

        /// <summary>
        /// Value used by the retry system while the code is running, 
        /// this value can scale up and down based on throttling limits. 
        /// </summary>
        private TimeSpan _retryPauseTimeRunning;

        /// <summary>
        /// Throttling - ErrorCode for Rate Limit exceeded
        /// </summary>
        private const int RateLimitExceededErrorCode = -2147015902;
        /// <summary>
        /// Throttling - ErrorCode for TimeLimitExceeded
        /// </summary>
        private const int TimeLimitExceededErrorCode = -2147015903;
        /// <summary>
        /// Throttling - ErrorCode for Concurrency Limit Exceeded
        /// </summary>
        private const int ConcurrencyLimitExceededErrorCode = -2147015898;

        /// <summary>
        /// Parameter used to change the default layering behavior during solution import
        /// </summary>
        private const string DESIREDLAYERORDERPARAM = "DesiredLayerOrder";

        /// <summary>
        /// Parameter used to pass the solution name - Telemetry only
        /// </summary>
        private const string SOLUTIONNAMEPARAM = "SolutionName";

        /// <summary>
        /// Internal Organization Service Interface used for Testing
        /// </summary>
        internal IOrganizationService _TestOrgSvcInterface { get; set; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets max retry count.
        /// </summary>
        public int MaxRetryCount
        {
            get { return _maxRetryCount; }
            set { _maxRetryCount = value; }
        }

        /// <summary>
        /// Gets or sets retry pause time.
        /// </summary>
        public TimeSpan RetryPauseTime
        {
            get { return _retryPauseTime; }
            set { _retryPauseTime = value; }
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
                if (_BatchManager != null)
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
                if (CdsConnectionSvc != null && (
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.Certificate ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.ExternalTokenManagement ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.ClientSecret))
                    return CdsConnectionSvc.Authority;
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
                if (CdsConnectionSvc != null && CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                    return CdsConnectionSvc.UserId;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Gets or Sets the Max Connection Timeout for the connection. 
        /// Default setting is 2 min, 
        /// this property can also be set via app.config/app.settings with the property MaxCdsConnectionTimeOutMinutes
        /// </summary>
        public static TimeSpan MaxConnectionTimeout
        {
            get
            {
                return CdsConnectionService.MaxConnectionTimeout;
            }
            set
            {
                CdsConnectionService.MaxConnectionTimeout = value;
            }
        }

        /// <summary>
        /// Authentication Type to use 
        /// </summary>
        public AuthenticationType ActiveAuthenticationType
        {
            get
            {
                if (CdsConnectionSvc != null)
                    return CdsConnectionSvc.AuthenticationTypeInUse;
                else
                    return AuthenticationType.InvalidConnection;
            }
        }

        /// <summary>
        ///  Exposed OrganizationWebProxyClient for consumers
        /// </summary>
        public OrganizationWebProxyClient OrganizationWebProxyClient
        {
            get
            {
                if (CdsConnectionSvc != null)
                {
                    if (CdsConnectionSvc.CdsWebClient == null)
                    {
                        if (logEntry != null)
                            logEntry.Log("OrganizationWebProxyClient is null", TraceEventType.Error);
                        return null;
                    }
                    else
                        return CdsConnectionSvc.CdsWebClient;
                }
                else
                {
                    if (logEntry != null)
                        logEntry.Log("OrganizationWebProxyClient is null", TraceEventType.Error);
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the current access token in Use to connect to CDS. 
        /// Note: this is only available when a token based authentication process is in use. 
        /// </summary>
        public string CurrentAccessToken
        {
            get
            {
                if (CdsConnectionSvc != null && (
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.Certificate ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.ExternalTokenManagement ||
                    CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.ClientSecret))
                {
                    return CdsConnectionSvc.RefreshWebProxyClientToken().Result;
                }
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Pointer to CDS Service. 
        /// </summary>
        internal IOrganizationService _CdsService
        {
            get
            {
                // Added to support testing of CdsServiceClient direct code. 
                if (_TestOrgSvcInterface != null)
                    return _TestOrgSvcInterface;

                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.CdsWebClient;
                }
                else return null;
            }
        }

        /// <summary>
        /// Current user Record.
        /// </summary>
        internal WhoAmIResponse _SystemUser
        {
            get
            {
                if (CdsConnectionSvc != null)
                {
                    if (CdsConnectionSvc.CdsUser != null)
                        return CdsConnectionSvc.CdsUser;
                    else
                    {
                        WhoAmIResponse resp = CdsConnectionSvc.GetWhoAmIDetails(this);
                        CdsConnectionSvc.CdsUser = resp;
                        return resp;
                    }
                }
                else
                    return null;
            }
            set
            {
                CdsConnectionSvc.CdsUser = value;
            }
        }

        /// <summary>
        /// Returns the Last String Error that was created by the Cds Connection
        /// </summary>
        public string LastCdsError { get { if (logEntry != null) return logEntry.LastError; else return string.Empty; } }

        /// <summary>
        /// Returns the Last Exception from CDS. 
        /// </summary>
        public Exception LastCdsException { get { if (logEntry != null) return logEntry.LastException; else return null; } }

        /// <summary>
        /// Returns the Actual URI used to connect to CDS. 
        /// this URI could be influenced by user defined variables. 
        /// </summary>
        public Uri CdsConnectOrgUriActual { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.CdsConnectOrgUriActual; else return null; } }

        /// <summary>
        /// Returns the friendly name of the connected org. 
        /// </summary>
        public string ConnectedOrgFriendlyName { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.ConnectedOrgFriendlyName; else return null; } }
        /// <summary>
        /// 
        /// Returns the unique name for the org that has been connected. 
        /// </summary>
        public string ConnectedOrgUniqueName { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.CustomerOrganization; else return null; } }
        /// <summary>
        /// Returns the endpoint collection for the connected org. 
        /// </summary>
        public EndpointCollection ConnectedOrgPublishedEndpoints { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.ConnectedOrgPublishedEndpoints; else return null; } }

        /// <summary>
        /// This is the connection lock object that is used to control connection access for various threads. This should be used if you are using the CDS queries via Linq to lock the connection 
        /// </summary>
        public object ConnectionLockObject { get { return _lockObject; } }

        /// <summary>
        /// Returns the Version Number of the connected CDS organization. 
        /// If access before the Organization is connected, value returned will be null or 0.0 
        /// </summary>
        public Version ConnectedOrgVersion { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.OrganizationVersion; else return new Version(0, 0); } }

        /// <summary>
        /// ID of the connected organization. 
        /// </summary>
        public Guid ConnectedOrgId { get { if (CdsConnectionSvc != null) return CdsConnectionSvc.OrganizationId; else return Guid.Empty; } }

        /// <summary>
        /// Disabled internal cross thread safeties, this will gain much higher performance, however it places the requirements of thread safety on you, the developer. 
        /// </summary>
        public bool DisableCrossThreadSafeties { get { return _disableConnectionLocking; } set { _disableConnectionLocking = value; } }

        /// <summary>
        /// Returns the access token from the attached function.
        /// This is set via the CdsServiceContructor that accepts a target url and a function to return an access token. 
        /// </summary>
        internal Func<string, Task<string>> GetAccessToken { get; set; }

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
                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.CallerAADObjectId;
                }
                return null;
            }
            set
            {
                if (CdsConnectionSvc != null && CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= _minAADCallerIDSupportedVersion))
                    CdsConnectionSvc.CallerAADObjectId = value;
                else
                {
                    if (CdsConnectionSvc.OrganizationVersion != null)
                    {
                        CdsConnectionSvc.CallerAADObjectId = null; // Null value as this is not supported for this version. 
                        logEntry.Log($"Setting CallerAADObject ID not supported in version {CdsConnectionSvc.OrganizationVersion}");
                    }
                }
            }
        }

        /// <summary>
        /// This ID is used to support CDS Telemetry when trouble shooting SDK based errors.
        /// When Set by the caller, all CDS API Actions executed by this client will be tracked under a single session id for later troubleshooting. 
        /// For example, you are able to group all actions in a given run of your client ( several creates / reads and such ) under a given tracking id that is shared on all requests. 
        /// providing this ID when reporting a problem will aid in trouble shooting your issue. 
        /// </summary>
        public Guid? SessionTrackingId
        {
            get
            {
                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.SessionTrackingId;
                }
                return null;
            }

            set
            {
                if (CdsConnectionSvc != null && CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= _minSessionTrackingSupportedVersion))
                    CdsConnectionSvc.SessionTrackingId = value;
                else
                {
                    if (CdsConnectionSvc.OrganizationVersion != null)
                    {
                        CdsConnectionSvc.SessionTrackingId = null; // Null value as this is not supported for this version. 
                        logEntry.Log($"Setting SessionTrackingId ID not supported in version {CdsConnectionSvc.OrganizationVersion}");
                    }
                }
            }

        }

        /// <summary>
        /// This will force the CDS server to refresh the current metadata cache with current DB config.
        /// Note, that this is a performance impacting property. 
        /// Use of this flag will slow down operations server side as the server is required to check for consistency of the platform metadata against disk on each API call executed. 
        /// It is recommended to use this ONLY in conjunction with solution import or delete operations. 
        /// </summary>
        public bool ForceServerMetadataCacheConsistency
        {
            get
            {
                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.ForceServerCacheConsistency;
                }
                return false;
            }
            set
            {
                if (CdsConnectionSvc != null && CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= _minForceConsistencySupportedVersion))
                    CdsConnectionSvc.ForceServerCacheConsistency = value;
                else
                {
                    if (CdsConnectionSvc.OrganizationVersion != null)
                    {
                        CdsConnectionSvc.ForceServerCacheConsistency = false; // Null value as this is not supported for this version. 
                        logEntry.Log($"Setting ForceServerMetadataCacheConsistency not supported in version {CdsConnectionSvc.OrganizationVersion}");
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
                    _sdkVersionProperty = FileVersionInfo.GetVersionInfo(typeof(OrganizationWebProxyClient).Assembly.Location).FileVersion;
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
                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.TenantId;
                }
                else
                    return Guid.Empty;
            }
        }

        /// <summary>
        /// Gets the PowerPlatform Environment Id of the environment that is hosting this instance of CDS
        /// </summary>
        public string EnvironmentId
        {
            get
            {
                if (CdsConnectionSvc != null)
                {
                    return CdsConnectionSvc.EnvironmentId;
                }
                else
                    return string.Empty;
            }
        }

        #endregion

        #region Constructor and Setup methods

        /// <summary>
        /// Default / Non accessible constructor 
        /// </summary>
        private CdsServiceClient()
        { }

        /// <summary>
        /// Internal constructor used for testing. 
        /// </summary>
        /// <param name="orgSvc"></param>
        /// <param name="httpClient"></param>
        /// <param name="targetVersion"></param>
        internal CdsServiceClient(IOrganizationService orgSvc , HttpClient httpClient, Version targetVersion = null)
        {
            _TestOrgSvcInterface = orgSvc;
            logEntry = new CdsTraceLogger()
            {
                NumeberOfMinuetsToRetainInMemoryLogs = new TimeSpan(0,10,0),
                EnabledInMemoryLogCapture = true
            };
            CdsConnectionSvc = new CdsConnectionService(orgSvc);
            CdsConnectionSvc.WebApiHttpClient = httpClient;
            
            if ( targetVersion != null)
                CdsConnectionSvc.OrganizationVersion = targetVersion;

            _BatchManager = new BatchManager(logEntry);
            metadataUtlity = new MetadataUtility(this);
            dynamicAppUtility = new DynamicEntityUtility(this, metadataUtlity);
        }

        /// <summary>
        /// CdsServiceClient to accept the connectionstring as a parameter
        /// </summary>
        /// <param name="cdsConnectionString"></param>
        public CdsServiceClient(string cdsConnectionString)
        {
            if (string.IsNullOrEmpty(cdsConnectionString))
                throw new ArgumentNullException("CDS ConnectionString", "CDS ConnectionString cannot be null or empty.");

            ConnectToCdsService(cdsConnectionString);
        }

        /// <summary>
        /// Uses the Organization Web proxy Client provided by the user
        /// </summary>
        /// <param name="externalOrgWebProxyClient">User Provided Organization Web Proxy Client</param>
        public CdsServiceClient(OrganizationWebProxyClient externalOrgWebProxyClient)
        {
            CreateCdsServiceConnection(null, AuthenticationType.OAuth, string.Empty, string.Empty, string.Empty, null, string.Empty,
                MakeSecureString(string.Empty), string.Empty, string.Empty, string.Empty, false, false, null, null, string.Empty, null,
                PromptBehavior.Auto, string.Empty, externalOrgWebProxyClient);
        }

        /// <summary>
        /// Creates an instance of CdsServiceClient who's authentication is managed by the caller. 
        /// This requires the caller to implement a function that will accept the InstanceURI as a string will return the access token as a string on demand when the CdsServiceClient requires it. 
        /// This approach is recommended when working with WebApplications or applications that are required to implement an on Behalf of flow for user authentication. 
        /// </summary>
        /// <param name="instanceUrl">URL of the CDS instance to connect too.</param>
        /// <param name="tokenProviderFunction">Function that will be called when the access token is require for interaction with CDS.  This function must accept a string (InstanceURI) and return a string (accesstoken) </param>
        /// <param name="useUniqueInstance">A value of "true" Forces the CdsClient to create a new connection to the cds instance vs reusing an existing connection, Defaults to true.</param>
        public CdsServiceClient(Uri instanceUrl, Func<string, Task<string>> tokenProviderFunction, bool useUniqueInstance = true)
        {
            GetAccessToken = tokenProviderFunction ??
                throw new CdsConnectionException("tokenProviderFunction required for this constructor", new ArgumentNullException("tokenProviderFunction"));  // Set the function pointer or access. 

            CreateCdsServiceConnection(
                   null, AuthenticationType.ExternalTokenManagement, string.Empty, string.Empty, string.Empty, null,
                   string.Empty, null, string.Empty, string.Empty, string.Empty, true, useUniqueInstance, null, null,
                   string.Empty, null, PromptBehavior.Never, string.Empty, null, string.Empty, StoreName.My, null, instanceUrl);
        }

        /// <summary>
        /// Log in with OAuth for online connections.
        /// </summary>
        /// <param name="userId">User Id supplied</param>
        /// <param name="password">Password for login</param>
        /// <param name="regionGeo">Region where server is provisioned in for login</param>
        /// <param name="orgName">Name of the organization to connect</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">CDS Org Detail object, this is is returned from a query to the CDS Discovery Server service. not required.</param>
        /// <param name="user">The user identifier.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="promptBehavior">The prompt Behavior.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        /// <param name="externalOrgWebProxyClient">The proxy for OAuth.</param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        public CdsServiceClient(string userId, SecureString password, string regionGeo, string orgName, bool useUniqueInstance, OrganizationDetail orgDetail,
                UserIdentifier user, string clientId, Uri redirectUri, string tokenCachePath, OrganizationWebProxyClient externalOrgWebProxyClient, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false)
        {
            if ((externalOrgWebProxyClient == null) && (string.IsNullOrEmpty(clientId) || redirectUri == null))
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using OAuth Authentication without an external specified proxy, you have to specify clientId and redirectUri.");
            }
            CreateCdsServiceConnection(
                    null, AuthenticationType.OAuth, string.Empty, string.Empty, orgName, null,
                    userId, password, string.Empty, regionGeo, string.Empty, true, useUniqueInstance, orgDetail, user,
                    clientId, redirectUri, promptBehavior, tokenCachePath, externalOrgWebProxyClient, useDefaultCreds: useDefaultCreds);
        }

        /// <summary>
        /// Log in with OAuth for On-Premises connections.
        /// </summary>
        /// <param name="userId">User Id supplied</param>
        /// <param name="password">Password for login</param>
        /// <param name="domain">Domain</param>
        /// <param name="homeRealm">Name of the organization to connect</param>
        /// <param name="hostName">Host name of the server that is hosting the CDS web service</param>
        /// <param name="port">Port number on the CDS Host Server ( usually 444 )</param>
        /// <param name="orgName">Organization name for the CDS Instance.</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">CDS Org Detail object, this is returned from a query to the CDS Discovery Server service. not required.</param>
        /// <param name="user">The user identifier.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="promptBehavior">The prompt Behavior.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        /// <param name="externalOrgWebProxyClient">The proxy for OAuth.</param>
        public CdsServiceClient(string userId, SecureString password, string domain, string homeRealm, string hostName, string port, string orgName, bool useSsl, bool useUniqueInstance,
                OrganizationDetail orgDetail, UserIdentifier user, string clientId, Uri redirectUri, string tokenCachePath, OrganizationWebProxyClient externalOrgWebProxyClient, PromptBehavior promptBehavior = PromptBehavior.Auto)
        {
            if ((externalOrgWebProxyClient == null) && (string.IsNullOrEmpty(clientId) || redirectUri == null))
            {
                throw new ArgumentOutOfRangeException("authType",
                    "When using OAuth Authentication without an external specified proxy, you have to specify clientId and redirectUri.");
            }
            CreateCdsServiceConnection(
                    null, AuthenticationType.OAuth, hostName, port, orgName, null,
                    userId, password, domain, string.Empty, string.Empty, useSsl, useUniqueInstance, orgDetail,
                    user, clientId, redirectUri, promptBehavior, tokenCachePath, externalOrgWebProxyClient);
        }

        /// <summary>
        /// Log in with Certificate Auth On-Premises connections.
        /// </summary>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">URL of the CDS instance to connect too</param>
        /// <param name="orgName">Organization name for the CDS Instance.</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">CDS Org Detail object, this is is returned from a query to the CDS Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        public CdsServiceClient(X509Certificate2 certificate, StoreName certificateStoreName, string certificateThumbPrint, Uri instanceUrl, string orgName, bool useSsl, bool useUniqueInstance,
                OrganizationDetail orgDetail, string clientId, Uri redirectUri, string tokenCachePath)
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

            CreateCdsServiceConnection(
                    null, AuthenticationType.Certificate, string.Empty, string.Empty, orgName, null,
                    string.Empty, null, string.Empty, string.Empty, string.Empty, useSsl, useUniqueInstance, orgDetail,
                    null, clientId, redirectUri, PromptBehavior.Never, tokenCachePath, null, certificateThumbPrint, certificateStoreName, certificate, instanceUrl);
        }


        /// <summary>
        /// Log in with Certificate Auth OnLine connections.
        /// This requires the org API URI. 
        /// </summary>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">API URL of the CDS instance to connect too</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">CDS Org Detail object, this is is returned from a query to the CDS Discovery Server service. not required.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="redirectUri">The redirect URI application will be redirected post OAuth authentication.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        public CdsServiceClient(X509Certificate2 certificate, StoreName certificateStoreName, string certificateThumbPrint, Uri instanceUrl, bool useUniqueInstance, OrganizationDetail orgDetail,
                string clientId, Uri redirectUri, string tokenCachePath)
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

            CreateCdsServiceConnection(
                    null, AuthenticationType.Certificate, string.Empty, string.Empty, string.Empty, null,
                    string.Empty, null, string.Empty, string.Empty, string.Empty, true, useUniqueInstance, orgDetail, null,
                    clientId, redirectUri, PromptBehavior.Never, tokenCachePath, null, certificateThumbPrint, certificateStoreName, certificate, instanceUrl);
        }


        /// <summary>
        /// ClientID \ ClientSecret Based Authentication flow. 
        /// </summary>
        /// <param name="instanceUrl">Direct URL of CDS instance to connect too.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="clientSecret">Client Secret for Client Id.</param>
        /// <param name="useUniqueInstance">Use unique instance or reuse current connection.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        public CdsServiceClient(Uri instanceUrl, string clientId, string clientSecret, bool useUniqueInstance, string tokenCachePath)
        {
            CreateCdsServiceConnection(null,
                AuthenticationType.ClientSecret,
                string.Empty, string.Empty, string.Empty, null, string.Empty,
                MakeSecureString(clientSecret), string.Empty, string.Empty, string.Empty, true, useUniqueInstance,
                null, null, clientId, null, PromptBehavior.Never, tokenCachePath, null, null, instanceUrl: instanceUrl);
        }

        /// <summary>
        /// ClientID \ ClientSecret Based Authentication flow, allowing for Secure Client ID passing. 
        /// </summary>
        /// <param name="instanceUrl">Direct URL of CDS instance to connect too.</param>
        /// <param name="clientId">The registered client Id on Azure portal.</param>
        /// <param name="clientSecret">Client Secret for Client Id.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        /// <param name="useUniqueInstance">Use unique instance or reuse current connection.</param>
        public CdsServiceClient(Uri instanceUrl, string clientId, SecureString clientSecret, bool useUniqueInstance, string tokenCachePath)
        {
            CreateCdsServiceConnection(null,
                AuthenticationType.ClientSecret,
                string.Empty, string.Empty, string.Empty, null, string.Empty,
                clientSecret, string.Empty, string.Empty, string.Empty, true, useUniqueInstance,
                null, null, clientId, null, PromptBehavior.Never, tokenCachePath, null, null, instanceUrl: instanceUrl);
        }

        /// <summary>
        /// Parse the given connection string 
        /// Connects to CDS using CreateCdsWebServiceConnection
        /// </summary>
        /// <param name="cdsConnectionString"></param>
        internal void ConnectToCdsService(string cdsConnectionString)
        {
            var parsedCdsCon = CdsConnectionStringProcessor.Parse(cdsConnectionString);

            var serviceUri = parsedCdsCon.ServiceUri;

            var networkCredentials = parsedCdsCon.ClientCredentials != null && parsedCdsCon.ClientCredentials.Windows != null ?
                parsedCdsCon.ClientCredentials.Windows.ClientCredential : System.Net.CredentialCache.DefaultNetworkCredentials;

            string orgName = parsedCdsCon.Organization;

            if ((parsedCdsCon.SkipDiscovery && parsedCdsCon.ServiceUri != null) && string.IsNullOrEmpty(orgName))  
                // Orgname is mandatory if skip discovery is not passed
                throw new ArgumentNullException("Cds Instance Name or URL name Required", 
                        parsedCdsCon.IsOnPremOauth ? 
                        $"Unable to determine instance name to connect to from passed instance Uri, Uri does not match known online deployments." :
                        $"Unable to determine instance name to connect to from passed instance Uri. Uri does not match specification for OnPrem instances.");

            string homesRealm = parsedCdsCon.HomeRealmUri != null ? parsedCdsCon.HomeRealmUri.AbsoluteUri : string.Empty;

            string userId = parsedCdsCon.UserId;
            string password = parsedCdsCon.Password;
            string domainname = parsedCdsCon.DomainName;
            string onlineRegion = parsedCdsCon.CdsGeo;
            string clientId = parsedCdsCon.ClientId;
            string hostname = serviceUri.Host;
            string port = Convert.ToString(serviceUri.Port);



            Uri redirectUri = parsedCdsCon.RedirectUri;

            bool useSsl = serviceUri.Scheme == "https" ? true : false;

            switch (parsedCdsCon.AuthenticationType)
            {

                case AuthenticationType.OAuth:
                    hostname = parsedCdsCon.IsOnPremOauth ? hostname : string.Empty; // 
                    port = parsedCdsCon.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) && redirectUri == null)
                    {
                        throw new ArgumentNullException("ClientId and Redirect Name", "ClientId or Redirect uri cannot be null or empty.");
                    }


                    CreateCdsServiceConnection(null, parsedCdsCon.AuthenticationType, hostname, port, orgName, networkCredentials, userId,
                                                MakeSecureString(password), domainname, onlineRegion, homesRealm, useSsl, parsedCdsCon.UseUniqueConnectionInstance,
                                                    null, parsedCdsCon.UserIdentifier, clientId, redirectUri, parsedCdsCon.PromptBehavior, parsedCdsCon.TokenCacheStorePath, instanceUrl: parsedCdsCon.SkipDiscovery ? parsedCdsCon.ServiceUri : null, useDefaultCreds: parsedCdsCon.UseCurrentUser);
                    break;
                case AuthenticationType.Certificate:
                    hostname = parsedCdsCon.IsOnPremOauth ? hostname : string.Empty; // 
                    port = parsedCdsCon.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(parsedCdsCon.CertThumbprint))
                    {
                        throw new ArgumentNullException("ClientId or Certificate Thumbprint must be populated for Certificate Auth Type.");
                    }

                    StoreName targetStoreName = StoreName.My;
                    if (!string.IsNullOrEmpty(parsedCdsCon.CertStoreName))
                    {
                        Enum.TryParse<StoreName>(parsedCdsCon.CertStoreName, out targetStoreName);
                    }

                    CreateCdsServiceConnection(null, parsedCdsCon.AuthenticationType, hostname, port, orgName, null, string.Empty,
                                                null, string.Empty, onlineRegion, string.Empty, useSsl, parsedCdsCon.UseUniqueConnectionInstance,
                                                    null, null, clientId, redirectUri, PromptBehavior.Never, parsedCdsCon.TokenCacheStorePath, null, parsedCdsCon.CertThumbprint, targetStoreName, instanceUrl: parsedCdsCon.ServiceUri);

                    break;
                case AuthenticationType.ClientSecret:
                    hostname = parsedCdsCon.IsOnPremOauth ? hostname : string.Empty;
                    port = parsedCdsCon.IsOnPremOauth ? port : string.Empty;

                    if (string.IsNullOrEmpty(clientId) && string.IsNullOrEmpty(parsedCdsCon.ClientSecret))
                    {
                        throw new ArgumentNullException("ClientId and ClientSecret must be populated for ClientSecret Auth Type.",
                            $"Client Id={(string.IsNullOrEmpty(clientId) ? "Not Specfied and Required." : clientId)} | Client Secret={(string.IsNullOrEmpty(parsedCdsCon.ClientSecret) ? "Not Specfied and Required." : "Specfied")}");
                    }

                    CreateCdsServiceConnection(null, parsedCdsCon.AuthenticationType, hostname, port, orgName, null, string.Empty,
                                                 MakeSecureString(parsedCdsCon.ClientSecret), string.Empty, onlineRegion, string.Empty, useSsl, parsedCdsCon.UseUniqueConnectionInstance,
                                                    null, null, clientId, redirectUri, PromptBehavior.Never, parsedCdsCon.TokenCacheStorePath, null, null, instanceUrl: parsedCdsCon.ServiceUri);

                    break;
            }
        }


        /// <summary>
        /// Uses the Organization Web proxy Client provided by the user
        /// </summary>
        /// <param name="externalOrgWebProxyClient">User Provided Organization Web Proxy Client</param>
        /// <param name="isCloned">when true, skips init</param>
        internal CdsServiceClient(OrganizationWebProxyClient externalOrgWebProxyClient, bool isCloned = true , AuthenticationType orginalAuthType = AuthenticationType.OAuth)
        {
            CreateCdsServiceConnection(null, orginalAuthType, string.Empty, string.Empty, string.Empty, null, string.Empty,
                MakeSecureString(string.Empty), string.Empty, string.Empty, string.Empty, false, false, null, null, string.Empty, null,
                PromptBehavior.Auto, string.Empty, externalOrgWebProxyClient, isCloned: isCloned);
        }


        /// <summary>
        /// Sets up the CDS Web Service Connection
        ///  For Connecting via AD
        /// </summary>
        /// <param name="externalOrgServiceProxy">if populated, is the org service to use to connect to CDS</param>
        /// <param name="requestedAuthType">Authentication Type requested</param>
        /// <param name="hostName">Host name of the server that is hosting the CDS web service</param>
        /// <param name="port">Port number on the CDS Host Server ( usually 5555 )</param>
        /// <param name="orgName">Organization name for the CDS Instance.</param>
        /// <param name="credential">Network Credential Object used to login with</param>
        /// <param name="userId">Live ID to connect with</param>
        /// <param name="password">Live ID Password to connect with</param>
        /// <param name="domain">Name of the Domain where the CDS is deployed</param>
        /// <param name="Geo">Region hosting the CDS online Server, can be NA, EMEA, APAC</param>
        /// <param name="claimsHomeRealm">HomeRealm Uri for the user</param>
        /// <param name="useSsl">if true, https:// used</param>
        /// <param name="useUniqueInstance">if set, will force the system to create a unique connection</param>
        /// <param name="orgDetail">CDS Org Detail object, this is is returned from a query to the CDS Discovery Server service. not required.</param>
        /// <param name="user">The user identifier.</param>
        /// <param name="clientId">Registered Client Id on Azure</param>
        /// <param name="promptBehavior">Default Prompt Behavior</param>
        /// <param name="redirectUri">Registered redirect uri for ADAL to return</param>
        /// <param name="tokenCachePath">Token cache path where the token shall be stored for persistent storage</param>
        /// <param name="externalOrgWebProxyClient">OAuth related web proxy client</param>
        /// <param name="certificate">Certificate to use during login</param>
        /// <param name="certificateStoreName">StoreName to look in for certificate identified by certificateThumbPrint</param>
        /// <param name="certificateThumbPrint">ThumbPrint of the Certificate to load</param>
        /// <param name="instanceUrl">Actual URI of the Organization Instance</param>
        /// <param name="isCloned">When True, Indicates that the contruction request is coming from a clone operation. </param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        internal void CreateCdsServiceConnection(
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
            UserIdentifier user = null,
            string clientId = "",
            Uri redirectUri = null,
            PromptBehavior promptBehavior = PromptBehavior.Auto,
            string tokenCachePath = "",
            OrganizationWebProxyClient externalOrgWebProxyClient = null,
            string certificateThumbPrint = "",
            StoreName certificateStoreName = StoreName.My,
            X509Certificate2 certificate = null,
            Uri instanceUrl = null,
            bool isCloned = false,
            bool useDefaultCreds = false
            )
        {

            logEntry = new CdsTraceLogger
            {
                // Set initial properties
                EnabledInMemoryLogCapture = InMemoryLogCollectionEnabled,
                NumeberOfMinuetsToRetainInMemoryLogs = InMemoryLogCollectionTimeOutMinutes
            }; 

            CdsConnectionSvc = null;

#if (NETCOREAPP3_0 || NETCOREAPP3_1)
			if (requestedAuthType == AuthenticationType.OAuth)
			{
				IsReady = false;
                Utils.CdsConnectionException connExcept = new Utils.CdsConnectionException("Unsupported Connection request", 
                    new NotSupportedException("User/Password flows are not supported in .net core based client at this time."));
                logEntry.Log(connExcept);
                throw connExcept; 
            }
#endif

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
                        logEntry.Log(string.Format("DIRECTSET URL detected via Login OrgDetails Property, Setting Connect URI to {0}", instanceUrl.ToString()));
                    }
                }
            }



            try
            {

                // Support for things like Excel that do not run from a local directory. 
                if (File.Exists("microsoft.cds.sdk.dll"))
                {
                    // Do CDS Assembly version Check... 
                    // Must be assemblies of version 5.0.9688.1533 or newer. 
                    FileVersionInfo fv = FileVersionInfo.GetVersionInfo("microsoft.cds.sdk.dll");

                    if (fv != null)
                    {
                        Version fileVersion = new Version(fv.ProductVersion);
                        Version minVersion = new Version("5.0.9688.1533");
                        if (!(fileVersion >= minVersion))
                        {
                            logEntry.Log("!!WARNING!!! The version of the CDS product assemblies is less than the recommend version for this API; you must use version 5.0.9688.1533 or newer (Newer then the Oct-2011 service release)", TraceEventType.Warning);
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "CDS Version found is {0}", fv.ProductVersion), TraceEventType.Warning);
                        }
                    }
                }
            }
            catch
            {
                logEntry.Log("!!WARNING!!! Failed to determine the version of the CDS SDK Present", TraceEventType.Warning);
            }
            metadataUtlity = new MetadataUtility(this);
            dynamicAppUtility = new DynamicEntityUtility(this, metadataUtlity);

            // doing a direct Connect,  use Connection Manager to do the connect. 
            // if using an user provided connection,. 
            if (externalOrgWebProxyClient != null)
            {
                CdsConnectionSvc = new CdsConnectionService(externalOrgWebProxyClient, logEntry);
                CdsConnectionSvc.IsAClone = isCloned;
            }
            else
            {
                if (requestedAuthType == AuthenticationType.ExternalTokenManagement)
                {
                    CdsConnectionSvc = new CdsConnectionService(requestedAuthType, instanceUrl, useUniqueInstance, orgDetail, clientId, redirectUri, certificateThumbPrint, certificateStoreName, certificate, tokenCachePath, hostName, port, false, logEntry);
                    if (GetAccessToken != null)
                        CdsConnectionSvc.GetAccessToken = GetAccessToken;
                    else
                    {
                        // Should not get here,  however..
                        throw new CdsConnectionException("tokenProviderFunction required for ExternalTokenManagement Auth type, You must use the appropriate constructor for this auth type.", new ArgumentNullException("tokenProviderFunction"));
                    }
                }
                else
                {
                    // check to see what sort of login this is. 
                    if (requestedAuthType == AuthenticationType.OAuth)
                    {
                        if (!String.IsNullOrEmpty(hostName))
                            CdsConnectionSvc = new CdsConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, user, clientId, redirectUri, promptBehavior, tokenCachePath, hostName, port, true, instanceToConnectToo: instanceUrl, logSink: logEntry, useDefaultCreds: useDefaultCreds);
                        else
                            CdsConnectionSvc = new CdsConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, user, clientId, redirectUri, promptBehavior, tokenCachePath, hostName, port, false, instanceToConnectToo: instanceUrl, logSink: logEntry, useDefaultCreds: useDefaultCreds);
                    }
                    else if (requestedAuthType == AuthenticationType.Certificate)
                    {
                        CdsConnectionSvc = new CdsConnectionService(requestedAuthType, instanceUrl, useUniqueInstance, orgDetail, clientId, redirectUri, certificateThumbPrint, certificateStoreName, certificate, tokenCachePath, hostName, port, !String.IsNullOrEmpty(hostName), logSink: logEntry);
                    }
                    else if (requestedAuthType == AuthenticationType.ClientSecret)
                    {
                        if (!String.IsNullOrEmpty(hostName))
                            CdsConnectionSvc = new CdsConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, user, clientId, redirectUri, promptBehavior, tokenCachePath, hostName, port, true, instanceToConnectToo: instanceUrl, logSink: logEntry, useDefaultCreds: useDefaultCreds);
                        else
                            CdsConnectionSvc = new CdsConnectionService(requestedAuthType, orgName, userId, password, Geo, useUniqueInstance, orgDetail, user, clientId, redirectUri, promptBehavior, tokenCachePath, hostName, port, false, instanceToConnectToo: instanceUrl, logSink: logEntry, useDefaultCreds: useDefaultCreds);
                    }
                }
            }

            if (CdsConnectionSvc != null)
            {
                try
                {
                    // Assign the log entry host to the CdsConnectionService engine 
                    CdsConnectionService tempConnectService = null;
                    CdsConnectionSvc.InternetProtocalToUse = useSsl ? "https" : "http";
                    if (!CdsConnectionSvc.DoLogin(out tempConnectService))
                    {
                        this.logEntry.Log("Unable to Login to CDS", TraceEventType.Error);
                        IsReady = false;
                        return;
                    }
                    else
                    {
                        if (tempConnectService != null)
                        {
                            CdsConnectionSvc.Dispose();  // Clean up temp version and unassign assets. 
                            CdsConnectionSvc = tempConnectService;
                        }
                        CACHOBJECNAME = CdsConnectionSvc.ServiceCACHEName + ".LookupCache";

                        // Min supported version for batch operations. 
                        Version minVersion = new Version("5.0.9690.3000");
                        if (CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= minVersion))
                            _BatchManager = new BatchManager(logEntry);
                        else
                            logEntry.Log("Batch System disabled, CDS Server does not support this message call", TraceEventType.Information);

                        IsReady = true;

                    }
                }
                catch(Exception ex)
                {
                    throw new Utils.CdsConnectionException("Failed to connect to Common Data Service", ex);
                }
            }
        }

        #endregion

        #region Public General Interfaces

        /// <summary>
        /// Enabled only if InMemoryLogCollectionEnabled is true. 
        /// Return all logs currently stored for the cdsserviceclient in queue.
        /// </summary>
        public IEnumerable<Tuple<DateTime, string>> GetAllLogs()
        {
            IEnumerable<Tuple<DateTime, string>> source1 = logEntry == null ? Enumerable.Empty<Tuple<DateTime, string>>() : logEntry.Logs;
            IEnumerable<Tuple<DateTime, string>> source2 = this.CdsConnectionSvc == null
                ? Enumerable.Empty<Tuple<DateTime, string>>()
                : this.CdsConnectionSvc.GetAllLogs();
            return source1.Union(source2);
        }


        /// <summary>
        /// Clone, 'Clones" the current CDS Service client with a new connection to CDS. 
        /// Clone only works for connections creating using OAuth Protocol. 
        /// </summary>
        /// <returns>returns an active CdsServiceClient or null</returns>
        public CdsServiceClient Clone()
        {
            return Clone(null);
        }

        /// <summary>
        /// Clone, 'Clones" the current CDS Service client with a new connection to CDS. 
        /// Clone only works for connections creating using OAuth Protocol. 
        /// </summary>
        /// <param name="strongTypeAsm">Strong Type Assembly to reference as part of the create of the clone.</param>
        /// <returns></returns>
        public CdsServiceClient Clone(System.Reflection.Assembly strongTypeAsm)
        {
            if (CdsConnectionSvc == null || IsReady == false)
            {
                logEntry.Log("You must have successfully created a connection to CDS before it can be cloned.", TraceEventType.Error);
                return null;
            }

            OrganizationWebProxyClient proxy = null;
            if (CdsConnectionSvc.CdsConnectOrgUriActual != null)
            {
                if (strongTypeAsm == null)
                    proxy = new OrganizationWebProxyClient(CdsConnectionSvc.CdsConnectOrgUriActual, true);
                else
                    proxy = new OrganizationWebProxyClient(CdsConnectionSvc.CdsConnectOrgUriActual, strongTypeAsm);
            }
            else
            {
                var orgWebClient = CdsConnectionSvc.CdsWebClient;
                if (orgWebClient != null)
                {
                    if (strongTypeAsm == null)
                        proxy = new OrganizationWebProxyClient(orgWebClient.Endpoint.Address.Uri, true);
                    else
                        proxy = new OrganizationWebProxyClient(orgWebClient.Endpoint.Address.Uri, strongTypeAsm);
                }
                else
                {
                    logEntry.Log("Connection cannot be cloned.  There is currently no OAuth based connection active.");
                    return null;
                }
            }
            if (proxy != null)
            {
                proxy.HeaderToken = CdsConnectionSvc.CdsWebClient.HeaderToken;
                var SvcClient = new CdsServiceClient(proxy, true , CdsConnectionSvc.AuthenticationTypeInUse);
                SvcClient.CdsConnectionSvc.SetClonedProperties(this);
                SvcClient._BatchManager = _BatchManager;
                SvcClient.CallerAADObjectId = CallerAADObjectId;
                SvcClient.CallerId = CallerId;
                SvcClient.MaxRetryCount = _maxRetryCount;
                SvcClient.RetryPauseTime = _retryPauseTime;
                SvcClient.GetAccessToken = GetAccessToken; 

                return SvcClient;
            }
            else
            {
                logEntry.Log("Connection cannot be cloned.  There is currently no OAuth based connection active or it is mis-configured in the CdsServiceClient.");
                return null;
            }
        }

        #region CDS DiscoveryServerMethods

        /// <summary>
        /// Discovers the organizations.  can be used against regional discoveyr endpoints. 
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service URI.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="user">The user identifier.</param>
        /// <param name="clientId">The client Id.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        /// <param name="isOnPrem">The deployment type: OnPrem or Online.</param>
        /// <param name="authority">The authority provider for OAuth tokens. Unique if any already known.</param>
        /// <param name="useDefaultCreds">(Optional) if specified, tries to use the current user</param>
        /// <returns>A collection of organizations</returns>
        public static OrganizationDetailCollection DiscoverOrganizations(Uri discoveryServiceUri, ClientCredentials clientCredentials, UserIdentifier user, string clientId, Uri redirectUri, string tokenCachePath, bool isOnPrem, string authority, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false)
        {
            return CdsConnectionService.DiscoverOrganizations(discoveryServiceUri, clientCredentials, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, useDefaultCreds: useDefaultCreds);
        }

        /// <summary>
        /// Discovers the organizations, used for OAuth.
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service URI.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="user">The user identifier.</param>
        /// <param name="clientId">The client Id.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior.</param>
        /// <param name="tokenCachePath">The token cache path where token cache file is placed.</param>
        /// <param name="isOnPrem">The deployment type: OnPrem or Online.</param>
        /// <param name="authority">The authority provider for OAuth tokens. Unique if any already known.</param>
        /// <param name="useDefaultCreds">(Optional) if specified, tries to use the current user</param>
        /// <returns>A collection of organizations</returns>
        public static OrganizationDetailCollection DiscoverGlobalOrganizations(Uri discoveryServiceUri, ClientCredentials clientCredentials, UserIdentifier user, string clientId, Uri redirectUri, string tokenCachePath, bool isOnPrem, string authority, PromptBehavior promptBehavior = PromptBehavior.Auto, bool useDefaultCreds = false)
        {
            return CdsConnectionService.DiscoverOrganizations(discoveryServiceUri, clientCredentials, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, useGlobalDisco: true, useDefaultCreds: useDefaultCreds);
        }

        /// <summary>
        /// Discovers Organizations Using the global discovery service and an external source for access tokens
        /// </summary>
        /// <param name="discoveryServiceUri">Global discovery base URI to use to connect too,  if null will utilize the commercial Global Discovery Server.</param>
        /// <param name="tokenProviderFunction">Function that will provide access token to the discovery call.</param>
        /// <returns></returns>
        public static async Task<OrganizationDetailCollection> DiscoverGlobalOrganizations(Func<string,  Task<string>> tokenProviderFunction , Uri discoveryServiceUri = null)
        {
            if (discoveryServiceUri == null)
                discoveryServiceUri = new Uri(CdsConnectionService.GlobalDiscoveryAllInstancesUri); // use commercial GD

            return await CdsConnectionService.DiscoverGlobalOrganizations(discoveryServiceUri, tokenProviderFunction);
        }

        #endregion

        #region CDS Service Methods

        #region Batch Interface methods.
        /// <summary>
        /// Create a Batch Request for executing batch operations.  This returns an ID that will be used to identify a request as a batch request vs a "normal" request. 
        /// </summary>
        /// <param name="batchName">Name of the Batch</param>
        /// <param name="returnResults">Should Results be returned</param>
        /// <param name="continueOnError">Should the process continue on an error.</param>
        /// <returns></returns>
        public Guid CreateBatchOperationRequest(string batchName, bool returnResults = true, bool continueOnError = false)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            Guid guBatchId = Guid.Empty;
            if (_BatchManager != null)
            {
                // Try to create a new Batch here. 
                guBatchId = _BatchManager.CreateNewBatch(batchName, returnResults, continueOnError);
            }
            return guBatchId;
        }

        /// <summary>
        /// Returns the batch id for a given batch name. 
        /// </summary>
        /// <param name="batchName">Name of Batch</param>
        /// <returns></returns>
        public Guid GetBatchOperationIdRequestByName(string batchName)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            if (_BatchManager != null)
            {
                var b = _BatchManager.GetRequestBatchByName(batchName);
                if (b != null)
                    return b.BatchId;
            }
            return Guid.Empty;
        }


        /// <summary>
        /// Returns the organization request at a give position 
        /// </summary>
        /// <param name="batchId">ID of the batch</param>
        /// <param name="position">Position</param>
        /// <returns></returns>
        public OrganizationRequest GetBatchRequestAtPosition(Guid batchId, int position)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            RequestBatch b = GetBatchById(batchId);
            if (b != null)
            {
                if (b.BatchItems.Count >= position)
                    return b.BatchItems[position].Request;
            }
            return null;
        }

        /// <summary>
        /// Release a batch from the stack
        /// Once you have completed using a batch, you must release it from the system.
        /// </summary>
        /// <param name="batchId">ID of the batch</param>
        public void ReleaseBatchInfoById(Guid batchId)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return;
            }
            #endregion

            if (_BatchManager != null)
                _BatchManager.RemoveBatch(batchId);

        }

        /// <summary>
        /// TEMP
        /// </summary>
        /// <param name="batchId">ID of the batch</param>
        /// <returns></returns>
        public RequestBatch GetBatchById(Guid batchId)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            if (_BatchManager != null)
            {
                return _BatchManager.GetRequestBatchById(batchId);
            }
            return null;
        }

        /// <summary>
        /// Executes the batch command and then parses the retrieved items into a list. 
        /// If there exists a exception then the LastException would be filled with the first item that has the exception.
        /// </summary>
        /// <param name="batchId">ID of the batch to run</param>
        /// <returns>results which is a list of responses(type <![CDATA[ List<Dictionary<string, Dictionary<string, object>>> ]]>) in the order of each request or null or complete failure  </returns>
        public List<Dictionary<string, Dictionary<string, object>>> RetrieveBatchResponse(Guid batchId)
        {
            ExecuteMultipleResponse results = ExecuteBatch(batchId);
            if (results == null)
            {
                return null;
            }
            if (results.IsFaulted)
            {
                foreach (var response in results.Responses)
                {
                    if (response.Fault != null)
                    {
                        FaultException<OrganizationServiceFault> ex = new FaultException<OrganizationServiceFault>(response.Fault, new FaultReason(new FaultReasonText(response.Fault.Message)));

                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Execute Batch - {0}", batchId), TraceEventType.Verbose);
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ BatchExecution failed - : {0}\n\r{1}", response.Fault.Message, response.Fault.ErrorDetails.ToString()), TraceEventType.Error, ex);
                        break;
                    }
                }
            }
            List<Dictionary<string, Dictionary<string, object>>> retrieveMultipleResponseList = new List<Dictionary<string, Dictionary<string, object>>>();
            foreach (var response in results.Responses)
            {
                if (response.Response != null)
                {
                    retrieveMultipleResponseList.Add(CreateResultDataSet(((RetrieveMultipleResponse)response.Response).EntityCollection));
                }
            }
            return retrieveMultipleResponseList;
        }


        /// <summary>
        /// Begins running the Batch command. 
        /// </summary>
        /// <param name="batchId">ID of the batch to run</param>
        /// <returns>true if the batch begins, false if not. </returns>
        public ExecuteMultipleResponse ExecuteBatch(Guid batchId)
        {
            #region PreChecks
            logEntry.ResetLastError();
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!IsBatchOperationsAvailable)
            {
                logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            if (_BatchManager != null)
            {
                var b = _BatchManager.GetRequestBatchById(batchId);
                if (b.Status == BatchStatus.Complete || b.Status == BatchStatus.Running)
                {
                    logEntry.Log("Batch is not in the correct state to run", TraceEventType.Error);
                    return null;
                }

                if (!(b.BatchItems.Count > 0))
                {
                    logEntry.Log("No Items in the batch", TraceEventType.Error);
                    return null;
                }

                // Ready to run the batch. 
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executing Batch {0}|{1}, Sending {2} events.", b.BatchId, b.BatchName, b.BatchItems.Count), TraceEventType.Verbose);
                ExecuteMultipleRequest req = new ExecuteMultipleRequest();
                req.Settings = b.BatchRequestSettings;
                OrganizationRequestCollection reqstList = new OrganizationRequestCollection();

                // Make sure the batch is ordered. 
                reqstList.AddRange(b.BatchItems.Select(s => s.Request));

                req.Requests = reqstList;
                b.Status = BatchStatus.Running;
                ExecuteMultipleResponse resp = (ExecuteMultipleResponse)CdsCommand_Execute(req, "Execute Batch Command");
                // Need to add retry logic here to deal with a "server busy" status. 
                b.Status = BatchStatus.Complete;
                if (resp != null)
                {
                    if (resp.IsFaulted)
                        logEntry.Log("Batch request faulted.", TraceEventType.Warning);
                    b.BatchResults = resp;
                    return b.BatchResults;
                }
                logEntry.Log("Batch request faulted - No Results.", TraceEventType.Warning);
            }
            return null;
        }

        // Need methods here to work with the batch now, 
        // get items out by id, 
        // get batch request. 


        #endregion

        /// <summary>
        /// Uses the dynamic entity patter to create a new entity 
        /// </summary>
        /// <param name="entityName">Name of Entity To create</param>
        /// <param name="valueArray">Initial Values</param>
        /// <param name="applyToSolution">Optional: Applies the update with a solution by Unique name</param>
        /// <param name="enabledDuplicateDetection">Optional: if true, enabled CDS onboard duplicate detection</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>Guid on Success, Guid.Empty on fail</returns>
        public Guid CreateNewRecord(string entityName, Dictionary<string, CdsDataTypeWrapper> valueArray, string applyToSolution = "", bool enabledDuplicateDetection = false, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 

            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrEmpty(entityName))
                return Guid.Empty;

            if ((valueArray == null) || (valueArray.Count == 0))
                return Guid.Empty;


            // Create the New Entity Type. 
            Entity NewEnt = new Entity();
            NewEnt.LogicalName = entityName;

            AttributeCollection propList = new AttributeCollection();
            foreach (KeyValuePair<string, CdsDataTypeWrapper> i in valueArray)
            {
                AddValueToPropertyList(i, propList);
            }

            NewEnt.Attributes.AddRange(propList);

            CreateRequest createReq = new CreateRequest();
            createReq.Target = NewEnt;
            createReq.Parameters.Add("SuppressDuplicateDetection", !enabledDuplicateDetection);
            if (!string.IsNullOrWhiteSpace(applyToSolution))
                createReq.Parameters.Add("SolutionUniqueName", applyToSolution);

            CreateResponse createResp = null;

            if (AddRequestToBatch(batchId, createReq, entityName, string.Format(CultureInfo.InvariantCulture, "Request for Create on {0} queued", entityName)))
                return Guid.Empty;
            
            createResp = (CreateResponse)ExecuteCdsOrganizationRequest(createReq, entityName, useWebAPI: true);
            if (createResp != null)
            {
                return createResp.id;
            }
            else
                return Guid.Empty;

        }

        /// <summary>
        /// Generic update entity 
        /// </summary>
        /// <param name="entityName">String version of the entity name</param>
        /// <param name="keyFieldName">Key fieldname of the entity </param>
        /// <param name="id">Guid ID of the entity to update</param>
        /// <param name="fieldList">Fields to update</param>
        /// <param name="applyToSolution">Optional: Applies the update with a solution by Unique name</param>
        /// <param name="enabledDuplicateDetection">Optional: if true, enabled CDS onboard duplicate detection</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>true on success, false on fail</returns>
        public bool UpdateEntity(string entityName, string keyFieldName, Guid id, Dictionary<string, CdsDataTypeWrapper> fieldList, string applyToSolution = "", bool enabledDuplicateDetection = false, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null || id == Guid.Empty)
            {
                return false;
            }

            if (fieldList == null || fieldList.Count == 0)
                return false;

            Entity uEnt = new Entity();
            uEnt.LogicalName = entityName;


            AttributeCollection PropertyList = new AttributeCollection();

            #region MapCode
            foreach (KeyValuePair<string, CdsDataTypeWrapper> field in fieldList)
            {
                AddValueToPropertyList(field, PropertyList);
            }

            // Add the key... 
            // check to see if the key is in the import set already 
            if (!fieldList.ContainsKey(keyFieldName))
                PropertyList.Add(new KeyValuePair<string, object>(keyFieldName, id));

            #endregion

            uEnt.Attributes.AddRange(PropertyList.ToArray());
            uEnt.Id = id; 

            UpdateRequest req = new UpdateRequest();
            req.Target = uEnt;

            req.Parameters.Add("SuppressDuplicateDetection", !enabledDuplicateDetection);
            if (!string.IsNullOrWhiteSpace(applyToSolution))
                req.Parameters.Add("SolutionUniqueName", applyToSolution);


            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Updating {0} : {1}", entityName, id.ToString()), string.Format(CultureInfo.InvariantCulture, "Request for update on {0} queued", entityName)))
                return false;

            UpdateResponse resp = (UpdateResponse)ExecuteCdsOrganizationRequest(req, string.Format(CultureInfo.InvariantCulture, "Updating {0} : {1}", entityName, id.ToString()), useWebAPI: true);
            if (resp == null)
                return false;
            else
                return true;
        }


        /// <summary>
        /// Updates the State and Status of the Entity passed in. 
        /// </summary>
        /// <param name="entName">Name of the entity</param>
        /// <param name="id">Guid ID of the entity you are updating</param>
        /// <param name="stateCode">String version of the new state</param>
        /// <param name="statusCode">String Version of the new status</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
        /// <returns>true on success. </returns>
        public bool UpdateStateAndStatusForEntity(string entName, Guid id, string stateCode, string statusCode, Guid batchId = default(Guid))
        {
            return UpdateStateStatusForEntity(entName, id, stateCode, statusCode, batchId: batchId);
        }

        /// <summary>
        /// Updates the State and Status of the Entity passed in. 
        /// </summary>
        /// <param name="entName">Name of the entity</param>
        /// <param name="id">Guid ID of the entity you are updating</param>
        /// <param name="stateCode">Int version of the new state</param>
        /// <param name="statusCode">Int Version of the new status</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
        /// <returns>true on success. </returns>
        public bool UpdateStateAndStatusForEntity(string entName, Guid id, int stateCode, int statusCode, Guid batchId = default(Guid))
        {
            return UpdateStateStatusForEntity(entName, id, string.Empty, string.Empty, stateCode, statusCode, batchId);
        }


        /// <summary>
        /// Deletes an entity from the CDS
        /// </summary>
        /// <param name="entityType">entity type name</param>
        /// <param name="entityId">entity id</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
        /// <returns>true on success, false on failure</returns>
        public bool DeleteEntity(string entityType, Guid entityId, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return false;
            }

            DeleteRequest req = new DeleteRequest();
            req.Target = new EntityReference(entityType, entityId);

            if (batchId != Guid.Empty)
            {
                if (IsBatchOperationsAvailable)
                {
                    if (_BatchManager.AddNewRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId)))
                    {
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Request for Delete on {0} queued", entityType), TraceEventType.Verbose);
                        return false;
                    }
                    else
                        logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though. 
                    logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }

            if (batchId != Guid.Empty)
            {
                if (IsBatchOperationsAvailable)
                {
                    if (_BatchManager.AddNewRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Delete Entity = {0}, ID = {1}  queued", entityType, entityId)))
                    {
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Request for Delete. Entity = {0}, ID = {1}  queued", entityType, entityId), TraceEventType.Verbose);
                        return false;
                    }
                    else
                        logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though. 
                    logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId), string.Format(CultureInfo.InvariantCulture, "Request to Delete. Entity = {0}, ID = {1} Queued", entityType, entityId)))
                return false;

            DeleteResponse resp = (DeleteResponse)ExecuteCdsOrganizationRequest(req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId), useWebAPI: true);
            if (resp != null)
            {
                // Clean out the cache if the account happens to be stored in there. 
                if ((_CachObject != null) && (_CachObject.ContainsKey(entityType)))
                {
                    while (_CachObject[entityType].ContainsValue(entityId))
                    {
                        foreach (KeyValuePair<string, Guid> v in _CachObject[entityType].Values)
                        {
                            if (v.Value == entityId)
                            {
                                _CachObject[entityType].Remove(v.Key);
                                break;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a list of accounts based on the search parameters. 
        /// </summary>
        /// <param name="entityName">CDS Entity Type Name to search</param>
        /// <param name="searchParameters">Array of Search Parameters</param>
        /// <param name="fieldList">List of fields to retrieve, Null indicates all Fields</param>
        /// <param name="searchOperator">Logical Search Operator</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>List of matching Entity Types. </returns>

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member")]
        public Dictionary<string, Dictionary<string, object>> GetEntityDataBySearchParams(string entityName,
            Dictionary<string, string> searchParameters,
            LogicalSearchOperator searchOperator,
            List<string> fieldList,
            Guid batchId = default(Guid))
        {
            List<CdsSearchFilter> searchList = new List<CdsSearchFilter>();
            BuildSearchFilterListFromSearchTerms(searchParameters, searchList);

            string pgCookie = string.Empty;
            bool moreRec = false;
            return GetEntityDataBySearchParams(entityName, searchList, searchOperator, fieldList, null, -1, -1, string.Empty, out pgCookie, out moreRec, batchId);
        }


        /// <summary>
        /// Gets a list of accounts based on the search parameters. 
        /// </summary>
        /// <param name="entityName">CDS Entity Type Name to search</param>
        /// <param name="searchParameters">Array of Search Parameters</param>
        /// <param name="fieldList">List of fields to retrieve, Null indicates all Fields</param>
        /// <param name="searchOperator">Logical Search Operator</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>List of matching Entity Types. </returns>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member")]
        public Dictionary<string, Dictionary<string, object>> GetEntityDataBySearchParams(string entityName,
            List<CdsSearchFilter> searchParameters,
            LogicalSearchOperator searchOperator,
            List<string> fieldList, Guid batchId = default(Guid))
        {
            string pgCookie = string.Empty;
            bool moreRec = false;
            return GetEntityDataBySearchParams(entityName, searchParameters, searchOperator, fieldList, null, -1, -1, string.Empty, out pgCookie, out moreRec, batchId);
        }

        /// <summary>
        /// Searches for data from an entity based on the search parameters. 
        /// </summary>
        /// <param name="entityName">Name of the entity to search </param>
        /// <param name="searchParameters">Array of Search Parameters</param>
        /// <param name="fieldList">List of fields to retrieve, Null indicates all Fields</param>
        /// <param name="searchOperator">Logical Search Operator</param>
        /// <param name="pageCount">Number records per Page</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <param name="sortParameters">Sort order</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>List of matching Entity Types. </returns>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member")]
        public Dictionary<string, Dictionary<string, object>> GetEntityDataBySearchParams(string entityName,
            List<CdsSearchFilter> searchParameters,
            LogicalSearchOperator searchOperator,
            List<string> fieldList,
            Dictionary<string, LogicalSortOrder> sortParameters,
            int pageCount,
            int pageNumber,
            string pageCookie,
            out string outPageCookie,
            out bool isMoreRecords,
            Guid batchId = default(Guid)
            )
        {
            logEntry.ResetLastError();  // Reset Last Error 

            outPageCookie = string.Empty;
            isMoreRecords = false;

            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (searchParameters == null)
                searchParameters = new List<CdsSearchFilter>();

            // Build the query here. 
            QueryExpression query = BuildQueryFilter(entityName, searchParameters, fieldList, searchOperator);

            if (pageCount != -1)
            {
                PagingInfo pgInfo = new PagingInfo();
                pgInfo.Count = pageCount;
                pgInfo.PageNumber = pageNumber;
                pgInfo.PagingCookie = pageCookie;
                query.PageInfo = pgInfo;
            }

            if (sortParameters != null)
                if (sortParameters.Count > 0)
                {
                    List<OrderExpression> qExpressList = new List<OrderExpression>();
                    foreach (KeyValuePair<string, LogicalSortOrder> itm in sortParameters)
                    {
                        OrderExpression ordBy = new OrderExpression();
                        ordBy.AttributeName = itm.Key;
                        if (itm.Value == LogicalSortOrder.Ascending)
                            ordBy.OrderType = OrderType.Ascending;
                        else
                            ordBy.OrderType = OrderType.Descending;

                        qExpressList.Add(ordBy);
                    }

                    query.Orders.AddRange(qExpressList.ToArray());
                }


            RetrieveMultipleRequest retrieve = new RetrieveMultipleRequest();
            //retrieve.ReturnDynamicEntities = true;
            retrieve.Query = query;


            if (AddRequestToBatch(batchId, retrieve, "Running GetEntityDataBySearchParms", "Request For GetEntityDataBySearchParms Queued"))
                return null;


            RetrieveMultipleResponse retrieved;
            retrieved = (RetrieveMultipleResponse)CdsCommand_Execute(retrieve, "GetEntityDataBySearchParms");
            if (retrieved != null)
            {
                outPageCookie = retrieved.EntityCollection.PagingCookie;
                isMoreRecords = retrieved.EntityCollection.MoreRecords;

                return CreateResultDataSet(retrieved.EntityCollection);
            }
            else
                return null;
        }


        /// <summary>
        /// Searches for data based on a FetchXML query
        /// </summary>
        /// <param name="fetchXml">Fetch XML query data.</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>results or null</returns>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByFetchSearch(string fetchXml, Guid batchId = default(Guid))
        {
            EntityCollection ec = GetEntityDataByFetchSearchEC(fetchXml, batchId);
            if (ec != null)
                return CreateResultDataSet(ec);
            else
                return null;
        }


        /// <summary>
        /// Searches for data based on a FetchXML query
        /// </summary>
        /// <param name="fetchXml">Fetch XML query data.</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>results as an entity collection or null</returns>
        public EntityCollection GetEntityDataByFetchSearchEC(string fetchXml, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 

            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (string.IsNullOrWhiteSpace(fetchXml))
                return null;

            // This model directly requests the via FetchXML 
            RetrieveMultipleRequest req = new RetrieveMultipleRequest() { Query = new FetchExpression(fetchXml) };
            RetrieveMultipleResponse retrieved;

            if (AddRequestToBatch(batchId, req, "Running GetEntityDataByFetchSearchEC", "Request For GetEntityDataByFetchSearchEC Queued"))
                return null;

            retrieved = (RetrieveMultipleResponse)CdsCommand_Execute(req, "GetEntityDataByFetchSearch - Direct");
            if (retrieved != null)
            {
                return retrieved.EntityCollection;
            }
            else
                return null;
        }

        /// <summary>
        /// Searches for data based on a FetchXML query
        /// </summary>
        /// <param name="fetchXml">Fetch XML query data.</param>
        /// <param name="pageCount">Number records per Page</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>results or null</returns>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByFetchSearch(
                string fetchXml,
                int pageCount,
                int pageNumber,
                string pageCookie,
                out string outPageCookie,
                out bool isMoreRecords,
                Guid batchId = default(Guid))
        {
            EntityCollection ec = GetEntityDataByFetchSearchEC(fetchXml, pageCount, pageNumber, pageCookie, out outPageCookie, out isMoreRecords);
            if (ec != null)
                return CreateResultDataSet(ec);
            else
                return null;
        }

        /// <summary>
        /// Searches for data based on a FetchXML query
        /// </summary>
        /// <param name="fetchXml">Fetch XML query data.</param>
        /// <param name="pageCount">Number records per Page</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <returns>results as an Entity Collection or null</returns>
        public EntityCollection GetEntityDataByFetchSearchEC(
            string fetchXml,
            int pageCount,
            int pageNumber,
            string pageCookie,
            out string outPageCookie,
            out bool isMoreRecords,
            Guid batchId = default(Guid))
        {

            logEntry.ResetLastError();  // Reset Last Error 

            outPageCookie = string.Empty;
            isMoreRecords = false;

            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (string.IsNullOrWhiteSpace(fetchXml))
                return null;

            if (pageCount != -1)
            {
                // Add paging related parameter to fetch xml.
                fetchXml = AddPagingParametersToFetchXml(fetchXml, pageCount, pageNumber, pageCookie);
            }

            RetrieveMultipleRequest retrieve = new RetrieveMultipleRequest() { Query = new FetchExpression(fetchXml) };
            RetrieveMultipleResponse retrieved;

            if (AddRequestToBatch(batchId, retrieve, "Running GetEntityDataByFetchSearchEC", "Request For GetEntityDataByFetchSearchEC Queued"))
                return null;

            retrieved = (RetrieveMultipleResponse)CdsCommand_Execute(retrieve, "GetEntityDataByFetchSearch");
            if (retrieved != null)
            {
                outPageCookie = retrieved.EntityCollection.PagingCookie;
                isMoreRecords = retrieved.EntityCollection.MoreRecords;
                return retrieved.EntityCollection;
            }

            return null;
        }


        /// <summary>
        /// Queries an Object via a M to M Link 
        /// </summary>
        /// <param name="returnEntityName">Name of the entity you want return data from</param>
        /// <param name="primarySearchParameters">Search Prams for the Return Entity</param>
        /// <param name="linkedEntityName">Name of the entity you are linking too</param>
        /// <param name="linkedSearchParameters">Search Prams for the Entity you are linking too</param>
        /// <param name="linkedEntityLinkAttribName">Key field on the Entity you are linking too</param>
        /// <param name="m2MEntityName">CDS Name of the Relationship </param>
        /// <param name="returnEntityPrimaryId">Key field on the Entity you want to return data from</param>
        /// <param name="searchOperator">Search Operator to apply</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="fieldList">List of Fields from the Returned Entity you want</param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByLinkedSearch(
                string returnEntityName,
                Dictionary<string, string> primarySearchParameters,
                string linkedEntityName,
                Dictionary<string, string> linkedSearchParameters,
                string linkedEntityLinkAttribName,
                string m2MEntityName,
                string returnEntityPrimaryId,
                LogicalSearchOperator searchOperator,
                List<string> fieldList,
            Guid batchId = default(Guid))
        {
            List<CdsSearchFilter> primarySearchList = new List<CdsSearchFilter>();
            BuildSearchFilterListFromSearchTerms(primarySearchParameters, primarySearchList);

            List<CdsSearchFilter> linkedSearchList = new List<CdsSearchFilter>();
            BuildSearchFilterListFromSearchTerms(linkedSearchParameters, linkedSearchList);

            return GetEntityDataByLinkedSearch(returnEntityName, primarySearchList, linkedEntityName, linkedSearchList, linkedEntityLinkAttribName,
                        m2MEntityName, returnEntityPrimaryId, searchOperator, fieldList);

        }

        /// <summary>
        /// Queries an Object via a M to M Link 
        /// </summary>
        /// <param name="returnEntityName">Name of the entity you want return data from</param>
        /// <param name="primarySearchParameters">Search Prams for the Return Entity</param>
        /// <param name="linkedEntityName">Name of the entity you are linking too</param>
        /// <param name="linkedSearchParameters">Search Prams for the Entity you are linking too</param>
        /// <param name="linkedEntityLinkAttribName">Key field on the Entity you are linking too</param>
        /// <param name="m2MEntityName">CDS Name of the Relationship </param>
        /// <param name="returnEntityPrimaryId">Key field on the Entity you want to return data from</param>
        /// <param name="searchOperator">Search Operator to apply</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="fieldList">List of Fields from the Returned Entity you want</param>
        /// <param name="isReflexiveRelationship">If the relationship is defined as Entity:Entity or Account N:N Account, this parameter should be set to true</param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByLinkedSearch(
            string returnEntityName,
            List<CdsSearchFilter> /*Dictionary<string, string>*/ primarySearchParameters,
            string linkedEntityName,
            List<CdsSearchFilter> /*Dictionary<string, string>*/ linkedSearchParameters,
            string linkedEntityLinkAttribName,
            string m2MEntityName,
            string returnEntityPrimaryId,
            LogicalSearchOperator searchOperator,
            List<string> fieldList,
            Guid batchId = default(Guid),
            bool isReflexiveRelationship = false)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (primarySearchParameters == null && linkedSearchParameters == null)
                return null;

            if (primarySearchParameters == null)
                primarySearchParameters = new List<CdsSearchFilter>(); // new Dictionary<string, string>();

            if (linkedSearchParameters == null)
                linkedSearchParameters = new List<CdsSearchFilter>(); //new Dictionary<string, string>();



            #region Primary QueryFilter and Conditions

            FilterExpression primaryFilter = new FilterExpression();
            primaryFilter.Filters.AddRange(BuildFilterList(primarySearchParameters));

            #endregion

            #region Secondary QueryFilter and conditions

            FilterExpression linkedEntityFilter = new FilterExpression();
            linkedEntityFilter.Filters.AddRange(BuildFilterList(linkedSearchParameters));

            #endregion

            // Create Link Object for LinkedEnitty Name and add the filter info
            LinkEntity nestedLinkEntity = new LinkEntity();  // this is the Secondary 
            nestedLinkEntity.LinkToEntityName = linkedEntityName; // what Entity are we linking too... 
            nestedLinkEntity.LinkToAttributeName = linkedEntityLinkAttribName; // what Attrib are we linking To on that Entity
            nestedLinkEntity.LinkFromAttributeName = isReflexiveRelationship ? string.Format("{0}two", linkedEntityLinkAttribName) : linkedEntityLinkAttribName;  // what Attrib on the primary object are we linking too. 
            nestedLinkEntity.LinkCriteria = linkedEntityFilter; // Filtered query 

            //Create Link Object for Primary 
            LinkEntity m2mLinkEntity = new LinkEntity();
            m2mLinkEntity.LinkToEntityName = m2MEntityName; // this is the M2M table
            m2mLinkEntity.LinkToAttributeName = isReflexiveRelationship ? string.Format("{0}one", returnEntityPrimaryId) : returnEntityPrimaryId; // this is the name of the other side. 
            m2mLinkEntity.LinkFromAttributeName = returnEntityPrimaryId;
            m2mLinkEntity.LinkEntities.AddRange(new LinkEntity[] { nestedLinkEntity });


            // Return Cols
            // Create ColumnSet
            ColumnSet cols = null;
            if (fieldList != null && fieldList.Count > 0)
            {
                cols = new ColumnSet();
                cols.Columns.AddRange(fieldList.ToArray());
            }

            // Build Query 
            QueryExpression query = new QueryExpression();
            query.NoLock = false;  // Added to remove the Locks. 

            query.EntityName = returnEntityName; // Set to the requested entity Type 
            if (cols != null)
                query.ColumnSet = cols;
            else
                query.ColumnSet = new ColumnSet(true);// new AllColumns();

            query.Criteria = primaryFilter;
            query.LinkEntities.AddRange(new LinkEntity[] { m2mLinkEntity });

            //Dictionary<string, Dictionary<string, object>> Results = new Dictionary<string, Dictionary<string, object>>();


            RetrieveMultipleRequest req = new RetrieveMultipleRequest();
            req.Query = query;
            RetrieveMultipleResponse retrieved;


            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Running Get Linked data, returning {0}", returnEntityName), string.Format(CultureInfo.InvariantCulture, "Request for Get Linked data, returning {0}", returnEntityName)))
                return null;

            retrieved = (RetrieveMultipleResponse)CdsCommand_Execute(req, "Search On Linked Data");

            if (retrieved != null)
            {

                return CreateResultDataSet(retrieved.EntityCollection);
            }
            else
                return null;

        }

        /// <summary>
        /// Gets a List of variables from the account based on the list of field specified in the Fields List
        /// </summary>
        /// <param name="searchEntity">The entity to be searched.</param>
        /// <param name="entityId">ID of Entity to query </param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="fieldList">Populated Array of Key value pairs with the Results of the Search</param>
        /// <returns></returns>
        public Dictionary<string, object> GetEntityDataById(string searchEntity, Guid entityId, List<string> fieldList, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null || entityId == Guid.Empty)
            {
                return null;
            }

            EntityReference re = new EntityReference(searchEntity, entityId);
            if (re == null)
                return null;

            RetrieveRequest req = new RetrieveRequest();

            // Create ColumnSet
            ColumnSet cols = null;
            if (fieldList != null)
            {
                cols = new ColumnSet();
                cols.Columns.AddRange(fieldList.ToArray());
            }

            if (cols != null)
                req.ColumnSet = cols;
            else
                req.ColumnSet = new ColumnSet(true);// new AllColumns();

            req.Target = re; //getEnt; 

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Trying to Read a Record. Entity = {0} , ID = {1}", searchEntity, entityId.ToString()),
                string.Format(CultureInfo.InvariantCulture, "Request to Read a Record. Entity = {0} , ID = {1} queued", searchEntity, entityId.ToString())))
                return null;

            RetrieveResponse resp = (RetrieveResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Trying to Read a Record. Entity = {0} , ID = {1}", searchEntity, entityId.ToString()));
            if (resp == null)
                return null;

            if (resp.Entity == null)
                return null;

            try
            {
                // Not really doing an update here... just turning it into something I can walk. 
                Dictionary<string, object> resultSet = new Dictionary<string, object>();
                AddDataToResultSet(ref resultSet, resp.Entity);
                return resultSet;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// This creates a annotation [note] entry, related to a an existing entity
        /// <para>Required Properties in the fieldList</para>
        /// <para>notetext (string) = Text of the note,  </para>
        /// <para>subject (string) = this is the title of the note</para>
        /// </summary>
        /// <param name="targetEntityTypeName">Target Entity TypeID</param>
        /// <param name="targetEntityId">Target Entity ID</param>
        /// <param name="fieldList">Fields to populate</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns></returns>
        public Guid CreateAnnotation(string targetEntityTypeName, Guid targetEntityId, Dictionary<string, CdsDataTypeWrapper> fieldList, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 

            if (string.IsNullOrEmpty(targetEntityTypeName))
                return Guid.Empty;

            if (targetEntityId == Guid.Empty)
                return Guid.Empty;

            if (fieldList == null)
                fieldList = new Dictionary<string, CdsDataTypeWrapper>();

            fieldList.Add("objecttypecode", new CdsDataTypeWrapper(targetEntityTypeName, CdsFieldType.String));
            fieldList.Add("objectid", new CdsDataTypeWrapper(targetEntityId, CdsFieldType.Lookup, targetEntityTypeName));
            fieldList.Add("ownerid", new CdsDataTypeWrapper(_SystemUser.UserId, CdsFieldType.Lookup, "systemuser"));

            return CreateNewRecord("annotation", fieldList, batchId: batchId);

        }

        /// <summary>
        /// Creates a new activity against the target entity type
        /// </summary>
        /// <param name="activityEntityTypeName">Type of Activity you would like to create</param>
        /// <param name="regardingEntityTypeName">Entity type of the Entity you want to associate with.</param>
        /// <param name="subject">Subject Line of the Activity</param>
        /// <param name="description">Description Text of the Activity </param>
        /// <param name="regardingId">ID of the Entity to associate the Activity too</param>
        /// <param name="creatingUserId">User ID that Created the Activity *Calling user must have necessary permissions to assign to another user</param>
        /// <param name="fieldList">Additional fields to add as part of the activity creation</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>Guid of Activity ID or Guid.empty</returns>
        public Guid CreateNewActivityEntry(string activityEntityTypeName,
            string regardingEntityTypeName,
            Guid regardingId,
            string subject,
            string description,
            string creatingUserId,
            Dictionary<string, CdsDataTypeWrapper> fieldList = null,
            Guid batchId = default(Guid)
            )
        {

            #region PreChecks
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }
            if (string.IsNullOrWhiteSpace(activityEntityTypeName))
            {
                logEntry.Log("You must specify the activity type name to create", TraceEventType.Error);
                return Guid.Empty;
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "A Subject is required to create an activity of type {0}", regardingEntityTypeName), TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            Guid activityId = Guid.Empty;
            try
            {
                // reuse the passed in field list if its available, else punt and create a new one. 
                if (fieldList == null)
                    fieldList = new Dictionary<string, CdsDataTypeWrapper>();

                fieldList.Add("subject", new CdsDataTypeWrapper(subject, CdsFieldType.String));
                if (regardingId != Guid.Empty)
                    fieldList.Add("regardingobjectid", new CdsDataTypeWrapper(regardingId, CdsFieldType.Lookup, regardingEntityTypeName));
                if (!string.IsNullOrWhiteSpace(description))
                    fieldList.Add("description", new CdsDataTypeWrapper(description, CdsFieldType.String));

                // Create the base record. 
                activityId = CreateNewRecord(activityEntityTypeName, fieldList);

                // if I have a user ID,  try to assign it to that user. 
                if (!string.IsNullOrWhiteSpace(creatingUserId))
                {
                    Guid userId = GetLookupValueForEntity("systemuser", creatingUserId);

                    if (userId != Guid.Empty)
                    {
                        EntityReference newAction = new EntityReference(activityEntityTypeName, activityId);
                        EntityReference principal = new EntityReference("systemuser", userId);

                        AssignRequest arRequest = new AssignRequest();
                        arRequest.Assignee = principal;
                        arRequest.Target = newAction;
                        if (AddRequestToBatch(batchId, arRequest, string.Format(CultureInfo.InvariantCulture, "Trying to Assign a Record. Entity = {0} , ID = {1}", newAction.LogicalName, principal.LogicalName),
                                                string.Format(CultureInfo.InvariantCulture, "Request to Assign a Record. Entity = {0} , ID = {1} Queued", newAction.LogicalName, principal.LogicalName)))
                            return Guid.Empty;
                        CdsCommand_Execute(arRequest, "Assign Activity");
                    }
                }
            }
            catch (Exception exp)
            {
                this.logEntry.Log(exp);
            }
            return activityId;
        }

        /// <summary>
        /// Closes the Activity type specified. 
        /// The Activity Entity type supports fax , letter , and phonecall 
        /// <para>*Note: This will default to using English names for Status. if you need to use Non-English, you should populate the names for completed for the status and state.</para>
        /// </summary>
        /// <param name="activityEntityType">Type of Activity you would like to close.. Supports fax, letter, phonecall</param>
        /// <param name="activityId">ID of the Activity you want to close</param>
        /// <param name="stateCode">State Code configured on the activity</param>
        /// <param name="statusCode">Status code on the activity </param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>true if success false if not.</returns>
        public bool CloseActivity(string activityEntityType, Guid activityId, string stateCode = "completed", string statusCode = "completed", Guid batchId = default(Guid))
        {
            return UpdateStateStatusForEntity(activityEntityType, activityId, stateCode, statusCode, batchId: batchId);
        }

        /// <summary>
        /// Updates the state of an activity
        /// </summary>
        /// <param name="entName"></param>
        /// <param name="entId"></param>
        /// <param name="newState"></param>
        /// <param name="newStatus"></param>
        /// <param name="newStateid">ID for the new State ( Skips metadata lookup )</param>
        /// <param name="newStatusid">ID for new Status ( Skips Metadata Lookup)</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns></returns>
        private bool UpdateStateStatusForEntity(string entName, Guid entId, string newState, string newStatus, int newStateid = -1, int newStatusid = -1, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            SetStateRequest req = new SetStateRequest();
            req.EntityMoniker = new EntityReference(entName, entId);

            int istatuscode = -1;
            int istatecode = -1;

            // Modified to prefer IntID's first... this is in support of multi languages. 

            if (newStatusid != -1)
                istatuscode = newStatusid;
            else
            {
                if (!String.IsNullOrWhiteSpace(newStatus))
                {
                    PickListMetaElement picItem = GetPickListElementFromMetadataEntity(entName, "statuscode");
                    if (picItem != null)
                    {
                        var statusOption = picItem.Items.FirstOrDefault(s => s.DisplayLabel.Equals(newStatus, StringComparison.CurrentCultureIgnoreCase));
                        if (statusOption != null)
                            istatuscode = statusOption.PickListItemId;
                    }
                }
            }

            if (newStateid != -1)
                istatecode = newStateid;
            else
            {
                if (!string.IsNullOrWhiteSpace(newState))
                {
                    PickListMetaElement picItem2 = GetPickListElementFromMetadataEntity(entName, "statecode");
                    var stateOption = picItem2.Items.FirstOrDefault(s => s.DisplayLabel.Equals(newState, StringComparison.CurrentCultureIgnoreCase));
                    if (stateOption != null)
                        istatecode = stateOption.PickListItemId;
                }
            }

            if (istatecode == -1 && istatuscode == -1)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Cannot set status on {0}, State and Status codes not found, State = {1}, Status = {2}", entName, newState, newStatus), TraceEventType.Information);
                return false;
            }

            if (istatecode != -1)
                req.State = new OptionSetValue(istatecode);// "Completed";
            if (istatuscode != -1)
                req.Status = new OptionSetValue(istatuscode); //Status = 2;


            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Setting Activity State in CDS... {0}", entName), string.Format(CultureInfo.InvariantCulture, "Request for SetState on {0} queued", entName)))
                return false;

            SetStateResponse resp = (SetStateResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Setting Activity State in CDS... {0}", entName));
            if (resp != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Returns all Activities Related to a given Entity ID. 
        /// Only Account, Contact and Opportunity entities are supported.
        /// </summary>
        /// <param name="searchEntity">Type of Entity to search against</param>
        /// <param name="entityId">ID of the entity to search against. </param>
        /// <param name="fieldList">List of Field to return for the entity , null indicates all fields.</param>
        /// <param name="searchOperator">Search Operator to use</param>
        /// <param name="searchParameters">Filters responses based on search prams.</param>
        /// <param name="sortParameters">Sort order</param>
        /// <param name="pageCount">Number of Pages</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>Array of Activities</returns>
        public Dictionary<string, Dictionary<string, object>> GetActivitiesBy(
            string searchEntity,
            Guid entityId,
            List<string> fieldList,
            LogicalSearchOperator searchOperator,
            Dictionary<string, string> searchParameters,
            Dictionary<string, LogicalSortOrder> sortParameters,
            int pageCount,
            int pageNumber,
            string pageCookie,
            out string outPageCookie,
            out bool isMoreRecords,
            Guid batchId = default(Guid)
            )
        {
            List<CdsSearchFilter> searchList = new List<CdsSearchFilter>();
            BuildSearchFilterListFromSearchTerms(searchParameters, searchList);

            return GetEntityDataByRollup(searchEntity, entityId, "activitypointer", fieldList, searchOperator, searchList, sortParameters, pageCount, pageNumber, pageCookie, out outPageCookie, out isMoreRecords, batchId);
        }

        /// <summary>
        /// Returns all Activities Related to a given Entity ID. 
        /// Only Account, Contact and Opportunity entities are supported.
        /// </summary>
        /// <param name="searchEntity">Type of Entity to search against</param>
        /// <param name="entityId">ID of the entity to search against. </param>
        /// <param name="fieldList">List of Field to return for the entity , null indicates all fields.</param>
        /// <param name="searchOperator">Search Operator to use</param>
        /// <param name="searchParameters">Filters responses based on search prams.</param>
        /// <param name="sortParameters">Sort order</param>
        /// <param name="pageCount">Number of Pages</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>Array of Activities</returns>
        public Dictionary<string, Dictionary<string, object>> GetActivitiesBy(
           string searchEntity,
           Guid entityId,
           List<string> fieldList,
           LogicalSearchOperator searchOperator,
           List<CdsSearchFilter> searchParameters,
           Dictionary<string, LogicalSortOrder> sortParameters,
           int pageCount,
           int pageNumber,
           string pageCookie,
           out string outPageCookie,
           out bool isMoreRecords,
            Guid batchId = default(Guid)
           )
        {
            return GetEntityDataByRollup(searchEntity, entityId, "activitypointer", fieldList, searchOperator, searchParameters, sortParameters, pageCount, pageNumber, pageCookie, out outPageCookie, out isMoreRecords, batchId: batchId);
        }

        /// <summary>
        /// Returns all Activities Related to a given Entity ID. 
        /// Only Account, Contact and Opportunity entities are supported.
        /// </summary>
        /// <param name="searchEntity">Type of Entity to search against</param>
        /// <param name="entityId">ID of the entity to search against. </param>
        /// <param name="fieldList">List of Field to return for the entity , null indicates all fields.</param>
        /// <param name="searchOperator"></param>
        /// <param name="searchParameters">Filters responses based on search prams.</param>
        /// <returns>Array of Activities</returns>
        /// <param name="sortParameters">Sort Order</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="rollupfromEntity">Entity to Rollup from</param>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByRollup(
            string searchEntity,
            Guid entityId,
            string rollupfromEntity,
            List<string> fieldList,
            LogicalSearchOperator searchOperator,
            Dictionary<string, string> searchParameters,
            Dictionary<string, LogicalSortOrder> sortParameters,
            Guid batchId = default(Guid))
        {

            List<CdsSearchFilter> searchList = new List<CdsSearchFilter>();
            BuildSearchFilterListFromSearchTerms(searchParameters, searchList);


            string pgCookie = string.Empty;
            bool moreRec = false;

            return GetEntityDataByRollup(
                searchEntity, entityId, rollupfromEntity, fieldList,
                searchOperator, searchList, sortParameters, -1, -1, string.Empty,
                out pgCookie, out moreRec, batchId: batchId);
        }


        /// <summary>
        /// Returns all Activities Related to a given Entity ID. 
        /// Only Account, Contact and Opportunity entities are supported.
        /// </summary>
        /// <param name="searchEntity">Type of Entity to search against</param>
        /// <param name="entityId">ID of the entity to search against. </param>
        /// <param name="fieldList">List of Field to return for the entity , null indicates all fields.</param>
        /// <param name="rollupfromEntity">Entity to Rollup from</param>
        /// <param name="searchOperator">Search Operator to user</param>
        /// <param name="searchParameters">CDS Filter list to apply</param>
        /// <param name="sortParameters">Sort by</param>
        /// <param name="pageCount">Number of Pages</param>
        /// <param name="pageNumber">Current Page number</param>
        /// <param name="pageCookie">inbound place holder cookie</param>
        /// <param name="outPageCookie">outbound place holder cookie</param>
        /// <param name="isMoreRecords">is there more records or not</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, object>> GetEntityDataByRollup(
            string searchEntity,
            Guid entityId,
            string rollupfromEntity,
            List<string> fieldList,
            LogicalSearchOperator searchOperator,
            List<CdsSearchFilter> searchParameters,
            Dictionary<string, LogicalSortOrder> sortParameters,
            int pageCount,
            int pageNumber,
            string pageCookie,
            out string outPageCookie,
            out bool isMoreRecords,
            Guid batchId = default(Guid)
            )
        {
            logEntry.ResetLastError();  // Reset Last Error 
            outPageCookie = string.Empty;
            isMoreRecords = false;

            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            QueryExpression query = BuildQueryFilter(rollupfromEntity, searchParameters, fieldList, searchOperator);

            if (pageCount != -1)
            {
                PagingInfo pgInfo = new PagingInfo();
                pgInfo.Count = pageCount;
                pgInfo.PageNumber = pageNumber;
                pgInfo.PagingCookie = pageCookie;
                query.PageInfo = pgInfo;

            }

            if (sortParameters != null)
                if (sortParameters.Count > 0)
                {
                    List<OrderExpression> qExpressList = new List<OrderExpression>();
                    foreach (KeyValuePair<string, LogicalSortOrder> itm in sortParameters)
                    {
                        OrderExpression ordBy = new OrderExpression();
                        ordBy.AttributeName = itm.Key;
                        if (itm.Value == LogicalSortOrder.Ascending)
                            ordBy.OrderType = OrderType.Ascending;
                        else
                            ordBy.OrderType = OrderType.Descending;

                        qExpressList.Add(ordBy);
                    }

                    query.Orders.AddRange(qExpressList.ToArray());
                }

            if (query.Orders == null)
            {
                OrderExpression ordBy = new OrderExpression();
                ordBy.AttributeName = "createdon";
                ordBy.OrderType = OrderType.Descending;
                query.Orders.AddRange(new OrderExpression[] { ordBy });
            }

            EntityReference ro = new EntityReference(searchEntity, entityId);
            if (ro == null)
                return null;

            RollupRequest req = new RollupRequest();
            req.Query = query;
            req.RollupType = RollupType.Related;
            req.Target = ro;

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Running Get entitydatabyrollup... {0}", searchEntity), string.Format(CultureInfo.InvariantCulture, "Request for GetEntityDataByRollup on {0} queued", searchEntity)))
                return null;

            RollupResponse resp = (RollupResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Locating {0} by ID in CDS GetActivitesBy", searchEntity));
            if (resp == null)
                return null;

            if ((resp.EntityCollection != null) ||
                (resp.EntityCollection.Entities != null) ||
                (resp.EntityCollection.Entities.Count > 0)
                )
            {
                isMoreRecords = resp.EntityCollection.MoreRecords;
                outPageCookie = resp.EntityCollection.PagingCookie;
                return CreateResultDataSet(resp.EntityCollection);
            }
            else
                return null;
        }

        /// <summary>
        /// This function gets data from a Dictionary object, where "string" identifies the field name, and Object contains the data,
        /// this method then attempts to cast the result to the Type requested, if it cannot be cast an empty object is returned.
        /// </summary>
        /// <param name="results">Results from the query</param>
        /// <param name="key">key name you want</param>
        /// <typeparam name="T">Type if object to return</typeparam>
        /// <returns>object</returns>
        public T GetDataByKeyFromResultsSet<T>(Dictionary<string, object> results, string key)
        {
            try
            {
                if (results != null)
                {
                    if (results.ContainsKey(key))
                    {

                        if ((typeof(T) == typeof(int)) || (typeof(T) == typeof(string)))
                        {
                            try
                            {
                                string s = (string)results[key];
                                if (s.Contains("PICKLIST:"))
                                {
                                    try
                                    {
                                        //parse the PickList bit for what is asked for
                                        Collection<string> eleList = new Collection<string>(s.Split(':'));
                                        if (typeof(T) == typeof(int))
                                        {
                                            return (T)(object)Convert.ToInt32(eleList[1], CultureInfo.InvariantCulture);
                                        }
                                        else
                                            return (T)(object)eleList[3];
                                    }
                                    catch
                                    {
                                        // try to do the basic return 
                                        return (T)results[key];
                                    }
                                }
                            }
                            catch
                            {
                                if (results[key] is T)
                                    // try to do the basic return 
                                    return (T)results[key];
                            }
                        }

                        // MSB :: Added this method in light of new features in CDS 2011.. 
                        if (results[key] is T)
                            // try to do the basic return 
                            return (T)results[key];
                        else
                        {
                            if (results != null && results.ContainsKey(key))  // Specific To CDS 2011.. 
                            {
                                if (results.ContainsKey(key + "_Property"))
                                {
                                    // Check for the property entry - CDS 2011 Specific
                                    KeyValuePair<string, object> property = (KeyValuePair<string, object>)results[key + "_Property"];
                                    // try to return the casted value.
                                    if (property.Value is T)
                                        return (T)property.Value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logEntry.Log("Error In GetDataByKeyFromResultsSet (Non-Fatal)", TraceEventType.Verbose, ex);
            }
            return default(T);

        }

        /// <summary>
        /// Executes a named workflow on an object. 
        /// </summary>
        /// <param name="workflowName">name of the workflow to run</param>
        /// <param name="id">ID to exec against</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>Async Op ID of the WF or Guid.Empty</returns>
        public Guid ExecuteWorkflowOnEntity(string workflowName, Guid id, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (id == Guid.Empty)
            {
                this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception Executing workflow ({0}) on ID {1} in CDS  : " + "Target Entity Was not provided", workflowName, id), TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrEmpty(workflowName))
            {
                this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception Executing workflow ({0}) on ID {1} in CDS  : " + "Workflow Name Was not provided", workflowName, id), TraceEventType.Error);
                return Guid.Empty;
            }

            Dictionary<string, string> SearchParm = new Dictionary<string, string>();
            SearchParm.Add("name", workflowName);

            Dictionary<string, Dictionary<string, object>> rslts =
                    GetEntityDataBySearchParams("workflow", SearchParm, LogicalSearchOperator.None, null);

            if (rslts != null)
            {
                if (rslts.Count > 0)
                {
                    foreach (Dictionary<string, object> row in rslts.Values)
                    {
                        if (GetDataByKeyFromResultsSet<Guid>(row, "parentworkflowid") != Guid.Empty)
                            continue;
                        Guid guWorkflowID = GetDataByKeyFromResultsSet<Guid>(row, "workflowid");
                        if (guWorkflowID != Guid.Empty)
                        {
                            // Ok try to exec the workflow request
                            ExecuteWorkflowRequest wfRequest = new ExecuteWorkflowRequest();
                            wfRequest.EntityId = id;
                            wfRequest.WorkflowId = guWorkflowID;

                            if (AddRequestToBatch(batchId, wfRequest, string.Format(CultureInfo.InvariantCulture, "Executing workflow ({0}) on ID {1}", workflowName, id),
                                string.Format(CultureInfo.InvariantCulture, "Request to Execute workflow ({0}) on ID {1} Queued", workflowName, id)))
                                return Guid.Empty;

                            ExecuteWorkflowResponse wfResponse = (ExecuteWorkflowResponse)CdsCommand_Execute(wfRequest, string.Format(CultureInfo.InvariantCulture, "Executing workflow ({0}) on ID {1}", workflowName, id));
                            if (wfResponse != null)
                                return wfResponse.Id;
                            else
                                return Guid.Empty;
                        }
                        else
                        {
                            this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception Executing workflow ({0}) on ID {1} in CDS  : " + "Unable to Find Workflow by ID", workflowName, id), TraceEventType.Error);
                        }
                    }
                }
                else
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception Executing workflow ({0}) on ID {1} in CDS  : " + "Unable to Find Workflow by Name", workflowName, id), TraceEventType.Error);
                }
            }
            this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception Executing workflow ({0}) on ID {1} in CDS  : " + "Unable to Find Workflow by Name Search", workflowName, id), TraceEventType.Error);
            return Guid.Empty;
        }

        #region Solution and Data Import Methods
        /// <summary>
        /// Starts an Import request for CDS.
        /// <para>Supports a single file per Import request.</para>
        /// </summary>
        /// <param name="delayUntil">Delays the import jobs till specified time - Use DateTime.MinValue to Run immediately </param>
        /// <param name="importRequest">Import Data Request</param>
        /// <returns>Guid of the Import Request, or Guid.Empty.  If Guid.Empty then request failed.</returns>
        public Guid SubmitImportRequest(ImportRequest importRequest, DateTime delayUntil)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            // Error checking 
            if (importRequest == null)
            {
                this.logEntry.Log("************ Exception on SubmitImportRequest, importRequest is required", TraceEventType.Error);
                return Guid.Empty;
            }

            if (importRequest.Files == null || (importRequest.Files != null && importRequest.Files.Count == 0))
            {
                this.logEntry.Log("************ Exception on SubmitImportRequest, importRequest.Files is required and must have at least one file listed to import.", TraceEventType.Error);
                return Guid.Empty;
            }

            // Done error checking
            if (string.IsNullOrWhiteSpace(importRequest.ImportName))
                importRequest.ImportName = "User Requested Import";


            Guid ImportId = Guid.Empty;
            Guid ImportMap = Guid.Empty;
            Guid ImportFile = Guid.Empty;
            List<Guid> ImportFileIds = new List<Guid>();

            // Create Import Object 
            // The Import Object is the anchor for the Import job in CDS.
            Dictionary<string, CdsDataTypeWrapper> importFields = new Dictionary<string, CdsDataTypeWrapper>();
            importFields.Add("name", new CdsDataTypeWrapper(importRequest.ImportName, CdsFieldType.String));
            importFields.Add("modecode", new CdsDataTypeWrapper(importRequest.Mode, CdsFieldType.Picklist));  // 0 == Create , 1 = Update..
            ImportId = CreateNewRecord("import", importFields);

            if (ImportId == Guid.Empty)
                // Error here; 
                return Guid.Empty;

            #region Determin Map to Use
            //Guid guDataMapId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(importRequest.DataMapFileName) && importRequest.DataMapFileId == Guid.Empty)
                // User Requesting to use System Mapping here. 
                importRequest.UseSystemMap = true;  // Override whatever setting they had here. 
            else
            {
                // User providing information on a map to use. 
                // Query to get the map from the system 
                List<string> fldList = new List<string>();
                fldList.Add("name");
                fldList.Add("source");
                fldList.Add("importmapid");
                Dictionary<string, object> MapData = null;
                if (importRequest.DataMapFileId != Guid.Empty)
                {
                    // Have the id here... get the map based on the ID. 
                    MapData = GetEntityDataById("importmap", importRequest.DataMapFileId, fldList);
                }
                else
                {
                    // Search by name... exact match required. 
                    List<CdsSearchFilter> filters = new List<CdsSearchFilter>();
                    CdsSearchFilter filter = new CdsSearchFilter();
                    filter.FilterOperator = Microsoft.Xrm.Sdk.Query.LogicalOperator.And;
                    filter.SearchConditions.Add(new CdsFilterConditionItem() { FieldName = "name", FieldOperator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, FieldValue = importRequest.DataMapFileName });
                    filters.Add(filter);

                    // Search by Name..
                    Dictionary<string, Dictionary<string, object>> rslts = GetEntityDataBySearchParams("importmap", filters, LogicalSearchOperator.None, fldList);
                    if (rslts != null && rslts.Count > 0)
                    {
                        // if there is more then one record returned.. throw an error ( should not happen ) 
                        if (rslts.Count > 1)
                        {
                            // log error here.
                            this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on SubmitImportRequest, More then one mapping file was found for {0}, Specifiy the ID of the Mapfile to use", importRequest.DataMapFileName), TraceEventType.Error);
                            return Guid.Empty;
                        }
                        else
                        {
                            // Get my single record and move on..
                            MapData = rslts.First().Value;
                            // Update the Guid for the mapID.
                            importRequest.DataMapFileId = GetDataByKeyFromResultsSet<Guid>(MapData, "importmapid");
                        }
                    }
                }
                ImportMap = importRequest.DataMapFileId;


                // Now get the entity import mapping info,  We need this to get the source entity name from the map XML file.  
                if (ImportMap != Guid.Empty)
                {
                    // Iterate over the import files and update the entity names. 

                    fldList.Clear();
                    fldList.Add("sourceentityname");
                    List<CdsSearchFilter> filters = new List<CdsSearchFilter>();
                    CdsSearchFilter filter = new CdsSearchFilter();
                    filter.FilterOperator = Microsoft.Xrm.Sdk.Query.LogicalOperator.And;
                    filter.SearchConditions.Add(new CdsFilterConditionItem() { FieldName = "importmapid", FieldOperator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, FieldValue = ImportMap });
                    filters.Add(filter);
                    Dictionary<string, Dictionary<string, object>> al = GetEntityDataBySearchParams("importentitymapping", filters, LogicalSearchOperator.None, null);
                    if (al != null && al.Count > 0)
                    {
                        foreach (var row in al.Values)
                        {
                            importRequest.Files.ForEach(fi =>
                            {
                                if (fi.TargetEntityName.Equals(GetDataByKeyFromResultsSet<string>(row, "targetentityname"), StringComparison.OrdinalIgnoreCase))
                                    fi.SourceEntityName = GetDataByKeyFromResultsSet<string>(row, "sourceentityname");
                            });
                        }
                    }
                    else
                    {
                        if (ImportId != Guid.Empty)
                            DeleteEntity("import", ImportId);

                        // Failed to find mapping entry error , Map not imported properly
                        this.logEntry.Log("************ Exception on SubmitImportRequest, Cannot find mapping file information found MapFile Provided.", TraceEventType.Error);
                        return Guid.Empty;
                    }
                }
                else
                {
                    if (ImportId != Guid.Empty)
                        DeleteEntity("import", ImportId);

                    // Failed to find mapping entry error , Map not imported properly
                    this.logEntry.Log("************ Exception on SubmitImportRequest, Cannot find ImportMappingsFile Provided.", TraceEventType.Error);
                    return Guid.Empty;
                }

            }
            #endregion

            #region Create Import File for each File in array
            bool continueImport = true;
            Dictionary<string, CdsDataTypeWrapper> importFileFields = new Dictionary<string, CdsDataTypeWrapper>();
            foreach (var FileItem in importRequest.Files)
            {
                // Create the Import File Object - Loop though file objects and create as many as necessary. 
                // This is the row that has the data being imported as well as the status of the import file.
                importFileFields.Add("name", new CdsDataTypeWrapper(FileItem.FileName, CdsFieldType.String));
                importFileFields.Add("source", new CdsDataTypeWrapper(FileItem.FileName, CdsFieldType.String));
                importFileFields.Add("filetypecode", new CdsDataTypeWrapper(FileItem.FileType, CdsFieldType.Picklist)); // File Type is either : 0 = CSV , 1 = XML , 2 = Attachment 
                importFileFields.Add("content", new CdsDataTypeWrapper(FileItem.FileContentToImport, CdsFieldType.String));
                importFileFields.Add("enableduplicatedetection", new CdsDataTypeWrapper(FileItem.EnableDuplicateDetection, CdsFieldType.Boolean));
                importFileFields.Add("usesystemmap", new CdsDataTypeWrapper(importRequest.UseSystemMap, CdsFieldType.Boolean)); // Use the System Map to get somthing done. 
                importFileFields.Add("sourceentityname", new CdsDataTypeWrapper(FileItem.SourceEntityName, CdsFieldType.String));
                importFileFields.Add("targetentityname", new CdsDataTypeWrapper(FileItem.TargetEntityName, CdsFieldType.String));
                importFileFields.Add("datadelimitercode", new CdsDataTypeWrapper(FileItem.DataDelimiter, CdsFieldType.Picklist));   // 1 = " | 2 =   | 3 = ' 
                importFileFields.Add("fielddelimitercode", new CdsDataTypeWrapper(FileItem.FieldDelimiter, CdsFieldType.Picklist));  // 1 = : | 2 = , | 3 = ' 
                importFileFields.Add("isfirstrowheader", new CdsDataTypeWrapper(FileItem.IsFirstRowHeader, CdsFieldType.Boolean));
                importFileFields.Add("processcode", new CdsDataTypeWrapper(1, CdsFieldType.Picklist));
                if (FileItem.IsRecordOwnerATeam)
                    importFileFields.Add("recordsownerid", new CdsDataTypeWrapper(FileItem.RecordOwner, CdsFieldType.Lookup, "team"));
                else
                    importFileFields.Add("recordsownerid", new CdsDataTypeWrapper(FileItem.RecordOwner, CdsFieldType.Lookup, "systemuser"));

                importFileFields.Add("importid", new CdsDataTypeWrapper(ImportId, CdsFieldType.Lookup, "import"));
                if (ImportMap != Guid.Empty)
                    importFileFields.Add("importmapid", new CdsDataTypeWrapper(ImportMap, CdsFieldType.Lookup, "importmap"));

                ImportFile = CreateNewRecord("importfile", importFileFields);
                if (ImportFile == Guid.Empty)
                {
                    continueImport = false;
                    break;
                }
                ImportFileIds.Add(ImportFile);
                importFileFields.Clear();
            }

            #endregion


            // if We have an Import File... Activate Import. 
            if (continueImport)
            {
                ParseImportResponse parseResp = (ParseImportResponse)CdsCommand_Execute(new ParseImportRequest() { ImportId = ImportId },
                    string.Format(CultureInfo.InvariantCulture, "************ Exception Executing ParseImportRequest for ImportJob ({0})", importRequest.ImportName));
                if (parseResp.AsyncOperationId != Guid.Empty)
                {
                    if (delayUntil != DateTime.MinValue)
                    {
                        importFileFields.Clear();
                        importFileFields.Add("postponeuntil", new CdsDataTypeWrapper(delayUntil, CdsFieldType.DateTime));
                        UpdateEntity("asyncoperation", "asyncoperationid", parseResp.AsyncOperationId, importFileFields);
                    }

                    TransformImportResponse transformResp = (TransformImportResponse)CdsCommand_Execute(new TransformImportRequest() { ImportId = ImportId },
                        string.Format(CultureInfo.InvariantCulture, "************ Exception Executing TransformImportRequest for ImportJob ({0})", importRequest.ImportName));
                    if (transformResp != null)
                    {
                        if (delayUntil != DateTime.MinValue)
                        {
                            importFileFields.Clear();
                            importFileFields.Add("postponeuntil", new CdsDataTypeWrapper(delayUntil.AddSeconds(1), CdsFieldType.DateTime));
                            UpdateEntity("asyncoperation", "asyncoperationid", transformResp.AsyncOperationId, importFileFields);
                        }

                        ImportRecordsImportResponse importResp = (ImportRecordsImportResponse)CdsCommand_Execute(new ImportRecordsImportRequest() { ImportId = ImportId },
                            string.Format(CultureInfo.InvariantCulture, "************ Exception Executing ImportRecordsImportRequest for ImportJob ({0})", importRequest.ImportName));
                        if (importResp != null)
                        {
                            if (delayUntil != DateTime.MinValue)
                            {
                                importFileFields.Clear();
                                importFileFields.Add("postponeuntil", new CdsDataTypeWrapper(delayUntil.AddSeconds(2), CdsFieldType.DateTime));
                                UpdateEntity("asyncoperation", "asyncoperationid", importResp.AsyncOperationId, importFileFields);
                            }

                            return ImportId;
                        }
                    }
                }
            }
            else
            {
                // Error.. Clean up the other records. 
                string err = LastCdsError;
                Exception ex = LastCdsException;

                if (ImportFileIds.Count > 0)
                {
                    ImportFileIds.ForEach(i =>
                    {
                        DeleteEntity("importfile", i);
                    });
                    ImportFileIds.Clear();
                }

                if (ImportId != Guid.Empty)
                    DeleteEntity("import", ImportId);

                // This is done to allow the error to be available to the user after the class cleans things up. 
                if (ex != null)
                    logEntry.Log(err, TraceEventType.Error, ex);
                else
                    logEntry.Log(err, TraceEventType.Error);

                return Guid.Empty;
            }
            return ImportId;
        }

        /// <summary>
        /// Used to upload a data map to the CDS
        /// </summary>
        /// <param name="dataMapXml">XML of the datamap in string form</param>
        /// <param name="replaceIds">True to have CDS replace ID's on inbound data, False to have inbound data retain its ID's</param>
        /// <param name="dataMapXmlIsFilePath">if true, dataMapXml is expected to be a File name and path to load.</param>
        /// <returns>Returns ID of the datamap or Guid.Empty</returns>
        public Guid ImportDataMapToCds(string dataMapXml, bool replaceIds = true, bool dataMapXmlIsFilePath = false)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrWhiteSpace(dataMapXml))
            {
                this.logEntry.Log("************ Exception on ImportDataMapToCds, dataMapXml is required", TraceEventType.Error);
                return Guid.Empty;
            }

            if (dataMapXmlIsFilePath)
            {
                // try to load the file from the file system 
                if (File.Exists(dataMapXml))
                {
                    try
                    {
                        string sContent = "";
                        using (var a = File.OpenText(dataMapXml))
                        {
                            sContent = a.ReadToEnd();
                        }

                        dataMapXml = sContent;
                    }
                    #region Exception handlers for files
                    catch (UnauthorizedAccessException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, Unauthorized Access to file: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (ArgumentNullException ex)
                    {
                        this.logEntry.Log("************ Exception on LoadDataMapToCds, File path not specified", TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (ArgumentException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, File path is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (PathTooLongException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, File path is too long. Paths must be less than 248 characters, and file names must be less than 260 characters\n{0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, File path is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (FileNotFoundException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, File Not Found: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (NotSupportedException ex)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadDataMapToCds, File path or name is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    #endregion
                }
                else
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on LoadImportDataMapToCds, File path specified in dataMapXml is not found: {0}", dataMapXml), TraceEventType.Error);
                    return Guid.Empty;
                }

            }

            ImportMappingsImportMapResponse resp = (ImportMappingsImportMapResponse)CdsCommand_Execute(new ImportMappingsImportMapRequest() { MappingsXml = dataMapXml, ReplaceIds = replaceIds },
                "************ Exception Executing ImportMappingsImportMapResponse for ImportDataMapToCds");
            if (resp != null)
            {
                if (resp.ImportMapId != Guid.Empty)
                {
                    return resp.ImportMapId;
                }
            }

            return Guid.Empty;
        }


        /// <summary>
        /// Import Solution Async used Execute Async pattern to run a solution import. 
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on CDS organizations version 7.2 or higher.  This imports the CDS solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="extraParameters">Extra parameters</param>
        /// <returns>Returns the Async Job ID.  To find the status of the job, query the AsyncOperation Entity using GetEntityDataByID using the returned value of this method</returns>
        public Guid ImportSolutionToCdsAsync(string solutionPath, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return ImportSolutionToCdsImpl(solutionPath, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, true, extraParameters);
        }


        /// <summary>
        /// <para>
        /// Imports a CDS solution to the CDS Server currently connected.
        /// <para>*** Note: this is a blocking call and will take time to Import to CDS ***</para>
        /// </para>
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on CDS organizations version 7.2 or higher.  This imports the CDS solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="extraParameters">Extra parameters</param>
        public Guid ImportSolutionToCds(string solutionPath, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return ImportSolutionToCdsImpl(solutionPath, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, false, extraParameters);
        }

        /// <summary>
        /// Executes a Delete and Propmote Request against CDS using the Async Pattern.
        /// </summary>
        /// <param name="uniqueName">Unique Name of solution to be upgraded</param>
        /// <returns>Returns the Async Job ID.  To find the status of the job, query the AsyncOperation Entity using GetEntityDataByID using the returned value of this method</returns>
        public Guid DeleteAndPromoteSolutionAsync(string uniqueName)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }
            // Test for non blank unique name. 
            if (string.IsNullOrEmpty(uniqueName))
            {
                logEntry.Log("Solution UniqueName is required.", TraceEventType.Error);
                return Guid.Empty;
            }

            DeleteAndPromoteRequest delReq = new DeleteAndPromoteRequest()
            {
                UniqueName = uniqueName
            };

            // Assign Tracking ID
            Guid requestTrackingId = Guid.NewGuid();
            delReq.RequestId = requestTrackingId;

            // Execute Async here
            ExecuteAsyncRequest req = new ExecuteAsyncRequest() { Request = delReq };
            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1} - Created Async DeleteAndPromoteSolutionRequest : RequestID={0} ",
            requestTrackingId.ToString(), uniqueName), TraceEventType.Verbose);
            ExecuteAsyncResponse resp = (ExecuteAsyncResponse)CdsCommand_Execute(req, "Submitting DeleteAndPromoteSolution Async Request");
            if (resp != null)
            {
                if (resp.AsyncJobId != Guid.Empty)
                {
                    logEntry.Log(string.Format("{1} - AsyncJobID for DeleteAndPromoteSolution {0}.", resp.AsyncJobId, uniqueName), TraceEventType.Verbose);
                    return resp.AsyncJobId;
                }
            }

            logEntry.Log(string.Format("{0} - Failed execute Async Job for DeleteAndPromoteSolution.", uniqueName), TraceEventType.Error);
            return Guid.Empty;
        }

        /// <summary>
        /// <para>
        /// Request CDS to install sample data shipped with Cds. Note this is process will take a few moments to execute.  
        /// <para>This method will return once the request has been submitted.</para>
        /// </para>
        /// </summary>
        /// <returns>ID of the Async job executing the request</returns>
        public Guid InstallSampleDataToCds()
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (ImportStatus.NotImported != IsSampleDataInstalled())
            {
                logEntry.Log("************ InstallSampleDataToCds failed, sample data is already installed on CDS", TraceEventType.Error);
                return Guid.Empty;
            }

            // Create Request to Install Sample data. 
            InstallSampleDataRequest loadSampledataRequest = new InstallSampleDataRequest() { RequestId = Guid.NewGuid() };
            InstallSampleDataResponse resp = (InstallSampleDataResponse)CdsCommand_Execute(loadSampledataRequest, "Executing InstallSampleDataRequest for InstallSampleDataToCds");
            if (resp == null)
                return Guid.Empty;
            else
                return loadSampledataRequest.RequestId.Value;
        }

        /// <summary>
        /// <para>
        /// Request CDS to remove sample data shipped with CDS. Note this is process will take a few moments to execute.  
        /// This method will return once the request has been submitted.
        /// </para>
        /// </summary>
        /// <returns>ID of the Async job executing the request</returns>
        public Guid UninstallSampleDataFromCds()
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (ImportStatus.NotImported == IsSampleDataInstalled())
            {
                logEntry.Log("************ DeleteSampleDataFromCds failed, sample data is not installed on CDS", TraceEventType.Error);
                return Guid.Empty;
            }

            UninstallSampleDataRequest removeSampledataRequest = new UninstallSampleDataRequest() { RequestId = Guid.NewGuid() };
            UninstallSampleDataResponse resp = (UninstallSampleDataResponse)CdsCommand_Execute(removeSampledataRequest, "Executing UninstallSampleDataRequest for UninstallSampleDataFromCds");
            if (resp == null)
                return Guid.Empty;
            else
                return removeSampledataRequest.RequestId.Value;
        }

        /// <summary>
        /// Determines if the CDS sample data has been installed 
        /// </summary>
        /// <returns>True if the sample data is installed, False if not. </returns>
        public ImportStatus IsSampleDataInstalled()
        {
            try
            {
                // Query the Org I'm connected to to get the sample data import info. 
                Dictionary<string, Dictionary<string, object>> theOrg =
                GetEntityDataBySearchParams("organization",
                    new Dictionary<string, string>(), LogicalSearchOperator.None, new List<string>() { "sampledataimportid" });

                if (theOrg != null && theOrg.Count > 0)
                {
                    var v = theOrg.FirstOrDefault();
                    if (v.Value != null && v.Value.Count > 0)
                    {
                        if (GetDataByKeyFromResultsSet<Guid>(v.Value, "sampledataimportid") != Guid.Empty)
                        {
                            string sampledataimportid = GetDataByKeyFromResultsSet<Guid>(v.Value, "sampledataimportid").ToString();
                            logEntry.Log(string.Format("sampledataimportid = {0}", sampledataimportid), TraceEventType.Verbose);
                            Dictionary<string, string> basicSearch = new Dictionary<string, string>();
                            basicSearch.Add("importid", sampledataimportid);
                            Dictionary<string, Dictionary<string, object>> importSampleData = GetEntityDataBySearchParams("import", basicSearch, LogicalSearchOperator.None, new List<string>() { "statuscode" });

                            if (importSampleData != null && importSampleData.Count > 0)
                            {
                                var import = importSampleData.FirstOrDefault();
                                if (import.Value != null)
                                {
                                    OptionSetValue ImportStatusResult = GetDataByKeyFromResultsSet<OptionSetValue>(import.Value, "statuscode");
                                    if (ImportStatusResult != null)
                                    {
                                        logEntry.Log(string.Format("sampledata import job result = {0}", ImportStatusResult.Value), TraceEventType.Verbose);
                                        //This Switch Case needs to be in Sync with the CDS Import StatusCode.
                                        switch (ImportStatusResult.Value)
                                        {
                                            // 4 is the Import Status Code for Complete Import
                                            case 4: return ImportStatus.Completed;
                                            // 5 is the Import Status Code for the Failed Import
                                            case 5: return ImportStatus.Failed;
                                            // Rest (Submitted, Parsing, Transforming, Importing) are different stages of Inprogress Import hence putting them under same case.
                                            default: return ImportStatus.InProgress;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return ImportStatus.NotImported;
            //return false;
        }

        /// <summary>
        /// ImportStatus Reasons
        /// </summary>
        public enum ImportStatus
        {
            /// <summary> Not Yet Imported </summary>
            NotImported = 0,
            /// <summary> Import is in Progress </summary>
            InProgress = 1,
            /// <summary> Import has Completed </summary>
            Completed = 2,
            /// <summary> Import has Failed </summary>
            Failed = 3
        };

        #endregion

        /// <summary>
        /// Associates one Entity to another where an M2M Relationship Exists. 
        /// </summary>
        /// <param name="entityName1">Entity on one side of the relationship</param>
        /// <param name="entity1Id">The Id of the record on the first side of the relationship</param>
        /// <param name="entityName2">Entity on the second side of the relationship</param>
        /// <param name="entity2Id">The Id of the record on the second side of the relationship</param>
        /// <param name="relationshipName">Relationship name between the 2 entities</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>true on success, false on fail</returns>
        public bool CreateEntityAssociation(string entityName1, Guid entity1Id, string entityName2, Guid entity2Id, string relationshipName, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(entityName1) || string.IsNullOrEmpty(entityName2) || entity1Id == Guid.Empty || entity2Id == Guid.Empty)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in CreateEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            AssociateEntitiesRequest req = new AssociateEntitiesRequest();
            req.Moniker1 = new EntityReference(entityName1, entity1Id);
            req.Moniker2 = new EntityReference(entityName2, entity2Id);
            req.RelationshipName = relationshipName;


            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Creating association between({0}) and {1}", entityName1, entityName2),
                    string.Format(CultureInfo.InvariantCulture, "Request to Create association between({0}) and {1} Queued", entityName1, entityName2)))
                return true;

            AssociateEntitiesResponse resp = (AssociateEntitiesResponse)CdsCommand_Execute(req, "Executing CreateEntityAssociation");
            if (resp != null)
                return true;

            return false;
        }

        /// <summary>
        /// Associates multiple entities of the same time to a single entity 
        /// </summary>
        /// <param name="targetEntity">Entity that things will be related too.</param>
        /// <param name="targetEntity1Id">ID of entity that things will be related too</param>
        /// <param name="sourceEntityName">Entity that you are relating from</param>
        /// <param name="sourceEntitieIds">ID's of the entities you are relating from</param>
        /// <param name="relationshipName">Name of the relationship between the target and the source entities.</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="isReflexiveRelationship">Optional: if set to true, indicates that this is a N:N using a reflexive relationship</param>
        /// <returns>true on success, false on fail</returns>
        public bool CreateMultiEntityAssociation(string targetEntity, Guid targetEntity1Id, string sourceEntityName, List<Guid> sourceEntitieIds, string relationshipName, Guid batchId = default(Guid), bool isReflexiveRelationship = false)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(targetEntity) || string.IsNullOrEmpty(sourceEntityName) || targetEntity1Id == Guid.Empty || sourceEntitieIds == null || sourceEntitieIds.Count == 0)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in CreateMultiEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            AssociateRequest req = new AssociateRequest();
            req.Relationship = new Relationship(relationshipName);
            if (isReflexiveRelationship) // used to determine if the relationship role is reflexive. 
                req.Relationship.PrimaryEntityRole = EntityRole.Referenced;
            req.RelatedEntities = new EntityReferenceCollection();
            foreach (Guid g in sourceEntitieIds)
            {
                req.RelatedEntities.Add(new EntityReference(sourceEntityName, g));
            }
            req.Target = new EntityReference(targetEntity, targetEntity1Id);

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Creating multi association between({0}) and {1}", targetEntity, sourceEntityName),
                    string.Format(CultureInfo.InvariantCulture, "Request to Create multi association between({0}) and {1} queued", targetEntity, sourceEntityName)))
                return true;

            AssociateResponse resp = (AssociateResponse)CdsCommand_Execute(req, "Executing CreateMultiEntityAssociation");
            if (resp != null)
                return true;

            return false;
        }

        /// <summary>
        /// Removes the Association between 2 entity items where an M2M Relationship Exists. 
        /// </summary>
        /// <param name="entityName1">Entity on one side of the relationship</param>
        /// <param name="entity1Id">The Id of the record on the first side of the relationship</param>
        /// <param name="entityName2">Entity on the second side of the relationship</param>
        /// <param name="entity2Id">The Id of the record on the second side of the relationship</param>
        /// <param name="relationshipName">Relationship name between the 2 entities</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>true on success, false on fail</returns>
        public bool DeleteEntityAssociation(string entityName1, Guid entity1Id, string entityName2, Guid entity2Id, string relationshipName, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(entityName1) || string.IsNullOrEmpty(entityName2) || entity1Id == Guid.Empty || entity2Id == Guid.Empty)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in DeleteEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            DisassociateEntitiesRequest req = new DisassociateEntitiesRequest();
            req.Moniker1 = new EntityReference(entityName1, entity1Id);
            req.Moniker2 = new EntityReference(entityName2, entity2Id);
            req.RelationshipName = relationshipName;

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Executing DeleteEntityAssociation between ({0}) and {1}", entityName1, entityName2),
                              string.Format(CultureInfo.InvariantCulture, "Request to Execute DeleteEntityAssociation between ({0}) and {1} Queued", entityName1, entityName2)))
                return true;

            DisassociateEntitiesResponse resp = (DisassociateEntitiesResponse)CdsCommand_Execute(req, "Executing DeleteEntityAssociation");
            if (resp != null)
                return true;

            return false;
        }

        /// <summary>
        /// Assign an Entity to the specified user ID
        /// </summary>
        /// <param name="userId">User ID to assign too</param>
        /// <param name="entityName">Target entity Name</param>
        /// <param name="entityId">Target entity id</param>
        /// <param name="batchId">Batch ID of to use, Optional</param>
        /// <returns></returns>
        public bool AssignEntityToUser(Guid userId, string entityName, Guid entityId, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null || userId == Guid.Empty || entityId == Guid.Empty)
            {
                return false;
            }

            AssignRequest assignRequest = new AssignRequest();
            assignRequest.Assignee = new EntityReference("systemuser", userId);
            assignRequest.Target = new EntityReference(entityName, entityId);

            if (AddRequestToBatch(batchId, assignRequest, string.Format(CultureInfo.InvariantCulture, "Assigning entity ({0}) to {1}", entityName, userId.ToString()),
                  string.Format(CultureInfo.InvariantCulture, "Request to Assign entity ({0}) to {1} Queued", entityName, userId.ToString())))
                return true;

            AssignResponse arResp = (AssignResponse)CdsCommand_Execute(assignRequest, "Routing a Ticket to User WIP Bin");
            if (arResp != null)
                return true;

            return false;
        }

        /// <summary>
        /// This will route a Entity to a public queue, 
        /// </summary>
        /// <param name="entityId">ID of the Entity to route</param>
        /// <param name="entityName">Name of the Entity that the Id describes</param>
        /// <param name="queueName">Name of the Queue to Route Too</param>
        /// <param name="workingUserId">ID of the user id to set as the working system user</param>
        /// <param name="setWorkingByUser">if true Set the worked by when doing the assign</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns>true on success</returns>
        public bool AddEntityToQueue(Guid entityId, string entityName, string queueName, Guid workingUserId, bool setWorkingByUser = false, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null || entityId == Guid.Empty)
            {
                return false;
            }

            Dictionary<string, string> SearchParams = new Dictionary<string, string>();
            SearchParams.Add("name", queueName);

            // Get the Target QUeue
            Dictionary<string, Dictionary<string, object>> rslts = GetEntityDataBySearchParams("queue", SearchParams, LogicalSearchOperator.None, null);
            if (rslts != null)
                if (rslts.Count > 0)
                {
                    Guid guQueueID = Guid.Empty;
                    foreach (Dictionary<string, object> row in rslts.Values)
                    {
                        // got something
                        guQueueID = GetDataByKeyFromResultsSet<Guid>(row, "queueid");
                        break;
                    }

                    if (guQueueID != Guid.Empty)
                    {


                        AddToQueueRequest req = new AddToQueueRequest();
                        req.DestinationQueueId = guQueueID;
                        req.Target = new EntityReference(entityName, entityId);

                        // Set the worked by user if the request includes it. 
                        if (setWorkingByUser)
                        {
                            Entity queItm = new Entity("queueitem");
                            queItm.Attributes.Add("workerid", new EntityReference("systemuser", workingUserId));
                            req.QueueItemProperties = queItm;
                        }

                        if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Assigning entity to queue ({0}) to {1}", entityName, guQueueID.ToString()),
                                    string.Format(CultureInfo.InvariantCulture, "Request to Assign entity to queue ({0}) to {1} Queued", entityName, guQueueID.ToString())))
                            return true;

                        AddToQueueResponse resp = (AddToQueueResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Adding a item to queue {0} in CDS", queueName));
                        if (resp != null)
                            return true;
                        else
                            return false;
                    }
                }
            return false;
        }

        /// <summary>
        /// this will send an Email to the 
        /// </summary>
        /// <param name="emailid">ID of the Email activity</param>
        /// <param name="token">Tracking Token or Null</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <returns></returns>
        public bool SendSingleEmail(Guid emailid, string token, Guid batchId = default(Guid))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null || emailid == Guid.Empty)
            {
                return false;
            }

            if (token == null)
                token = string.Empty;

            // Send the mail now. 
            SendEmailRequest req = new SendEmailRequest();
            req.EmailId = emailid;
            req.TrackingToken = token;
            req.IssueSend = true; // Send it now. 

            if (AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Send Direct email ({0}) tracking token {1}", emailid.ToString(), token),
                    string.Format(CultureInfo.InvariantCulture, "Request to Send Direct email ({0}) tracking token {1} Queued", emailid.ToString(), token)))
                return true;

            SendEmailResponse sendresp = (SendEmailResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Sending email ({0}) from CDS", emailid));
            if (sendresp != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Returns the user ID of the currently logged in user. 
        /// </summary>
        /// <returns></returns>
        public Guid GetMyCdsUserId()
        {
            return _SystemUser.UserId;
        }

        #endregion

        #region CDS MetadataService methods


        /// <summary>
        /// Gets a PickList, Status List or StateList from the metadata of an attribute
        /// </summary>
        /// <param name="targetEntity">text name of the entity to query</param>
        /// <param name="attribName">name of the attribute to query</param>
        /// <returns></returns>
        public PickListMetaElement GetPickListElementFromMetadataEntity(string targetEntity, string attribName)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService != null)
            {
                List<AttributeData> attribDataList = dynamicAppUtility.GetAttributeDataByEntity(targetEntity, attribName);
                if (attribDataList.Count > 0)
                {
                    // have data.. 
                    // need to make sure its really a pick list. 
                    foreach (AttributeData attributeData in attribDataList)
                    {
                        switch (attributeData.AttributeType)
                        {
                            case AttributeTypeCode.Picklist:
                            case AttributeTypeCode.Status:
                            case AttributeTypeCode.State:
                                PicklistAttributeData pick = (PicklistAttributeData)attributeData;
                                PickListMetaElement resp = new PickListMetaElement((string)pick.ActualValue, pick.AttributeLabel, pick.DisplayValue);
                                if (pick.PicklistOptions != null)
                                {
                                    foreach (OptionMetadata opt in pick.PicklistOptions)
                                    {
                                        PickListItem itm = null;
                                        itm = new PickListItem((string)GetLocalLabel(opt.Label), (int)opt.Value.Value);
                                        resp.Items.Add(itm);
                                    }
                                }
                                return resp;
                            default:
                                break;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a global option set from CDS. 
        /// </summary>
        /// <param name="globalOptionSetName">Name of the Option Set To get</param>
        /// <returns>OptionSetMetadata or null</returns>
        public OptionSetMetadata GetGlobalOptionSetMetadata(string globalOptionSetName)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            try
            {
                return metadataUtlity.GetGlobalOptionSetMetadata(globalOptionSetName);
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting optionset metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }


        /// <summary>
        /// Returns a list of entities with basic data from CDS
        /// </summary>
        /// <param name="onlyPublished">defaults to true, will only return published information</param>
        /// <param name="filter">EntityFilter to apply to this request, note that filters other then Default will consume more time.</param>
        /// <returns></returns>
        public List<EntityMetadata> GetAllEntityMetadata(bool onlyPublished = true, EntityFilters filter = EntityFilters.Default)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return metadataUtlity.GetAllEntityMetadata(onlyPublished, filter);
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns the Metadata for an entity from CDS, defaults to basic data only. 
        /// </summary>
        /// <param name="entityLogicalname">Logical name of the entity</param>
        /// <param name="queryFilter">filter to apply to the query, defaults to default entity data.</param>
        /// <returns></returns>
        public EntityMetadata GetEntityMetadata(string entityLogicalname, EntityFilters queryFilter = EntityFilters.Default)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return metadataUtlity.GetEntityMetadata(queryFilter, entityLogicalname);
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns the Form Entity References for a given form type. 
        /// </summary>
        /// <param name="entityLogicalname">logical name of the entity you are querying for form data.</param>
        /// <param name="formTypeId">Form Type you want</param>
        /// <returns>List of Entity References for the form type requested.</returns>
        public List<EntityReference> GetEntityFormIdListByType(string entityLogicalname, FormTypeId formTypeId)
        {
            logEntry.ResetLastError();
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                RetrieveFilteredFormsRequest req = new RetrieveFilteredFormsRequest();
                req.EntityLogicalName = entityLogicalname;
                req.FormType = new OptionSetValue((int)formTypeId);
                RetrieveFilteredFormsResponse resp = (RetrieveFilteredFormsResponse)CdsCommand_Execute(req, "GetEntityFormIdListByType");
                if (resp != null)
                    return resp.SystemForms.ToList();
                else
                    return null;
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns all attributes on a entity
        /// </summary>
        /// <param name="entityLogicalname">returns all attributes on a entity</param>
        /// <returns></returns>
        public List<AttributeMetadata> GetAllAttributesForEntity(string entityLogicalname)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return metadataUtlity.GetAllAttributesMetadataByEntity(entityLogicalname);
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Gets metadata for a specific entity's attribute.
        /// </summary>
        /// <param name="entityLogicalname">Name of the entity</param>
        /// <param name="attribName">Attribute Name</param>
        /// <returns></returns>
        public AttributeMetadata GetEntityAttributeMetadataForAttribute(string entityLogicalname, string attribName)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return metadataUtlity.GetAttributeMetadata(entityLogicalname, attribName);
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Gets an Entity Name by Logical name or Type code. 
        /// </summary>
        /// <param name="entityName">logical name of the entity </param>
        /// <param name="entityTypeCode">Type code for the entity </param>
        /// <returns>Localized name for the entity in the current users language</returns>
        public string GetEntityDisplayName(string entityName, int entityTypeCode = -1)
        {
            return GetEntityDisplayNameImpl(entityName, entityTypeCode);
        }

        /// <summary>
        /// Gets an Entity Name by Logical name or Type code. 
        /// </summary>
        /// <param name="entityName">logical name of the entity </param>
        /// <param name="entityTypeCode">Type code for the entity </param>
        /// <returns>Localized plural name for the entity in the current users language</returns>
        public string GetEntityDisplayNamePlural(string entityName, int entityTypeCode = -1)
        {
            return GetEntityDisplayNameImpl(entityName, entityTypeCode, true);
        }

        /// <summary>
        /// This will clear the Metadata cache for either all entities or the specified entity 
        /// </summary>
        /// <param name="entityName">Optional: name of the entity to clear cached info for</param>
        public void ResetLocalMetadataCache(string entityName = "")
        {
            if (metadataUtlity != null)
                metadataUtlity.ClearCachedEntityMetadata(entityName);
        }

        /// <summary>
        /// Gets the Entity Display Name. 
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityTypeCode"></param>
        /// <param name="getPlural"></param>
        /// <returns></returns>
        private string GetEntityDisplayNameImpl(string entityName, int entityTypeCode = -1, bool getPlural = false)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return string.Empty;
            }

            if (entityTypeCode == -1 && string.IsNullOrWhiteSpace(entityName))
            {
                logEntry.Log("Target entity or Type code is required", TraceEventType.Error);
                return string.Empty;
            }
            #endregion

            try
            {
                // Get the entity by type code if necessary. 
                if (entityTypeCode != -1)
                    entityName = metadataUtlity.GetEntityLogicalName(entityTypeCode);

                if (string.IsNullOrWhiteSpace(entityName))
                {
                    logEntry.Log("Target entity or Type code is required", TraceEventType.Error);
                    return string.Empty;
                }



                // Pull Object type code for this object. 
                EntityMetadata entData =
                    metadataUtlity.GetEntityMetadata(EntityFilters.Entity, entityName);

                if (entData != null)
                {
                    if (getPlural)
                    {
                        if (entData.DisplayCollectionName != null && entData.DisplayCollectionName.UserLocalizedLabel != null)
                            return entData.DisplayCollectionName.UserLocalizedLabel.Label;
                        else
                            return entityName; // Default to echo the same name back 
                    }
                    else
                    {
                        if (entData.DisplayName != null && entData.DisplayName.UserLocalizedLabel != null)
                            return entData.DisplayName.UserLocalizedLabel.Label;
                        else
                            return entityName; // Default to echo the same name back 
                    }
                }

            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the typecode of an entity by name. 
        /// </summary>
        /// <param name="entityName">name of the entity to get the type code on</param>
        /// <returns></returns>
        public string GetEntityTypeCode(string entityName)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return string.Empty;
            }

            if (string.IsNullOrEmpty(entityName))
            {
                logEntry.Log("Target entity is required", TraceEventType.Error);
                return string.Empty;
            }
            #endregion

            try
            {

                // Pull Object type code for this object. 
                EntityMetadata entData =
                    metadataUtlity.GetEntityMetadata(EntityFilters.Entity, entityName);

                if (entData != null)
                {
                    if (entData.ObjectTypeCode != null && entData.ObjectTypeCode.HasValue)
                    {
                        return entData.ObjectTypeCode.Value.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return string.Empty;
        }


        /// <summary>
        /// Returns the Entity name for the given Type code
        /// </summary>
        /// <param name="entityTypeCode"></param>
        /// <returns></returns>
        public string GetEntityName(int entityTypeCode)
        {
            return metadataUtlity.GetEntityLogicalName(entityTypeCode);
        }


        /// <summary>
        /// Adds an option to a pick list on an entity. 
        /// </summary>
        /// <param name="targetEntity">Entity Name to Target</param>
        /// <param name="attribName">Attribute Name on the Entity</param>
        /// <param name="locLabelList">List of Localized Labels</param>
        /// <param name="valueData">integer Value</param>
        /// <param name="publishOnComplete">Publishes the Update to the Live system.. note this is a time consuming process.. if you are doing a batch up updates, call PublishEntity Separately when you are finished.</param>
        /// <returns>true on success, on fail check last error.</returns>
        public bool CreateOrUpdatePickListElement(string targetEntity, string attribName, List<LocalizedLabel> locLabelList, int valueData, bool publishOnComplete)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            #region Basic Checks
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(targetEntity))
            {
                logEntry.Log("Target entity is required", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(attribName))
            {
                logEntry.Log("Target attribute name is required", TraceEventType.Error);
                return false;
            }

            if (locLabelList == null || locLabelList.Count == 0)
            {
                logEntry.Log("Target Labels are required", TraceEventType.Error);
                return false;
            }

            LoadCdsLCIDs(); // Load current languages . 

            // Clear out the Metadata for this object. 
            if (metadataUtlity != null)
                metadataUtlity.ClearCachedEntityMetadata(targetEntity);

            EntityMetadata entData =
                metadataUtlity.GetEntityMetadata(targetEntity);

            if (!entData.IsCustomEntity.Value)
            {
                // Only apply this if the entity is not a custom entity 
                if (valueData <= 199999)
                {
                    logEntry.Log("Option Value must exceed 200000", TraceEventType.Error);
                    return false;
                }
            }


            #endregion

            // get the values for the requested attribute. 
            PickListMetaElement listData = GetPickListElementFromMetadataEntity(targetEntity, attribName);
            if (listData == null)
            {
                // error here.
            }

            bool isUpdate = false;
            if (listData.Items != null && listData.Items.Count != 0)
            {
                // Check to see if the value we are looking to insert already exists by name or value. 
                List<string> DisplayLabels = new List<string>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (DisplayLabels.Contains(loclbl.Label))
                        continue;
                    else
                        DisplayLabels.Add(loclbl.Label);
                }

                foreach (PickListItem pItem in listData.Items)
                {
                    // check the value by id. 
                    if (pItem.PickListItemId == valueData)
                    {
                        if (DisplayLabels.Contains(pItem.DisplayLabel))
                        {
                            DisplayLabels.Clear();
                            logEntry.Log("PickList Element exists, No Change required.", TraceEventType.Error);
                            return false;
                        }
                        isUpdate = true;
                        break;
                    }

                    //// Check the value by name...  by putting this hear, we will handle a label update vs a Duplicate label. 
                    if (DisplayLabels.Contains(pItem.DisplayLabel))
                    {
                        // THis is an ERROR State... While CDS will allow 2 labels with the same text, it looks weird. 
                        DisplayLabels.Clear();
                        logEntry.Log("Label Name exists, Please use a different display name for the label.", TraceEventType.Error);
                        return false;
                    }
                }

                DisplayLabels.Clear();
            }

            if (isUpdate)
            {
                // update request
                UpdateOptionValueRequest updateReq = new UpdateOptionValueRequest();
                updateReq.AttributeLogicalName = attribName;
                updateReq.EntityLogicalName = targetEntity;
                updateReq.Label = new Label();
                List<LocalizedLabel> lblList = new List<LocalizedLabel>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (CdsLoadedLCIDList.Contains(loclbl.LanguageCode))
                    {
                        LocalizedLabel lbl = new LocalizedLabel()
                        {
                            Label = loclbl.Label,
                            LanguageCode = loclbl.LanguageCode
                        };
                        lblList.Add(lbl);
                    }
                }
                updateReq.Label.LocalizedLabels.AddRange(lblList.ToArray());
                updateReq.Value = valueData;
                updateReq.MergeLabels = true;

                UpdateOptionValueResponse UpdateResp = (UpdateOptionValueResponse)CdsCommand_Execute(updateReq, "Updating a PickList Element in CDS");
                if (UpdateResp == null)
                    return false;
            }
            else
            {
                // create request. 
                // Create a new insert request 
                InsertOptionValueRequest req = new InsertOptionValueRequest();

                req.AttributeLogicalName = attribName;
                req.EntityLogicalName = targetEntity;
                req.Label = new Label();
                List<LocalizedLabel> lblList = new List<LocalizedLabel>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (CdsLoadedLCIDList.Contains(loclbl.LanguageCode))
                    {
                        LocalizedLabel lbl = new LocalizedLabel()
                        {
                            Label = loclbl.Label,
                            LanguageCode = loclbl.LanguageCode
                        };
                        lblList.Add(lbl);
                    }
                }
                req.Label.LocalizedLabels.AddRange(lblList.ToArray());
                req.Value = valueData;


                InsertOptionValueResponse resp = (InsertOptionValueResponse)CdsCommand_Execute(req, "Creating a PickList Element in CDS");
                if (resp == null)
                    return false;

            }

            // Publish the update if asked to. 
            if (publishOnComplete)
                return PublishEntity(targetEntity);
            else
                return true;
        }

        /// <summary>
        /// Publishes an entity to the production system, 
        /// used in conjunction with the Metadata services.
        /// </summary>
        /// <param name="entityName">Name of the entity to publish</param>
        /// <returns>True on success</returns>
        public bool PublishEntity(string entityName)
        {
            // Now Publish the update. 
            string sPublishUpdateXml =
                           string.Format(CultureInfo.InvariantCulture, "<importexportxml><entities><entity>{0}</entity></entities><nodes /><securityroles/><settings/><workflows/></importexportxml>",
                           entityName);

            PublishXmlRequest pubReq = new PublishXmlRequest();
            pubReq.ParameterXml = sPublishUpdateXml;

            PublishXmlResponse rsp = (PublishXmlResponse)CdsCommand_Execute(pubReq, "Publishing a PickList Element in CDS");
            if (rsp != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Loads the Currently loaded languages for CDS
        /// </summary>
        /// <returns></returns>
        private bool LoadCdsLCIDs()
        {
            // Now Publish the update. 
            // Check to see if the Language ID's are loaded. 
            if (CdsLoadedLCIDList == null)
            {
                CdsLoadedLCIDList = new List<int>();

                // load the CDS Language List. 
                RetrieveAvailableLanguagesRequest lanReq = new RetrieveAvailableLanguagesRequest();
                RetrieveAvailableLanguagesResponse rsp = (RetrieveAvailableLanguagesResponse)CdsCommand_Execute(lanReq, "Reading available languages from CDS");
                if (rsp == null)
                    return false;
                if (rsp.LocaleIds != null)
                {
                    foreach (int iLCID in rsp.LocaleIds)
                    {
                        if (CdsLoadedLCIDList.Contains(iLCID))
                            continue;
                        else
                            CdsLoadedLCIDList.Add(iLCID);
                    }
                }
            }
            return true;
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
            //If tokenCachePath is not supplied it will take from the constructor  of token cache and delete the file.
            if (_CdsServiceClientTokenCache == null)
                _CdsServiceClientTokenCache = new CdsServiceClientTokenCache(tokenCachePath);
            return _CdsServiceClientTokenCache.Clear(tokenCachePath);
        }

        #endregion

        #region CDSUtilites

        /// <summary>
        /// Adds paging related parameter to the input fetchXml
        /// </summary>
        /// <param name="fetchXml">Input fetch Xml</param>
        /// <param name="pageCount">The number of records to be fetched</param>
        /// <param name="pageNum">The page number</param>
        /// <param name="pageCookie">Page cookie</param>
        /// <returns></returns>
        private String AddPagingParametersToFetchXml(string fetchXml, int pageCount, int pageNum, string pageCookie)
        {
            if (String.IsNullOrWhiteSpace(fetchXml))
            {
                return fetchXml;
            }

            XmlDocument fetchdoc = XmlUtil.CreateXmlDocument(fetchXml);
            XmlElement fetchroot = fetchdoc.DocumentElement;

            XmlAttribute pageAttribute = fetchdoc.CreateAttribute("page");
            pageAttribute.Value = pageNum.ToString(CultureInfo.InvariantCulture);

            XmlAttribute countAttribute = fetchdoc.CreateAttribute("count");
            countAttribute.Value = pageCount.ToString(CultureInfo.InvariantCulture);

            XmlAttribute pagingCookieAttribute = fetchdoc.CreateAttribute("paging-cookie");
            pagingCookieAttribute.Value = pageCookie;

            fetchroot.Attributes.Append(pageAttribute);
            fetchroot.Attributes.Append(countAttribute);
            fetchroot.Attributes.Append(pagingCookieAttribute);

            return fetchdoc.DocumentElement.OuterXml;
        }

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
        /// Builds the Query expression to use with a Search. 
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="searchParams"></param>
        /// <param name="fieldList"></param>
        /// <param name="searchOperator"></param>
        /// <returns></returns>
        private static QueryExpression BuildQueryFilter(string entityName, List<CdsSearchFilter> searchParams, List<string> fieldList, LogicalSearchOperator searchOperator)
        {
            // Create ColumnSet
            ColumnSet cols = null;
            if (fieldList != null)
            {
                cols = new ColumnSet();
                cols.Columns.AddRange(fieldList.ToArray());
            }

            List<FilterExpression> filters = BuildFilterList(searchParams);

            // Link Filter. 
            FilterExpression Queryfilter = new FilterExpression();
            Queryfilter.Filters.AddRange(filters);

            // Add Logical relationship.
            if (searchOperator == LogicalSearchOperator.Or)
                Queryfilter.FilterOperator = LogicalOperator.Or;
            else
                Queryfilter.FilterOperator = LogicalOperator.And;


            // Build Query 
            QueryExpression query = new QueryExpression();
            query.EntityName = entityName; // Set to the requested entity Type 
            if (cols != null)
                query.ColumnSet = cols;
            else
                query.ColumnSet = new ColumnSet(true);// new AllColumns();

            query.Criteria = Queryfilter;
            query.NoLock = true; // Added to remove locking on queries. 
            return query;
        }

        /// <summary>
        /// Creates a SearchFilterList from a Search string Dictionary 
        /// </summary>
        /// <param name="inSearchParams">Inbound Search Strings</param>
        /// <param name="outSearchList">List that will be populated</param>
        private static void BuildSearchFilterListFromSearchTerms(Dictionary<string, string> inSearchParams, List<CdsSearchFilter> outSearchList)
        {
            if (inSearchParams != null)
            {
                foreach (var item in inSearchParams)
                {
                    CdsSearchFilter f = new CdsSearchFilter();
                    f.FilterOperator = LogicalOperator.And;
                    f.SearchConditions.Add(new CdsFilterConditionItem()
                    {
                        FieldName = item.Key,
                        FieldValue = item.Value,
                        FieldOperator = string.IsNullOrWhiteSpace(item.Value) ? ConditionOperator.Null : item.Value.Contains("%") ? ConditionOperator.Like : ConditionOperator.Equal
                    });
                    outSearchList.Add(f);
                }
            }
        }

        /// <summary>
        /// Builds the filter list for a query
        /// </summary>
        /// <param name="searchParams"></param>
        /// <returns></returns>
        private static List<FilterExpression> BuildFilterList(List<CdsSearchFilter> searchParams)
        {
            List<FilterExpression> filters = new List<FilterExpression>();
            // Create Conditions
            foreach (CdsSearchFilter conditionItemList in searchParams)
            {
                FilterExpression filter = new FilterExpression();
                foreach (CdsFilterConditionItem conditionItem in conditionItemList.SearchConditions)
                {
                    ConditionExpression condition = new ConditionExpression();
                    condition.AttributeName = conditionItem.FieldName;
                    condition.Operator = conditionItem.FieldOperator;
                    if (!(condition.Operator == ConditionOperator.NotNull || condition.Operator == ConditionOperator.Null))
                        condition.Values.Add(conditionItem.FieldValue);

                    filter.AddCondition(condition);
                }
                if (filter.Conditions.Count > 0)
                {
                    filter.FilterOperator = conditionItemList.FilterOperator;
                    filters.Add(filter);
                }
            }
            return filters;
        }

        /// <summary>
        /// Get the localize label from a CDS Label. 
        /// </summary>
        /// <param name="cdsLabel"></param>
        /// <returns></returns>
        private static string GetLocalLabel(Label cdsLabel)
        {
            foreach (LocalizedLabel lbl in cdsLabel.LocalizedLabels)
            {
                // try to get the current display langue code. 
                if (lbl.LanguageCode == CultureInfo.CurrentUICulture.LCID)
                {
                    return lbl.Label;
                }
            }
            return cdsLabel.UserLocalizedLabel.Label;
        }

        /// <summary>
        /// Adds data from a Entity to result set
        /// </summary>
        /// <param name="resultSet"></param>
        /// <param name="dataEntity"></param>
        private static void AddDataToResultSet(ref Dictionary<string, object> resultSet, Entity dataEntity)
        {
            if (dataEntity == null)
                return;
            if (resultSet == null)
                return;
            try
            {
                foreach (var p in dataEntity.Attributes)
                {
                    resultSet.Add(p.Key + "_Property", p);
                    resultSet.Add(p.Key, dataEntity.FormattedValues.ContainsKey(p.Key) ? dataEntity.FormattedValues[p.Key] : p.Value);
                }

            }
            catch { }
        }

        /// <summary>
        /// Gets the Lookup Value GUID for any given entity name
        /// </summary>
        /// <param name="entName">Entity you are looking for</param>
        /// <param name="Value">Value you are looking for</param>
        /// <returns>ID of the lookup value in the entity</returns>
        private Guid GetLookupValueForEntity(string entName, string Value)
        {
            // Check for existence of cached list.
            if (_CachObject == null)
            {

                _CachObject = (Dictionary<string, Dictionary<string, object>>)System.Runtime.Caching.MemoryCache.Default[CACHOBJECNAME];
                if (_CachObject == null)
                    _CachObject = new Dictionary<string, Dictionary<string, object>>();
            }

            Guid guResultID = Guid.Empty;

            if ((_CachObject.ContainsKey(entName.ToString())) && (_CachObject[entName.ToString()].ContainsKey(Value)))
                return (Guid)_CachObject[entName.ToString()][Value];

            switch (entName)
            {
                case "transactioncurrency":
                    guResultID = LookupEntitiyID(Value, entName, "transactioncurrencyid", "currencyname");
                    break;
                case "subject":
                    guResultID = LookupEntitiyID(Value, entName, "subjectid", "title"); //LookupSubjectIDForName(Value);
                    break;
                case "systemuser":
                    guResultID = LookupEntitiyID(Value, entName, "systemuserid", "domainname");
                    break;
                case "pricelevel":
                    guResultID = LookupEntitiyID(Value, entName, "pricelevelid", "name");
                    break;

                case "product":
                    guResultID = LookupEntitiyID(Value, entName, "productid", "productnumber");
                    break;
                case "uom":
                    guResultID = LookupEntitiyID(Value, entName, "uomid", "name");
                    break;
                default:
                    return Guid.Empty;
            }


            // High effort objects that are generally not changed during the live cycle of a connection are cached here. 
            if (guResultID != Guid.Empty)
            {
                if (!_CachObject.ContainsKey(entName.ToString()))
                    _CachObject.Add(entName.ToString(), new Dictionary<string, object>());
                _CachObject[entName.ToString()].Add(Value, guResultID);

                System.Runtime.Caching.MemoryCache.Default.Add(CACHOBJECNAME, _CachObject, DateTime.Now.AddMinutes(5));
            }

            return guResultID;

        }

        /// <summary>
        /// Lookup a entity ID by a single search element. 
        /// Used for Lookup Lists.
        /// </summary>
        /// <param name="SearchValue">Text to search for</param>
        /// <param name="ent">Entity Type to Search in </param>
        /// <param name="IDField">Field that contains the id</param>
        /// <param name="SearchField">Field to Search against</param>
        /// <returns>Guid of Entity or Empty Guid</returns>
        private Guid LookupEntitiyID(string SearchValue, string ent, string IDField, string SearchField)
        {
            try
            {
                Guid guID = Guid.Empty;
                List<string> FieldList = new List<string>();
                FieldList.Add(IDField);

                Dictionary<string, string> SearchList = new Dictionary<string, string>();
                SearchList.Add(SearchField, SearchValue);

                Dictionary<string, Dictionary<string, object>> rslts = GetEntityDataBySearchParams(ent, SearchList, LogicalSearchOperator.None, FieldList);

                if (rslts != null)
                {
                    foreach (Dictionary<string, object> rsl in rslts.Values)
                    {
                        if (rsl.ContainsKey(IDField))
                        {
                            guID = (Guid)rsl[IDField];
                        }
                    }
                }
                return guID;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Adds values for an update to a CDS propertyList
        /// </summary>
        /// <param name="Field"></param>
        /// <param name="PropertyList"></param>
        /// <returns></returns>
        internal void AddValueToPropertyList(KeyValuePair<string, CdsDataTypeWrapper> Field, AttributeCollection PropertyList)
        {
            if (string.IsNullOrEmpty(Field.Key))
                // throw exception 
                throw new System.ArgumentOutOfRangeException("valueArray", "Missing CDS field name");

            try
            {
                switch (Field.Value.Type)
                {

                    case CdsFieldType.Boolean:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (bool)Field.Value.Value));
                        break;

                    case CdsFieldType.DateTime:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (DateTime)Field.Value.Value));
                        break;

                    case CdsFieldType.Decimal:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Convert.ToDecimal(Field.Value.Value)));
                        break;

                    case CdsFieldType.Float:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Convert.ToDouble(Field.Value.Value)));
                        break;

                    case CdsFieldType.Money:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new Money(Convert.ToDecimal(Field.Value.Value))));
                        break;

                    case CdsFieldType.Number:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (int)Field.Value.Value));
                        break;

                    case CdsFieldType.Customer:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new EntityReference(Field.Value.ReferencedEntity, (Guid)Field.Value.Value)));
                        break;

                    case CdsFieldType.Lookup:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new EntityReference(Field.Value.ReferencedEntity, (Guid)Field.Value.Value)));
                        break;

                    case CdsFieldType.Picklist:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new OptionSetValue((int)Field.Value.Value)));
                        break;

                    case CdsFieldType.String:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (string)Field.Value.Value));
                        break;

                    case CdsFieldType.Raw:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Field.Value.Value));
                        break;

                    case CdsFieldType.UniqueIdentifier:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (Guid)Field.Value.Value));
                        break;
                }
            }
            catch (InvalidCastException castEx)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed when casting CdsDataTypeWrapper wrapped objects to the CDS Type. Field : {0}", Field.Key), TraceEventType.Error, castEx);
                throw;
            }
            catch (System.Exception ex)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed when casting CdsDataTypeWrapper wrapped objects to the CDS Type. Field : {0}", Field.Key), TraceEventType.Error, ex);
                throw;
            }

        }

        /// <summary>
        /// Creates and Returns a Search Result Set
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        private static Dictionary<string, Dictionary<string, object>> CreateResultDataSet(EntityCollection resp)
        {
            Dictionary<string, Dictionary<string, object>> Results = new Dictionary<string, Dictionary<string, object>>();
            foreach (Entity bEnt in resp.Entities)
            {
                // Not really doing an update here... just turning it into something I can walk. 
                Dictionary<string, object> SearchRstls = new Dictionary<string, object>();
                AddDataToResultSet(ref SearchRstls, bEnt);
                // Add Ent name and ID 
                SearchRstls.Add("ReturnProperty_EntityName", bEnt.LogicalName);
                SearchRstls.Add("ReturnProperty_Id ", bEnt.Id);
                Results.Add(Guid.NewGuid().ToString(), SearchRstls);
            }
            if (Results.Count > 0)
                return Results;
            else
                return null;
        }

        /// <summary>
        /// Adds a request to a batch with display and handling logic
        /// will fail out if batching is not enabled. 
        /// </summary>
        /// <param name="batchId">ID of the batch to add too</param>
        /// <param name="req">Organization request to Add</param>
        /// <param name="batchTagText">Batch Add Text, this is the text that will be reflected when the batch is added - appears in the batch diags</param>
        /// <param name="successText">Success Added Batch - appears in webSvcActions diag</param>
        /// <returns></returns>
        internal bool AddRequestToBatch(Guid batchId, OrganizationRequest req, string batchTagText, string successText)
        {
            if (batchId != Guid.Empty)
            {
                if (IsBatchOperationsAvailable)
                {
                    if (_BatchManager.AddNewRequestToBatch(batchId, req, batchTagText))
                    {
                        logEntry.Log(successText, TraceEventType.Verbose);
                        return true;
                    }
                    else
                        logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though. 
                    logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }
            return false;
        }



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
        /// <returns></returns>
        public HttpResponseMessage ExecuteCdsWebRequest(HttpMethod method, string queryString, string body, Dictionary<string, List<string>> customHeaders, string contentType = default(string))
        {
            logEntry.ResetLastError();  // Reset Last Error 
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return null;
            }

            if (string.IsNullOrEmpty(queryString) && string.IsNullOrEmpty(body))
            {
                logEntry.Log("Execute Web Request failed, queryString and body cannot be null", TraceEventType.Error);
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

            var result = CdsCommand_WebExecute(queryString, body, method, customHeaders, contentType, string.Empty).Result;
            if (result == null)
                throw LastCdsException;
            else
                return result;
        }

        /// <summary>
        /// Executes a CDS Organization Request (thread safe) and returns the organization response object. Also adds metrics for logging support.
        /// </summary>
        /// <param name="req">Organization Request  to run</param>
        /// <param name="logMessageTag">Message identifying what this request in logging.</param>
        /// <param name="useWebAPI">When True, uses the webAPI to execute the organization Request.  This works for only Create at this time.</param>
        /// <returns>Result of request or null.</returns>
        public OrganizationResponse ExecuteCdsOrganizationRequest(OrganizationRequest req, string logMessageTag = "User Defined" , bool useWebAPI = false)
        {
            if (req != null)
            {
                useWebAPI = Utilities.IsRequestValidForTranslationToWebAPI(req) ? useWebAPI : false;
                if (!useWebAPI)
                {
                    return CdsCommand_Execute(req, logMessageTag);
                }
                else
                {
                    // use Web API. 
                    return CdsCommand_WebAPIProcess_Execute(req, logMessageTag);
                }
            }
            else
            {
                logEntry.Log("Execute Organization Request failed, Organization Request cannot be null", TraceEventType.Error);
                return null;
            }
        }

        private OrganizationResponse CdsCommand_WebAPIProcess_Execute(OrganizationRequest req, string logMessageTag)
        {
            if (Utilities.IsRequestValidForTranslationToWebAPI(req)) // THIS WILL GET REMOVED AT SOME POINT, TEMP FOR TRANSTION  //TODO:REMOVE ON COMPELTE
            {
                HttpMethod methodToExecute = HttpMethod.Get;
                Entity cReq = null;
                if (req.Parameters.ContainsKey("Target") && req.Parameters["Target"] is Entity ent) // this should cover things that have targets. 
                {
                    cReq = ent; 
                }
                else if (req.Parameters.ContainsKey("Target") && req.Parameters["Target"] is EntityReference entRef) // this should cover things that have targets. 
                {
                    cReq = new Entity(entRef.LogicalName, entRef.Id); 
                }

                if (cReq != null)
                { 
                    // if CRUD type. get Entity 
                    var EntityData = metadataUtlity.GetEntityMetadata(EntityFilters.Entity, cReq.LogicalName);
                    if (EntityData == null)
                    {
                        logEntry.Log($"Execute Organization Request failed, failed to acquire entity data for {cReq.LogicalName}", TraceEventType.Warning);
                        return null;
                    }
                    // Get Entity Set name from Entity being addressed 
                    var EntSetname = EntityData.EntitySetName;
                    // generate webAPI Create request. 
                    string postUri = string.Empty;
                    string requestName = req.RequestName.ToLower();

                    switch (requestName)
                    {
                        case "create":
                            methodToExecute = HttpMethod.Post;
                            break;
                        case "update":
                        case "upsert":
                            methodToExecute = new HttpMethod("Patch");
                            break;
                        case "delete":
                            methodToExecute = HttpMethod.Delete;
                            break;
                        default:
                            // Abort request
                            logEntry.Log("Execute Organization Request failed, WebAPI is only supported for Create, Upsert, Update, and Delete message types at this time.", TraceEventType.Error);
                            return null;
                    }

                    if ( methodToExecute != HttpMethod.Post)
                    {
                        if (cReq.KeyAttributes?.Any() == true)
                        {

                        string keycollection = string.Empty;
                            foreach (var itm in cReq.KeyAttributes)
                            {
                                if (itm.Value is EntityReference er)
                                {
                                    keycollection += $"_{itm.Key}_value='{er.Id.ToString("P")}',";

                                    //IEnumerable<string> keys = cReq.KeyAttributes.Select(s => $"_{s.Key}_value='{((EntityReference)s.Value).Id.ToString().Replace("'", "''")}'");
                                    //keycollection += $"{EntityData.EntitySetName}({string.Join("&", keys)})";

                                    //if (cReq.Id != Guid.Empty)
                                    //{
                                    //    var s2 = EntityData.EntitySetName + cReq.Id.ToString("P");
                                    //}


                                    //// Add support for ER for KEY. 
                                    //keycollection += $"{itm.Key}@odata.bind='{$"/{metadataUtlity.GetEntityMetadata(Xrm.Sdk.Metadata.EntityFilters.Entity, er.LogicalName).EntitySetName}({er.Id})"}',";
                                }
                                else 
                                {
                                    keycollection += $"{itm.Key}='{itm.Value}',";
                                }
                            }
                            keycollection = keycollection.Remove(keycollection.Length - 1); // remove trailing , 
                            postUri = $"{EntityData.EntitySetName}({keycollection})";
                        }
                        else
                        {
                            postUri = $"{EntityData.EntitySetName}({cReq.Id})";
                        }
                    }
                    else
                    {
                        // its just a post. 
                        postUri = $"{EntityData.EntitySetName}";
                    }

                    string bodyOfRequest = string.Empty;
                    if (cReq.Attributes != null && cReq.Attributes.Count > 0)
                    {
                        var requestBodyObject = Utilities.ToExpandoObject(cReq.Attributes, metadataUtlity);
                        bodyOfRequest = Newtonsoft.Json.JsonConvert.SerializeObject(requestBodyObject);
                    }

                    // Execute request 
                    Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>();
                    headers.Add("Prefer", new List<string>() { "odata.include-annotations=*" });

                    var sResp = CdsCommand_WebExecute(postUri, bodyOfRequest, methodToExecute, headers, "application/json", logMessageTag).Result;
                    if (sResp != null && sResp.IsSuccessStatusCode)
                    {
                        if (req is CreateRequest)
                        {
                            Guid createdRecId = Guid.Empty;
                            // find location code. 
                            if (sResp.Headers.Location != null)
                            {
                                string locationReferance = sResp.Headers.Location.Segments.Last();
                                string ident = locationReferance.Substring(locationReferance.IndexOf("(") + 1, 36);
                                Guid.TryParse(ident, out createdRecId);
                            }
                            Microsoft.Xrm.Sdk.Messages.CreateResponse zResp = new CreateResponse();
                            zResp.Results.Add("id", createdRecId);
                            return zResp;
                        }
                        else if (req is UpdateRequest)
                        {
                            return new UpdateResponse();
                        }
                        else if (req is DeleteRequest)
                        {
                            return new DeleteResponse(); 
                        } else
                            return null;
                    }
                    else
                        return null;
                }
                else
                    return null; 
            }
            else
            {
                logEntry.Log("Execute Organization Request failed, WebAPI is only supported for Create, Update, Delete message types at this time.", TraceEventType.Error);
                return null;
            }
        }

        /// <summary>
        /// Executes a row level delete on a CDS entity ( thread safe ) and returns true or false. Also adds metrics for logging support.
        /// </summary>
        /// <param name="entName">Name of the Entity to delete from</param>
        /// <param name="entId">ID of the row to delete</param>
        /// <param name="logMessageTag">Message identifying what this request in logging</param>
        /// <returns>True on success, False on fail. </returns>
        public bool ExecuteCdsEntityDeleteRequest(string entName, Guid entId, string logMessageTag = "User Defined")
        {
            if (string.IsNullOrWhiteSpace(entName))
            {
                logEntry.Log("Execute Delete Request failed, Entity Name cannot be null or empty", TraceEventType.Error);
                return false;
            }
            if (entId == Guid.Empty)
            {
                logEntry.Log("Execute Delete Request failed, Guid to delete cannot be null or empty", TraceEventType.Error);
                return false;
            }

            DeleteRequest req = new DeleteRequest();
            req.Target = new EntityReference(entName, entId);

            DeleteResponse resp = (DeleteResponse)CdsCommand_Execute(req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entName, entId));
            if (resp != null)
            {
                return true;
            }
            return false;
        }

        #endregion


        /// <summary>
        /// <para>
        /// Imports a CDS solution to the CDS Server currently connected.
        /// <para>*** Note: this is a blocking call and will take time to Import to CDS ***</para>
        /// </para>
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on CDS organizations version 7.2 or higher.  This imports the CDS solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="useAsync">Requires the use of an Async Job to do the import. </param>
        /// <param name="extraParameters">Extra parameters</param>
        /// <returns>Returns the Import Solution Job ID.  To find the status of the job, query the ImportJob Entity using GetEntityDataByID using the returned value of this method</returns>
        internal Guid ImportSolutionToCdsImpl(string solutionPath, out Guid importId, bool activatePlugIns, bool overwriteUnManagedCustomizations, bool skipDependancyOnProductUpdateCheckOnInstall, bool importAsHoldingSolution, bool isInternalUpgrade, bool useAsync, Dictionary<string, object> extraParameters)
        {
            logEntry.ResetLastError();  // Reset Last Error 
            importId = Guid.Empty;
            if (_CdsService == null)
            {
                logEntry.Log("CDS Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                this.logEntry.Log("************ Exception on ImportSolutionToCds, SolutionPath is required", TraceEventType.Error);
                return Guid.Empty;
            }

            //Extract extra parameters if they exist
            string solutionName = string.Empty;
            LayerDesiredOrder desiredLayerOrder = null;

            if (extraParameters != null)
            {
                solutionName = extraParameters.ContainsKey(SOLUTIONNAMEPARAM) ? extraParameters[SOLUTIONNAMEPARAM].ToString() : string.Empty;
                desiredLayerOrder = extraParameters.ContainsKey(DESIREDLAYERORDERPARAM) ? extraParameters[DESIREDLAYERORDERPARAM] as LayerDesiredOrder : null;
            }

            string solutionNameForLogging = string.IsNullOrWhiteSpace(solutionName) ? string.Empty : string.Concat(solutionName, " - ");

            // try to load the file from the file system 
            if (File.Exists(solutionPath))
            {
                try
                {
                    importId = Guid.NewGuid();
                    byte[] fileData = File.ReadAllBytes(solutionPath);
                    ImportSolutionRequest SolutionImportRequest = new ImportSolutionRequest()
                    {
                        CustomizationFile = fileData,
                        PublishWorkflows = activatePlugIns,
                        ImportJobId = importId,
                        OverwriteUnmanagedCustomizations = overwriteUnManagedCustomizations
                    };

                    //If the desiredLayerOrder is null don't add it to the request. This ensures backward compatibility. It makes old packages work on old builds
                    if (desiredLayerOrder != null)
                    {
                        //If package contains the LayerDesiredOrder hint but the server doesn't support the new message, we want the package to fail
                        //The server will throw - "Unrecognized request parameter: LayerDesiredOrder" - That's the desired behavior
                        //The hint is only enforced on the first time a solution is added to the org. If we allow it to go, the import will succeed, but the desired state won't be achieved
                        SolutionImportRequest.LayerDesiredOrder = desiredLayerOrder;

                        string solutionsInHint = string.Join(",", desiredLayerOrder.Solutions.Select(n => n.Name).ToArray());

                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{0}DesiredLayerOrder clause present: Type: {1}, Solutions: {2}", solutionNameForLogging, desiredLayerOrder.Type, solutionsInHint), TraceEventType.Verbose);
                    }

                    if (IsBatchOperationsAvailable)
                    {
                        // Support for features added in UR12
                        SolutionImportRequest.SkipProductUpdateDependencies = skipDependancyOnProductUpdateCheckOnInstall;
                    }

                    if (importAsHoldingSolution)  // If Import as Holding is set.. 
                    {
                        // Check for Min version of CDS for support of Import as Holding solution. 
                        Version minVersion = new Version("7.2.0.9");
                        if (CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= minVersion))
                        {
                            // Use Parameters to add the property here to support the underlying Xrm API on the incorrect version. 
                            SolutionImportRequest.Parameters.Add("HoldingSolution", importAsHoldingSolution);
                        }
                    }

                    // Set IsInternalUpgrade flag on request only for upgrade scenario for V9 only.
                    if (isInternalUpgrade)
                    {
                        if (CdsConnectionSvc.OrganizationVersion != null && (CdsConnectionSvc.OrganizationVersion >= new Version("9.0.0.0")))
                        {
                            SolutionImportRequest.Parameters["IsInternalUpgrade"] = true;
                        }
                    }

                    if (useAsync)
                    {
                        // Assign Tracking ID
                        Guid requestTrackingId = Guid.NewGuid();
                        SolutionImportRequest.RequestId = requestTrackingId;
                        // Creating Async Solution Import request. 
                        ExecuteAsyncRequest req = new ExecuteAsyncRequest() { Request = SolutionImportRequest };
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1}Created Async ImportSolutionRequest : RequestID={0} ",
                            requestTrackingId.ToString(), solutionNameForLogging), TraceEventType.Verbose);
                        ExecuteAsyncResponse asyncResp = (ExecuteAsyncResponse)CdsCommand_Execute(req, solutionNameForLogging + "Executing Request for ImportSolutionToCdsAsync : ");
                        if (asyncResp == null)
                            return Guid.Empty;
                        else
                            return asyncResp.AsyncJobId;
                    }
                    else
                    {
                        ImportSolutionResponse resp = (ImportSolutionResponse)CdsCommand_Execute(SolutionImportRequest, solutionNameForLogging + "Executing ImportSolutionRequest for ImportSolutionToCds");
                        if (resp == null)
                            return Guid.Empty;
                        else
                            return importId;
                    }
                }
                #region Exception handlers for files
                catch (UnauthorizedAccessException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, Unauthorized Access to file: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (ArgumentNullException ex)
                {
                    this.logEntry.Log("************ Exception on ImportSolutionToCds, File path not specified", TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (ArgumentException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File path is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (PathTooLongException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File path is too long. Paths must be less than 248 characters, and file names must be less than 260 characters\n{0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (DirectoryNotFoundException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File path is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (FileNotFoundException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File Not Found: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (NotSupportedException ex)
                {
                    this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File path or name is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                #endregion
            }
            else
            {
                this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolutionToCds, File path specified in dataMapXml is not found: {0}", solutionPath), TraceEventType.Error);
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Executes a CDS Create Request and returns the organization response object. 
        /// </summary>
        /// <param name="req">Request to run</param>
        /// <param name="errorStringCheck">Formatted Error string</param>
        /// <returns>Result of create request or null.</returns>
        internal OrganizationResponse CdsCommand_Execute(OrganizationRequest req, string errorStringCheck)
        {
            Guid requestTrackingId = Guid.NewGuid();
            OrganizationResponse resp = null;
            Stopwatch logDt = new Stopwatch();
            TimeSpan LockWait = TimeSpan.Zero;
            int retryCount = 0;
            bool retry = false;

            do
            {
                try
                {
                    _retryPauseTimeRunning = _retryPauseTime; // Set the default time for each loop. 
                    retry = false;
                    if (!_disableConnectionLocking)
                        if (_lockObject == null)
                            _lockObject = new object();

                    if (CdsConnectionSvc != null && CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                        CdsConnectionSvc.CalledbyExecuteRequest = true;
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

                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - {0}{1}: RequestID={2} {3}",
                        req.RequestName,
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestTrackingId.ToString(),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : ""
                        ), TraceEventType.Verbose);

                    logDt.Restart();
                    if (!_disableConnectionLocking) // Allow Developer to override Cross Thread Safeties
                        lock (_lockObject)
                        {
                            if (logDt.Elapsed > TimeSpan.FromMilliseconds(0000010))
                                LockWait = logDt.Elapsed;
                            logDt.Restart();
                            rsp = _CdsService.Execute(req);
                        }
                    else
                        rsp = _CdsService.Execute(req);

                    logDt.Stop();
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - {0}{2}: {5}RequestID={3} {4}: duration={1}",
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
                    retry = ShouldRetry(req, ex, retryCount, out isThrottled);
                    if (retry)
                    {
                        RetryRequest(req, requestTrackingId, LockWait, logDt, ex, errorStringCheck, ref retryCount, isThrottled);
                    }
                    else
                    {
                        LogRetry(retryCount, req, true, isThrottled: isThrottled);
                        LogException(req, ex, errorStringCheck);
                        //keep it in end so that LastCdsError could be a better message.
                        LogFailure(req, requestTrackingId, LockWait, logDt, ex, errorStringCheck, true);
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
            if (retryCount >= _maxRetryCount)
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
            else if (ex is FaultException<OrganizationServiceFault>)
            {
                var OrgEx = (FaultException<OrganizationServiceFault>)ex;
                if (OrgEx.Detail.ErrorCode == RateLimitExceededErrorCode ||
                    OrgEx.Detail.ErrorCode == TimeLimitExceededErrorCode ||
                    OrgEx.Detail.ErrorCode == ConcurrencyLimitExceededErrorCode)
                {
                    // Error was raised by a instance throttle trigger. 
                    if (OrgEx.Detail.ErrorCode == RateLimitExceededErrorCode)
                    {
                        // Use Retry-After delay when specified
                        _retryPauseTimeRunning = (TimeSpan)OrgEx.Detail.ErrorDetails["Retry-After"];
                    }
                    else
                    {
                        // else use exponential back off delay
                        _retryPauseTimeRunning = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
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

        /// <summary>
        /// retry request
        /// </summary>
        /// <param name="req">request</param>
        /// <param name="requestTrackingId">requestTrackingId</param>
        /// <param name="LockWait">LockWait</param>
        /// <param name="logDt">logDt</param>
        /// <param name="ex">ex</param>
        /// <param name="errorStringCheck">errorStringCheck</param>
        /// <param name="retryCount">retryCount</param>
        /// <param name="isThrottled">when set indicated this was caused by a Throttle</param>
        private void RetryRequest(OrganizationRequest req, Guid requestTrackingId, TimeSpan LockWait, Stopwatch logDt, Exception ex, string errorStringCheck, ref int retryCount, bool isThrottled , string webUriReq = "")
        {
            retryCount++;
            LogFailure(req, requestTrackingId, LockWait, logDt, ex, errorStringCheck, webUriMessageReq:webUriReq);
            LogRetry(retryCount, req, isThrottled: isThrottled);
            System.Threading.Thread.Sleep(_retryPauseTimeRunning);
        }

        /// <summary>
        /// log failure message
        /// </summary>
        /// <param name="req">request</param>
        /// <param name="requestTrackingId">requestTrackingId</param>
        /// <param name="LockWait">LockWait</param>
        /// <param name="logDt">logDt</param>
        /// <param name="ex">ex</param>
        /// <param name="errorStringCheck">errorStringCheck</param>
        /// <param name="isTerminalFailure">represents if it is final retry failure</param>
        private void LogFailure(OrganizationRequest req, Guid requestTrackingId, TimeSpan LockWait, Stopwatch logDt, Exception ex, string errorStringCheck, bool isTerminalFailure = false, string webUriMessageReq = "")
        {
            if (req != null)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{6}Failed to Execute Command - {0}{1} : {5}RequestID={2} {3}: {8} duration={4} ExceptionMessage = {7}", req.RequestName, _disableConnectionLocking ? " : DisableCrossThreadSafeties=true :" : string.Empty, requestTrackingId.ToString(), LockWait == TimeSpan.Zero ? string.Empty : string.Format(": LockWaitDuration={0} ", LockWait.ToString()), logDt.Elapsed.ToString(),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : "", isTerminalFailure ? "[TerminalFailure] " : "", ex.Message, errorStringCheck), TraceEventType.Error, ex);
            }
            else if (ex is HttpOperationException httpOperationException)
            {
                JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
                var errorMessage = contentBody["error"]["message"].ToString();

                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{6}Failed to Execute Command - {0}{1} : {5}RequestID={2} {3}: {8} duration={4} ExceptionMessage = {7}",
                    webUriMessageReq, 
                    _disableConnectionLocking ? " : DisableCrossThreadSafeties=true :" : string.Empty, 
                    requestTrackingId.ToString(), 
                    string.Empty, 
                    logDt.Elapsed.ToString(),
                    SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : "", 
                    isTerminalFailure ? "[TerminalFailure] " : "", 
                    errorMessage, 
                    errorStringCheck), 
                    TraceEventType.Error, ex);
            }
        }
        /// <summary>
        /// log retry message
        /// </summary>
        /// <param name="retryCount">retryCount</param>
        /// <param name="req">request</param>
        /// <param name="isTerminalFailure">represents if it is final retry failure</param>
        /// <param name="isThrottled">If set, indicates that this was caused by a throttle</param>
        private void LogRetry(int retryCount, OrganizationRequest req, bool isTerminalFailure = false, bool isThrottled = false , string webUriMessageReq = "")
        {
            string ReqName = string.Empty;
            if (req != null)
                ReqName = req.RequestName;
            else
                ReqName = webUriMessageReq;

            if (retryCount == 0)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "No retry attempted for Command {0}", ReqName), TraceEventType.Verbose);
            }
            else if (isTerminalFailure == true)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Retry Completed at {0} for Command {1}", $"Retry No={retryCount}", ReqName), TraceEventType.Verbose);
            }
            else
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{0} for Command {1}", $"Retry No={retryCount} Retry=Started IsThrottle={isThrottled} Delay={_retryPauseTimeRunning.ToString()}", ReqName), TraceEventType.Verbose);
            }
        }

        /// <summary>
        /// log exception message
        /// </summary>
        /// <param name="req">request</param>
        /// <param name="ex">exception</param>
        /// <param name="errorStringCheck">errorStringCheck</param>
        private void LogException(OrganizationRequest req, Exception ex, string errorStringCheck, string webUriMessageReq = "")
        {
            if (req != null)
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ {3} - {2} : {0} |=> {1}", errorStringCheck, ex.Message, req.RequestName, ex.GetType().Name), TraceEventType.Error, ex);
            }
            else if (ex is HttpOperationException httpOperationException)
            {
                JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
                var errorMessage = contentBody["error"]["message"].ToString();
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ {3} - {2} : {0} |=> {1}", errorStringCheck, errorMessage, webUriMessageReq, ex.GetType().Name), TraceEventType.Error, ex);
            }
            else
            {
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ {3} - {2} : {0} |=> {1}", errorStringCheck, ex.Message, "UNKNOWN" , ex.GetType().Name), TraceEventType.Error, ex);
            }
        }

        /// <summary>
        /// Makes a web request to the connected XRM instance.
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
        /// <param name="method">Http Method you want to pass.</param>
        /// <param name="body">Content your passing to the request</param>
        /// <param name="customHeaders">Headers in addition to the default headers added by for Executing a web request</param>
        /// <param name="errorStringCheck"></param>
        /// <param name="contentType">Content Type to pass in if executing a post request</param>
        /// <returns></returns>
        internal async Task<HttpResponseMessage> CdsCommand_WebExecute(string queryString, string body, HttpMethod method, Dictionary<string, List<string>> customHeaders, string contentType, string errorStringCheck, Guid requestTrackingId = default(Guid))
        {
            Stopwatch logDt = new Stopwatch();
            int retryCount = 0;
            bool retry = false;

            if (requestTrackingId == Guid.Empty)
                requestTrackingId = Guid.NewGuid();


            // Default Odata 4.0 headers. 
            Dictionary<string, string> defaultODataHeaders = new Dictionary<string, string>();
            defaultODataHeaders.Add("Accept", "application/json");
            defaultODataHeaders.Add("OData-MaxVersion", "4.0");
            defaultODataHeaders.Add("OData-Version", "4.0");
            defaultODataHeaders.Add("If-None-Match", "");

            // Supported Version Check. 
            if (!(ConnectedOrgVersion > _minWebAPISupportedVersion))
            {
                logEntry.Log(string.Format("Web API Service is not supported by the CdsServiceClient in {0} version of XRM", ConnectedOrgVersion), TraceEventType.Error, new InvalidOperationException(string.Format("Web API Service is not supported by the CdsServiceClient in {0} version of XRM", ConnectedOrgVersion)));
                return null;
            }

            if (CdsConnectionSvc != null && CdsConnectionSvc.AuthenticationTypeInUse == AuthenticationType.OAuth)
                CdsConnectionSvc.CalledbyExecuteRequest = true;

            // Format URI for request.
            Uri TargetUri = null;
            string ConnectUri = this.ConnectedOrgPublishedEndpoints[EndpointType.OrganizationDataService];
            if (!ConnectUri.Contains("/data/"))
            {
                Uri tempUri = new Uri(ConnectUri);
                // Not using GD,  update for web API
                ConnectUri = string.Format(CdsConnectionSvc.BaseWebAPIDataFormat, $"{tempUri.Scheme}://{tempUri.DnsSafeHost}", ConnectedOrgVersion.ToString(2));
            }

            if (!Uri.TryCreate(string.Format("{0}{1}", ConnectUri, queryString), UriKind.Absolute, out TargetUri))
            {
                logEntry.Log(string.Format("Invalid URI formed for request - {0}", string.Format("{2} {0}{1}", ConnectUri, queryString, method)), TraceEventType.Error);
                return null;
            }

            // Add Headers. 
            if (customHeaders == null)
                customHeaders = new Dictionary<string, List<string>>();
            else
            {
                if (customHeaders.ContainsKey(Utilities.CDSRequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER))
                {
                    customHeaders.Remove(Utilities.CDSRequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER);
                    logEntry.Log(string.Format("Removing customer header {0} - Use CallerAADObjectId property instead", Utilities.CDSRequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER));
                }

                if (customHeaders.ContainsKey(Utilities.CDSRequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER))
                {
                    customHeaders.Remove(Utilities.CDSRequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER);
                    logEntry.Log(string.Format("Removing customer header {0} - Use CallerId property instead", Utilities.CDSRequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER));
                }

                if (customHeaders.ContainsKey(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY))
                {
                    customHeaders.Remove(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY);
                    logEntry.Log(string.Format("Removing customer header {0} - Use ForceServerMetadataCacheConsistency property instead", Utilities.CDSRequestHeaders.FORCE_CONSISTENCY));
                }
            }

            // Add Default headers. 
            foreach (var hdr in defaultODataHeaders)
            {
                if (customHeaders.ContainsKey(hdr.Key))
                    customHeaders.Remove(hdr.Key);

                customHeaders.Add(hdr.Key, new List<string>() { hdr.Value });
            }

            // Add headers. 
            if (CallerId != Guid.Empty)
            {
                {
                    customHeaders.Add(Utilities.CDSRequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER, new List<string>() { CallerId.ToString() });
                }
            }
            else
            {
                if (CallerAADObjectId.HasValue)
                {
                    // Value in Caller object ID. 
                    if (CallerAADObjectId.Value != null && CallerAADObjectId.Value != Guid.Empty)
                    {
                        customHeaders.Add(Utilities.CDSRequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER, new List<string>() { CallerAADObjectId.ToString() });
                    }
                }
            }

            // Add authorization header. 
            if (!customHeaders.ContainsKey(Utilities.CDSRequestHeaders.AUTHORIZATION_HEADER))
                customHeaders.Add(Utilities.CDSRequestHeaders.AUTHORIZATION_HEADER, new List<string>() { string.Format("Bearer {0}", await CdsConnectionSvc.RefreshWebProxyClientToken()) });

            // Add tracking headers
            // Request id
            if (!customHeaders.ContainsKey(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID))
            {
                customHeaders.Add(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID, new List<string>() { requestTrackingId.ToString() });
            }
            else
            {
                Guid guTempId = Guid.Empty;
                List<string> keyValues = customHeaders[Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID];
                if (keyValues != null && keyValues.Count > 0)
                    Guid.TryParse(keyValues.First(), out guTempId);

                if (guTempId == Guid.Empty) // passed in value did not parse.
                {
                    // Assign Tracking Guid in 
                    customHeaders.Remove(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID);
                    customHeaders.Add(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID, new List<string>() { requestTrackingId.ToString() });
                }
                else
                    requestTrackingId = guTempId;

            }
            // Session id. 
            if (SessionTrackingId.HasValue && SessionTrackingId != Guid.Empty && !customHeaders.ContainsKey(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID))
                customHeaders.Add(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID, new List<string>() { SessionTrackingId.Value.ToString() });

            // Add force Consistency 
            if (ForceServerMetadataCacheConsistency && !customHeaders.ContainsKey(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY))
                customHeaders.Add(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY, new List<string>() { "Strong" });

            HttpResponseMessage resp = null;
            do
            {
                logDt.Restart(); // start clock. 

                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - {0}{1}: RequestID={2} {3}",
                        $"{method} {queryString}",
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestTrackingId.ToString(),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : ""
                        ), TraceEventType.Verbose);
                try
                {
                    resp = await CdsConnectionService.ExecuteHttpRequestAsync(TargetUri.ToString(), method, body: body, customHeaders: customHeaders, logSink: logEntry, contentType: contentType, requestTrackingId: requestTrackingId, sessionTrackingId: SessionTrackingId.HasValue ? SessionTrackingId.Value : Guid.Empty, suppressDebugMessage:true , providedHttpClient:CdsConnectionSvc.WebApiHttpClient).ConfigureAwait(false);

                    logDt.Stop();
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - {0}{2}: {4}RequestID={3} : duration={1}",
                        $"{method} {queryString}",
                        logDt.Elapsed.ToString(),
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestTrackingId.ToString(),
                        SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : ""
                        ), TraceEventType.Verbose);
                }
                catch (System.Exception ex)
                {
                    if (ex is HttpOperationException httpOperationException)
                    {
                        bool isThrottled = false;
                        retry = ShouldRetryWebAPI(ex, retryCount, out isThrottled);
                        if (retry)
                        {
                            RetryRequest(null, requestTrackingId, TimeSpan.Zero, logDt, ex, errorStringCheck, ref retryCount, isThrottled, webUriReq: $"{method} {queryString}");
                        }
                        else
                        {
                            LogRetry(retryCount, null, true, isThrottled: isThrottled, webUriMessageReq: $"{method} {queryString}");
                            LogException(null, ex, errorStringCheck, webUriMessageReq: $"{method} {queryString}");
                            LogFailure(null, requestTrackingId, TimeSpan.Zero, logDt, ex, errorStringCheck, true, webUriMessageReq: $"{method} {queryString}");
                        }
                        resp = null;
                    }
                    else
                    {
                        retry = false; 
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Execute Command - {3} {0} : {2}RequestID={1}", queryString, requestTrackingId.ToString(), SessionTrackingId.HasValue && SessionTrackingId.Value != Guid.Empty ? $"SessionID={SessionTrackingId.Value.ToString()} : " : "", method), TraceEventType.Verbose);
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception - {2} : {0} |=> {1}", errorStringCheck, ex.Message, queryString), TraceEventType.Error, ex);
                        return null;
                    }
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
        /// <param name="ex">exception</param>
        /// <param name="retryCount">retry count</param>
        /// <param name="isThrottlingRetry">when true, indicates that the retry was caused by a throttle tripping.</param>
        /// <returns></returns>
        private bool ShouldRetryWebAPI(Exception ex, int retryCount, out bool isThrottlingRetry)
        {
            isThrottlingRetry = false;
            if (retryCount >= _maxRetryCount)
            {
                return false;
            }

            if (ex is HttpOperationException httpOperationException)
            {
                JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
                var errorCode = contentBody["error"]["code"].ToString();
                var errorMessage = contentBody["error"]["message"].ToString();
                //if (((string.Equals(req.RequestName.ToLower(), "retrieve"))
                //    && ((Utilities.ShouldAutoRetryRetrieveByEntityName(((Microsoft.Xrm.Sdk.EntityReference)req.Parameters["Target"]).LogicalName))))
                //    || (string.Equals(req.RequestName.ToLower(), "retrievemultiple")
                //    && (
                //            ((((RetrieveMultipleRequest)req).Query is FetchExpression) && Utilities.ShouldAutoRetryRetrieveByEntityName(((FetchExpression)((RetrieveMultipleRequest)req).Query).Query))
                //        || ((((RetrieveMultipleRequest)req).Query is QueryExpression) && Utilities.ShouldAutoRetryRetrieveByEntityName(((QueryExpression)((RetrieveMultipleRequest)req).Query).EntityName))
                //        )))
                //    return true;
                //else 
                if (errorCode.Equals("-2147204784") || errorCode.Equals("-2146233087") && errorMessage.Contains("SQL"))
                    return true;
                else if (httpOperationException.Response.StatusCode == HttpStatusCode.BadGateway)
                    return true;
                else if ((int)httpOperationException.Response.StatusCode == 429 ||
                    httpOperationException.Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Throttled. need to react according. 
                    if (errorCode == RateLimitExceededErrorCode.ToString() ||
                        errorCode == TimeLimitExceededErrorCode.ToString() ||
                        errorCode == ConcurrencyLimitExceededErrorCode.ToString())
                    {
                        if (errorCode == RateLimitExceededErrorCode.ToString())
                        {
                            // Use Retry-After delay when specified
                            if (httpOperationException.Response.Headers.ContainsKey("Retry-After"))
                                _retryPauseTimeRunning = TimeSpan.Parse(httpOperationException.Response.Headers["Retry-After"].FirstOrDefault());
                            _retryPauseTimeRunning = TimeSpan.FromSeconds(30); // default timespan. 
                        }
                        else
                        {
                            // else use exponential back off delay
                            _retryPauseTimeRunning = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                        }
                        isThrottlingRetry = true;
                        return true;
                    }
                }
            }
            else
                return false;

            return false; 
        }


        #endregion

        #endregion

        #region Support classes

        /// <summary>
        /// PickList data
        /// </summary>
        public sealed class PickListMetaElement
        {
            /// <summary>
            /// Current value of the PickList Item
            /// </summary>
            public string ActualValue { get; set; }
            /// <summary>
            /// Displayed Label
            /// </summary>
            public string PickListLabel { get; set; }
            /// <summary>
            /// Displayed value for the PickList
            /// </summary>
            public string DisplayValue { get; set; }
            /// <summary>
            /// Array of Potential Pick List Items.
            /// </summary>
            public List<PickListItem> Items { get; set; }

            /// <summary>
            /// Default Constructor
            /// </summary>
            public PickListMetaElement()
            {
                Items = new List<PickListItem>();
            }

            /// <summary>
            /// Constructs a PickList item with data. 
            /// </summary>
            /// <param name="actualValue"></param>
            /// <param name="displayValue"></param>
            /// <param name="pickListLabel"></param>
            public PickListMetaElement(string actualValue, string displayValue, string pickListLabel)
            {
                Items = new List<PickListItem>();
                ActualValue = actualValue;
                PickListLabel = pickListLabel;
                DisplayValue = displayValue;
            }
        }

        /// <summary>
        /// PickList Item
        /// </summary>
        public sealed class PickListItem
        {
            /// <summary>
            /// Display label for the PickList Item
            /// </summary>
            public string DisplayLabel { get; set; }
            /// <summary>
            /// ID of the picklist item
            /// </summary>
            public int PickListItemId { get; set; }

            /// <summary>
            /// Default Constructor
            /// </summary>
            public PickListItem()
            {
            }

            /// <summary>
            /// Constructor with data. 
            /// </summary>
            /// <param name="label"></param>
            /// <param name="id"></param>
            public PickListItem(string label, int id)
            {
                DisplayLabel = label;
                PickListItemId = id;
            }
        }

        /// <summary>
        /// CDS Filter class.
        /// </summary>
        public sealed class CdsSearchFilter
        {
            /// <summary>
            /// List of CDS Filter conditions
            /// </summary>
            public List<CdsFilterConditionItem> SearchConditions { get; set; }
            /// <summary>
            /// CDS Filter Operator
            /// </summary>
            public LogicalOperator FilterOperator { get; set; }

            /// <summary>
            /// Creates an empty CDS Search Filter. 
            /// </summary>
            public CdsSearchFilter()
            {
                SearchConditions = new List<CdsFilterConditionItem>();
            }
        }

        /// <summary>
        /// CDS Filter item. 
        /// </summary>
        public sealed class CdsFilterConditionItem
        {
            /// <summary>
            /// CDS Field name to Filter on
            /// </summary>
            public string FieldName { get; set; }
            /// <summary>
            /// Value to use for the Filter
            /// </summary>
            public object FieldValue { get; set; }
            /// <summary>
            /// CDS Operator to apply
            /// </summary>
            public ConditionOperator FieldOperator { get; set; }

        }

        /// <summary>
        /// Describes an import request for CDS
        /// </summary>
        public sealed class ImportRequest
        {
            #region Vars
            // Import Items..
            /// <summary>
            /// Name of the Import Request.  this Name will appear in CDS
            /// </summary>
            public string ImportName { get; set; }
            /// <summary>
            /// Sets or gets the Import Mode.
            /// </summary>
            public ImportMode Mode { get; set; }

            // Import Map Items. 
            /// <summary>
            /// ID of the DataMap to use
            /// </summary>
            public Guid DataMapFileId { get; set; }
            /// <summary>
            /// Name of the DataMap File to use
            /// ID or Name is required
            /// </summary>
            public string DataMapFileName { get; set; }

            /// <summary>
            /// if True, infers the map from the type of entity requested.. 
            /// </summary>
            public bool UseSystemMap { get; set; }

            /// <summary>
            /// List of files to import in this job,  there must be at least one. 
            /// </summary>
            public List<ImportFileItem> Files { get; set; }


            #endregion

            /// <summary>
            /// Mode of the Import, Update or Create
            /// </summary>
            public enum ImportMode
            {
                /// <summary>
                /// Create a new Import
                /// </summary>
                Create = 0,
                /// <summary>
                /// Update to Imported Items
                /// </summary>
                Update = 1
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            public ImportRequest()
            {
                Files = new List<ImportFileItem>();
            }

        }

        /// <summary>
        /// Describes an Individual Import Item. 
        /// </summary>
        public class ImportFileItem
        {
            /// <summary>
            /// File Name of Individual file
            /// </summary>
            public string FileName { get; set; }
            /// <summary>
            /// Type of Import file.. XML or CSV
            /// </summary>
            public FileTypeCode FileType { get; set; }
            /// <summary>
            /// This is the CSV file you wish to import,
            /// </summary>
            public string FileContentToImport { get; set; }
            /// <summary>
            /// This enabled duplicate detection rules
            /// </summary>
            public bool EnableDuplicateDetection { get; set; }
            /// <summary>
            /// Name of the entity that Originated the data. 
            /// </summary>
            public string SourceEntityName { get; set; }
            /// <summary>
            /// Name of the entity that Target Entity the data. 
            /// </summary>
            public string TargetEntityName { get; set; }
            /// <summary>
            /// This is the delimiter for the Data,
            /// </summary>
            public DataDelimiterCode DataDelimiter { get; set; }
            /// <summary>
            /// this is the field separator
            /// </summary>
            public FieldDelimiterCode FieldDelimiter { get; set; }
            /// <summary>
            /// Is the first row of the CSV the RowHeader?
            /// </summary>
            public bool IsFirstRowHeader { get; set; }
            /// <summary>
            /// UserID or Team ID of the Record Owner ( from systemuser ) 
            /// </summary>
            public Guid RecordOwner { get; set; }
            /// <summary>
            /// Set true if the Record Owner is a Team
            /// </summary>
            public bool IsRecordOwnerATeam { get; set; }

            /// <summary>
            /// Key used to delimit data in the import file
            /// </summary>
            public enum DataDelimiterCode
            {
                /// <summary>
                /// Specifies "
                /// </summary>
                DoubleQuotes = 1,   // "
                /// <summary>
                /// Specifies no delimiter
                /// </summary>
                None = 2,           // 
                /// <summary>
                /// Specifies '
                /// </summary>
                SingleQuote = 3     // ' 
            }

            /// <summary>
            /// Key used to delimit fields in the import file
            /// </summary>
            public enum FieldDelimiterCode
            {
                /// <summary>
                /// Specifies :
                /// </summary>
                Colon = 1,
                /// <summary>
                /// Specifies ,
                /// </summary>
                Comma = 2,
                /// <summary>
                /// Specifies ' 
                /// </summary>
                SingleQuote = 3
            }

            /// <summary>
            /// Type if file described in the FileContentToImport
            /// </summary>
            public enum FileTypeCode
            {
                /// <summary>
                /// CSV File Type
                /// </summary>
                CSV = 0,
                /// <summary>
                /// XML File type
                /// </summary>
                XML = 1
            }

        }

        /// <summary>
        /// Logical Search Pram to apply to over all search. 
        /// </summary>
        public enum LogicalSearchOperator
        {
            /// <summary>
            /// Do not apply the Search Operator
            /// </summary>
            None = 0,
            /// <summary>
            /// Or Search
            /// </summary>
            Or = 1,
            /// <summary>
            /// And Search
            /// </summary>
            And = 2
        }

        /// <summary>
        /// Logical Search Pram to apply to over all search. 
        /// </summary>
        public enum LogicalSortOrder
        {
            /// <summary>
            /// Sort in Ascending
            /// </summary>
            Ascending = 0,
            /// <summary>
            /// Sort in Descending
            /// </summary>
            Descending = 1,
        }

        /// <summary>
        /// Used with GetFormIdsForEntity Call
        /// </summary>
        public enum FormTypeId
        {
            /// <summary>
            /// Dashboard form
            /// </summary>
            Dashboard = 0,
            /// <summary>
            /// Appointment book, for service requests. 
            /// </summary>
            AppointmentBook = 1,
            /// <summary>
            /// Main or default form
            /// </summary>
            Main = 2,
            //MiniCampaignBo = 3,  // Not used in 2011
            //Preview = 4,          // Not used in 2011
            /// <summary>
            /// Mobile default form
            /// </summary>
            Mobile = 5,
            /// <summary>
            /// User defined forms
            /// </summary>
            Other = 100
        }

        #endregion

        #region IOrganzation Service Proxy - Proxy object
        /// <summary>
        /// Issues an Associate Request to CDS.
        /// </summary>
        /// <param name="entityName">Entity Name to associate to</param>
        /// <param name="entityId">ID if Entity to associate to</param>
        /// <param name="relationship">Relationship Name</param>
        /// <param name="relatedEntities">Entities to associate</param>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            AssociateResponse resp = (AssociateResponse)ExecuteCdsOrganizationRequest(new AssociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }, "Associate To CDS via IOrganizationService");
            if (resp == null)
                throw LastCdsException;
        }

        /// <summary>
        /// Issues a Create request to CDS
        /// </summary>
        /// <param name="entity">Entity to create</param>
        /// <returns>ID of newly created entity</returns>
        public Guid Create(Entity entity)
        {
            // Relay to Update request. 
            CreateResponse resp = (CreateResponse)ExecuteCdsOrganizationRequest(
                new CreateRequest() 
                { 
                    Target = entity 
                }
                , "Create To CDS via IOrganizationService" 
                , useWebAPI:true);
            if (resp == null)
                throw LastCdsException;

            return resp.id;
        }

        /// <summary>
        /// Issues a Delete request to CDS 
        /// </summary>
        /// <param name="entityName">Entity name to delete</param>
        /// <param name="id">ID if entity to delete</param>
        public void Delete(string entityName, Guid id)
        {
            DeleteResponse resp = (DeleteResponse)ExecuteCdsOrganizationRequest(
                new DeleteRequest()
                {
                    Target = new EntityReference(entityName, id)
                }
                , "Delete Request to CDS via IOrganizationService"
                , useWebAPI: true);
            if (resp == null)
                throw LastCdsException;
        }

        /// <summary>
        /// Issues a Disassociate Request to CDS.
        /// </summary>
        /// <param name="entityName">Entity Name to disassociate from</param>
        /// <param name="entityId">ID if Entity to disassociate from</param>
        /// <param name="relationship">Relationship Name</param>
        /// <param name="relatedEntities">Entities to disassociate</param>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            DisassociateResponse resp = (DisassociateResponse)ExecuteCdsOrganizationRequest(new DisassociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            }, "Disassociate To CDS via IOrganizationService");
            if (resp == null)
                throw LastCdsException;
        }

        /// <summary>
        /// Executes a general organization request
        /// </summary>
        /// <param name="request">Request object</param>
        /// <returns>Response object</returns>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            OrganizationResponse resp = ExecuteCdsOrganizationRequest(request, string.Format("Execute ({0}) request to CDS from IOrganizationService", request.RequestName) , useWebAPI:true);
            if (resp == null)
                throw LastCdsException;
            return resp;
        }

        /// <summary>
        /// Issues a Retrieve Request to CDS 
        /// </summary>
        /// <param name="entityName">Entity name to request</param>
        /// <param name="id">ID of the entity to request</param>
        /// <param name="columnSet">ColumnSet to request</param>
        /// <returns>Entity object</returns>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            RetrieveResponse resp = (RetrieveResponse)ExecuteCdsOrganizationRequest(
                new RetrieveRequest()
                {
                    ColumnSet = columnSet,
                    Target = new EntityReference(entityName, id)
                }
                , "Retrieve Request to CDS via IOrganizationService");
            if (resp == null)
                throw LastCdsException;

            return resp.Entity;
        }

        /// <summary>
        /// Issues a RetrieveMultiple Request to CDS
        /// </summary>
        /// <param name="query">Query to Request</param>
        /// <returns>EntityCollection Result</returns>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            RetrieveMultipleResponse resp = (RetrieveMultipleResponse)ExecuteCdsOrganizationRequest(new RetrieveMultipleRequest() { Query = query }, "RetrieveMultiple to CDS via IOrganizationService");
            if (resp == null)
                throw LastCdsException;

            return resp.EntityCollection;
        }

        /// <summary>
        /// Issues an update to CDS. 
        /// </summary>
        /// <param name="entity">Entity to update into CDS</param>
        public void Update(Entity entity)
        {
            // Relay to Update request. 
            UpdateResponse resp = (UpdateResponse)ExecuteCdsOrganizationRequest(
                new UpdateRequest() 
                { 
                    Target = entity 
                }
                , "UpdateRequest To CDS via IOrganizationService"
                , useWebAPI:true);

            if (resp == null)
                throw LastCdsException;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_CdsServiceClientTokenCache != null)
                        _CdsServiceClientTokenCache.Dispose();


                    if (logEntry != null)
                    {
                        logEntry.Dispose();
                    }

                    if (CdsConnectionSvc != null)
                    {
                        try
                        {
                            if (CdsConnectionSvc.CdsWebClient != null)
                                CdsConnectionSvc.CdsWebClient.Dispose();
                        }
                        catch { }
                        CdsConnectionSvc.Dispose();
                    }

                    CdsConnectionSvc = null;

                }
                disposedValue = true;
            }
        }


        /// <summary>
        /// Disposed the resources used by the CdsServiceClient. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
        #endregion

    }
}
