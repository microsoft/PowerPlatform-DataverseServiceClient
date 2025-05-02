#region using
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache;
using Microsoft.PowerPlatform.Dataverse.Client.Connector;
using Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises;
using Microsoft.PowerPlatform.Dataverse.Client.Exceptions;
using Microsoft.PowerPlatform.Dataverse.Client.HttpUtils;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.WebServiceClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    ///  Decision switch for the sort of Auth to login to Dataverse with
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// Active Directory Auth
        /// </summary>
        AD = 0,
        /// <summary>
        /// OAuth based Auth
        /// </summary>
        OAuth = 5,
        /// <summary>
        /// Certificate based Auth
        /// </summary>
        Certificate = 6,
        /// <summary>
        /// Client Id + Secret Auth type.
        /// </summary>
        ClientSecret = 7,
        /// <summary>
        /// Enabled Host to manage Auth token for Dataverse connections.
        /// </summary>
        ExternalTokenManagement = 99,
        /// <summary>
        /// Invalid connection
        /// </summary>
        InvalidConnection = -1,
    }

    /// <summary>
    /// Handles login and setup the connections for Dataverse
    /// </summary>
    internal sealed class ConnectionService : IConnectionService, IDisposable
    {
        #region variables
        [NonSerializedAttribute]
        private OrganizationWebProxyClientAsync _svcWebClientProxy;
        private OrganizationServiceProxyAsync _svcOnPremClientProxy;
        private OrganizationWebProxyClientAsync _externalWebClientProxy; // OAuth specific web service proxy

        [NonSerializedAttribute]
        private WhoAmIResponse user;                        // Dataverse user entity that is the service.
        private string _hostname;                           // Host name of the Dataverse server
        private string _port;                               // Port the WebService is on
        private OrganizationDetail _OrgDetail;               // if provided by the calling system, bypasses all discovery server lookup processed.
        private bool _orgReadingDetails = false;
        private string _organization;                       // Org that is being inquired on..
        private AuthenticationType _eAuthType;             // Default setting for Auth Cred;

        [NonSerializedAttribute]
        private NetworkCredential _AccessCred;   // Network that is accessing  used for AD based Auth
        [NonSerializedAttribute]
        private ClientCredentials _UserClientCred;           // Describes the user client credential when accessing claims or SPLA based services.
        [NonSerializedAttribute]
        private string _InternetProtocalToUse = "http";      // Which Internet protocol to use to connect.

        /// <summary>
        /// This is the actual Dataverse OrgURI used to connect, which could be influenced by the host name given during the connect process.
        /// </summary>
        private Uri _ActualDataverseOrgUri;

        private string _LiveID;
        private SecureString _LivePass;
        private string _DataverseOnlineRegion;                    // Region of Dataverse Online to use.
        private string _ServiceCACHEName = "Microsoft.PowerPlatform.Dataverse.Client.Service"; // this is the base cache key name that will be used to cache the service.
        private static readonly IMemoryCache _clientMemoryCache = new MemoryCache(new MemoryCacheOptions());   // InMemory Cache for the Local dataverse Client.

        //OAuth Params
        private PromptBehavior _promptBehavior;             // prompt behavior
        private string _tokenCachePath;                     // user specified token cache file path
        private bool _isOnPremOAuth = false;                // Identifies whether the connection is for OnPrem or Online Deployment for OAuth
        private bool _isCalledbyExecuteRequest = false;     //Flag indicating that the an request called by Execute_Command
        private bool _isDefaultCredsLoginForOAuth = false;  //Flag indicating that the user is trying to login with the current user id.

        /// <summary>
        /// Configuration
        /// </summary>
        private IOptions<ConfigurationOptions> _configuration = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>();

        /// <summary>
        /// TimeSpan used to control the offset of the token reacquire behavior for none user Auth flows.
        /// </summary>
        private readonly TimeSpan _tokenOffSetTimeSpan = TimeSpan.FromMinutes(2);

        ///// <summary>
        ///// if Set to true then the connection is for one use and should be cleand out of cache when completed.
        ///// </summary>
        //private bool unqueInstance = false;

        /// <summary>
        /// Client or App Id to use.
        /// </summary>
        internal string _clientId;

        /// <summary>
        /// uri specifying the redirection uri post OAuth auth
        /// </summary>
        private Uri _redirectUri;

        /// <summary>
        /// Resource to connect to
        /// </summary>
        internal string _resource;

        /// <summary>
        /// cached authority reading from credential manager
        /// </summary>
        internal static string _authority;

        /// <summary>
        /// when certificate Auth is used,  this is the certificate that is used to execute the connection.
        /// </summary>
        private X509Certificate2 _certificateOfConnection;
        /// <summary>
        /// ThumbPrint of the Certificate to use.
        /// </summary>
        private string _certificateThumbprint;
        /// <summary>
        /// Location where the certificate identified by the Certificate thumb print can be found.
        /// </summary>
        private StoreName _certificateStoreLocation = StoreName.My;

        /// <summary>
        /// Uri that will be used to connect to Dataverse for Cert Auth.
        /// </summary>
        private Uri _targetInstanceUriToConnectTo = null;

        /// <summary>
        /// format string for building the org connect URI
        /// </summary>
        private readonly string SoapOrgUriFormat = @"{0}://{1}/XRMServices/2011/Organization.svc";
        /// <summary>
        /// format string for Global discovery for SOAP API
        /// </summary>
        private static readonly string _baseSoapOrgUriFormat = @"{0}/XRMServices/2011/Organization.svc";

        /// <summary>
        /// format string for building the WebAPI connect URI
        /// </summary>
        private readonly string WebApiUriFormat = @"{0}://{1}/api/data/v{2}/";

        /// <summary>
        /// format string for Global discovery WebAPI
        /// </summary>
        private static readonly string _baseWebApiUriFormat = @"{0}/api/data/v{1}/";

        /// <summary>
        /// Provides the base format for creating GD URL's
        /// </summary>
        private static readonly string _baselineGlobalDiscoveryFormater = "https://{0}/api/discovery/v{1}/{2}";

        /// <summary>
        /// format string for the global discovery service
        /// </summary>
        private static readonly string _commercialGlobalDiscoBaseWebAPIUriFormat = "https://globaldisco.crm.dynamics.com/api/discovery/v{0}/{1}";
        /// <summary>
        /// version of the global discovery service.
        /// </summary>
        private static readonly string _globlaDiscoVersion = "2.0";

        /// <summary>
        /// organization id placeholder.
        /// </summary>
        private Guid _OrganizationId;

        /// <summary>
        /// Max connection timeout property
        /// https://docs.microsoft.com/en-us/azure/app-service/faq-availability-performance-application-issues#why-does-my-request-time-out-after-230-seconds
        /// Azure Load Balancer has a default idle timeout setting of four minutes. This is generally a reasonable response time limit for a web request.
        /// </summary>
        private static TimeSpan _MaxConnectionTimeout = Utils.AppSettingsHelper.GetAppSettingTimeSpan("MaxDataverseConnectionTimeOutMinutes", Utils.AppSettingsHelper.TimeSpanFromKey.Minutes, TimeSpan.FromMinutes(4));

        /// <summary>
        /// Tenant ID
        /// </summary>
        private Guid _TenantId;

        /// <summary>
        /// Connected Environment Id
        /// </summary>
        private string _EnvironmentId;

        /// <summary>
        /// TestHelper for Testing sim.
        /// </summary>
        private IOrganizationService _testSupportIOrg;

        /// <summary>
        /// Value used by the retry system while the code is running,
        /// this value can scale up and down based on throttling limits.
        /// </summary>
        private TimeSpan _retryPauseTimeRunning;

        /// <summary>
        /// Known types factory
        /// </summary>
        private KnownTypesFactory _knownTypesFactory = new KnownTypesFactory();

        #endregion

        #region Properties

        /// ************** MSAL Properties ****************

        /// <summary>
        /// MSAL Object, Can be either a PublicClient or a Confidential Client, depending on Context.
        /// </summary>
        internal object _MsalAuthClient = null;

        /// <summary>
        /// This is carries the result of the token authentication flow to optimize token retrieval.
        /// </summary>
        internal AuthenticationResult _authenticationResultContainer = null;

        /// <summary>
        /// Selected user located as a result, used to optimize token acquire on second round.
        /// </summary>
        internal IAccount _userAccount = null;

        /// <summary>
        /// Lock object used to control access to the acquire token flows when in clone mode.
        /// MSAL does not appear to be threadsafe when multiple threads attempt to access the same MSAL object.
        /// </summary>
        internal readonly static object lockObject = new object();

        /// <summary>
        /// MemoryBacked Token cache object.
        /// Used for Confidential clients primarily.
        /// </summary>
        internal MemoryBackedTokenCache _memoryBackedTokenCache = null;

        // ********* End MSAL Properties ********

        /// <summary>
        /// Memory cache for current instance.
        /// </summary>
        internal IMemoryCache LocalMemoryCache { get { return _clientMemoryCache; } }


        /// <summary>
        /// When true, indicates the construction is coming from a clone process.
        /// </summary>
        internal bool IsAClone { get; set; }

        /// <summary>
        /// AAD Object ID of caller.  Valid in XRM 8.1 + only.
        /// </summary>
        public Guid? CallerAADObjectId { get; set; }

        /// <summary>
        /// httpclient that is in use for this connection
        /// </summary>
        internal HttpClient WebApiHttpClient { get; set; }

        /// <summary>
        /// This ID is used to support Dataverse Telemetry when trouble shooting SDK based errors.
        /// When Set by the caller, all Dataverse API Actions executed by this client will be tracked under a single session id for later troubleshooting.
        /// For example, you are able to group all actions in a given run of your client ( several creates / reads and such ) under a given tracking id that is shared on all requests.
        /// providing this ID when reporting a problem will aid in trouble shooting your issue.
        /// </summary>
        internal Guid? SessionTrackingId { get; set; }

        /// <summary>
        /// This will force the server to refresh the current metadata cache with current DB config.
        /// Note, that this is a performance impacting event. Use of this flag will slow down operations server side as the server is required to check for consistency on each API call executed.
        /// </summary>
        internal bool ForceServerCacheConsistency { get; set; }

        /// <summary>
        /// returns the URL to global discovery for querying all instances.
        /// </summary>
        internal static string GlobalDiscoveryAllInstancesUri { get { return string.Format(_commercialGlobalDiscoBaseWebAPIUriFormat, _globlaDiscoVersion, "Instances"); } }
        /// <summary>
        /// Format string for calling global disco for a specific instance.
        /// </summary>
        private static string GlobalDiscoveryInstanceUriFormat { get { return string.Format(_commercialGlobalDiscoBaseWebAPIUriFormat, _globlaDiscoVersion, "Instances({0})"); } }

        /// <summary>
        /// Service CacheName
        /// </summary>
        internal string ServiceCACHEName { get { return _ServiceCACHEName; } }

        /// <summary>
        /// Cached Authority
        /// </summary>
        internal string Authority { get { return _authority; } }

        /// <summary>
        ///  AAD authentication context
        /// </summary>
        internal AuthenticationResult AuthContext { get { return _authenticationResultContainer; } }

        /// <summary>
        /// Cached userid
        /// </summary>
        internal string UserId { get { return _userAccount != null ? _userAccount.Username : string.Empty; } }

        /// <summary>
        /// Flag indicating that the an request called by Execute_Command used for OAuth
        /// </summary>
        internal bool CalledbyExecuteRequest
        {
            get { return _isCalledbyExecuteRequest; }
            set { _isCalledbyExecuteRequest = value; }
        }

        /// <summary>
        /// When set to true, indicates that the organization details have allready been read. 
        /// </summary>
        internal bool OrgDetailsRead = false;

        /// <summary>
        /// Bit flag used to detect if the current object is being cloned 
        /// </summary>
        internal bool _isCloning = false; 

        /// <summary>
        /// Logging provider for DataverseConnectionServiceobject.
        /// </summary>
        internal DataverseTraceLogger logEntry { get; set; }

        /// <summary>
        /// Returns Logs from this process.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<Tuple<DateTime, string>> GetAllLogs()
        {
            return this.logEntry == null ? Enumerable.Empty<Tuple<DateTime, string>>() : this.logEntry.Logs;
        }


        /// <summary>
        /// if set to true, the log provider is set locally
        /// </summary>
        public bool isLogEntryCreatedLocally { get; set; }

        /// <summary>
        /// Get and Set of network credentials...
        /// </summary>
        internal System.Net.NetworkCredential DataverseServiceAccessCredential
        {
            get { return _AccessCred; }
            set { _AccessCred = value; }
        }

        /// <summary>
        /// Type of protocol to use
        /// </summary>
        internal string InternetProtocolToUse { get { return _InternetProtocalToUse; } set { _InternetProtocalToUse = value; } }

        /// <summary>
        /// returns the connected organization detail object.
        /// </summary>
        internal OrganizationDetail ConnectedOrganizationDetail
        {
            get
            {
                if (!_isCloning)
                {
                    if (_OrgDetail == null || !OrgDetailsRead)
                    {
                        lock (lockObject)
                        {
                            _orgReadingDetails = true;
                            IOrganizationService svc = null;
                            if (WebClient != null || OnPremClient != null)
                            {
                                if (WebClient != null) svc = WebClient; else svc = OnPremClient;
                                RefreshInstanceDetails(svc, _targetInstanceUriToConnectTo).ConfigureAwait(false).GetAwaiter().GetResult();
                            }
                            _orgReadingDetails = false;
                            OrgDetailsRead = true;
                        }
                    }
                }
                return _OrgDetail;
            }
            set { _OrgDetail = value; }
        }

        /// <summary>
        ///
        /// </summary>
        internal AuthenticationType AuthenticationTypeInUse
        {
            get
            {
                return _eAuthType;
            }
        }

        /// <summary>
        /// Returns the Dataverse Web Client
        /// </summary>
        internal OrganizationWebProxyClientAsync WebClient
        {
            get
            {
                if (_svcWebClientProxy != null)
                {
                    try
                    {
                        if (!_svcWebClientProxy.Endpoint.EndpointBehaviors.Contains(typeof(DataverseTelemetryBehaviors)))
                        {
                            _svcWebClientProxy.Endpoint.EndpointBehaviors.Add(new DataverseTelemetryBehaviors(this));
                        }
                    }
                    catch { }
                }
                return _svcWebClientProxy;
            }
        }

        /// <summary>
        /// Returns the Dataverse Client for OnPrem. 
        /// </summary>
        internal OrganizationServiceProxyAsync OnPremClient
        {
            get
            {
                if (_svcOnPremClientProxy != null)
                {
                    try
                    {
                        AttachProxyHander(_svcOnPremClientProxy);
                    }
                    catch { }
                }
                return _svcOnPremClientProxy;
            }
        }

        /// <summary>
        /// Get / Set the Dataverse Organization that the customer exists in
        /// </summary>
        internal string CustomerOrganization
        {
            get
            {
                if (_orgReadingDetails)
                    return null;
                else
                    return ConnectedOrganizationDetail.UniqueName;
            }
        }

        /// <summary>
        /// Gets / Set the Dataverse Host Port that the web service is listening on
        /// </summary>
        internal string HostPort
        {
            get { return _port; }
            set { _port = value; }
        }

        /// <summary>
        /// Gets / Set the Dataverse Hostname that the web service is listening on.
        /// </summary>
        internal string HostName
        {
            get { return _hostname; }
            set { _hostname = value; }
        }


        /// <summary>
        /// Returns the Current Dataverse User.
        /// </summary>
        internal WhoAmIResponse CurrentUser
        {
            get { return user; }
            set { user = value; }
        }

        /// <summary>
        /// Returns the Actual URI used to connect to Dataverse.
        /// this URI could be influenced by user defined variables.
        /// </summary>
        internal Uri ConnectOrgUriActual { get { return _ActualDataverseOrgUri; } }

        /// <summary>
        /// base URL for the oData WebAPI
        /// </summary>
        internal Uri ConnectODataBaseUriActual { get; set; }

        /// <summary>
        /// Flag indicating that the an External connection to Dataverse is used to connect.
        /// </summary>
        internal bool UseExternalConnection = false;

        /// <summary>
        /// Returns the friendly name of the connected org.
        /// </summary>
        internal string ConnectedOrgFriendlyName
        {
            get
            {
                if (_orgReadingDetails)
                    return null;
                else
                    return ConnectedOrganizationDetail.FriendlyName; 
            }
        }

        /// <summary>
        /// Returns the endpoint collection for the connected org.
        /// </summary>
        internal EndpointCollection ConnectedOrgPublishedEndpoints
        {
            get
            {
                if (_orgReadingDetails)
                    return null;
                else
                    return ConnectedOrganizationDetail.Endpoints;
            }
        }

        /// <summary>
        /// Version Number of the organization, if null Discovery service process was not run or the value returned was unreadable.
        /// </summary>
        internal Version OrganizationVersion { get; set; }

        /// <summary>
        /// Organization ID of connected org.
        /// </summary>
        internal Guid OrganizationId
        {
            get
            {
                if (_OrganizationId == Guid.Empty && ConnectedOrganizationDetail != null)
                {
                    _OrganizationId = ConnectedOrganizationDetail.OrganizationId;
                }
                return _OrganizationId;
            }
            set
            {
                _OrganizationId = value;
            }
        }

        /// <summary>
        /// Gets or sets the TenantId
        /// </summary>
        internal Guid TenantId
        {
            get
            {
                if (_TenantId == Guid.Empty && ConnectedOrganizationDetail != null)
                {
                    Guid.TryParse(ConnectedOrganizationDetail.TenantId, out _TenantId);
                }
                return _TenantId;
            }
            set
            {
                _TenantId = value;
            }
        }

        /// <summary>
        /// Gets or sets the Environment Id.
        /// </summary>
        internal string EnvironmentId
        {
            get
            {
                if (string.IsNullOrEmpty(_EnvironmentId) && ConnectedOrganizationDetail != null)
                {
                    _EnvironmentId = ConnectedOrganizationDetail.EnvironmentId;
                }
                return _EnvironmentId;
            }
            set
            {
                _EnvironmentId = value;
            }
        }

        /// <summary>
        /// Function to call to get access token for the current operation.
        /// Set based on constructor call and is specific to the instance of the Client that was created.
        /// </summary>
        internal Func<string, Task<string>> GetAccessTokenAsync { get; set; }


        /// <summary>
        /// Function to call to get the current headers for this request
        /// </summary>
        internal Func<Task<Dictionary<string, string>>> RequestAdditionalHeadersAsync { get; set; }


        /// <summary>
        /// returns the format string for the baseWebAPI
        /// </summary>
        internal string BaseWebAPIDataFormat { get { return _baseWebApiUriFormat; } }

        /// <summary>
        /// Gets or Sets the Max Connection timeout for the connection to Dataverse/XRM
        /// default is 2min.
        /// </summary>
        internal static TimeSpan MaxConnectionTimeout
        {
            get { return _MaxConnectionTimeout; }
            set { _MaxConnectionTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the value to enabled cookie relay on this connection.
        /// </summary>
        internal bool EnableCookieRelay
        {
            get => _configuration.Value.EnableAffinityCookie;
            set => _configuration.Value.EnableAffinityCookie = value;
        }

        /// <summary>
        /// Cookies that are being passed though clients, when cookies are used
        /// </summary>
        internal ConcurrentDictionary<string, string> CurrentCookieCollection { get; set; } = null;

        /// <summary>
        /// Server Hint for the number of concurrent threads that would provide optimal processing. 
        /// </summary>
        internal int RecommendedDegreesOfParallelism { get; set; } = 5; // Default value. 

        #endregion

        /// <summary>
        /// TEST Support Constructor.
        /// </summary>
        /// <param name="testIOrganziationSvc"></param>
        /// <param name="baseConnectUrl"></param>
        /// <param name="mockClient"></param>
        /// <param name="logger"></param>
        internal ConnectionService(IOrganizationService testIOrganziationSvc, string baseConnectUrl, HttpClient mockClient, ILogger logger)
        {
            _testSupportIOrg = testIOrganziationSvc;
            WebApiHttpClient = mockClient;
            logEntry = new DataverseTraceLogger(logger);
            isLogEntryCreatedLocally = true;

            _OrgDetail = new OrganizationDetail();
            _OrgDetail.Endpoints.Add(EndpointType.OrganizationDataService, baseConnectUrl);

            ConnectedOrganizationDetail = _OrgDetail; 
            RefreshInstanceDetails(testIOrganziationSvc, null).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets up an initialized the Dataverse Service interface.
        /// </summary>
        /// <param name="externalOrgWebProxyClient">This is an initialized organization web Service proxy</param>
        /// <param name="authType">Authentication Type to use</param>
        /// <param name="logSink">incoming Log Sink</param>
        /// <param name="isClone">is this being created as part of a clone request</param>
        internal ConnectionService(OrganizationWebProxyClientAsync externalOrgWebProxyClient, AuthenticationType authType, DataverseTraceLogger logSink = null, bool isClone = false)
        {
            if (logSink == null)
            {
                logEntry = new DataverseTraceLogger();
                isLogEntryCreatedLocally = true;
            }
            else
            {
                logEntry = logSink;
                isLogEntryCreatedLocally = false;
            }

            // is this a clone request
            IsAClone = isClone;
            _externalWebClientProxy = externalOrgWebProxyClient;

            if (_externalWebClientProxy != null)
            {
                AttachWebProxyHander(_externalWebClientProxy);

                // Set timeouts.
                _externalWebClientProxy.InnerChannel.OperationTimeout = _MaxConnectionTimeout;
                _externalWebClientProxy.Endpoint.Binding.SendTimeout = _MaxConnectionTimeout;
                _externalWebClientProxy.Endpoint.Binding.ReceiveTimeout = _MaxConnectionTimeout;
            }
            UseExternalConnection = true;
            GenerateCacheKeys(true);
            _eAuthType = authType;
        }

        /// <summary>
        /// Sets up a Connection to Dataverse OnPremsie 
        /// </summary>
        /// <param name="authType">Type of Authentication to use, AD or IDF  </param>
        /// <param name="hostName">Host name to connect too</param>
        /// <param name="port">Port Number to Connect too</param>
        /// <param name="orgName">Organization to Connect to</param>
        /// <param name="providedCredential">Credential to use to connect</param>
        /// <param name="useUniqueCacheName">flag that will tell the instance to create a Unique Name for the Dataverse Cache Objects.</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Dataverse Discovery Server service. not required.</param>
        /// <param name="logSink">incoming LogSink</param>
        /// <param name="instanceToConnectToo">instance to connect too</param>
        /// <param name="isClone">is this part of a clone request</param>
        internal ConnectionService(
            AuthenticationType authType,                        // type of auth, AD 
            string hostName,                                    // Host name your connecting too.. AD only
            string port,                                        // Host Port your connecting too.. AD only
            string orgName,                                     // Dataverse Organization Name your connecting too
            System.Net.NetworkCredential providedCredential,    // Network credential to use to connect to CRM
            bool useUniqueCacheName,                            // tells the system to create a unique cache name for this instance.
            OrganizationDetail orgDetail,                       // Tells the client connection to bypass all discovery server behaviors and use this detail object.
            DataverseTraceLogger logSink = null,
            Uri instanceToConnectToo = null,
            bool isClone = false)
        {
            if (authType != AuthenticationType.AD)
                throw new ArgumentOutOfRangeException(nameof(authType), "Invalid Authentication type");

            if (logSink == null)
            {
                logEntry = new DataverseTraceLogger();
                isLogEntryCreatedLocally = true;
            }
            else
            {
                logEntry = logSink;
                isLogEntryCreatedLocally = false;
            }

            // is this a clone request
            IsAClone = isClone; 

            // Override WebAPI for Login flow. 
            _configuration.Value.UseWebApiLoginFlow = false;

            UseExternalConnection = false;
            _eAuthType = authType;
            _hostname = hostName;
            _port = port;
            _organization = orgName;
            _AccessCred = providedCredential;
            _OrgDetail = orgDetail;
            _targetInstanceUriToConnectTo = instanceToConnectToo;
            GenerateCacheKeys(useUniqueCacheName);
        }

        /// <summary>
        /// Sets up and initializes the Dataverse Service interface using OAuth for user flows.
        /// </summary>
        /// <param name="authType">Only OAuth User flows are supported in this constructor</param>
        /// <param name="orgName">Organization to Connect too</param>
        /// <param name="livePass">Live Password to use</param>
        /// <param name="liveUserId">Live ID to use</param>
        /// <param name="onlineRegion">OnlineRegion</param>
        /// <param name="useUniqueCacheName">flag that will tell the instance to create a Unique Name for the connection Cache Objects.</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Discovery Server service. not required.</param>
        /// <param name="clientId">Client Id of the registered application.</param>
        /// <param name="redirectUri">RedirectUri for the application redirecting to</param>
        /// <param name="promptBehavior">Whether to prompt when no username/password</param>
        /// <param name="hostName">Hostname to connect to</param>
        /// <param name="port">Port to connect to</param>
        /// <param name="onPrem">Token Cache Path supplied for storing OAuth tokens</param>
        /// <param name="logSink">Incoming Log Provide</param>
        /// <param name="instanceToConnectToo">Targeted Instance to connector too.</param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
        /// <param name="tokenCacheStorePath">(optional) if provided sets the path to write the token cache too.</param>
        /// <param name="isClone">Is this a clone request</param>
        internal ConnectionService(
            AuthenticationType authType,    // Only OAuth is supported in this constructor.
            string orgName,                 // Organization Name your connecting too
            string liveUserId,              // Live ID - Live only
            SecureString livePass,          // Live PW - Live Only
            string onlineRegion,
            bool useUniqueCacheName,        // tells the system to create a unique cache name for this instance.
            OrganizationDetail orgDetail,
            string clientId,                // The client Id of the client registered with Azure
            Uri redirectUri,                // The redirectUri telling the redirect login window
            PromptBehavior promptBehavior,  // The prompt behavior for MSAL library
            string hostName,                // Host name to connect to
            string port,                    // Port used to connect to
            bool onPrem,
            DataverseTraceLogger logSink = null,
            Uri instanceToConnectToo = null,
            bool useDefaultCreds = false,
            string tokenCacheStorePath = null, 
            bool isClone = false
            )
        {
            if (authType != AuthenticationType.OAuth && authType != AuthenticationType.ClientSecret)
                throw new ArgumentOutOfRangeException(nameof(authType), "This constructor only supports the OAuth or Client Secret Auth types");

            if (logSink == null)
            {
                logEntry = new DataverseTraceLogger();
                isLogEntryCreatedLocally = true;
            }
            else
            {
                logEntry = logSink;
                isLogEntryCreatedLocally = false;
            }

            // is this a clone request
            IsAClone = isClone;
            UseExternalConnection = false;
            _eAuthType = authType;
            _organization = orgName;
            _LiveID = liveUserId;
            _LivePass = livePass;
            _DataverseOnlineRegion = onlineRegion;
            _OrgDetail = orgDetail;
            _clientId = clientId;
            _redirectUri = redirectUri;
            _promptBehavior = promptBehavior;
            _tokenCachePath = tokenCacheStorePath;
            _hostname = hostName;
            _port = port;
            _isOnPremOAuth = onPrem;
            _targetInstanceUriToConnectTo = instanceToConnectToo;
            _isDefaultCredsLoginForOAuth = useDefaultCreds;
            GenerateCacheKeys(useUniqueCacheName);
        }

        /// <summary>
        /// Sets up and initializes the Dataverse Service interface using Certificate Auth.
        /// </summary>
        /// <param name="authType">Only Certificate flows are supported in this constructor</param>
        /// <param name="useUniqueCacheName">flag that will tell the instance to create a Unique Name for the Cache Objects.</param>
        /// <param name="orgDetail">Dataverse Org Detail object, this is is returned from a query to the Discovery Server service. not required.</param>
        /// <param name="clientId">Client Id of the registered application.</param>
        /// <param name="redirectUri">RedirectUri for the application redirecting to</param>
        /// <param name="hostName">Hostname to connect to</param>
        /// <param name="port">Port to connect to</param>
        /// <param name="onPrem">Modifies system behavior for ADAL based auth for OnPrem</param>
        /// <param name="certStoreName">StoreName on this machine where the certificate with the thumb print passed can be located</param>
        /// <param name="certifcate">X509Certificate to be used to login to this connection, if populated, Thumb print and StoreLocation are ignored. </param>
        /// <param name="certThumbprint">Thumb print of the Certificate to use for this connection.</param>
        /// <param name="instanceToConnectToo">Direct Instance Uri to Connect To</param>
        /// <param name="logSink">Incoming Log Sink data</param>
        /// <param name="tokenCacheStorePath">(optional) if provided, sets the path to store or read user tokens</param>
        /// <param name="isClone">Is this a clone request</param>
        internal ConnectionService(
            AuthenticationType authType,    // Only Certificate is supported in this constructor.
            Uri instanceToConnectToo,       // set the connection instance to use.
            bool useUniqueCacheName,        // tells the system to create a unique cache name for this instance.
            OrganizationDetail orgDetail,
            string clientId,                // The client Id of the client registered with Azure
            Uri redirectUri,                // The redirectUri telling the redirect login window
            string certThumbprint,          // thumb print of the certificate to use
            StoreName certStoreName,        // Where to find the Certificate identified by the thumb print.
            X509Certificate2 certifcate,    // loaded and configured certificate to use.
            string hostName,                // Host name to connect to
            string port,                    // Port used to connect to
            bool onPrem,
            DataverseTraceLogger logSink = null,
            string tokenCacheStorePath = null, 
            bool isClone = false)
        {
            if (authType != AuthenticationType.Certificate && authType != AuthenticationType.ExternalTokenManagement)
                throw new ArgumentOutOfRangeException(nameof(authType), "This constructor only supports the Certificate Auth type");

            if (logSink == null)
            {
                logEntry = new DataverseTraceLogger();
                isLogEntryCreatedLocally = true;
            }
            else
            {
                logEntry = logSink;
                isLogEntryCreatedLocally = false;
            }

            // is this a clone request
            IsAClone = isClone;
            UseExternalConnection = false;
            _eAuthType = authType;
            _targetInstanceUriToConnectTo = instanceToConnectToo;
            _OrgDetail = orgDetail;
            _clientId = clientId;
            _redirectUri = redirectUri;
            _tokenCachePath = tokenCacheStorePath;
            _hostname = hostName;
            _port = port;
            _isOnPremOAuth = onPrem;
            _certificateOfConnection = certifcate;
            _certificateThumbprint = certThumbprint;
            _certificateStoreLocation = certStoreName;
            GenerateCacheKeys(useUniqueCacheName);
        }

        /// <summary>
        /// Loges into Dataverse using the supplied parameters.
        /// </summary>
        /// <returns></returns>
        public bool DoLogin(out ConnectionService ConnectionObject)
        {
            // Initializes the Dataverse Service.
            bool IsConnected = IntilizeService(out ConnectionObject);
            return IsConnected;
        }

        /// <summary>
        /// This is to deal with 2 instances of the ConnectionService being created in the Same Running Instance that would need to connect to different Dataverse servers.
        /// </summary>
        /// <param name="useUniqueCacheName"></param>
        private void GenerateCacheKeys(bool useUniqueCacheName)
        {
            // This is to deal with 2 instances of the ConnectionService being created in the Same Running Instance that would need to connect to different Dataverse servers.
            if (useUniqueCacheName)
            {
                //unqueInstance = true; // this instance is unique.
                _authority = string.Empty;
                Guid guID = Guid.NewGuid();
                _ServiceCACHEName = _ServiceCACHEName + guID.ToString(); // Creating a unique instance name for the cache object.
            }
        }

        /// <summary>
        /// Initializes the Dataverse Service
        /// </summary>
        /// <returns>Return true on Success, false on failure</returns>
        private bool IntilizeService(out ConnectionService ConnectionObject)
        {
            // Get the Dataverse Service.
            IOrganizationService dvService = this.GetCachedService(out ConnectionObject);

            if (dvService != null)
            {
                if (_svcWebClientProxy != null)
                    _svcWebClientProxy.Dispose();

                if (_svcOnPremClientProxy != null)
                    _svcOnPremClientProxy.Dispose();

                if (dvService is OrganizationWebProxyClientAsync orgWebClient)
                {
                    _svcWebClientProxy = orgWebClient;
                }
                else
                {
                    if (dvService is OrganizationServiceProxyAsync orgOnPremClient)
                    {
                        _svcOnPremClientProxy = orgOnPremClient;
                    }
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Try's to gets the Cached Dataverse Service from memory.
        /// on Failure, Initialize a New instance.
        /// </summary>
        /// <returns></returns>
        private IOrganizationService GetCachedService(out ConnectionService ConnectionObject)
        {
            // try to get the object from Memory .
            if (!string.IsNullOrEmpty(_ServiceCACHEName))
            {
                try
                {
                    var objProspectiveCachedClient = _clientMemoryCache.Get(_ServiceCACHEName);
                    if (objProspectiveCachedClient != null && objProspectiveCachedClient is ConnectionService service)
                        ConnectionObject = service;
                    else
                        ConnectionObject = null;
                }
                catch (Exception ex)
                {
                    logEntry?.Log("Failed to get cached service object from memory", TraceEventType.Warning, ex);
                    ConnectionObject = null;
                }
            }
            else
                ConnectionObject = null;
            if (ConnectionObject == null)
            {
                // No Service found.. Init the Service and try to bring it online.
                IOrganizationService localSvc = InitServiceAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (localSvc == null)
                    return null;

                if (!string.IsNullOrEmpty(_ServiceCACHEName))
                {
                    // Set or update the cache the Service for 5 min.
                    _clientMemoryCache.Set(_ServiceCACHEName, this, DateTimeOffset.Now.AddMinutes(5));
                }
                return localSvc;
            }
            else
            {
                //service from Cache .. get user associated with the connection
                try
                {
                    // Removed call to WHoAMI as it is amused when picking up cache that the reauth logic will be exercised by the first call to the server.
                    if (ConnectionObject._svcWebClientProxy != null)
                        return (IOrganizationService)ConnectionObject._svcWebClientProxy;
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    logEntry.Log("Failed to Create a connection to Dataverse", TraceEventType.Error, ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Initialize a Connection to Dataverse
        /// </summary>
        /// <returns></returns>
        private async Task<IOrganizationService> InitServiceAsync()
        {
            // Dataverse Service Endpoint to work with
            IOrganizationService dvService = null;
            Stopwatch dtQueryTimer = Stopwatch.StartNew();
            try
            {
                if (!IsAClone)
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Initialize Dataverse connection Started - AuthType: {0}", _eAuthType.ToString()), TraceEventType.Verbose);

                if (UseExternalConnection)
                {
                    #region Use Externally provided connection
                    if (_externalWebClientProxy != null)
                        dvService = _externalWebClientProxy;
                    if (dvService == null)
                    {
                        this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Externally provided connection to Dataverse Service Not available"),
                                   TraceEventType.Error);
                        return null;
                    }

                    if (!IsAClone)
                    {
                        // Get Version of organization:
                        Guid guRequestId = Guid.NewGuid();
                        RetrieveVersionRequest verRequest = new RetrieveVersionRequest() { RequestId = guRequestId };
                        logEntry.Log(string.Format("Externally provided connection to Dataverse Service - Retrieving Version Info. RequestId:{0}", guRequestId.ToString()), TraceEventType.Verbose);

                        try
                        {

                            RetrieveVersionResponse verResp = (RetrieveVersionResponse)((IOrganizationService)dvService).Execute(verRequest);
                            Version OutVersion = null;
                            if (Version.TryParse(verResp.Version, out OutVersion))
                                OrganizationVersion = OutVersion;
                            else
                                OrganizationVersion = new Version("0.0");
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Externally provided connection to Dataverse Service - Org Version: {0}", OrganizationVersion.ToString()), TraceEventType.Verbose);
                        }
                        catch (Exception ex)
                        {
                            // Failed to get version info :
                            // Log it..
                            logEntry.Log("Failed to retrieve version info from connected Dataverse organization", TraceEventType.Error, ex);
                        }
                    }
                    else
                        logEntry.Log("Cloned Connection, Retrieve version info from connected Dataverse organization not called");
                    #endregion
                }
                else
                {
                    if (_eAuthType == AuthenticationType.AD ||(_eAuthType == AuthenticationType.OAuth && _isOnPremOAuth == true) || (_eAuthType == AuthenticationType.Certificate && _isOnPremOAuth == true))
                    {
                        #region AD or SPLA Auth
                        try
                        {
                            if (_targetInstanceUriToConnectTo == null) // This is TEMP until Discovery Services interface is working for OnPrem. 
                                throw new DataverseConnectionException("Environment URL is required when connecting to On Premises"); // Block discovery. 

                            // Given Direct Url.. connect to the Direct URL
                            if (_targetInstanceUriToConnectTo != null)
                            {
                                dvService = await DoDirectLoginAsync(true).ConfigureAwait(false);
                            }
                            else
                            {
                                //TODO :/// Remove Discovery support for Onprem. 
                                #region TO BE DELETED
                                string DvUrl = string.Empty;
                                #region AD
                                if (_OrgDetail == null)
                                {
                                    // Build Discovery Server Connection
                                    if (!string.IsNullOrWhiteSpace(_port))
                                    {
                                        // http://<Server>:<port>/XRMServices/2011/Discovery.svc?wsdl
                                        DvUrl = String.Format(CultureInfo.InvariantCulture,
                                            "{0}://{1}:{2}/XRMServices/2011/Discovery.svc",
                                            _InternetProtocalToUse,
                                            _hostname,
                                            _port);
                                    }
                                    else
                                    {
                                        DvUrl = String.Format(CultureInfo.InvariantCulture,
                                            "{0}://{1}/XRMServices/2011/Discovery.svc",
                                            _InternetProtocalToUse,
                                            _hostname);
                                    }
                                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Discovery URI is = {0}", DvUrl), TraceEventType.Information);
                                    if (!Uri.IsWellFormedUriString(DvUrl, UriKind.Absolute))
                                    {
                                        // Throw error here.
                                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Discovery URI is malformed = {0}", DvUrl), TraceEventType.Error);

                                        return null;
                                    }
                                }
                                else
                                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Process is bypassed.. OrgDetail object was provided"), TraceEventType.Information);


                                _UserClientCred = new ClientCredentials();
                                Uri uUserHomeRealm = null;


                                if (_eAuthType == AuthenticationType.Certificate)
                                {
                                    // Certificate based .. get the Cert.
                                    if (_certificateOfConnection == null && !string.IsNullOrEmpty(_certificateThumbprint))
                                    {
                                        // Certificate is not passed in. Thumbprint found... try to acquire the cert.
                                        _certificateOfConnection = FindCertificate(_certificateThumbprint, _certificateStoreLocation, logEntry);
                                        if (_certificateOfConnection == null)
                                        {
                                            // Fail.. no Cert.
                                            throw new Exception("Failed to locate or read certificate from passed thumbprint.", logEntry.LastException);
                                        }
                                    }
                                }
                                else
                                {
                                    if (_eAuthType == AuthenticationType.OAuth)
                                    {
                                        // oAuthBased.
                                        _UserClientCred.UserName.Password = string.Empty;
                                        _UserClientCred.UserName.UserName = string.Empty;
                                    }
                                }

                                OrganizationDetail orgDetail = null;
                                if (_OrgDetail == null)
                                {
                                    // Discover Orgs Url.
                                    Uri uDvUrl = new Uri(DvUrl);

                                    // This will try to discover any organizations that the user has access too,  one way supports AD / IFD and the other supports Claims
                                    DiscoverOrganizationsResult discoverOrganizationsResult = null;

                                    if (_eAuthType == AuthenticationType.OAuth)
                                    {
                                        discoverOrganizationsResult = await DiscoverOrganizationsAsync(uDvUrl, _UserClientCred, _clientId, _redirectUri, _promptBehavior, true, _authority, logSink: logEntry, tokenCacheStorePath: _tokenCachePath).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        if (_eAuthType == AuthenticationType.Certificate)
                                        {
                                            discoverOrganizationsResult = await DiscoverOrganizationsAsync(uDvUrl, _certificateOfConnection, _clientId, true, _authority, logEntry, tokenCacheStorePath: _tokenCachePath).ConfigureAwait(false);
                                        }
                                    }


                                    // Check the Result to see if we have Orgs back
                                    if (discoverOrganizationsResult.OrganizationDetailCollection != null && discoverOrganizationsResult.OrganizationDetailCollection.Count > 0)
                                    {
                                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Found {0} Org(s)", discoverOrganizationsResult.OrganizationDetailCollection.Count), TraceEventType.Information);
                                        orgDetail = discoverOrganizationsResult.OrganizationDetailCollection.FirstOrDefault(o => string.Compare(o.UniqueName, _organization, StringComparison.CurrentCultureIgnoreCase) == 0);
                                        if (orgDetail == null)
                                            orgDetail = discoverOrganizationsResult.OrganizationDetailCollection.FirstOrDefault(o => string.Compare(o.FriendlyName, _organization, StringComparison.CurrentCultureIgnoreCase) == 0);

                                        if (orgDetail == null)
                                        {
                                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Organization not found. Org = {0}", _organization), TraceEventType.Error);
                                            return null;
                                        }
                                    }
                                    else
                                    {
                                        // error here.
                                        logEntry.Log("No Organizations found.", TraceEventType.Error);
                                        return null;
                                    }
                                }
                                else
                                {
                                    orgDetail = _OrgDetail; // Assign to passed in value.
                                    OrgDetailsRead = true;
                                }

                                // Try to connect to Dataverse here.
                                dvService = await ConnectAndInitServiceAsync(orgDetail, true, uUserHomeRealm).ConfigureAwait(false);

                                #endregion
                                #endregion

                            }


                            if (dvService == null)
                            {
                                logEntry.Log("Failed to connect to Dataverse", TraceEventType.Error);
                                return null;
                            }

                            if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ClientSecret)
                                dvService = (OrganizationWebProxyClientAsync)dvService;



                        }
                        catch (Exception ex)
                        {
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to login to Dataverse, Error was : {0}", ex.Message),
                                   TraceEventType.Error, ex);
                            if (dvService != null)
                            {
                                ((OrganizationWebProxyClient)dvService).Dispose();
                                dvService = null;
                            }
                            throw;
                        }
                        #endregion
                    }
                    else
                        if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ClientSecret || _eAuthType == AuthenticationType.ExternalTokenManagement)
                    {
                        #region oAuth | CERT

                        #region CERT AUTH
                        if (_eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ExternalTokenManagement || _eAuthType == AuthenticationType.ClientSecret)
                        {
                            if (_eAuthType == AuthenticationType.Certificate)
                            {
                                if (_certificateOfConnection == null && !string.IsNullOrEmpty(_certificateThumbprint))
                                {
                                    // Certificate is not passed in. Thumbprint found... try to acquire the cert.
                                    _certificateOfConnection = FindCertificate(_certificateThumbprint, _certificateStoreLocation, logEntry);
                                    if (_certificateOfConnection == null)
                                    {
                                        // Fail.. no Cert.
                                        throw new Exception("Failed to locate or read certificate from passed thumbprint.", logEntry.LastException);
                                    }
                                }
                            }

                            // Given Direct Url.. connect to the Direct URL
                            if (_targetInstanceUriToConnectTo != null)
                            {
                                dvService = await DoDirectLoginAsync().ConfigureAwait(false);
                            }
                        }
                        #endregion

                        #region Not Certificate Auth
                        if ((_eAuthType != AuthenticationType.Certificate && _eAuthType != AuthenticationType.ClientSecret && _eAuthType != AuthenticationType.ExternalTokenManagement))
                        {
                            if (!_isDefaultCredsLoginForOAuth)
                            {
                                _UserClientCred = new ClientCredentials();
                                _UserClientCred.UserName.UserName = _LiveID;
                                if (_LivePass != null && _LivePass.Length > 0)
                                    _UserClientCred.UserName.Password = _LivePass.ToUnsecureString();
                            }
                        }


                        if ((_eAuthType != AuthenticationType.Certificate && _eAuthType != AuthenticationType.ClientSecret && _eAuthType != AuthenticationType.ExternalTokenManagement) || _targetInstanceUriToConnectTo == null)
                        {
                            if (_OrgDetail == null && _targetInstanceUriToConnectTo != null)
                            {
                                // User provided a direct link to login
                                dvService = await DoDirectLoginAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                DiscoveryServers onlineServerList = new DiscoveryServers();
                                try
                                {

                                    OrgList orgList = await FindDiscoveryServerAsync(onlineServerList).ConfigureAwait(false);

                                    if (orgList.OrgsList != null && orgList.OrgsList.Count > 0)
                                    {
                                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Found {0} Org(s)", orgList.OrgsList.Count), TraceEventType.Information);
                                        if (orgList.OrgsList.Count == 1)
                                        {
                                            _OrgDetail = orgList.OrgsList.First().OrgDetail;
                                            OrgDetailsRead = true;
                                            dvService = await ConnectAndInitServiceAsync(orgList.OrgsList.First().OrgDetail, false, null).ConfigureAwait(false);
                                            if (dvService != null)
                                            {
                                                dvService = (OrganizationWebProxyClientAsync)dvService;

                                                // Update Region
                                                _DataverseOnlineRegion = onlineServerList.GetServerShortNameByDisplayName(orgList.OrgsList.First().DiscoveryServerName);
                                                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "User Organization ({0}) found in Discovery Server {1} - ONLY ORG FOUND", orgList.OrgsList.First().OrgDetail.UniqueName, _DataverseOnlineRegion));
                                            }

                                        }
                                        else
                                        {
                                            if (!string.IsNullOrWhiteSpace(_organization))
                                            {
                                                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Looking for Organization = {0} in the results from Discovery server list.", _organization), TraceEventType.Information);
                                                // Find the Stored org in the returned collection..
                                                OrgByServer orgDetail = Utilities.DeterminOrgDataFromOrgInfo(orgList, _organization);

                                                if (orgDetail != null && !string.IsNullOrEmpty(orgDetail.OrgDetail.UniqueName))
                                                {
                                                    // Found it ..
                                                    _OrgDetail = orgDetail.OrgDetail;
                                                    OrgDetailsRead = true; 
                                                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "found User Org = {0} in results", _organization), TraceEventType.Information);
                                                    dvService = await ConnectAndInitServiceAsync(orgDetail.OrgDetail, false, null).ConfigureAwait(false);
                                                    if (dvService != null)
                                                    {
                                                        dvService = (OrganizationWebProxyClientAsync)dvService;
                                                        _DataverseOnlineRegion = onlineServerList.GetServerShortNameByDisplayName(orgList.OrgsList.First().DiscoveryServerName);
                                                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "User Org ({0}) found in Discovery Server {1}", orgDetail.OrgDetail.UniqueName, _DataverseOnlineRegion));
                                                    }
                                                    else
                                                        return null;

                                                }
                                                else
                                                    return null;
                                            }
                                            else
                                                return null;
                                        }
                                    }
                                    else
                                    {
                                        // Error here.
                                        logEntry.Log("No Orgs Found", TraceEventType.Information);

                                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "No Organizations Found, Searched online. Region Setting = {0}", _DataverseOnlineRegion)
                                            , TraceEventType.Error);
                                        return null;
                                    }
                                }
                                finally
                                {
                                    onlineServerList.Dispose(); // Clean up array.
                                }
                            }
                        }
                        #endregion
                        #endregion
                    }
                    else
                        return null;
                }

                return (IOrganizationService)dvService;

            }
            #region Login / Discovery Server Exception handlers

            catch (MessageSecurityException ex)
            {
                // Login to Live Failed.
                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Invalid Login Information : {0}", ex.Message),
                               TraceEventType.Error, ex);
                throw;

            }
            catch (WebException ex)
            {
                // Check the result for Errors.
                if (!string.IsNullOrEmpty(ex.Message) && ex.Message.Contains("HTTP status 401"))
                {
                    // Login Error.
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to Login to Dataverse: {0}", ex.Message), TraceEventType.Error, ex);

                }
                else
                {
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to Dataverse: {0}", ex.Message), TraceEventType.Error, ex);

                }
                throw;
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException == null)
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to Dataverse: {0}", ex.Message), TraceEventType.Error, ex);
                else
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to Dataverse: {0}", ex.InnerException.Message), TraceEventType.Error, ex);
                throw;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == null)
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to Dataverse: {0}", ex.Message), TraceEventType.Error, ex);
                else
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to Dataverse: {0}", ex.InnerException.Message), TraceEventType.Error, ex);
                throw;
            }
            finally
            {
                dtQueryTimer.Stop();
            }
            #endregion
            //return null;

        }

        /// <summary>
        /// Executes a direct login using the current configuration.
        /// </summary>
        /// <param name="IsOnPrem">if set indicates an onPrem Authentication</param>
        /// <returns></returns>
        private async Task<IOrganizationService> DoDirectLoginAsync(bool IsOnPrem = false)
        {
            logEntry.Log("Direct Login Process Started", TraceEventType.Verbose);
            Stopwatch sw = Stopwatch.StartNew();

            IOrganizationService dvService = null;
            Uri OrgWorkingURI = null;
            if (!IsOnPrem || _eAuthType == AuthenticationType.OAuth) // Use this even if its onPrem, when auth type == oauth. 
            {
                OrgWorkingURI = new Uri(string.Format(SoapOrgUriFormat, _targetInstanceUriToConnectTo.Scheme, _targetInstanceUriToConnectTo.DnsSafeHost));
            }
            else
            {
                OrgWorkingURI = new Uri(string.Format(SoapOrgUriFormat, _targetInstanceUriToConnectTo.Scheme, $"{_targetInstanceUriToConnectTo.DnsSafeHost}/{_organization}"));
            }
            _targetInstanceUriToConnectTo = OrgWorkingURI;

            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Attempting to Connect to Uri {0}", _targetInstanceUriToConnectTo.ToString()), TraceEventType.Information);
            OrgByServer orgDetail = new OrgByServer();
            orgDetail.OrgDetail = new OrganizationDetail();
            orgDetail.OrgDetail.Endpoints[EndpointType.OrganizationService] = _targetInstanceUriToConnectTo.ToString();

            if (!IsOnPrem)
            {
                dvService = await ConnectAndInitServiceAsync(orgDetail.OrgDetail, false, null).ConfigureAwait(false);
            }
            else 
            {
                _UserClientCred = new ClientCredentials();
                Uri uUserHomeRealm = null;

                if (_eAuthType == AuthenticationType.OAuth)
                {
                    // oAuthBased.
                    if (_LivePass != null )
                        _UserClientCred.UserName.Password =  _LivePass.ToUnsecureString();
                    _UserClientCred.UserName.UserName = _LiveID;
                }
                // You cannot use Default config for anything other then AD or IFD Setting's in the Auth Realm.
                else
                {
                    if (//_eAuthType == AuthenticationType.IFD ||
                         _eAuthType == AuthenticationType.AD)
                    {
                        //IFD or AD ...
                        // Credentials support Default Credentials...
                        if (_AccessCred == null)
                            _AccessCred = System.Net.CredentialCache.DefaultNetworkCredentials; // Use default creds..
                        else
                            _UserClientCred = GetClientCredentials(_AccessCred);
                    }
                    else
                    {
                        // CLAIMS FOR THE MOMENT 
                        
                        //// Credentials Required.
                        //if (!string.IsNullOrWhiteSpace(_HomeRealmUrl))
                        //{
                        //    if (!Uri.IsWellFormedUriString(_HomeRealmUrl, UriKind.RelativeOrAbsolute))
                        //    {
                        //        logEntry.Log($"HomeRealm URL is bad : URL = {_HomeRealmUrl}", TraceEventType.Error);
                        //        return null;
                        //    }
                        //    uUserHomeRealm = new Uri(_HomeRealmUrl);

                        //    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "HomeRealm is = {0}", uUserHomeRealm.ToString()), TraceEventType.Information);
                        //}
                        //else
                        //    uUserHomeRealm = null;

                        //_DeviceCredentials = new ClientCredentials();
                        //string userName = _ClaimsuserId.Split('@')[0];

                        //_DeviceCredentials.UserName.UserName = _ClaimsuserId;
                        //_DeviceCredentials.UserName.Password = _Claimspassword.ToUnsecureString();

                        //if (_UserClientCred == null)
                        //    _UserClientCred = new ClientCredentials();

                        //_UserClientCred.UserName.UserName = userName;
                        //_UserClientCred.UserName.Password = _Claimspassword.ToUnsecureString();
                    }
                }
                dvService = await ConnectAndInitServiceAsync(orgDetail.OrgDetail, true, uUserHomeRealm).ConfigureAwait(false);
            }
            
            
            if (dvService != null)
            {
                OrganizationVersion = Version.Parse("9.0.0.0"); 
                //await GetServerVersion(dvService, _targetInstanceUriToConnectTo).ConfigureAwait(false);
                //await RefreshInstanceDetails(dvService, _targetInstanceUriToConnectTo).ConfigureAwait(false);
                // Format the URL for WebAPI service.
                if (OrganizationVersion != null && OrganizationVersion.Major >= 8)
                {
                    // Need to come back to this later to allow it to connect to the correct API endpoint.
                    ConnectODataBaseUriActual = new Uri(string.Format(WebApiUriFormat, _targetInstanceUriToConnectTo.Scheme, _targetInstanceUriToConnectTo.DnsSafeHost, OrganizationVersion.ToString(2)));
                }
            }
            sw.Stop();
            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Direct Login Process {0} - duration {1}", dvService != null ? "Succeeded" : "Failed", sw.Elapsed.Duration().ToString()));
            return dvService;
        }

        /// <summary>
        /// Get Organization version Information for Setup.
        /// </summary>
        /// <param name="dvService"></param>
        /// <param name="uriOfInstance"></param>
        /// <returns></returns>
        /// <exception cref="DataverseConnectionException"></exception>
        private async Task GetServerVersion(IOrganizationService dvService, Uri uriOfInstance)
        {
            if (dvService != null)
            {
                try
                {
                    Guid trackingID = Guid.NewGuid();
                    logEntry.Log(string.Format("Querying Organization Version. Request ID: {0}", trackingID));
                    Stopwatch dtQueryTimer = Stopwatch.StartNew();

                    RetrieveVersionResponse getVersionResp = null;
                    var request = new RetrieveVersionRequest() { RequestId = trackingID };
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - RetrieveVersionRequest : RequestId={0}", trackingID));
                    if (_configuration.Value.UseWebApiLoginFlow)
                    {
                        OrganizationResponse orgResp = await Command_WebAPIProcess_ExecuteAsync(
                            request, null, false, null, Guid.Empty, false, _configuration.Value.MaxRetryCount, _configuration.Value.RetryPauseTime, new CancellationToken(), uriOfInstance, true).ConfigureAwait(false);
                        try
                        {
                            getVersionResp = (RetrieveVersionResponse)orgResp;
                        }
                        catch (Exception ex)
                        {
                            dtQueryTimer.Stop();
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - RetrieveVersionRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
                            logEntry.Log("************ Exception - Failed to lookup current organization version", TraceEventType.Error, ex);
                            throw new DataverseOperationException($"Failure to convert OrganziationResponse to requested type - request was {request.RequestName}", ex);
                        }
                    }
                    else
                    {
                        try
                        {
                            getVersionResp = (RetrieveVersionResponse)dvService.Execute(request);
                        }
                        catch (Exception ex)
                        {
                            dtQueryTimer.Stop();
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - RetrieveVersionRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
                            logEntry.Log("************ Exception - Failed to lookup current organization version", TraceEventType.Error, ex);
                            throw new DataverseOperationException("Exception - Failed to lookup current organization version", ex);
                        }
                    }
                    dtQueryTimer.Stop();
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - RetrieveVersionRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()));

                    if (getVersionResp != null)
                    {
                        OrganizationVersion = new Version("0.0.0.0");
                        try
                        {
                            if (Version.TryParse(getVersionResp.Version, out Version outVer))
                            {
                                OrganizationVersion = outVer;
                            }
                        }
                        catch { };
                    }
                    logEntry.Log("Completed Parsing Organization Instance Version", TraceEventType.Verbose);
                }
                catch (Exception ex)
                {
                    logEntry.Log("************ Exception - Fault While initializing client - RetrieveVersionRequest", TraceEventType.Error, ex);
                    HttpOperationException opEx = Utilities.SeekExceptionOnStack<HttpOperationException>(ex);
                    if (opEx != null)
                        throw DataverseConnectionException.GenerateClientConnectionException(opEx);
                    else
                        throw new DataverseConnectionException("Exception - Fault While initializing client - RetrieveVersionRequest", ex);
                }
            }
        }

        /// <summary>
        /// Refresh the organization instance details.
        /// </summary>
        /// <param name="dvService">ConnectionSvc</param>
        /// <param name="uriOfInstance">Instance URL</param>
        private async Task RefreshInstanceDetails(IOrganizationService dvService, Uri uriOfInstance)
        {
            // Load the organization instance details
            if (dvService != null)
            {
                try
                {
                    //TODO:// Add Logic here to improve perf by connecting to global disco.
                    Guid trackingID = Guid.NewGuid();
                    logEntry.Log(string.Format("Querying Organization Instance Details. Request ID: {0}", trackingID));
                    Stopwatch dtQueryTimer = Stopwatch.StartNew();

                    var request = new RetrieveCurrentOrganizationRequest() { AccessType = 0, RequestId = trackingID };
                    RetrieveCurrentOrganizationResponse resp;
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - RetrieveCurrentOrganizationRequest : RequestId={0}", trackingID));
                    if (_configuration.Value.UseWebApiLoginFlow)
                    {
                        OrganizationResponse orgResp = await Command_WebAPIProcess_ExecuteAsync(
                            request, null, false, null, Guid.Empty, false, _configuration.Value.MaxRetryCount, _configuration.Value.RetryPauseTime, new CancellationToken(), uriOfInstance, true).ConfigureAwait(false);
                        try
                        {
                            resp = (RetrieveCurrentOrganizationResponse)orgResp;
                        }
                        catch (Exception ex)
                        {
                            dtQueryTimer.Stop();
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - RetrieveCurrentOrganizationRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
                            logEntry.Log("************ Exception - Failed to lookup current organization information", TraceEventType.Error, ex);
                            throw new DataverseOperationException($"Failure to convert OrganziationResponse to requested type - request was {request.RequestName}", ex);
                        }
                    }
                    else
                    {
                        try
                        {
                            resp = (RetrieveCurrentOrganizationResponse)dvService.Execute(request);
                        }
                        catch (Exception ex)
                        {
                            dtQueryTimer.Stop();
                            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - RetrieveCurrentOrganizationRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
                            logEntry.Log("************ Exception - Failed to lookup current organization information", TraceEventType.Error, ex);
                            throw new DataverseOperationException("Exception - Failed to lookup current organization data", ex);
                        }
                    }
                    dtQueryTimer.Stop();
                    // Left in information mode intentionally
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - RetrieveCurrentOrganizationRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()));

                    if (resp?.Detail != null)
                    {
                        _OrgDetail = new OrganizationDetail();
                        //Add Endpoints.
                        foreach (var ep in resp.Detail.Endpoints)
                        {
                            string endPointName = ep.Key.ToString();
                            EndpointType epd = EndpointType.OrganizationDataService;
                            Enum.TryParse<EndpointType>(endPointName, out epd);

                            if (!_OrgDetail.Endpoints.ContainsKey(epd))
                                _OrgDetail.Endpoints.Add(epd, ep.Value);
                            else
                                _OrgDetail.Endpoints[epd] = ep.Value;
                        }
                        _OrgDetail.FriendlyName = resp.Detail.FriendlyName;
                        _OrgDetail.OrganizationId = resp.Detail.OrganizationId;
                        _OrgDetail.OrganizationVersion = resp.Detail.OrganizationVersion;
                        _OrgDetail.EnvironmentId = resp.Detail.EnvironmentId;
                        _OrgDetail.TenantId = resp.Detail.TenantId;
                        _OrgDetail.Geo = resp.Detail.Geo;
                        _OrgDetail.UrlName = resp.Detail.UrlName;

                        OrganizationState ostate = OrganizationState.Disabled;
                        Enum.TryParse<OrganizationState>(_OrgDetail.State.ToString(), out ostate);

                        _OrgDetail.State = ostate;
                        _OrgDetail.UniqueName = resp.Detail.UniqueName;
                        _OrgDetail.UrlName = resp.Detail.UrlName;
                        _OrgDetail.DatacenterId = resp.Detail.DatacenterId;
                        _OrgDetail.SchemaType = resp.Detail.SchemaType;
                        _OrgDetail.OrganizationType = resp.Detail.OrganizationType;

                    }
                    _organization = _OrgDetail.UniqueName;

                    // try to create a version number from the org.
                    OrganizationVersion = new Version("0.0.0.0");
                    try
                    {
                        if (Version.TryParse(_OrgDetail.OrganizationVersion, out Version outVer))
                        {
                            OrganizationVersion = outVer;
                        }
                    }
                    catch { };

                    if (_OrgDetail != null)
                    {
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture,
                            "Read Organization Information ({0} version: {1})", _OrgDetail.UniqueName, (_OrgDetail.OrganizationVersion ?? "Unknown").ToString()));
                    }
                    logEntry.Log("Completed Parsing Organization Instance Details", TraceEventType.Verbose);
                }
                catch (Exception ex)
                {
                    logEntry.Log("************ Exception - Fault While initializing client - RefreshInstanceDetails", TraceEventType.Error, ex);
                    HttpOperationException opEx = Utilities.SeekExceptionOnStack<HttpOperationException>(ex);
                    if ( opEx != null )
                        throw DataverseConnectionException.GenerateClientConnectionException(opEx);
                    else
                        throw new DataverseConnectionException("Exception - Fault While initializing client - RefreshInstanceDetails", ex);
                }
            }
        }

        /// <summary>
        /// Get current user info.
        /// </summary>
        /// <param name="trackingID"></param>
        /// <param name="dvService"></param>
        internal async Task<WhoAmIResponse> GetWhoAmIDetails(IOrganizationService dvService, Guid trackingID = default)
        {
            if (dvService != null)
            {
                Stopwatch dtQueryTimer = Stopwatch.StartNew();
                try
                {
                    if (trackingID == Guid.Empty)
                        trackingID = Guid.NewGuid();

                    WhoAmIRequest req = new WhoAmIRequest();
                    if (trackingID != Guid.Empty) // Add Tracking number of present.
                        req.RequestId = trackingID;

                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - WhoAmIRequest : RequestId={0}", trackingID.ToString()));
                    WhoAmIResponse resp;
                    if (_configuration.Value.UseWebApiLoginFlow)
                    {
                        resp = (WhoAmIResponse)(await Command_WebAPIProcess_ExecuteAsync(
                            req, null, false, null, Guid.Empty, false, _configuration.Value.MaxRetryCount, _configuration.Value.RetryPauseTime, new CancellationToken(), inLoginFlow:true).ConfigureAwait(false));
                    }
                    else
                    {
                        resp = (WhoAmIResponse)dvService.Execute(req);
                    }

                    // Left in information mode intentionally.
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - WhoAmIRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()));
                    return resp;
                }
                catch (Exception ex)
                {
                    logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - WhoAmIRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
                    logEntry.Log("************ Exception - Failed to lookup current user", TraceEventType.Error, ex);
                    throw new DataverseOperationException("Exception - Failed to lookup current user", ex);
                }
                finally
                {
                    dtQueryTimer.Stop();
                }
            }
            else
                logEntry.Log("Cannot Look up current user - No Connection to work with.", TraceEventType.Error);

            return null;

        }

        /// <summary>
        /// Sets Properties on the cloned instance.
        /// </summary>
        /// <param name="sourceClient">Source instance to clone from</param>
        internal void SetClonedProperties(ServiceClient sourceClient)
        {
            if (sourceClient is null)
                throw new ArgumentNullException(nameof(sourceClient));

            if (sourceClient._connectionSvc is null)
                throw new NullReferenceException("Source Connection Service is Failed, Cannot create a clone.");

            // Sets the cloned properties from the caller.
            int debugingCloneStateFilter = 0;
            try
            {
                System.Diagnostics.Trace.WriteLine($"Cloning {sourceClient._connectionSvc._ServiceCACHEName} to create {_ServiceCACHEName}");
                _isCloning = true;
                OrgDetailsRead = sourceClient._connectionSvc.OrgDetailsRead;
                debugingCloneStateFilter++;
                user = sourceClient.SystemUser;
                debugingCloneStateFilter++;
                ConnectedOrganizationDetail = sourceClient.OrganizationDetail;
                debugingCloneStateFilter++;
                OrganizationVersion = sourceClient._connectionSvc.OrganizationVersion;
                debugingCloneStateFilter++;
                OrganizationId = sourceClient.ConnectedOrgId;
                debugingCloneStateFilter++;
                _ActualDataverseOrgUri = sourceClient.ConnectedOrgUriActual;
                debugingCloneStateFilter++;
                _MsalAuthClient = sourceClient._connectionSvc._MsalAuthClient;
                debugingCloneStateFilter++;
                _authenticationResultContainer = sourceClient._connectionSvc._authenticationResultContainer;
                debugingCloneStateFilter++;
                TenantId = sourceClient.TenantId;
                debugingCloneStateFilter++;
                EnvironmentId = sourceClient.EnvironmentId;
                debugingCloneStateFilter++;
                GetAccessTokenAsync = sourceClient.GetAccessToken;
                debugingCloneStateFilter++;
                _clientId = sourceClient._connectionSvc._clientId;
                debugingCloneStateFilter++;
                _certificateStoreLocation = sourceClient._connectionSvc._certificateStoreLocation;
                debugingCloneStateFilter++;
                _certificateThumbprint = sourceClient._connectionSvc._certificateThumbprint;
                debugingCloneStateFilter++;
                _certificateOfConnection = sourceClient._connectionSvc._certificateOfConnection;
                debugingCloneStateFilter++;
                _redirectUri = sourceClient._connectionSvc._redirectUri;
                debugingCloneStateFilter++;
                _resource = sourceClient._connectionSvc._resource;
                debugingCloneStateFilter++;
                EnableCookieRelay = sourceClient._connectionSvc.EnableCookieRelay;
                debugingCloneStateFilter++;
                RequestAdditionalHeadersAsync = sourceClient._connectionSvc.RequestAdditionalHeadersAsync;
                debugingCloneStateFilter++;
            }
            catch (Exception ex)
            {
                throw new DataverseOperationException($"Failed constructing cloned connection. debugstate={debugingCloneStateFilter}", ex);
            }
            finally 
            { 
                _isCloning = false;  // reset flag. 
            }
        }

        #region WebAPI Interface Utilities

        internal async Task<OrganizationResponse> Command_WebAPIProcess_ExecuteAsync(OrganizationRequest req, string logMessageTag, bool bypassPluginExecution,
            MetadataUtility metadataUtlity, Guid callerId, bool disableConnectionLocking, int maxRetryCount, TimeSpan retryPauseTime, CancellationToken cancellationToken, Uri uriOfInstance = null, bool inLoginFlow = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Utilities.IsRequestValidForTranslationToWebAPI(req , inLoginFlow))
            {
                logEntry.Log("Execute Organization Request failed, WebAPI is only supported for limited type of messages at this time.", TraceEventType.Error);
                return null;
            }

            HttpMethod methodToExecute = Utilities.RequestNameToHttpVerb(req.RequestName);
            Entity cReq = null;
            if (req.Parameters.ContainsKey("Target") && req.Parameters["Target"] is Entity ent) 
            {
                cReq = ent;
            }
            else if (req.Parameters.ContainsKey("Target") && req.Parameters["Target"] is EntityReference entRef)
            {
                cReq = entRef.KeyAttributes.Any()
                    ? new Entity(entRef.LogicalName, entRef.KeyAttributes)
                    : new Entity(entRef.LogicalName, entRef.Id);
                cReq.RowVersion = entRef.RowVersion; 
            }

            EntityMetadata entityMetadata = null;
            if (cReq != null)
            {
                // if CRUD type. get Entity
                cancellationToken.ThrowIfCancellationRequested();
                entityMetadata = metadataUtlity.GetEntityMetadata(EntityFilters.Relationships, cReq.LogicalName);
                if (entityMetadata == null)
                {
                    logEntry.Log($"Execute Organization Request failed, failed to acquire entity data for {cReq.LogicalName}", TraceEventType.Warning);
                    return null;
                }
            }

            // generate webAPI Create request.
            string postUri = Utilities.ConstructWebApiRequestUrl(req, methodToExecute, cReq, entityMetadata);
            string bodyOfRequest = string.Empty;

            ExpandoObject requestBodyObject = null;

            if (cReq != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                requestBodyObject = Utilities.ToExpandoObject(cReq, metadataUtlity, methodToExecute, logEntry);
                if (cReq.RelatedEntities != null && cReq.RelatedEntities.Count > 0)
                    requestBodyObject = Utilities.ReleatedEntitiesToExpandoObject(requestBodyObject, cReq.LogicalName, cReq.RelatedEntities, metadataUtlity, methodToExecute, logEntry);
            }
            else
            {
                if (methodToExecute == HttpMethod.Post)
                {
                    requestBodyObject = req.ToExpandoObject();
                }
            }

            if (requestBodyObject != null)
                bodyOfRequest = System.Text.Json.JsonSerializer.Serialize(requestBodyObject);

            // Process request params.
            if (req.Parameters.ContainsKey(Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION))
            {
                if (req.Parameters[Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION].GetType() == typeof(bool) &&
                        (bool)req.Parameters[Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION])
                {
                    bypassPluginExecution = true;
                }
            }

            string solutionUniqueNameHeaderValue = string.Empty;
            if (req.Parameters.ContainsKey(Utilities.RequestHeaders.SOLUTIONUNIQUENAME))
            {
                if (req.Parameters[Utilities.RequestHeaders.SOLUTIONUNIQUENAME].GetType() == typeof(string) &&
                        !String.IsNullOrEmpty((string)req.Parameters[Utilities.RequestHeaders.SOLUTIONUNIQUENAME]))
                {
                    solutionUniqueNameHeaderValue = req.Parameters[Utilities.RequestHeaders.SOLUTIONUNIQUENAME].ToString();
                }
            }

            bool? suppressDuplicateDetection = null;
            if (req.Parameters.ContainsKey(Utilities.RequestHeaders.SUPPRESSDUPLICATEDETECTION))
            {
                if (req.Parameters[Utilities.RequestHeaders.SUPPRESSDUPLICATEDETECTION].GetType() == typeof(bool))
                {
                    suppressDuplicateDetection = (bool)req.Parameters[Utilities.RequestHeaders.SUPPRESSDUPLICATEDETECTION];
                }
            }

            string tagValue = string.Empty;
            if (req.Parameters.ContainsKey(Utilities.RequestHeaders.TAG))
            {
                if (req.Parameters[Utilities.RequestHeaders.TAG].GetType() == typeof(string) &&
                        !String.IsNullOrEmpty((string)req.Parameters[Utilities.RequestHeaders.TAG]))
                {
                    tagValue = req.Parameters[Utilities.RequestHeaders.TAG].ToString();
                }
            }

            string rowVersion = string.Empty;
            string IfMatchHeaderTag = string.Empty;
            if (req.Parameters.ContainsKey(Utilities.RequestHeaders.CONCURRENCYBEHAVIOR) &&
                (ConcurrencyBehavior)req.Parameters[Utilities.RequestHeaders.CONCURRENCYBEHAVIOR] != ConcurrencyBehavior.Default)
            {
                // Found concurrency flag.
                if (!string.IsNullOrEmpty(cReq.RowVersion))
                {
                    rowVersion = cReq.RowVersion;
                    // Now manage behavior.
                    // if IfRowVersionMatches == Upsert/update/Delete should only work if record exists by rowRowVersion. == If-Match + RowVersion.
                    // If AlwaysOverwrite == Upsert/update/Delete should only work if record exists at all == No IF-MatchTag.
                    if ((ConcurrencyBehavior)req.Parameters[Utilities.RequestHeaders.CONCURRENCYBEHAVIOR] == ConcurrencyBehavior.AlwaysOverwrite)
                    {
                        IfMatchHeaderTag = "If-Match";
                        rowVersion = "*";
                    }
                    if ((ConcurrencyBehavior)req.Parameters[Utilities.RequestHeaders.CONCURRENCYBEHAVIOR] == ConcurrencyBehavior.IfRowVersionMatches)
                    {
                        IfMatchHeaderTag = "If-Match";
                    }
                }
                else
                {
                    DataverseOperationException opEx = new DataverseOperationException("Request Failed, RowVersion is missing and is required when ConcurrencyBehavior is set to a value other than Default.");
                    logEntry.Log(opEx);
                    return null;
                }
            }
            else
            {
                switch (req.RequestName.ToLowerInvariant())
                {
                    case "update":
                    case "delete":
                        // Set the default behavior for update/delete
                        // this will cause update to fail ( not run upsert ) if there is no concurrency behavior.
                        IfMatchHeaderTag = "If-Match";
                        rowVersion = "*";
                        break;
                }

            }

            // Setup headers.
            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>();
            headers.Add("Prefer", new List<string>() { "odata.include-annotations=*" });

            if (!string.IsNullOrEmpty(IfMatchHeaderTag))
            {
                if (rowVersion != "*")
                {
                    headers.Add(IfMatchHeaderTag, new List<string>() { $"W/\"{rowVersion}\"" });
                }
                else
                {
                    headers.Add(IfMatchHeaderTag, new List<string>() { $"*" });
                }
            }

            if (bypassPluginExecution && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(OrganizationVersion, Utilities.FeatureVersionMinimums.AllowBypassCustomPlugin))
            {
                headers.Add($"{Utilities.RequestHeaders.DATAVERSEHEADERPROPERTYPREFIX}{Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION}", new List<string>() { "true" });
            }

            if (!string.IsNullOrEmpty(solutionUniqueNameHeaderValue))
            {
                headers.Add($"{Utilities.RequestHeaders.DATAVERSEHEADERPROPERTYPREFIX}{Utilities.RequestHeaders.SOLUTIONUNIQUENAME}", new List<string>() { solutionUniqueNameHeaderValue });
            }

            if (suppressDuplicateDetection.HasValue)
            {
                headers.Add($"{Utilities.RequestHeaders.DATAVERSEHEADERPROPERTYPREFIX}{Utilities.RequestHeaders.SUPPRESSDUPLICATEDETECTION}", new List<string>() { suppressDuplicateDetection.ToString() });
            }

            string addedQueryParams = "";
            // modify post URI
            if (!string.IsNullOrEmpty(tagValue))
            {
                //UriBuilder uriBuilder = new UriBuilder(postUri);
                var paramValues = System.Web.HttpUtility.ParseQueryString(addedQueryParams);
                paramValues.Add($"{Utilities.RequestHeaders.TAG}", tagValue);
                addedQueryParams = paramValues.ToString();
            }

            // add queryParms to the PostUri.
            if (!string.IsNullOrEmpty(addedQueryParams))
            {
                postUri = $"{postUri}?{addedQueryParams}";
            }

            Guid userProvidedRequestId = Guid.Empty;
            if (req.RequestId.HasValue && req.RequestId != Guid.Empty )
            {
                userProvidedRequestId = req.RequestId.Value;
            }

            RequestBinderUtil.GetAdditionalHeaders(headers, req); 

            // Execute request
            var sResp = await Command_WebExecuteAsync(postUri, bodyOfRequest, methodToExecute, headers, "application/json", logMessageTag, callerId, disableConnectionLocking, maxRetryCount, retryPauseTime, uriOfInstance, cancellationToken: cancellationToken, requestTrackingId: userProvidedRequestId).ConfigureAwait(false);
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
                }
                else if (req is UpsertRequest)
                {
                    //var upsertReturn = new UpsertResponse();
                    return null;
                }
                else
                {
                    var json = await sResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (_knownTypesFactory.TryCreate($"{req.RequestName}Response", out var response, json))
                    {
                        OrganizationResponse resp = (OrganizationResponse)response;
                        if (string.IsNullOrEmpty(resp.ResponseName))
                            resp.ResponseName = $"{req.RequestName}Response";

                        return resp;
                    }

                    var orgResponse = new OrganizationResponse();

                    orgResponse.ResponseName = $"{req.RequestName}Response";

                    return orgResponse;
                }
            }
            else
                return null;
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
        /// <param name="callerId">current caller ID</param>
        /// <param name="disableConnectionLocking">disable connection locking</param>
        /// <param name="maxRetryCount">max retry count</param>
        /// <param name="retryPauseTime">retry pause time</param>
        /// <param name="uriOfInstance">uri of instance</param>
        /// <param name="requestTrackingId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<HttpResponseMessage> Command_WebExecuteAsync(string queryString, string body, HttpMethod method, Dictionary<string, List<string>> customHeaders,
            string contentType, string errorStringCheck, Guid callerId, bool disableConnectionLocking, int maxRetryCount, TimeSpan retryPauseTime, Uri uriOfInstance = null, Guid requestTrackingId = default, CancellationToken cancellationToken = default)
        {
            Stopwatch logDt = Stopwatch.StartNew();
            int retryCount = 0;
            bool retry = false;

            if (requestTrackingId == Guid.Empty)
                requestTrackingId = Guid.NewGuid();


            // Default Odata 4.0 headers.
            Dictionary<string, string> defaultODataHeaders = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { "OData-MaxVersion", "4.0" },
                { "OData-Version", "4.0" }
            };
            //defaultODataHeaders.Add("If-None-Match", "");

            // Supported Version Check.
            if (OrganizationVersion != null && !(Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(OrganizationVersion, Utilities.FeatureVersionMinimums.WebAPISupported)))
            {
                logEntry.Log($"Web API Service is not supported by the ServiceClient in {OrganizationVersion} version of XRM", TraceEventType.Error, new InvalidOperationException($"Web API Service is not supported by the ServiceClient in {OrganizationVersion} version of XRM"));
                return null;
            }

            if (AuthenticationTypeInUse == AuthenticationType.OAuth)
                CalledbyExecuteRequest = true;

            // Format URI for request.
            Uri TargetUri = null;
            string webApiUri = null;
            ConnectedOrgPublishedEndpoints?.TryGetValue(EndpointType.OrganizationDataService, out webApiUri);
            if (webApiUri == null || !webApiUri.Contains("/data/"))
            {
                Uri tempUri = string.IsNullOrWhiteSpace(webApiUri) ? uriOfInstance : new Uri(webApiUri);
                // Not using GD,  update for web API
                webApiUri = string.Format(BaseWebAPIDataFormat, $"{tempUri.Scheme}://{tempUri.DnsSafeHost}", OrganizationVersion != null ? OrganizationVersion.ToString(2) : "9.0");
            }

            if (!Uri.TryCreate($"{webApiUri}{queryString}", UriKind.Absolute, out TargetUri))
            {
                logEntry.Log(string.Format("Invalid URI formed for request - {0}", string.Format("{2} {0}{1}", webApiUri, queryString, method)), TraceEventType.Error);
                return null;
            }

            // Add Headers.
            if (customHeaders == null)
                customHeaders = new Dictionary<string, List<string>>();
            else
            {
                if (customHeaders.ContainsKey(Utilities.RequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER))
                {
                    customHeaders.Remove(Utilities.RequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER);
                    logEntry.Log(string.Format("Removing customer header {0} - Use CallerAADObjectId property instead", Utilities.RequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER));
                }

                if (customHeaders.ContainsKey(Utilities.RequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER))
                {
                    customHeaders.Remove(Utilities.RequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER);
                    logEntry.Log(string.Format("Removing customer header {0} - Use CallerId property instead", Utilities.RequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER));
                }

                if (customHeaders.ContainsKey(Utilities.RequestHeaders.FORCE_CONSISTENCY))
                {
                    customHeaders.Remove(Utilities.RequestHeaders.FORCE_CONSISTENCY);
                    logEntry.Log(string.Format("Removing customer header {0} - Use ForceServerMetadataCacheConsistency property instead", Utilities.RequestHeaders.FORCE_CONSISTENCY));
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
            if (callerId != Guid.Empty)
            {
                customHeaders.Add(Utilities.RequestHeaders.CALLER_OBJECT_ID_HTTP_HEADER, new List<string>() { callerId.ToString() });
            }
            else
            {
                if (CallerAADObjectId.HasValue)
                {
                    // Value in Caller object ID.
                    if (CallerAADObjectId.Value != Guid.Empty)
                    {
                        customHeaders.Add(Utilities.RequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER, new List<string>() { CallerAADObjectId.ToString() });
                    }
                }
            }

            // Add tracking headers
            // Request id
            if (!customHeaders.ContainsKey(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID))
            {
                customHeaders.Add(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID, new List<string>() { requestTrackingId.ToString() });
            }
            else
            {
                Guid guTempId = Guid.Empty;
                List<string> keyValues = customHeaders[Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID];
                if (keyValues != null && keyValues.Count > 0)
                    Guid.TryParse(keyValues.First(), out guTempId);

                if (guTempId == Guid.Empty) // passed in value did not parse.
                {
                    // Assign Tracking Guid in
                    customHeaders.Remove(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID);
                    customHeaders.Add(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID, new List<string>() { requestTrackingId.ToString() });
                }
                else
                    requestTrackingId = guTempId;

            }
            // Session id.
            if (SessionTrackingId.HasValue && SessionTrackingId != Guid.Empty && !customHeaders.ContainsKey(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID))
                customHeaders.Add(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID, new List<string>() { SessionTrackingId.Value.ToString() });

            // Add force Consistency
            if (ForceServerCacheConsistency && !customHeaders.ContainsKey(Utilities.RequestHeaders.FORCE_CONSISTENCY))
                customHeaders.Add(Utilities.RequestHeaders.FORCE_CONSISTENCY, new List<string>() { "Strong" });

            // handle added headers added via the RequestAdditionaHeadersAsync method.
            if (RequestAdditionalHeadersAsync != null)
            {
                try
                {
                    var addedHeaders = RequestAdditionalHeadersAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (addedHeaders != null && addedHeaders.Count() > 0)
                    {
                        foreach (var hrdProp in addedHeaders)
                        {
                            // General handling from here on out.
                            if (customHeaders.Keys.Contains(hrdProp.Key))
                            {
                                if (customHeaders[hrdProp.Key] == null)
                                    customHeaders[hrdProp.Key] = new List<string>();

                                customHeaders[hrdProp.Key].Add(hrdProp.Value);
                            }
                            else
                            {
                                customHeaders.Add(hrdProp.Key, new List<string>() { hrdProp.Value });
                            }
                        }
                        addedHeaders.Clear();
                    }
                }
                finally { } // do nothing on header failure for now.
            }

            if (EnableCookieRelay && CurrentCookieCollection != null)
            {
                customHeaders.Add("Cookie", Utilities.GetCookiesFromCollectionAsArray(CurrentCookieCollection));
            }

            HttpResponseMessage resp = null;

            //RequestId Logging line for telemetry 
            string requestIdLogSegement = logEntry.GetFormatedRequestSessionIdString(requestTrackingId, SessionTrackingId);
            do
            {
                retry = false; // Set intial state. 

                // Add authorization header. - Here to catch the situation where a token expires during retry.
                if (!customHeaders.ContainsKey(Utilities.RequestHeaders.AUTHORIZATION_HEADER))
                    customHeaders.Add(Utilities.RequestHeaders.AUTHORIZATION_HEADER, new List<string>() { string.Format("Bearer {0}", await RefreshClientTokenAsync().ConfigureAwait(false)) });
                logDt.Stop();
                logDt = Stopwatch.StartNew();

                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Execute Command - {0}{1}: {2}",
                        $"{method} {queryString}",
                        string.IsNullOrEmpty(errorStringCheck) ? "" : $" : {errorStringCheck} ",
                        requestIdLogSegement
                        ), TraceEventType.Verbose);
                try
                {
                    resp = await ConnectionService.ExecuteHttpRequestAsync(
                            TargetUri.ToString(),
                            method,
                            cancellationToken: cancellationToken,
                            body: body,
                            customHeaders: customHeaders,
                            logSink: logEntry,
                            contentType: contentType,
                            requestTrackingId: requestTrackingId,
                            sessionTrackingId: SessionTrackingId.HasValue ? SessionTrackingId.Value : Guid.Empty,
                            providedHttpClient: WebApiHttpClient == null ? ClientServiceProviders.Instance.GetService<IHttpClientFactory>().CreateClient("DataverseHttpClientFactory") : WebApiHttpClient
                            ).ConfigureAwait(false);

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
                        retry = ShouldRetryWebAPI(ex, retryCount, maxRetryCount, retryPauseTime, out isThrottled);
                        if (retry)
                        {
                            retryCount = await Utilities.RetryRequest(null, requestTrackingId, TimeSpan.Zero, logDt, logEntry, SessionTrackingId, disableConnectionLocking, _retryPauseTimeRunning, ex, errorStringCheck, retryCount, isThrottled, webUriReq: $"{method} {queryString}", cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            logEntry.LogRetry(retryCount, null, _retryPauseTimeRunning, true, isThrottled: isThrottled, webUriMessageReq: $"{method} {queryString}");
                            logEntry.LogException(null, ex, errorStringCheck, webUriMessageReq: $"{method} {queryString}");
                            logEntry.LogFailure(null, requestTrackingId, SessionTrackingId, disableConnectionLocking, TimeSpan.Zero, logDt, ex, errorStringCheck, true, webUriMessageReq: $"{method} {queryString}");
                            throw;
                        }
                        resp = null;
                    }
                    else
                    {
                        retry = false;
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Execute Command - {2} {0} : {1}", queryString, requestIdLogSegement, method), TraceEventType.Verbose);
                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception - {2} : {0} |=> {1}", errorStringCheck, ex.Message, queryString), TraceEventType.Error, ex);
                        throw;
                    }
                }
                finally
                {
                    logDt.Stop();
                }
            } while (retry);

            if (resp != null)
            {
                CurrentCookieCollection = Utilities.GetAllCookiesFromHeader(resp.Headers.SingleOrDefault(header => header.Key == Utilities.ResponseHeaders.SETCOOKIE).Value?.ToArray(), CurrentCookieCollection);
                string dregreeofparallelismHint = resp.Headers.SingleOrDefault(header => header.Key == Utilities.ResponseHeaders.RECOMMENDEDDEGREESOFPARALLELISM).Value?.FirstOrDefault();
                if (!string.IsNullOrEmpty(dregreeofparallelismHint))
                {
                    if (int.TryParse(dregreeofparallelismHint, out int idop))
                    {
                        RecommendedDegreesOfParallelism = idop;
                    }
                }
            }

            return resp;
        }

        /// <summary>
        /// retry request or not
        /// </summary>
        /// <param name="ex">exception</param>
        /// <param name="retryCount">retry count</param>
        /// <param name="maxRetryCount">max retry count</param>
        /// <param name="retryPauseTime">retry pause time</param>
        /// <param name="isThrottlingRetry">when true, indicates that the retry was caused by a throttle tripping.</param>
        /// <returns></returns>
        private bool ShouldRetryWebAPI(Exception ex, int retryCount, int maxRetryCount, TimeSpan retryPauseTime, out bool isThrottlingRetry)
        {
            isThrottlingRetry = false;
            if (retryCount >= maxRetryCount)
            {
                return false;
            }

            if (ex is HttpOperationException httpOperationException)
            {
                if (httpOperationException.Response.StatusCode == HttpStatusCode.Unauthorized)
                    return false;

                JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
                var errorCode = contentBody["error"]["code"].ToString();
                var errorMessage = DataverseTraceLogger.GetFirstLineFromString(contentBody["error"]["message"].ToString()).Trim();
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
                else if (httpOperationException.Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    _retryPauseTimeRunning = retryPauseTime; // default timespan.
                    isThrottlingRetry = true;
                }
                else if ((int)httpOperationException.Response.StatusCode == 429 ||
                    httpOperationException.Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Throttled. need to react according.
                    if (errorCode == ((int)ErrorCodes.ThrottlingBurstRequestLimitExceededError).ToString() ||
                        errorCode == ((int)ErrorCodes.ThrottlingTimeExceededError).ToString() ||
                        errorCode == ((int)ErrorCodes.ThrottlingConcurrencyLimitExceededError).ToString())
                    {
                        if (httpOperationException.Response.Headers.TryGetValue("Retry-After", out var retryAfter) && double.TryParse(retryAfter.FirstOrDefault(), out var retrySeconds))
                        { 
                            // Note: Retry-After header is in seconds.
                             _retryPauseTimeRunning = TimeSpan.FromSeconds(retrySeconds);
                        }
                        else
                        { 
                            _retryPauseTimeRunning = retryPauseTime.Add(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); ; // default timespan with back off is response does not contain the tag..
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

        /// <summary>
        /// Makes a call to a web API to support request to XRM.
        /// </summary>
        /// <param name="uri">URI of request target</param>
        /// <param name="method">method being used</param>
        /// <param name="body">body of request</param>
        /// <param name="customHeaders">Headers applied to request</param>
        /// <param name="cancellationToken">Cancellation token if required</param>
        /// <param name="logSink">Log Sink if being called externally.</param>
        /// <param name="requestTrackingId">ID of the request if set by an external caller</param>
        /// <param name="contentType">content type to use when calling into the remote host</param>
        /// <param name="sessionTrackingId">Session Tracking ID to assoicate with the request.</param>
        /// <param name="providedHttpClient"></param>
        /// <returns></returns>
        internal static async Task<HttpResponseMessage> ExecuteHttpRequestAsync(string uri, HttpMethod method, string body = default(string), Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken), DataverseTraceLogger logSink = null, Guid? requestTrackingId = null, string contentType = default(string), Guid? sessionTrackingId = null, HttpClient providedHttpClient = null)
        {
            bool isLogEntryCreatedLocaly = false;
            if (logSink == null)
            {
                logSink = new DataverseTraceLogger();
                isLogEntryCreatedLocaly = true;
            }

            Guid RequestId = Guid.NewGuid();
            if (requestTrackingId.HasValue)
                RequestId = requestTrackingId.Value;

            HttpResponseMessage _httpResponse = null;
            Stopwatch logDt = Stopwatch.StartNew();
            try
            {
                using (var _httpRequest = new HttpRequestMessage())
                {
                    _httpRequest.Method = method;
                    _httpRequest.RequestUri = new System.Uri(uri);

                    // Set Headers
                    // Add User Agent and request id to send.
                    string Agent = "Unknown";
                    if (AppDomain.CurrentDomain != null)
                    {
                        Agent = AppDomain.CurrentDomain.FriendlyName;
                    }
                    Agent = $"{Agent} (DataverseSvcClient:{Environs.DvSvcClientFileVersion})";

                    if (!_httpRequest.Headers.Contains(Utilities.RequestHeaders.USER_AGENT_HTTP_HEADER))
                        _httpRequest.Headers.TryAddWithoutValidation(Utilities.RequestHeaders.USER_AGENT_HTTP_HEADER, string.IsNullOrEmpty(Agent) ? "" : Agent);

                    if (customHeaders != null)
                    {
                        foreach (var _header in customHeaders)
                        {
                            _httpRequest.Headers.TryAddWithoutValidation(_header.Key, _header.Value);
                        }

                        if (!_httpRequest.Headers.Contains(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID))
                            _httpRequest.Headers.TryAddWithoutValidation(Utilities.RequestHeaders.X_MS_CLIENT_REQUEST_ID, RequestId.ToString());

                        if (!_httpRequest.Headers.Contains(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID) && sessionTrackingId.HasValue && sessionTrackingId != Guid.Empty)
                            _httpRequest.Headers.TryAddWithoutValidation(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID, sessionTrackingId.ToString());

                        if (!_httpRequest.Headers.Contains("Connection"))
                            _httpRequest.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");

                    }

                    string _requestContent = null;
                    if (!string.IsNullOrEmpty(body))
                    {
                        HttpContent contentPost = null;
                        if (!string.IsNullOrEmpty(contentType))
                        {
                            contentPost = new StringContent(body);
                            if (contentPost.Headers.Contains(Utilities.RequestHeaders.CONTENT_TYPE)) // Remove the default content type if its there.
                                contentPost.Headers.Remove(Utilities.RequestHeaders.CONTENT_TYPE);
                            contentPost.Headers.TryAddWithoutValidation(Utilities.RequestHeaders.CONTENT_TYPE, contentType); // Replace with added content type
                        }
                        else
                            contentPost = new StringContent(body, Encoding.UTF8, "application/json");

                        _httpRequest.Content = contentPost;
                        _requestContent = contentPost.AsString();
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    logSink.Log(string.Format("Begin Sending request to {3} {0} : {2}RequestID={1}", _httpRequest.RequestUri.AbsolutePath, RequestId, sessionTrackingId.HasValue && sessionTrackingId.Value != Guid.Empty ? $" SessionID={sessionTrackingId.Value.ToString()} : " : "", method), TraceEventType.Verbose);

                    if (providedHttpClient != null)
                    {
                        logDt.Stop();
                        logDt = Stopwatch.StartNew();
                        try
                        {
                            if (providedHttpClient.Timeout != MaxConnectionTimeout)
                            {
                                providedHttpClient.Timeout = MaxConnectionTimeout; // Set Max connection Timeout
                            }
                        }
                        catch { }
                        _httpResponse = await providedHttpClient.SendAsync(_httpRequest, cancellationToken).ConfigureAwait(false);
                        logDt.Stop();
                    }
                    else
                    {
                        // Fall though logic to deal with an Http client not being passed in.
                        using (HttpClient httpCli = new HttpClient())
                        {
                            logDt.Stop();
                            logDt = Stopwatch.StartNew();
                            try
                            {
                                if (httpCli.Timeout != MaxConnectionTimeout)
                                {
                                    httpCli.Timeout = MaxConnectionTimeout; // Set Max connection Timeout
                                }
                            }
                            catch { }
                            _httpResponse = await httpCli.SendAsync(_httpRequest, cancellationToken).ConfigureAwait(false);
                            logDt.Stop();
                        }
                    }
                    HttpStatusCode _statusCode = _httpResponse.StatusCode;
                    logSink.Log(string.Format("Response for request to WebAPI {5} {0} : StatusCode={1} : {4}RequestID={2} : Duration={3}", _httpRequest.RequestUri.AbsolutePath, _statusCode, RequestId, logDt.Elapsed.ToString(), sessionTrackingId.HasValue && sessionTrackingId.Value != Guid.Empty ? $" SessionID={sessionTrackingId.Value.ToString()} : " : "", method), TraceEventType.Verbose);

                    cancellationToken.ThrowIfCancellationRequested();
                    string _responseContent = null;
                    if (!_httpResponse.IsSuccessStatusCode)
                    {
                        var ex = new HttpOperationException(string.Format("Operation returned an invalid status code '{0}'", _statusCode));
                        if (_httpResponse.Content != null)
                        {
                            _responseContent = await _httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            _responseContent = string.Empty;
                        }
                        ex.Request = new HttpRequestMessageWrapper(_httpRequest, _requestContent);
                        ex.Response = new HttpResponseMessageWrapper(_httpResponse, _responseContent);
                        logSink.Log(string.Format("Failure Response for request to WebAPI {5} {0} : StatusCode={1} : {4}RequestID={3} : {2}", _httpRequest.RequestUri.AbsolutePath, _statusCode, _responseContent, RequestId, sessionTrackingId.HasValue && sessionTrackingId.Value != Guid.Empty ? $" SessionID={sessionTrackingId.Value.ToString()} : " : "", method), TraceEventType.Error);

                        _httpRequest.Dispose();
                        if (_httpResponse != null)
                        {
                            _httpResponse.Dispose();
                        }
                        throw ex;
                    }
                    return _httpResponse;
                }
            }
            finally
            {
                logDt.Stop();

                if (isLogEntryCreatedLocaly)
                    logSink.Dispose();
            }
        }


        #endregion

        #region Service utilities.
        /// <summary>
        /// Discovers the organizations (OAuth Specific)
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service uri.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="clientId">The client id of registered Azure app.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
        /// <param name="isOnPrem">Determines whether onprem or </param>
        /// <param name="authority">The authority identifying the registered tenant</param>
        /// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
        /// <param name="useGlobalDisco">Use the global disco path. </param>
        /// <param name="useDefaultCreds">(optional) If true attempts login using current user</param>
        /// <param name="externalLogger">Logging provider <see cref="ILogger"/></param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns>The list of organizations discovered.</returns>
        internal static async Task<DiscoverOrganizationsResult> DiscoverOrganizationsAsync(Uri discoveryServiceUri, ClientCredentials clientCredentials, string clientId, Uri redirectUri, PromptBehavior promptBehavior, bool isOnPrem, string authority, DataverseTraceLogger logSink = null, bool useGlobalDisco = false, bool useDefaultCreds = false, string tokenCacheStorePath = null, ILogger externalLogger = null, CancellationToken cancellationToken = default)
        {
            bool isLogEntryCreatedLocaly = false;
            if (logSink == null)
            {
                logSink = new DataverseTraceLogger(externalLogger);
                isLogEntryCreatedLocaly = true;
            }

            try
            {
                logSink.Log("DiscoverOrganizations - Called using user of MFA Auth for : " + discoveryServiceUri.ToString());
                if (!useGlobalDisco)
                    return await DiscoverOrganizations_InternalAsync(discoveryServiceUri, clientCredentials, null, clientId, redirectUri, promptBehavior, isOnPrem, authority, useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath, logSink: logSink, cancellationToken: cancellationToken).ConfigureAwait(false);
                else
                {
                    return await DiscoverGlobalOrganizationsAsync(discoveryServiceUri, clientCredentials, null, clientId, redirectUri, promptBehavior, isOnPrem, authority, logSink: logSink, useDefaultCreds: useDefaultCreds, tokenCacheStorePath: tokenCacheStorePath, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

            }
            finally
            {
                if (isLogEntryCreatedLocaly)
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Discovers the organizations (OAuth Specific)
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service uri.</param>
        /// <param name="loginCertificate">The certificate to use to login</param>
        /// <param name="clientId">The client id of registered Azure app.</param>
        /// <param name="isOnPrem">Determines whether onprem or </param>
        /// <param name="authority">The authority identifying the registered tenant</param>
        /// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
        /// <param name="useDefaultCreds">(optional) If true, attempts login with current user.</param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <returns>The list of organizations discovered.</returns>
        internal static async Task<DiscoverOrganizationsResult> DiscoverOrganizationsAsync(Uri discoveryServiceUri, X509Certificate2 loginCertificate, string clientId, bool isOnPrem, string authority, DataverseTraceLogger logSink = null, bool useDefaultCreds = false, string tokenCacheStorePath = null)
        {
            bool isLogEntryCreatedLocaly = false;
            if (logSink == null)
            {
                logSink = new DataverseTraceLogger();
                isLogEntryCreatedLocaly = true;
            }
            try
            {
                logSink.Log("DiscoverOrganizations - Called using Certificate Auth for : " + discoveryServiceUri.ToString());
                return await DiscoverOrganizations_InternalAsync(discoveryServiceUri, null, loginCertificate, clientId, null, PromptBehavior.Never, isOnPrem, authority, useDefaultCreds, logSink: logSink, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
            }
            finally
            {
                if (isLogEntryCreatedLocaly)
                    logSink.Dispose();
            }
        }


        /// <summary>
        /// Async Global Disco Query endpoint.. works with the external token provider flow for UserID flows
        /// </summary>
        /// <param name="discoveryServiceUri">GD URI</param>
        /// <param name="tokenProviderFunction">Pointer to the token provider handler</param>
        /// <param name="logSink">Logging endpoint (optional)</param>
        /// <param name="externalLogger">Logging provider <see cref="ILogger"/></param>
        /// <param name="tokenCacheStorePath">(optional) path to log store</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns>Populated OrganizationDetailCollection or Null.</returns>
        internal static async Task<OrganizationDetailCollection> DiscoverGlobalOrganizationsAsync(Uri discoveryServiceUri, Func<string, Task<string>> tokenProviderFunction, DataverseTraceLogger logSink = null, string tokenCacheStorePath = null, ILogger externalLogger = null, CancellationToken cancellationToken = default)
        {
            bool isLogEntryCreatedLocaly = false;
            if (logSink == null)
            {
                logSink = new DataverseTraceLogger(externalLogger);
                isLogEntryCreatedLocaly = true;
            }

            // if the discovery URL does not contain api/discovery , base it and use it in the commercial format base.
            // Check must be here as well to deal with remote auth.
            if (!(discoveryServiceUri.Segments.Contains("api") && discoveryServiceUri.Segments.Contains("discovery")))
            {
                // do not have the full API URL here.
                discoveryServiceUri = new Uri(string.Format(_baselineGlobalDiscoveryFormater, discoveryServiceUri.DnsSafeHost, _globlaDiscoVersion, "Instances"));
            }

            try
            {
                logSink.Log("DiscoverOrganizations - : " + discoveryServiceUri.ToString());
                string AuthToken = await tokenProviderFunction(discoveryServiceUri.ToString()).ConfigureAwait(false);
                return await QueryGlobalDiscoveryAsync(AuthToken, discoveryServiceUri, logSink, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (isLogEntryCreatedLocaly)
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Discovers the organizations (OAuth Specific)
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service uri.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="loginCertificate">The Certificate used to login</param>
        /// <param name="clientId">The client id of registered Azure app.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
        /// <param name="isOnPrem">Determines whether onprem or </param>
        /// <param name="authority">The authority identifying the registered tenant</param>
        /// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
        /// <param name="useDefaultCreds">(optional) If true, tries to login with current users credentials</param>
        /// <param name="tokenCacheStorePath">(optional) token filePath, if set to empty string, the default path is used</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns>The list of organizations discovered.</returns>
        private static async Task<DiscoverOrganizationsResult> DiscoverOrganizations_InternalAsync(Uri discoveryServiceUri, ClientCredentials clientCredentials, X509Certificate2 loginCertificate, string clientId, Uri redirectUri, PromptBehavior promptBehavior, bool isOnPrem, string authority, bool useDefaultCreds = false, string tokenCacheStorePath = null, DataverseTraceLogger logSink = null, CancellationToken cancellationToken = default)
        {
            bool createdLogSource = false;
            Stopwatch dtStartQuery = Stopwatch.StartNew();
            try
            {
                if (logSink == null)
                {
                    // when set, the log source is locally created.
                    createdLogSource = true;
                    logSink = new DataverseTraceLogger();
                }


                // Initialize discovery service proxy.
                logSink.Log("DiscoverOrganizations - Initializing Discovery Server Object with " + discoveryServiceUri.ToString());

                DiscoveryWebProxyClient svcDiscoveryProxy = null;
                Uri targetServiceUrl = null;
                string authToken = string.Empty;
                string resource = string.Empty; // not used here..

                // Execute Authentication Request and return token And ServiceURI
                IAccount user = null;
                object msalAuthClientOut = null;
                ExecuteAuthenticationResults authRequestResult = await AuthProcessor.ExecuteAuthenticateServiceProcessAsync(discoveryServiceUri, clientCredentials, loginCertificate, clientId, redirectUri, promptBehavior, isOnPrem, authority, null, logSink: logSink, useDefaultCreds: useDefaultCreds, addVersionInfoToUri: false, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
                AuthenticationResult authenticationResult = null;
                authToken = authRequestResult.GetAuthTokenAndProperties(out authenticationResult, out targetServiceUrl, out msalAuthClientOut, out authority, out resource, out user, out MemoryBackedTokenCache memTokenCache);
                memTokenCache.ClearCache();
                memTokenCache = null;

                svcDiscoveryProxy = new DiscoveryWebProxyClient(targetServiceUrl);
                svcDiscoveryProxy.HeaderToken = authToken;

                // Get all organizations.
                RetrieveOrganizationsRequest orgRequest = new RetrieveOrganizationsRequest()
                {
                    AccessType = EndpointAccessType.Default,
                    Release = OrganizationRelease.Current
                };

                try
                {
                    dtStartQuery.Stop();
                    dtStartQuery = Stopwatch.StartNew();
                    RetrieveOrganizationsResponse orgResponse = (RetrieveOrganizationsResponse)svcDiscoveryProxy.Execute(orgRequest);
                    dtStartQuery.Stop();

                    if (null == orgResponse)
                        throw new Exception("Organizations response is not properly initialized.");

                    logSink.Log(string.Format(CultureInfo.InvariantCulture, "DiscoverOrganizations - Discovery Server Get Orgs Call Complete - Elapsed:{0}", dtStartQuery.Elapsed.ToString()));

                    // Return the collection.
                    return new DiscoverOrganizationsResult(orgResponse.Details, user);
                }
                catch (System.Exception ex)
                {
                    logSink.Log("ERROR REQUESTING ORGS FROM THE DISCOVERY SERVER", TraceEventType.Error);
                    logSink.Log(ex);
                    throw;
                }
            }
            finally
            {
                if (dtStartQuery.IsRunning) dtStartQuery.Stop();

                //TODO:// UPDATE TOKEN CACHE CLEAN UP.
                //if (authContext != null && authContext.TokenCache is CdsServiceClientTokenCache)
                //	((CdsServiceClientTokenCache)authContext.TokenCache).Dispose();

                if (createdLogSource) // Only dispose it if it was created locally.
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Discovers the organizations (OAuth Specific)
        /// </summary>
        /// <param name="discoveryServiceUri">The discovery service uri.</param>
        /// <param name="clientCredentials">The client credentials.</param>
        /// <param name="loginCertificate">The Certificate used to login</param>
        /// <param name="clientId">The client id of registered Azure app.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
        /// <param name="isOnPrem">Determines whether onprem or </param>
        /// <param name="authority">The authority identifying the registered tenant</param>
        /// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
        /// <param name="useGlobalDisco">(optional) utilize Global discovery service</param>
        /// <param name="useDefaultCreds">(optional) if true, attempts login with the current users credentials</param>
        /// <param name="tokenCacheStorePath">(optional) token filePath, if set to empty string, the default path is used</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns>The list of organizations discovered.</returns>
        private static async Task<DiscoverOrganizationsResult> DiscoverGlobalOrganizationsAsync(Uri discoveryServiceUri, ClientCredentials clientCredentials, X509Certificate2 loginCertificate, string clientId, Uri redirectUri, PromptBehavior promptBehavior, bool isOnPrem, string authority, DataverseTraceLogger logSink = null, bool useGlobalDisco = false, bool useDefaultCreds = false, string tokenCacheStorePath = null, CancellationToken cancellationToken = default)
        {
            bool createdLogSource = false;
            try
            {
                if (logSink == null)
                {
                    // when set, the log source is locally created.
                    createdLogSource = true;
                    logSink = new DataverseTraceLogger();
                }

                if (discoveryServiceUri == null)
                    throw new ArgumentNullException(nameof(discoveryServiceUri), "Discovery service uri cannot be null.");

                // if the discovery URL does not contain api/discovery , base it and use it in the commercial format base.
                // Check needs to be in 2 places as there are 2 different ways Auth can occur.
                if (!(discoveryServiceUri.Segments.Contains("api") && discoveryServiceUri.Segments.Contains("discovery")))
                {
                    // do not have the full API URL here.
                    discoveryServiceUri = new Uri(string.Format(_baselineGlobalDiscoveryFormater, discoveryServiceUri.DnsSafeHost, _globlaDiscoVersion, "Instances"));
                }


                DateTime dtStartQuery = DateTime.UtcNow;
                // Initialize discovery service proxy.
                logSink.Log("DiscoverGlobalOrganizations - Initializing Discovery Server Object with " + discoveryServiceUri.ToString());

                Uri targetServiceUrl = null;
                string authToken = string.Empty;
                string resource = string.Empty; // not used here..

                // Develop authority here.
                // Form challenge for global disco
                Uri authChallengeUri = new Uri($"{discoveryServiceUri.Scheme}://{discoveryServiceUri.DnsSafeHost}/api/aad/challenge");

                // Execute Authentication Request and return token And ServiceURI
                //Uri targetResourceRequest = new Uri(string.Format("{0}://{1}/api/discovery/", discoveryServiceUri.Scheme , discoveryServiceUri.DnsSafeHost));
                ExecuteAuthenticationResults authRequestResult = await AuthProcessor.ExecuteAuthenticateServiceProcessAsync(authChallengeUri, clientCredentials, loginCertificate, clientId, redirectUri, promptBehavior, isOnPrem, authority, null, logSink: logSink, useDefaultCreds: useDefaultCreds, addVersionInfoToUri: false, tokenCacheStorePath: tokenCacheStorePath).ConfigureAwait(false);
                authToken = authRequestResult.GetAuthTokenAndProperties(out AuthenticationResult authenticationResult, out targetServiceUrl, out object msalAuthClientOut, out authority, out resource, out IAccount user, out MemoryBackedTokenCache _);

                // Get the GD Info and return.
                var organizationDetailCollection = await QueryGlobalDiscoveryAsync(authToken, discoveryServiceUri, logSink, cancellationToken).ConfigureAwait(false);

                return new DiscoverOrganizationsResult(organizationDetailCollection, user);
            }
            finally
            {
                //TODO: CLEAN UP TOKEN CACHE
                //if (authContext != null && authContext.TokenCache is CdsServiceClientTokenCache)
                //	((CdsServiceClientTokenCache)authContext.TokenCache).Dispose();

                if (createdLogSource) // Only dispose it if it was created localy.
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Queries the global discovery service
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="discoveryServiceUri"></param>
        /// <param name="logSink"></param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns></returns>
        private static async Task<OrganizationDetailCollection> QueryGlobalDiscoveryAsync(string authToken, Uri discoveryServiceUri, DataverseTraceLogger logSink = null, CancellationToken cancellationToken = default)
        {
            bool createdLogSource = false;

            if (logSink == null)
            {
                // when set, the log source is locally created.
                createdLogSource = true;
                logSink = new DataverseTraceLogger();
            }

            if (discoveryServiceUri == null)
                throw new ArgumentNullException(nameof(discoveryServiceUri), "Discovery service uri cannot be null.");

            Stopwatch dtStartQuery = Stopwatch.StartNew();
            // Initialize discovery service proxy.
            logSink.Log("QueryGlobalDiscovery - Initializing Discovery Server Uri with " + discoveryServiceUri.ToString());

            try
            {
                var headers = new Dictionary<string, List<string>>();
                headers.Add(Utilities.RequestHeaders.AUTHORIZATION_HEADER, new List<string>());
                headers[Utilities.RequestHeaders.AUTHORIZATION_HEADER].Add(string.Format("Bearer {0}", authToken));

                var a = await ExecuteHttpRequestAsync(discoveryServiceUri.ToString(), HttpMethod.Get, customHeaders: headers, logSink: logSink, cancellationToken: cancellationToken).ConfigureAwait(false);
                string body = await a.Content.ReadAsStringAsync().ConfigureAwait(false);
                // Parse the out put into a discovery request.
                var b = JsonConvert.DeserializeObject<GlobalDiscoveryModel>(body);

                OrganizationDetailCollection orgList = new OrganizationDetailCollection();
                foreach (var inst in b.Instances)
                {
                    Version orgVersion = new Version("8.0");
                    Version.TryParse(inst.Version, out orgVersion); // try parsing the version out.

                    EndpointCollection ep = new EndpointCollection();
                    ep.Add(EndpointType.WebApplication, inst.Url);
                    ep.Add(EndpointType.OrganizationDataService, string.Format(_baseWebApiUriFormat, inst.ApiUrl, orgVersion.ToString(2)));
                    ep.Add(EndpointType.OrganizationService, string.Format(_baseSoapOrgUriFormat, inst.ApiUrl));

                    OrganizationDetail d = new OrganizationDetail();
                    d.FriendlyName = inst.FriendlyName;
                    d.OrganizationId = inst.Id;
                    d.OrganizationVersion = inst.Version;
                    d.State = (OrganizationState)Enum.Parse(typeof(OrganizationState), inst.State.ToString());
                    d.UniqueName = inst.UniqueName;
                    d.UrlName = inst.UrlName;
                    d.EnvironmentId = !string.IsNullOrEmpty(inst.EnvironmentId) ? inst.EnvironmentId : string.Empty;
                    d.Geo = !string.IsNullOrEmpty(inst.Region) ? inst.Region : string.Empty;
                    d.TenantId = !string.IsNullOrEmpty(inst.TenantId) ? inst.TenantId : string.Empty;
                    d.SchemaType = !string.IsNullOrEmpty(inst.SchemaType) ? inst.SchemaType : string.Empty;
                    d.OrganizationType = (Xrm.Sdk.Organization.OrganizationType)Enum.Parse(typeof(Xrm.Sdk.Organization.OrganizationType), inst.OrganizationType.ToString());
                    System.Reflection.PropertyInfo proInfo = d.GetType().GetProperty("Endpoints");
                    if (proInfo != null)
                    {
                        proInfo.SetValue(d, ep, null);
                    }

                    if (!Utilities.IsValidOrganizationUrl(d))
                    {
                        logSink.Log(string.Format(CultureInfo.InvariantCulture, "QueryGlobalDiscovery - Invalid Url returned from Discovery ({0})", d.Endpoints[EndpointType.OrganizationService]));
                        continue;
                    }
                    orgList.Add(d);
                }
                dtStartQuery.Stop();
                logSink.Log(string.Format(CultureInfo.InvariantCulture, "QueryGlobalDiscovery - Discovery Server Get Orgs Call Complete - Elapsed:{0}", dtStartQuery.Elapsed.ToString()));

                // Return the collection.
                return orgList;
            }
            catch (System.Exception ex)
            {
                logSink.Log("ERROR REQUESTING ORGS FROM THE DISCOVERY SERVER", TraceEventType.Error);
                logSink.Log(ex);
                throw;
            }
            finally
            {
                if (dtStartQuery.IsRunning) dtStartQuery.Stop();

                if (createdLogSource) // Only dispose it if it was created locally.
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Returns the error code that is contained in SoapException.Detail.
        /// </summary>
        /// <param name="errorInfo">An XmlNode that contains application specific error information.</param>
        /// <returns>Error code text or empty string.</returns>
        private static string GetErrorCode(XmlNode errorInfo)
        {
            XmlNode code = errorInfo.SelectSingleNode("//code");

            if (code != null)
                return code.InnerText;
            else
                return "";
        }

        /// <summary>
        /// Gets the client credentials.
        /// </summary>
        /// <param name="networkCredential">The network credential.</param>
        /// <returns>The client credentials object.</returns>
        private static ClientCredentials GetClientCredentials(NetworkCredential networkCredential)
        {
            ClientCredentials clientCredentials = new ClientCredentials();
            if (null == networkCredential)
            {
                // Current user network credentials.
                clientCredentials.Windows.ClientCredential = CredentialCache.DefaultNetworkCredentials;
            }
            else
            {

                // Windows credentials.
                clientCredentials.Windows.ClientCredential = networkCredential;
            }
            return clientCredentials;
        }

        /// <summary>
        /// Attempts to get the certificate for the thumbprint passed in
        /// </summary>
        /// <param name="certificateThumbprint">Thumbprint of Certificate to Load</param>
        /// <param name="storeName">Name of the store to look for the certificate in.</param>
        /// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
        /// <returns></returns>
        private static X509Certificate2 FindCertificate(string certificateThumbprint, StoreName storeName, DataverseTraceLogger logSink)
        {
            logSink.Log(string.Format("Looking for certificate with thumbprint: {0}..", certificateThumbprint));
            // Look in both current user and local machine.
            var storeLocations = new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine };
            try
            {
                X509Certificate2Collection certificates = null;
                if (storeLocations.Any(storeLocation => TryFindCertificatesInStore(certificateThumbprint, storeLocation, storeName, out certificates)))
                {
                    logSink.Log(string.Format("Found certificate with thumbprint: {0}!", certificateThumbprint));
                    return certificates[0];
                }
            }
            catch (Exception ex)
            {
                logSink.Log(string.Format("Failed to find certificate with thumbprint: {0}.", certificateThumbprint), TraceEventType.Error, ex);
                return null;
            }
            logSink.Log(string.Format("Failed to find certificate with thumbprint: {0}.", certificateThumbprint), TraceEventType.Error);
            return null;
        }

        /// <summary>
        /// Used to locate the certificate in the store and return a collection of certificates that match the thumbprint.
        /// </summary>
        /// <param name="certificateThumbprint">Thumbprint to search for</param>
        /// <param name="location">Where to search for on the machine</param>
        /// <param name="certReproName">Where in the store to look for the certificate</param>
        /// <param name="certificates">collection of certificates found</param>
        /// <returns>True if found, False if not.</returns>
        private static bool TryFindCertificatesInStore(string certificateThumbprint, StoreLocation location, StoreName certReproName, out X509Certificate2Collection certificates)
        {
            var store = new X509Store(certReproName, location);
            store.Open(OpenFlags.ReadOnly);

            certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);
            store.Close();

            return certificates.Count > 0;
        }


        /// <summary>
        /// Connects too and initializes the Dataverse org Data service.
        /// </summary>
        /// <param name="orgdata">Organization Data</param>
        /// <param name="IsOnPrem">True if called from the OnPrem Branch</param>
        /// <param name="homeRealmUri"> URI of the users Home Realm or null</param>
        [SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", MessageId = "proxy")]
        private async Task<IOrganizationService> ConnectAndInitServiceAsync(OrganizationDetail orgdata, bool IsOnPrem, Uri homeRealmUri)
        {
            //_ActualOrgDetailUsed = orgdata;
            _ActualDataverseOrgUri = BuildOrgConnectUri(orgdata);
            logEntry.Log($"Organization Service URI is '{_ActualDataverseOrgUri}'", TraceEventType.Information);

            // Set the Org into system config
            _organization = orgdata.UniqueName;
            ConnectedOrganizationDetail = orgdata;

            var logDt = Stopwatch.StartNew();
            // Build User Credential
            logEntry.Log("ConnectAndInitService - Initializing Organization Service Object", TraceEventType.Verbose);
            // this to provide trouble shooting information when determining org connect failures.
            logEntry.Log($"ConnectAndInitService - Requesting connection to Organization with Dataverse Version: {(orgdata.OrganizationVersion == null ? "No organization data available" : orgdata.OrganizationVersion)}", TraceEventType.Information);

            // try to create a version number from the org.
            OrganizationVersion = null;
            try
            {
                if (Version.TryParse(orgdata.OrganizationVersion, out var tempVer))
                    OrganizationVersion = tempVer;
            }
            catch { };

            // uses the R7 added connection models to support faster and more precise control of Auth.
            bool useR7Connect = true;
            if (IsOnPrem)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(orgdata.OrganizationVersion))
                    {

                        if (OrganizationVersion != null)
                            if (OrganizationVersion.Major >= 5 && OrganizationVersion.Build > 9688)
                                useR7Connect = true;
                    }
                }
                catch { } // Do nothing here... if this fails,  then the version number From CRM didn't return correctly or was not present. Use IServiceManagement
            }

            OrganizationServiceProxyAsync proxy = null;
            OrganizationWebProxyClientAsync svcWebClientProxy = null;

            if (_eAuthType == AuthenticationType.OAuth
                || _eAuthType == AuthenticationType.Certificate
                || _eAuthType == AuthenticationType.ExternalTokenManagement
                || _eAuthType == AuthenticationType.ClientSecret)
            {
                string resource = string.Empty;
                string Authority = string.Empty;

                Uri targetServiceUrl = null;

                string authToken = string.Empty;

                if (_eAuthType == AuthenticationType.ExternalTokenManagement)
                {
                    // Call External hook here.
                    try
                    {
                        targetServiceUrl = targetServiceUrl = AuthProcessor.GetUriBuilderWithVersion(_ActualDataverseOrgUri).Uri;
                        if (GetAccessTokenAsync != null)
                            authToken = await GetAccessTokenAsync(targetServiceUrl.ToString()).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(authToken))
                        {
                            logDt.Stop();
                            throw new Exception("ExternalTokenManagement Authentication Requested but not configured correctly. 002");
                        }
                    }
                    catch (Exception ex)
                    {
                        logDt.Stop();
                        throw new Exception("ExternalTokenManagement Authentication Requested but not configured correctly. 003", ex);
                    }
                }
                else
                {
                    // Execute Authentication Request and return token And ServiceURI
                    ExecuteAuthenticationResults authRequestResult = await AuthProcessor.ExecuteAuthenticateServiceProcessAsync(_ActualDataverseOrgUri, _UserClientCred, _certificateOfConnection, _clientId, _redirectUri, _promptBehavior, IsOnPrem, _authority, _MsalAuthClient, logEntry, useDefaultCreds: _isDefaultCredsLoginForOAuth, clientSecret: _eAuthType == AuthenticationType.ClientSecret ? _LivePass : null, tokenCacheStorePath: _tokenCachePath).ConfigureAwait(false);
                    authToken = authRequestResult.GetAuthTokenAndProperties(out _authenticationResultContainer, out targetServiceUrl, out _MsalAuthClient, out _authority, out _resource, out _userAccount, out _memoryBackedTokenCache);
                }
                _ActualDataverseOrgUri = targetServiceUrl;
                svcWebClientProxy = new OrganizationWebProxyClientAsync(targetServiceUrl, true);
                AttachWebProxyHander(svcWebClientProxy);
                svcWebClientProxy.HeaderToken = authToken;

                if (svcWebClientProxy != null)
                {
                    // Set default timeouts
                    svcWebClientProxy.InnerChannel.OperationTimeout = _MaxConnectionTimeout;
                    svcWebClientProxy.Endpoint.Binding.SendTimeout = _MaxConnectionTimeout;
                    svcWebClientProxy.Endpoint.Binding.ReceiveTimeout = _MaxConnectionTimeout;
                }
            }
            else if (useR7Connect)
            {
                // R7Connect flow 
                logEntry.Log("ConnectAndInitServiceAsync - OnPREM - Using ISerivceManagement");
                //var targetServiceUrl = AuthProcessor.GetUriBuilderWithVersion(_ActualDataverseOrgUri).Uri;
                //svcWebClientProxy = new OrganizationWebProxyClientAsync(targetServiceUrl, true, _UserClientCred.Windows.ClientCredential);
                //svcWebClientProxy.ClientCredentials.Windows.ClientCredential = new NetworkCredential(_UserClientCred.Windows.ClientCredential.UserName, _UserClientCred.Windows.ClientCredential.SecurePassword, _UserClientCred.Windows.ClientCredential.Domain);
                //AttachWebProxyHander(svcWebClientProxy);

                Xrm.Sdk.Client.IServiceManagement<IOrganizationServiceAsync> organizationSvcConfig = null;
                object objectRlst = OnPremises_Auth.CreateAndAuthenticateProxy<IOrganizationServiceAsync>(organizationSvcConfig, _ActualDataverseOrgUri, homeRealmUri, _UserClientCred, "ConnectAndInitServiceAsync - OnPREM - ", MaxConnectionTimeout, logEntry);
                if (objectRlst != null && objectRlst is OrganizationServiceProxyAsync)
                    proxy = (OrganizationServiceProxyAsync)objectRlst;

                if (proxy != null)
                    AttachProxyHander(proxy);
            }
            else
            {
                logEntry.Log("ConnectAndInitCrmOrgService - Using ISerivceConfig");
                try
                {
                    if (IsOnPrem)
                    {
                        if (homeRealmUri != null)
                        {
                            logEntry.Log("Connecting to CRM via Claims", TraceEventType.Information);

                            if (homeRealmUri.Host.Contains("blank"))
                                proxy = new OrganizationServiceProxyAsync(_ActualDataverseOrgUri, null, _UserClientCred, null);
                            else
                                proxy = new OrganizationServiceProxyAsync(_ActualDataverseOrgUri, homeRealmUri, _UserClientCred, null);
                        }
                        else
                        {
                            // Create the CRM Service Connection.
                            logEntry.Log("Connecting to CRM via AD or IFD", TraceEventType.Information);
                            proxy = new OrganizationServiceProxyAsync(_ActualDataverseOrgUri, null, GetClientCredentials(_AccessCred), null);
                        }
                    }
                    else
                    {
                        //TODO:// RE-REMOVE THIS CODE. 

                        // Connecting to Online via Live.
                        if (string.IsNullOrWhiteSpace(_LiveID))
                        {
                            // Error here .. Cannot use Default with Online.
                            proxy = null;
                        }
                        else
                        {
                            logEntry.Log("Connecting to CRM via Live", TraceEventType.Information);
                            proxy = new OrganizationServiceProxyAsync(_ActualDataverseOrgUri, null, _UserClientCred, null);
                        }
                    }
                    if (proxy != null)
                        AttachProxyHander(proxy);
                }
                catch
                {
                    if (proxy != null)
                        proxy.Dispose();

                    throw;
                }
            }


            logDt.Stop();
            logEntry.Log(string.Format(CultureInfo.InvariantCulture, "ConnectAndInitService - Proxy created, total elapsed time: {0}", logDt.Elapsed.ToString()));

            if (svcWebClientProxy != null)
                return svcWebClientProxy;
            else
                return proxy; // OnPrem
        }

        /// <summary>
        /// This method us used to wire up the telemetry behaviors to the webProxy connection
        /// </summary>
        /// <param name="proxy">Connection proxy to attach telemetry too</param>
        internal void AttachWebProxyHander(OrganizationWebProxyClientAsync proxy)
        {
            proxy.ChannelFactory.Opening += WebProxyChannelFactory_Opening;
        }

        /// <summary>
        /// This method us used to wire up the telemetry behaviors to the onPrem connection
        /// </summary>
        /// <param name="proxy">Connection proxy to attach telemetry too</param>
        internal void AttachProxyHander(OrganizationServiceProxyAsync proxy)
        {
            if (proxy.ServiceConfiguration != null && !proxy.ServiceConfiguration.CurrentServiceEndpoint.EndpointBehaviors.Contains(typeof(Xrm.Sdk.Client.ProxyTypesBehavior)))
                proxy.ServiceConfiguration.CurrentServiceEndpoint.EndpointBehaviors.Add(new Xrm.Sdk.Client.ProxyTypesBehavior());

            if (proxy.ServiceConfiguration != null)
                proxy.ServiceConfiguration.CurrentServiceEndpoint.EndpointBehaviors.Add(new DataverseTelemetryBehaviors(this));
        }


        /// <summary>
        /// Grab the Channel factory Open event and add the Telemetry behaviors.
        /// </summary>
        /// <param name="sender">incoming ChannelFactory</param>
        /// <param name="e">ignored</param>
        private void WebProxyChannelFactory_Opening(object sender, EventArgs e)
        {

            // Add Connection header support for Organization Web client.
            ChannelFactory fact = sender as ChannelFactory;
            if (fact != null)
            {
                if (!fact.Endpoint.EndpointBehaviors.Contains(typeof(DataverseTelemetryBehaviors)))
                {
                    fact.Endpoint.EndpointBehaviors.Add(new DataverseTelemetryBehaviors(this));
                    if (!IsAClone)
                        logEntry.Log("Added WebClient Header Hooks to the Request object.", TraceEventType.Verbose);
                }
            }
        }

        public string EncodeTo64(string strtoEncode)
        {
            byte[] encodeAsBytes
                  = System.Text.ASCIIEncoding.ASCII.GetBytes(strtoEncode);
            string returnValue
                  = System.Convert.ToBase64String(encodeAsBytes);
            return returnValue;
        }

        /// <summary>
        /// To Decode the string
        /// </summary>
        /// <param name="encodedData"></param>
        /// <returns></returns>
        static public string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes
                = System.Convert.FromBase64String(encodedData);
            string returnValue =
               System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }

        /// <summary>
        /// Builds the Organization Service Connect URI
        /// - This is done, potentially replacing the original string, to deal with the discovery service returning an unusable string, for example, a DNS name that does not resolve.
        /// </summary>
        /// <param name="orgdata">Org Data found from the Discovery Service.</param>
        /// <returns>Connection URI</returns>
        private Uri BuildOrgConnectUri(OrganizationDetail orgdata)
        {

            logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Start);

            // Build connection URL
            string DvUrl = string.Empty;
            Uri OrgEndPoint = new Uri(orgdata.Endpoints[EndpointType.OrganizationService]);

            logEntry.Log("DiscoveryServer indicated organization service location = " + OrgEndPoint.ToString(), TraceEventType.Verbose);
#if DEBUG
            if (TestingHelper.Instance.IsDebugEnvSelected())
            {
                return OrgEndPoint;
            }
#endif
            if (Utilities.IsValidOnlineHost(OrgEndPoint))
            {
                // Online ..> USE PROVIDED URI.
                logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Stop);
                return OrgEndPoint;
            }
            else
            {
                // A workaround added in this case to Check for _hostname to be null or empty if it's empty by constructor definitions they are online deployment type
                //And OAuth supports both online and onprem deployment so incase of online Oauth hostname will be empty and orgEndpoint has to be retrun ideally case
                // is to test both AuthType and Deployment type current code doesn't support that hence the workaround.
                if (String.IsNullOrEmpty(_hostname))
                {
                    logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Stop);
                    return OrgEndPoint; // O365 returns direct org end point.
                }
                else
                {

                    if (!OrgEndPoint.Scheme.Equals(_InternetProtocalToUse, StringComparison.OrdinalIgnoreCase))
                    {
                        logEntry.Log("Organization Services is using a different URI Scheme then requested,  switching to Discovery server specified scheme = " + OrgEndPoint.Scheme, TraceEventType.Stop);
                        _InternetProtocalToUse = OrgEndPoint.Scheme;
                    }

                    if (!string.IsNullOrWhiteSpace(_port))
                    {

                        DvUrl = String.Format(CultureInfo.InvariantCulture,
                            "{0}://{1}:{2}{3}", _InternetProtocalToUse, _hostname, _port, OrgEndPoint.PathAndQuery);
                    }
                    else
                    {
                        DvUrl = String.Format(CultureInfo.InvariantCulture,
                            "{0}://{1}{2}", _InternetProtocalToUse, _hostname, OrgEndPoint.PathAndQuery);
                    }

                    logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Stop);
                    return new Uri(DvUrl);
                }
            }


        }

        /// <summary>
        /// Iterates through the list of Dataverse Discovery Servers to find one that knows the user.
        /// </summary>
        /// <param name="onlineServerList"></param>
        private async Task<OrgList> FindDiscoveryServerAsync(DiscoveryServers onlineServerList)
        {
            OrgList orgsList = new OrgList();
            DiscoverOrganizationsResult discoverResult = null;

            if (_OrgDetail == null)
            {
                // If the user as Specified a server to use, try to get the org from that server.
                if (!string.IsNullOrWhiteSpace(_DataverseOnlineRegion))
                {
                    logEntry.Log("Using User Specified Server ", TraceEventType.Information);
                    // Server Specified...
                    DiscoveryServer svr = onlineServerList.GetServerByShortName(_DataverseOnlineRegion);
                    if (svr != null)
                    {
                        if (_eAuthType == AuthenticationType.OAuth && svr.RequiresRegionalDiscovery)
                        {
                            if (svr.RegionalGlobalDiscoveryServer == null)
                            {
                                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServerUri.ToString(), svr.DisplayName), TraceEventType.Information);
                                discoverResult = await QueryLiveDiscoveryServerAsync(svr.DiscoveryServerUri).ConfigureAwait(false); // Defaults to not using GD.
                            }
                            else
                            {
                                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Regional Global Discovery Server, ({1}) URI is = {0}", svr.RegionalGlobalDiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);
                                await QueryOnlineServersListAsync(onlineServerList.OSDPServers, orgsList, svr.DiscoveryServerUri, svr.RegionalGlobalDiscoveryServer).ConfigureAwait(false);
                                //col = QueryLiveDiscoveryServer(svr.DiscoveryServer); // Defaults to not using GD.
                                return orgsList;
                            }
                        }
                        else
                        {
                            if (_eAuthType == AuthenticationType.OAuth)
                            {
                                // OAuth, and GD is allowed.
                                logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Global Discovery Server ({0}) and filtering to region {1}", GlobalDiscoveryAllInstancesUri, _DataverseOnlineRegion), TraceEventType.Information);
                                await QueryOnlineServersListAsync(onlineServerList.OSDPServers, orgsList, svr.DiscoveryServerUri).ConfigureAwait(false);
                                return orgsList;
                            }
                            else
                            {
                                discoverResult = await QueryLiveDiscoveryServerAsync(svr.DiscoveryServerUri).ConfigureAwait(false);
                                if (discoverResult.OrganizationDetailCollection != null)
                                    AddOrgToOrgList(discoverResult.OrganizationDetailCollection, svr.DisplayName, svr.DiscoveryServerUri, ref orgsList);
                            }
                        }
                        return orgsList;
                    }
                    else
                        logEntry.Log("User Specified Server not found in Discovery server directory, running system wide search", TraceEventType.Information);
                }

                // Server is unspecified or the user chose ‘don’t know’
                if (_eAuthType == AuthenticationType.OAuth)
                {
                    // use GD.
                    discoverResult = await QueryLiveDiscoveryServerAsync(new Uri(GlobalDiscoveryAllInstancesUri), true).ConfigureAwait(false);
                    if (discoverResult.OrganizationDetailCollection != null)
                    {
                        bool isOnPrem = false;
                        foreach (var itm in discoverResult.OrganizationDetailCollection)
                        {
                            var orgObj = Utilities.DeterminDiscoveryDataFromOrgDetail(new Uri(itm.Endpoints[EndpointType.OrganizationService]), out isOnPrem);
                            if (orgObj != null)
                            {
                                AddOrgToOrgList(itm, orgObj.DisplayName, ref orgsList);
                            }
                            else
                            {
                                AddOrgToOrgList(itm, !string.IsNullOrEmpty(itm.Geo) ? itm.Geo : "Unknown", ref orgsList);
                            }
                        }
                    }
                    return orgsList;
                }
                else
                    await QueryOnlineServersListAsync(onlineServerList.OSDPServers, orgsList).ConfigureAwait(false);
            }
            else
            {
                // the org was preexisting
                logEntry.Log("User Specified Org details are used.", TraceEventType.Information);
                var col = new OrganizationDetailCollection();
                col.Add(_OrgDetail);
                AddOrgToOrgList(col, "User Defined Org Detail", new Uri(_OrgDetail.Endpoints[EndpointType.OrganizationService]), ref orgsList);
            }

            return orgsList;
        }

        /// <summary>
        /// Iterate over the discovery servers available.
        /// </summary>
        /// <param name="svrs"></param>
        /// <param name="orgsList"></param>
        /// <param name="trimToDiscoveryUri">Forces the results to be trimmed to this region when present</param>
        /// <param name="globalDiscoUriToUse">Overriding Global Discovery URI</param>
        private async Task<bool> QueryOnlineServersListAsync(ObservableCollection<DiscoveryServer> svrs, OrgList orgsList, Uri trimToDiscoveryUri = null, Uri globalDiscoUriToUse = null)
        {
            DiscoverOrganizationsResult discoverResult;

            // CHANGE HERE FOR GLOBAL DISCO ----
            // Execute Global Discovery
            if (_eAuthType == AuthenticationType.OAuth)
            {
                Uri gdUriToUse = globalDiscoUriToUse != null ? new Uri(string.Format(_baselineGlobalDiscoveryFormater, globalDiscoUriToUse.ToString(), _globlaDiscoVersion, "Instances")) : new Uri(GlobalDiscoveryAllInstancesUri);
                logEntry.Log(string.Format("Trying Global Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Information);
                try
                {
                    discoverResult = await QueryLiveDiscoveryServerAsync(gdUriToUse, true).ConfigureAwait(false);
                }
                catch (MessageSecurityException)
                {
                    logEntry.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Warning);
                    return false;
                }
                catch (Exception ex)
                {
                    logEntry.Log(string.Format("Exception while trying to connect Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Error, ex);
                    return false;
                }

                // if we have results.. add them to the AddOrgToOrgList object. ( need to iterate over the objects to match region to result. )

                if (discoverResult.OrganizationDetailCollection != null)
                {
                    bool isOnPrem = false;
                    foreach (var itm in discoverResult.OrganizationDetailCollection)
                    {
                        var orgObj = Utilities.DeterminDiscoveryDataFromOrgDetail(new Uri(itm.Endpoints[EndpointType.OrganizationService]), out isOnPrem);
                        if (orgObj != null)
                        {
                            if (trimToDiscoveryUri != null && !trimToDiscoveryUri.Equals(orgObj.DiscoveryServerUri))
                                continue;
                            AddOrgToOrgList(itm, orgObj.DisplayName, ref orgsList);
                        }
                        else
                        {
                            AddOrgToOrgList(itm, !string.IsNullOrEmpty(itm.Geo) ? itm.Geo : "Unknown", ref orgsList);
                        }
                    }
                }
            }
            else
            {
                // Scan Live servers.
                foreach (var svr in svrs)
                {
                    try
                    {
                        // Covers the "don't know" setting.
                        if (svr.DiscoveryServerUri == null) continue;

                        logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Live Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServerUri.ToString(), svr.DisplayName), TraceEventType.Information);

                        discoverResult = await QueryLiveDiscoveryServerAsync(svr.DiscoveryServerUri).ConfigureAwait(false);
                        if (discoverResult.OrganizationDetailCollection != null)
                            AddOrgToOrgList(discoverResult.OrganizationDetailCollection, svr.DisplayName, svr.DiscoveryServerUri, ref orgsList);
                    }
                    catch (MessageSecurityException)
                    {
                        logEntry.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServerUri.ToString(), svr.DisplayName), TraceEventType.Warning);
                        return false;
                    }
                    catch (Exception)
                    {
                        logEntry.Log(string.Format("Exception while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServerUri.ToString(), svr.DisplayName), TraceEventType.Error);
                        return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Query an individual Live System
        /// </summary>
        /// <param name="discoServer"></param>
        /// <param name="useGlobal">when try, uses global discovery</param>
        /// <returns></returns>
        private async Task<DiscoverOrganizationsResult> QueryLiveDiscoveryServerAsync(Uri discoServer, bool useGlobal = false)
        {
            logEntry.Log("QueryLiveDiscoveryServer()", TraceEventType.Start);
            try
            {
                if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.ClientSecret)
                {
                    return await DiscoverOrganizationsAsync(discoServer, _UserClientCred, _clientId, _redirectUri, _promptBehavior, false, _authority, logSink: logEntry, useGlobalDisco: useGlobal, tokenCacheStorePath: _tokenCachePath).ConfigureAwait(false);
                }
                else
                {
                    if (_eAuthType == AuthenticationType.Certificate)
                    {
                        return await DiscoverOrganizationsAsync(discoServer, _certificateOfConnection, _clientId, false, _authority, logEntry, tokenCacheStorePath: _tokenCachePath).ConfigureAwait(false);
                    }

                    return null;

                }
            }
            catch (SecurityAccessDeniedException)
            {
                // User Does not have any orgs on this server.
                return null;
            }
        }

        /// <summary>
        /// Adds an Org to the List of Orgs
        /// </summary>
        /// <param name="discoveryServer"></param>
        /// <param name="discoveryServerUri"></param>
        /// <param name="organizationDetailList"></param>
        /// <param name="orgList"></param>
        private void AddOrgToOrgList(OrganizationDetailCollection organizationDetailList, string discoveryServer, Uri discoveryServerUri, ref OrgList orgList)
        {
            foreach (OrganizationDetail o in organizationDetailList)
            {
                AddOrgToOrgList(o, discoveryServer, ref orgList);
            }
        }

        /// <summary>
        /// Adds an Org to the List of Orgs
        /// </summary>
        /// <param name="organizationDetail"></param>
        /// <param name="discoveryServer"></param>
        /// <param name="orgList"></param>
        private void AddOrgToOrgList(OrganizationDetail organizationDetail, string discoveryServer, ref OrgList orgList)
        {

            if (orgList == null) orgList = new OrgList();
            if (orgList.OrgsList == null) orgList.OrgsList = new ObservableCollection<OrgByServer>();

            orgList.OrgsList.Add(new OrgByServer()
            {
                DiscoveryServerName = discoveryServer,
                OrgDetail = organizationDetail
            });
        }

        /// <summary>
        /// Refresh web proxy client token
        /// </summary>
		internal async Task<string> RefreshClientTokenAsync()
        {
            string clientToken = string.Empty;
            
            if (disposedValue) { return clientToken; } // abort processing if disposed or dispose is occurring. 
            
            if (_authenticationResultContainer != null && !string.IsNullOrEmpty(_resource) && !string.IsNullOrEmpty(_clientId))
            {
                if (_authenticationResultContainer.ExpiresOn.ToUniversalTime() < DateTime.UtcNow.AddMinutes(1))
                {
#if DEBUG
                    System.Diagnostics.Trace.WriteLine($">>>>>>>>>>>>>>>>>>>> REQUESTING TOKEN {_ServiceCACHEName} - {Thread.CurrentThread.ManagedThreadId} - IsAClone {IsAClone} >>>>>>>>>>>>>>>>>>>>");
#endif

                    if (_MsalAuthClient is IPublicClientApplication pClient)
                    {
                        // this is a user based application.
                        if (_isCalledbyExecuteRequest && _promptBehavior != PromptBehavior.Never)
                        {
                            _isCalledbyExecuteRequest = false;
                        }
                        _authenticationResultContainer = await AuthProcessor.ObtainAccessTokenAsync(pClient, _authenticationResultContainer.Scopes.ToList(), _authenticationResultContainer.Account, _promptBehavior, _UserClientCred, _isDefaultCredsLoginForOAuth, logEntry).ConfigureAwait(false);
                    }
                    else
                    {
                        if (_MsalAuthClient is IConfidentialClientApplication cClient)
                        {
                            _authenticationResultContainer = await AuthProcessor.ObtainAccessTokenAsync(cClient, _authenticationResultContainer.Scopes.ToList(), logEntry).ConfigureAwait(false);
                        }
                    }
                }

                clientToken = _authenticationResultContainer.AccessToken;
                if (_svcWebClientProxy != null)
                    _svcWebClientProxy.HeaderToken = clientToken;
            }

            if (_eAuthType == AuthenticationType.ExternalTokenManagement)
            {
                // Call External hook here.
                try
                {
                    if (GetAccessTokenAsync != null)
                    {
                        clientToken = await GetAccessTokenAsync(_ActualDataverseOrgUri.ToString()).ConfigureAwait(false);
                        if (_svcWebClientProxy != null)
                            _svcWebClientProxy.HeaderToken = clientToken;
                    }
                    else
                        throw new DataverseOperationException("External Authentication Requested but not configured correctly. Faulted In Request Access Token 004");

                }
                catch (Exception ex)
                {
                    throw new DataverseOperationException("External Authentication Requested but not configured correctly. 005", ex);
                }
            }

            return clientToken;
        }

        #region IDisposable Support
        ///// <summary>
        ///// Reset disposed state to handle this object being pulled from cache.
        ///// </summary>
        //private void ResetDisposedState()
        //{
        //    // reset the disposed state to deal with the object being pulled from cache.
        //    disposedValue = false;
        //}
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposedValue = true;
                    if (isLogEntryCreatedLocally)
                    {
                        logEntry?.Dispose();
                    }

                    if (CurrentCookieCollection != null)
                    {
                        CurrentCookieCollection.Clear();
                        CurrentCookieCollection = null; 
                    }

                    //if (unqueInstance)
                    //{
                    // Clean the connect out of memory.
                    _clientMemoryCache.Remove(_ServiceCACHEName);
                    //}

                    try
                    {
                        if (_svcWebClientProxy != null)
                        {
                            if (_svcWebClientProxy.Endpoint.EndpointBehaviors.Contains(typeof(DataverseTelemetryBehaviors)))
                            {
                                _svcWebClientProxy.ChannelFactory.Opening -= WebProxyChannelFactory_Opening;
                                if (_svcWebClientProxy.ChannelFactory.Endpoint.EndpointBehaviors.Contains(typeof(DataverseTelemetryBehaviors)))
                                {
                                    _svcWebClientProxy.ChannelFactory.Endpoint.EndpointBehaviors.Remove(typeof(DataverseTelemetryBehaviors));
                                }

                                if (_svcWebClientProxy.Endpoint.EndpointBehaviors.Contains(typeof(DataverseTelemetryBehaviors)))
                                {
                                    _svcWebClientProxy.Endpoint.EndpointBehaviors.Remove(typeof(DataverseTelemetryBehaviors));
                                }
                            }
                            _svcWebClientProxy.Dispose();
                            _svcWebClientProxy = null; 
                        }
                    }
                    catch { }; // Failed to dispose.. no way to notify this right now.. let it go .
                }
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
        #endregion

    }
}
