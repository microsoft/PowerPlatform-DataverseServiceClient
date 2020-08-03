using Microsoft.Crm.Sdk.Messages;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.WebServiceClient;
using Microsoft.PowerPlatform.Cds.Client.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	///  Decision switch for the sort of Auth to login to CDS with 
	/// </summary>
	public enum AuthenticationType
	{
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
		/// Enabled Host to manage Auth token for CDS connections.
		/// </summary>
		ExternalTokenManagement = 99,
		/// <summary>
		/// Invalid connection
		/// </summary>
		InvalidConnection = -1,
	}

	/// <summary>
	/// Handles login and setup the connections for CDS
	/// </summary>
	internal sealed class CdsConnectionService : ICdsConnectionService, IDisposable
	{
		#region variables 
		[NonSerializedAttribute]
		private OrganizationWebProxyClient _svcWebClientProxy;
		private OrganizationWebProxyClient _externalWebClientProxy; // OAuth specific web service proxy

		[NonSerializedAttribute]
		private WhoAmIResponse user;                        // CDS user entity that is the service. 
		private string _hostname;                           // Host name of the CDS server
		private string _port;                               // Port the WebService is on
		private string _organization;                       // Org that is being inquired on.. 
		private AuthenticationType _eAuthType;             // Default setting for Auth Cred;

		[NonSerializedAttribute]
		private System.Net.NetworkCredential _AccessCred;   // Network that is accessing  used for AD based Auth
		[NonSerializedAttribute]
		private ClientCredentials _UserClientCred;           // Describes the user client credential when accessing claims or SPLA based services.  
		[NonSerializedAttribute]
		private string _InternetProtocalToUse = "http";      // Which Internet protocol to use to connect. 

		private OrganizationDetail _OrgDetail;               // if provided by the calling system, bypasses all discovery server lookup processed. 
															 //private OrganizationDetail _ActualOrgDetailUsed;     // Org Detail that was used by the Auth system when it created the proxy. 

		/// <summary>
		/// This is the actual CDS OrgURI used to connect, which could be influenced by the host name given during the connect process. 
		/// </summary>
		private Uri _ActualCdsOrgUri;

		private string _LiveID;                             
		private SecureString _LivePass;                      
		private string _CdsOnlineRegion;                    // Region of Cds Online to use. 
		private string _ServiceCACHEName = "Microsoft.PowerPlatform.Cds.Client.CdsService"; // this is the base cache key name that will be used to cache the service. 

		//OAuth Params
		private static bool _ADALLoggingSet = false;        // Switch to see if we need to add logging. 
		private static Version _ADALAsmVersion = null;      // Version of the ADAL Lib in use. 
		private UserIdentifier _user;                       // user to login with
		private string _clientId;                           // client id to register your application for OAuth
		private Uri _redirectUri;                           // uri specifying the redirection uri post OAuth auth
		private PromptBehavior _promptBehavior;             // prompt behavior
		private string _tokenCachePath;                     // user specified token cache file path  
		private AuthenticationContext _authenticationContext; // unique authentication context used to login for tenant
		private string _resource;                           // Resource to connect to
		private bool _isOnPremOAuth = false;                // Identifies whether the connection is for OnPrem or Online Deployment for OAuth
		private static string _authority;                   //cached authority reading from credential manager
		private static string _userId = null;               //cached userid reading from config file
		private bool _isCalledbyExecuteRequest = false;     //Flag indicating that the an request called by Execute_Command
		private bool _isDefaultCredsLoginForOAuth = false;  //Flag indicating that the user is trying to login with the current user id. 

		/// <summary>
		/// If set to true, will relay any received cookie back to the server. 
		/// Defaulted to true.
		/// </summary>
		private bool _enableCookieRelay = Utils.AppSettingsHelper.GetAppSetting<bool>("PreferConnectionAffinity", true);

		/// <summary>
		///  switch for actions that should run only once. 
		/// </summary>
		//private bool firstPass = true;

		/// <summary>
		/// last Authentication Response from oAuth. 
		/// </summary>
		private AuthenticationResult _oAuthar;

		/// <summary>
		/// TimeSpan used to control the offset of the token reacquire behavior for none user Auth flows. 
		/// </summary>
		private readonly TimeSpan _tokenOffSetTimeSpan = new TimeSpan(0, 2, 0);

		/// <summary>
		/// if Set to true then the connection is for one use and should be cleand out of cache when completed. 
		/// </summary>
		private bool unqueInstance = false;
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
		/// Uri that will be used to connect to Cds for Cert Auth. 
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
		/// version of the globaldiscovery service. 
		/// </summary>
		private static readonly string _globlaDiscoVersion = "2.0";

		/// <summary>
		/// organization id placeholder.
		/// </summary>
		private Guid _OrganizationId;

		/// <summary>
		/// Max connection timeout property 
		/// </summary>
		private static TimeSpan _MaxConnectionTimeout = Utils.AppSettingsHelper.GetAppSettingTimeSpan("MaxCdsConnectionTimeOutMinutes", Utils.AppSettingsHelper.TimeSpanFromKey.Minutes, new TimeSpan(0, 0, 2, 0));

		/// <summary>
		/// Pointer to Host System which can return the access token. 
		/// </summary>
		private Func<string> GetAccessTokenFromParent;

		/// <summary>
		/// Tenant ID
		/// </summary>
		private Guid _TenantId;

		/// <summary>
		/// Connected Environment Id
		/// </summary>
		private string _EnvironmentId;

		private IOrganizationService _testSupportIOrg;
		#endregion

		#region Properties
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
		/// This ID is used to support CDS Telemetry when trouble shooting SDK based errors.
		/// When Set by the caller, all CDS API Actions executed by this client will be tracked under a single session id for later troubleshooting. 
		/// For example, you are able to group all actions in a given run of your client ( seveal creates / reads and such ) under a given tracking id that is shared on all requests. 
		/// providing this ID when when reporting a problem will aid in trouble shooting your issue. 
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
		internal AuthenticationContext AuthContext { get { return _authenticationContext; } }

		/// <summary>
		/// Cached userid
		/// </summary>
		internal string UserId { get { return _userId; } }

		/// <summary>
		/// Flag indicating that the an request called by Execute_Command used for OAuth
		/// </summary>
		internal bool CalledbyExecuteRequest
		{
			get { return _isCalledbyExecuteRequest; }
			set { _isCalledbyExecuteRequest = value; }
		}

		/// <summary>
		/// Logging provider for CdsConnectionServiceobject. 
		/// </summary>
		private CdsTraceLogger logEntry { get; set; }

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
		public bool isLogEntryCreatedLocaly { get; set; }

		/// <summary>
		/// Get and Set of network credentials... 
		/// </summary>
		internal System.Net.NetworkCredential CdsServiceAccessCredential
		{
			get { return _AccessCred; }
			set { _AccessCred = value; }
		}

		/// <summary>
		/// Type of protocol to use
		/// </summary>
		internal string InternetProtocalToUse { get { return _InternetProtocalToUse; } set { _InternetProtocalToUse = value; } }

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
		/// Returns the CDS Web Client
		/// </summary>
		internal OrganizationWebProxyClient CdsWebClient
		{
			get
			{
				if (_externalWebClientProxy != null)
				{
					if (GetAccessTokenFromParent != null)
						_externalWebClientProxy.HeaderToken = GetAccessTokenFromParent();

					return _externalWebClientProxy;
				}

				if (_svcWebClientProxy != null)
				{
					RefreshWebProxyClientToken().GetAwaiter().GetResult(); // Only call this if the connection is not null
					try
					{
						if (!_svcWebClientProxy.Endpoint.EndpointBehaviors.Contains(typeof(CdsServiceTelemetryBehaviors)))
						{
							_svcWebClientProxy.Endpoint.EndpointBehaviors.Add(new CdsServiceTelemetryBehaviors(this));
						}
					}
					catch { }
				}
				return _svcWebClientProxy;
			}
		}

		/// <summary>
		/// Get / Set the CDS Organization that the customer exists in
		/// </summary>
		internal string CustomerOrganization
		{
			get { return _organization; }
			set { _organization = value; }
		}

		/// <summary>
		/// Gets / Set the CDS Host Port that the web service is listening on
		/// </summary>
		internal string CdsHostPort
		{
			get { return _port; }
			set { _port = value; }
		}

		/// <summary>
		/// Gets / Set the CDS Hostname that the web service is listening on. 
		/// </summary>
		internal string CdsHostName
		{
			get { return _hostname; }
			set { _hostname = value; }
		}


		/// <summary>
		/// Returns the Current CDS User. 
		/// </summary>
		internal WhoAmIResponse CdsUser
		{
			get { return user; }
			set { user = value; }
		}

		/// <summary>
		/// Returns the Actual URI used to connect to CDS. 
		/// this URI could be influenced by user defined variables. 
		/// </summary>
		internal Uri CdsConnectOrgUriActual { get { return _ActualCdsOrgUri; } }

		/// <summary>
		/// base URL for the oData WebAPI
		/// </summary>
		internal Uri CdsConnectODataBaseUriActual { get; set; }

		/// <summary>
		/// Flag indicating that the an External connection to CDS is used to connect. 
		/// </summary>
		internal bool UseExternalConnection = false;

		/// <summary>
		/// Returns the friendly name of the connected org. 
		/// </summary>
		internal string ConnectedOrgFriendlyName { get; private set; }

		/// <summary>
		/// Returns the endpoint collection for the connected org. 
		/// </summary>
		internal EndpointCollection ConnectedOrgPublishedEndpoints { get; set; }

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
				if (_OrganizationId == Guid.Empty && _OrgDetail != null)
				{
					_OrganizationId = _OrgDetail.OrganizationId;
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
				if (_TenantId == Guid.Empty && _OrgDetail != null)
				{
					Guid.TryParse(_OrgDetail.TenantId, out _TenantId);
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
				if (string.IsNullOrEmpty(_EnvironmentId) && _OrgDetail != null)
				{
					_EnvironmentId = _OrgDetail.EnvironmentId;
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
		/// Set based on constructor call and is specifice to the instance of the CdsClient that was created.
		/// </summary>
		internal Func<string, Task<string>> GetAccessToken { get; set; }

		/// <summary>
		/// returns the format string for the baseWebAPI 
		/// </summary>
		internal string BaseWebAPIDataFormat { get { return _baseWebApiUriFormat; } }

		/// <summary>
		/// Gets or Sets the Max Connection timeout for the connection to CDS/XRM
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
			get { return _enableCookieRelay; }
			set { _enableCookieRelay = value; }
		}

		#endregion

		/// <summary>
		/// TEST Support Constructor.
		/// </summary>
		/// <param name="testIOrganziationSvc"></param>
		internal CdsConnectionService( IOrganizationService testIOrganziationSvc)
		{
			_testSupportIOrg = testIOrganziationSvc;
			logEntry = new CdsTraceLogger();
			isLogEntryCreatedLocaly = true;
			RefreshInstanceDetails(testIOrganziationSvc, null); 
		}

		/// <summary>
		/// Sets up an initialized the CDS Service interface.
		/// </summary>
		/// <param name="externalOrgWebProxyClient">This is an initialized organization web Service proxy</param>
		/// <param name="logSink">incoming Log Sink</param>
		internal CdsConnectionService(OrganizationWebProxyClient externalOrgWebProxyClient, CdsTraceLogger logSink = null)
		{
			if (logSink == null)
			{
				logEntry = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}
			else
			{
				logEntry = logSink;
				isLogEntryCreatedLocaly = false;
			}

			_externalWebClientProxy = externalOrgWebProxyClient;

			if (_externalWebClientProxy != null)
			{
				// Set timeouts. 
				_externalWebClientProxy.InnerChannel.OperationTimeout = _MaxConnectionTimeout;
				_externalWebClientProxy.Endpoint.Binding.SendTimeout = _MaxConnectionTimeout;
				_externalWebClientProxy.Endpoint.Binding.ReceiveTimeout = _MaxConnectionTimeout;
			}
			UseExternalConnection = true;
			GenerateCacheKeys(true);
			_eAuthType = AuthenticationType.OAuth;

			// Setup instance specific httpHandler
			WebApiHttpClient = new HttpClient(); 
		}

		/// <summary>
		/// Sets up and initializes the CDS Service interface using OAuth for user flows.
		/// </summary>
		/// <param name="authType">Only OAuth User flows are supported in this constructor</param>
		/// <param name="orgName">Organization to Connect too</param>
		/// <param name="livePass">Live Password to use</param>
		/// <param name="liveUserId">Live ID to use</param>
		/// <param name="crmOnlineRegion">CrmOnlineRegion</param>
		/// <param name="useUniqueCacheName">flag that will tell the instance to create a Unique Name for the CRM Cache Objects.</param>
		/// <param name="orgDetail">CRM Org Detail object, this is is returned from a query to the CRM Discovery Server service. not required.</param>
		/// <param name="user">Identifies the user who is logging in</param>
		/// <param name="clientId">Client Id of the registered application.</param>
		/// <param name="redirectUri">RedirectUri for the application redirecting to</param>
		/// <param name="promptBehavior">Whether to prompt when no username/password</param>
		/// <param name="tokenCachePath">Token Cache Path supplied for storing OAuth tokens</param>
		/// <param name="hostName">Hostname to connect to</param>
		/// <param name="port">Port to connect to</param>
		/// <param name="onPrem">Token Cache Path supplied for storing OAuth tokens</param>
		/// <param name="logSink">Incoming Log Provide</param>
		/// <param name="instanceToConnectToo">Targeted Instance to connector too.</param>
		/// <param name="useDefaultCreds">(optional) If true attempts login using current user ( Online ) </param>
		internal CdsConnectionService(
			AuthenticationType authType,    // Only OAuth is supported in this constructor.
			string orgName,                 // CRM Organization Name your connecting too
			string liveUserId,             // Live ID - Live only 
			SecureString livePass,               // Live Pw - Live Only
			string crmOnlineRegion,
			bool useUniqueCacheName,        // tells the system to create a unique cache name for this instance. 
			OrganizationDetail orgDetail,
			UserIdentifier user,            // User Identifier for unique user
			string clientId,                // The client Id of the client registered with Azure
			Uri redirectUri,                // The redirectUri telling the redirect login window 
			PromptBehavior promptBehavior,  // The prompt behavior for ADAL library
			string tokenCachePath,          // Tells the client connection to bypass all discovery server behaviors and use this detail object
			string hostName,                // Host name to connect to
			string port,                    // Port used to connect to
			bool onPrem,
			CdsTraceLogger logSink = null,
			Uri instanceToConnectToo = null,
			bool useDefaultCreds = false)
		{
			if (authType != AuthenticationType.OAuth && authType != AuthenticationType.ClientSecret)
				throw new ArgumentOutOfRangeException("authType", "This constructor only supports the OAuth or Client Secret Auth types");

			if (logSink == null)
			{
				logEntry = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}
			else
			{
				logEntry = logSink;
				isLogEntryCreatedLocaly = false;
			}

			UseExternalConnection = false;
			_eAuthType = authType;
			_organization = orgName;
			_LiveID = liveUserId;
			_LivePass = livePass;
			_CdsOnlineRegion = crmOnlineRegion;
			_OrgDetail = orgDetail;
			_user = user;
			_clientId = clientId;
			_redirectUri = redirectUri;
			_promptBehavior = promptBehavior;
			_tokenCachePath = tokenCachePath;
			_hostname = hostName;
			_port = port;
			_isOnPremOAuth = onPrem;
			_targetInstanceUriToConnectTo = instanceToConnectToo;
			_isDefaultCredsLoginForOAuth = useDefaultCreds;
			GenerateCacheKeys(useUniqueCacheName);

			// Setup instance specific httpHandler
			WebApiHttpClient = new HttpClient();
		}

		/// <summary>
		/// Sets up and initializes the CDS Service interface using Certificate Auth.
		/// </summary>
		/// <param name="authType">Only Certificate flows are supported in this constructor</param>
		/// <param name="useUniqueCacheName">flag that will tell the instance to create a Unique Name for the CRM Cache Objects.</param>
		/// <param name="orgDetail">CRM Org Detail object, this is is returned from a query to the CRM Discovery Server service. not required.</param>
		/// <param name="clientId">Client Id of the registered application.</param>
		/// <param name="redirectUri">RedirectUri for the application redirecting to</param>
		/// <param name="tokenCachePath">Token Cache Path supplied for storing OAuth tokens</param>
		/// <param name="hostName">Hostname to connect to</param>
		/// <param name="port">Port to connect to</param>
		/// <param name="onPrem">Modifies system behavior for ADAL based auth for OnPrem</param>
		/// <param name="certStoreName">StoreName on this machine where the certificate with the thumb print passed can be located</param>
		/// <param name="certifcate">X509Certificate to be used to login to this connection, if populated, Thumb print and StoreLocation are ignored. </param>
		/// <param name="certThumbprint">Thumb print of the Certificate to use for this connection.</param>
		/// <param name="instanceToConnectToo">Direct Instance Uri to Connect To</param>
		/// <param name="logSink">Incoming Log Sink data</param>
		internal CdsConnectionService(
			AuthenticationType authType,    // Only Certificate is supported in this constructor.
			Uri instanceToConnectToo,       // set the connection instance to use. 
			bool useUniqueCacheName,        // tells the system to create a unique cache name for this instance. 
			OrganizationDetail orgDetail,
			string clientId,                // The client Id of the client registered with Azure
			Uri redirectUri,                // The redirectUri telling the redirect login window 
			string certThumbprint,          // thumb print of the certificate to use
			StoreName certStoreName,        // Where to find the Certificate identified by the thumb print. 
			X509Certificate2 certifcate,    // loaded and configured certificate to use. 
			string tokenCachePath,          // Tells the client connection to bypass all discovery server behaviors and use this detail object
			string hostName,                // Host name to connect to
			string port,                    // Port used to connect to
			bool onPrem,
			CdsTraceLogger logSink = null)
		{
			if (authType != AuthenticationType.Certificate && authType != AuthenticationType.ExternalTokenManagement)
				throw new ArgumentOutOfRangeException("authType", "This constructor only supports the Certificate Auth type");

			if (logSink == null)
			{
				logEntry = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}
			else
			{
				logEntry = logSink;
				isLogEntryCreatedLocaly = false;
			}

			UseExternalConnection = false;
			_eAuthType = authType;
			_targetInstanceUriToConnectTo = instanceToConnectToo;
			_OrgDetail = orgDetail;
			_clientId = clientId;
			_redirectUri = redirectUri;
			_tokenCachePath = tokenCachePath;
			_hostname = hostName;
			_port = port;
			_isOnPremOAuth = onPrem;
			_certificateOfConnection = certifcate;
			_certificateThumbprint = certThumbprint;
			_certificateStoreLocation = certStoreName;
			GenerateCacheKeys(useUniqueCacheName);

			// Setup instance specific httpHandler
			WebApiHttpClient = new HttpClient();

		}

		/// <summary>
		/// Loges into CDS using the supplied parameters. 
		/// </summary>
		/// <returns></returns>
		public bool DoLogin(out CdsConnectionService ConnectionObject)
		{
			// Initializes the CDS Service. 
			bool IsConnected = IntilizeService(out ConnectionObject);
			return IsConnected;
		}

		/// <summary>
		/// This is to deal with 2 instances of the CdsConnectionService being created in the Same Running Instance that would need to connect to different Cds servers.
		/// </summary>
		/// <param name="useUniqueCacheName"></param>
		private void GenerateCacheKeys(bool useUniqueCacheName)
		{
			// This is to deal with 2 instances of the CdsConnectionService being created in the Same Running Instance that would need to connect to different CDS servers. 
			if (useUniqueCacheName)
			{
				unqueInstance = true; // this instance is unique. 
				_authority = string.Empty;
				_userId = null;
				_authenticationContext = null;
				Guid guID = Guid.NewGuid();
				_ServiceCACHEName = _ServiceCACHEName + guID.ToString(); // Creating a unique instance name for the cache object. 
			}
		}

		/// <summary>
		/// Initializes the CDS Service
		/// </summary>
		/// <returns>Return true on Success, false on failure</returns>
		private bool IntilizeService(out CdsConnectionService ConnectionObject)
		{
			// Get the CDS Service. 
			IOrganizationService cdsService = this.GetCachedCDSService(out ConnectionObject);

			if (cdsService != null)
			{
				_svcWebClientProxy = (OrganizationWebProxyClient)cdsService;
				return true;
			}
			else
				return false;
		}

		/// <summary>
		/// Try's to gets the Cached CDS Service from memory.
		/// on Failure, Initialize a New instance. 
		/// </summary>
		/// <returns></returns>
		private IOrganizationService GetCachedCDSService(out CdsConnectionService ConnectionObject)
		{
			// try to get the object from Memory . 
			if (!string.IsNullOrEmpty(_ServiceCACHEName))
				ConnectionObject = (CdsConnectionService)System.Runtime.Caching.MemoryCache.Default[_ServiceCACHEName];
			else
				ConnectionObject = null;
			if (ConnectionObject == null)
			{
				// No Service found.. Init the Service and try to bring it online. 
				IOrganizationService LocalCdsSvc = InitCdsService().Result;
				if (LocalCdsSvc == null)
					return null;

				if (!string.IsNullOrEmpty(_ServiceCACHEName))
				{
					// Cache the Service for 5 min. 
					System.Runtime.Caching.MemoryCache.Default.Add(_ServiceCACHEName, this, DateTime.Now.AddMinutes(5));
				}
				return LocalCdsSvc;
			}
			else
			{
				//service from Cache .. get user associated with the connection 
				try
				{
					// Removed call to WHoAMI as it is amused when picking up cache that the reauth logic will be exercised by the first call to the server. 
					ConnectionObject.ResetDisposedState(); // resetting disposed state as this object was pulled from cache. 
					if (ConnectionObject._svcWebClientProxy != null)
						return (IOrganizationService)ConnectionObject._svcWebClientProxy;
					else
						return null;
				}
				catch (Exception ex)
				{
					logEntry.Log("Failed to Create a connection to CDS", TraceEventType.Error , ex); 
					return null;
				}
			}
		}

		/// <summary>
		/// Initialize a Connection to CDS 
		/// </summary>
		/// <returns></returns>
		private async Task<IOrganizationService> InitCdsService()
		{
			// CDS Service Endpoint to work with 
			IOrganizationService cdsService = null;
			Stopwatch dtQueryTimer = new Stopwatch();
			try
			{
				logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Initialize CDS connection Started - AuthType: {0}", _eAuthType.ToString()), TraceEventType.Verbose);
				if (UseExternalConnection)
				{
					#region Use Externally provided connection
					if (_externalWebClientProxy != null)
						cdsService = _externalWebClientProxy;
					if (cdsService == null)
					{
						this.logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Externally provided connection to CDS Service Not available"),
								   TraceEventType.Error);
						return null;
					}

					if (!IsAClone)
					{
						// Get Version of organization: 
						Guid guRequestId = Guid.NewGuid();
						RetrieveVersionRequest verRequest = new RetrieveVersionRequest() { RequestId = guRequestId };
						logEntry.Log(string.Format("Externally provided connection to CDS Service - Retrieving Version Info. RequestId:{0}", guRequestId.ToString()), TraceEventType.Verbose);

						try
						{

							RetrieveVersionResponse verResp = (RetrieveVersionResponse)((IOrganizationService)cdsService).Execute(verRequest);
							Version OutVersion = null;
							if (Version.TryParse(verResp.Version, out OutVersion))
								OrganizationVersion = OutVersion;
							else
								OrganizationVersion = new Version("0.0");
							logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Externally provided connection to CDS Service - Org Version: {0}", OrganizationVersion.ToString()), TraceEventType.Verbose);
						}
						catch (Exception ex)
						{
							// Failed to get version info : 
							// Log it.. 
							logEntry.Log("Failed to retrieve version info from connected cds organization", TraceEventType.Error, ex);
						}
					}
					else
						logEntry.Log("Cloned Connection, Retrieve version info from connected cds organization not called");
					#endregion
				}
				else
				{
					if ((_eAuthType == AuthenticationType.OAuth && _isOnPremOAuth == true) || (_eAuthType == AuthenticationType.Certificate && _isOnPremOAuth == true))
					{
						#region AD or SPLA Auth
						try
						{
							string CrmUrl = string.Empty;
							#region AD
							if (_OrgDetail == null)
							{
								// Build Discovery Server Connection
								if (!string.IsNullOrWhiteSpace(_port))
								{
									// http://<Server>:<port>/XRMServices/2011/Discovery.svc?wsdl
									CrmUrl = String.Format(CultureInfo.InvariantCulture,
										"{0}://{1}:{2}/XRMServices/2011/Discovery.svc",
										_InternetProtocalToUse,
										_hostname,
										_port);
								}
								else
								{
									CrmUrl = String.Format(CultureInfo.InvariantCulture,
										"{0}://{1}/XRMServices/2011/Discovery.svc",
										_InternetProtocalToUse,
										_hostname);
								}
								logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Discovery URI is = {0}", CrmUrl), TraceEventType.Information);
								if (!Uri.IsWellFormedUriString(CrmUrl, UriKind.Absolute))
								{
									// Throw error here. 
									logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Discovery URI is malformed = {0}", CrmUrl), TraceEventType.Error);

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
								Uri uCrmUrl = new Uri(CrmUrl);

								// This will try to discover any organizations that the user has access too,  one way supports AD / IFD and the other supports Claims  
								OrganizationDetailCollection orgs = null;

								if (_eAuthType == AuthenticationType.OAuth)
								{
									orgs = DiscoverOrganizations(uCrmUrl, _UserClientCred, _user, _clientId, _redirectUri, _promptBehavior, _tokenCachePath, true, _authority, logEntry);
								}
								else
								{
									if (_eAuthType == AuthenticationType.Certificate)
									{
										orgs = DiscoverOrganizations(uCrmUrl, _certificateOfConnection, _clientId, _tokenCachePath, true, _authority, logEntry);
									}
								}


								// Check the Result to see if we have Orgs back 
								if (orgs != null && orgs.Count > 0)
								{
									logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Found {0} Org(s)", orgs.Count), TraceEventType.Information);
									orgDetail = orgs.FirstOrDefault(o => string.Compare(o.UniqueName, _organization, StringComparison.CurrentCultureIgnoreCase) == 0);
									if (orgDetail == null)
										orgDetail = orgs.FirstOrDefault(o => string.Compare(o.FriendlyName, _organization, StringComparison.CurrentCultureIgnoreCase) == 0);

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
								orgDetail = _OrgDetail; // Assign to passed in value. 

							// Try to connect to CRM here. 
							cdsService = await ConnectAndInitCdsOrgService(orgDetail, true, uUserHomeRealm);

							if (cdsService == null)
							{
								logEntry.Log("Failed to connect to CDS", TraceEventType.Error);
								return null;
							}
							
							if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ClientSecret)
								cdsService = (OrganizationWebProxyClient)cdsService;
							
							#endregion

						}
						catch (Exception ex)
						{
							logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to login to CDS, Error was : {0}", ex.Message),
								   TraceEventType.Error, ex);
							if (cdsService != null)
							{
								((OrganizationWebProxyClient)cdsService).Dispose();
								cdsService = null;
							}
							return null;
						}
						#endregion
					}
					else
						if ( _eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ClientSecret || _eAuthType == AuthenticationType.ExternalTokenManagement)
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
								cdsService = await DoDirectLogin();
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
								cdsService = await DoDirectLogin();
							}
							else
							{
								CdsDiscoveryServers onlineServerList = new CdsDiscoveryServers();
								try
								{

									CdsOrgList orgList = FindCdsDiscoveryServer(onlineServerList);

									if (orgList.OrgsList != null && orgList.OrgsList.Count > 0)
									{
										logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Found {0} Org(s)", orgList.OrgsList.Count), TraceEventType.Information);
										if (orgList.OrgsList.Count == 1)
										{
											cdsService = await ConnectAndInitCdsOrgService(orgList.OrgsList.First().OrgDetail, false, null);
											if (cdsService != null)
											{
												cdsService = (OrganizationWebProxyClient)cdsService;

												// Update Region
												_CdsOnlineRegion = onlineServerList.GetServerShortNameByDisplayName(orgList.OrgsList.First().DiscoveryServerName);
												logEntry.Log(string.Format(CultureInfo.InvariantCulture, "User Organization ({0}) found in Discovery Server {1} - ONLY ORG FOUND", orgList.OrgsList.First().OrgDetail.UniqueName, _CdsOnlineRegion));
											}

										}
										else
										{
											if (!string.IsNullOrWhiteSpace(_organization))
											{
												logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Looking for Organization = {0} in the results from CRM's Discovery server list.", _organization), TraceEventType.Information);
												// Find the Stored org in the returned collection..
												CdsOrgByServer orgDetail = Utilities.DeterminOrgDataFromOrgInfo(orgList, _organization);

												if (orgDetail != null && !string.IsNullOrEmpty(orgDetail.OrgDetail.UniqueName))
												{
													// Found it .. 
													logEntry.Log(string.Format(CultureInfo.InvariantCulture, "found User Org = {0} in results", _organization), TraceEventType.Information);
													cdsService = await ConnectAndInitCdsOrgService(orgDetail.OrgDetail, false, null);
													if (cdsService != null)
													{
														cdsService = (OrganizationWebProxyClient)cdsService;
														_CdsOnlineRegion = onlineServerList.GetServerShortNameByDisplayName(orgList.OrgsList.First().DiscoveryServerName);
														logEntry.Log(string.Format(CultureInfo.InvariantCulture, "User Org ({0}) found in Discovery Server {1}", orgDetail.OrgDetail.UniqueName, _CdsOnlineRegion));
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

										logEntry.Log(string.Format(CultureInfo.InvariantCulture, "No Organizations Found, Searched online. Region Setting = {0}", _CdsOnlineRegion)
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

				// Do a WHO AM I request to make sure the connection is good. 
				if (!UseExternalConnection)
				{
					Guid guIntialTrackingID = Guid.NewGuid();
					logEntry.Log(string.Format("Beginning Validation of CDS Connection. RequestID: {0}", guIntialTrackingID.ToString()));
					dtQueryTimer.Restart();
					user = GetWhoAmIDetails(cdsService, guIntialTrackingID);
					dtQueryTimer.Stop();
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Validation of CDS Connection Complete, total duration: {0}", dtQueryTimer.Elapsed.ToString()));
				}
				else
				{
					logEntry.Log("External CDS Connection Provided, Skipping Validation");
				}

				return (IOrganizationService)cdsService;
				
			}
			#region Login / Discovery Server Exception handlers

			catch (MessageSecurityException ex)
			{
				// Login to Live Failed. 
				logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Invalid Login Information : {0}", ex.Message),
							   TraceEventType.Error, ex);
				throw ex;

			}
			catch (WebException ex)
			{
				// Check the result for Errors.
				if (!string.IsNullOrEmpty(ex.Message) && ex.Message.Contains("HTTP status 401"))
				{
					// Login Error. 
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to Login to CDS: {0}", ex.Message),TraceEventType.Error, ex);

				}
				else
				{
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to CDS: {0}", ex.Message),TraceEventType.Error, ex);

				}
				throw ex;
			}
			catch (InvalidOperationException ex)
			{
				if (ex.InnerException == null)
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to CDS: {0}", ex.Message),TraceEventType.Error, ex);
				else
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to CDS: {0}", ex.InnerException.Message),TraceEventType.Error, ex);
				throw ex;
			}
			catch (Exception ex)
			{
				if (ex.InnerException == null)
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to CDS: {0}", ex.Message),TraceEventType.Error, ex);
				else
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Unable to connect to CDS: {0}", ex.InnerException.Message),TraceEventType.Error, ex);
				throw ex;
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
		/// <returns></returns>
		private async Task<IOrganizationService> DoDirectLogin()
		{
			logEntry.Log("Direct Login Process Started", TraceEventType.Verbose);
			Stopwatch sw = new Stopwatch();
			sw.Start();

			IOrganizationService cdsService = null;

			Uri OrgWorkingURI = new Uri(string.Format(SoapOrgUriFormat, _targetInstanceUriToConnectTo.Scheme, _targetInstanceUriToConnectTo.DnsSafeHost));
			_targetInstanceUriToConnectTo = OrgWorkingURI;

			logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Attempting to Connect to Uri {0}", _targetInstanceUriToConnectTo.ToString()), TraceEventType.Information);
			CdsOrgByServer orgDetail = new CdsOrgByServer();
			orgDetail.OrgDetail = new OrganizationDetail();
			orgDetail.OrgDetail.Endpoints[EndpointType.OrganizationService] = _targetInstanceUriToConnectTo.ToString();

			cdsService = await ConnectAndInitCdsOrgService(orgDetail.OrgDetail, false, null);
			if (cdsService != null)
			{
				RefreshInstanceDetails(cdsService, _targetInstanceUriToConnectTo);
				if (_OrgDetail != null)
				{
					logEntry.Log(string.Format(CultureInfo.InvariantCulture,
						"Connected to User Organization ({0} version: {1})", _OrgDetail.UniqueName, (_OrgDetail.OrganizationVersion ?? "Unknown").ToString()));
				}
				else
				{
					logEntry.Log("Organization Details Unavailable due to SkipOrgDetails flag set to True, to populate organization details on login, do not set SkipOrgDetails or set it to false.");
				}

				// Format the URL for WebAPI service. 
				if (OrganizationVersion != null && OrganizationVersion.Major >= 8)
				{
					// Need to come back to this later to allow it to connect to the correct API endpoint. 
					CdsConnectODataBaseUriActual = new Uri(string.Format(WebApiUriFormat, _targetInstanceUriToConnectTo.Scheme, _targetInstanceUriToConnectTo.DnsSafeHost, OrganizationVersion.ToString(2)));
				}
			}
			sw.Stop();
			logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Direct Login Process {0} - duration {1}", cdsService != null ? "Succeeded" : "Failed", sw.Elapsed.Duration().ToString()), TraceEventType.Verbose);
			return cdsService;
		}


		/// <summary>
		/// Refresh the organization instance details. 
		/// </summary>
		/// <param name="cdsService">CdsConnectionSvc</param>
		/// <param name="uriOfInstance">Instance URL</param>
		private void RefreshInstanceDetails(IOrganizationService cdsService, Uri uriOfInstance)
		{
			// Load the organization instance details 
			if (cdsService != null)
			{
				//TODO:// Add Logic here to improve perf by connecting to global disco. 
				Guid guRequestId = Guid.NewGuid();
				logEntry.Log(string.Format("Querying Organization Instance Details. Request ID: {0}", guRequestId));
				Stopwatch dtQueryTimer = new Stopwatch();
				dtQueryTimer.Restart();
				RetrieveCurrentOrganizationResponse resp = (RetrieveCurrentOrganizationResponse)cdsService.Execute(new RetrieveCurrentOrganizationRequest() { AccessType = 0, RequestId = guRequestId });
				dtQueryTimer.Stop();
				logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Completed Querying Organization Instance Details, total duration: {0}", dtQueryTimer.Elapsed.ToString()));
				if (resp.Detail != null)
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
				}

				_organization = _OrgDetail.UniqueName;
				ConnectedOrgFriendlyName = _OrgDetail.FriendlyName;
				ConnectedOrgPublishedEndpoints = _OrgDetail.Endpoints;

				// try to create a version number from the org. 
				OrganizationVersion = new Version("0.0.0.0");
				try
				{
					Version outVer = null;
					if (Version.TryParse(_OrgDetail.OrganizationVersion, out outVer))
					{
						OrganizationVersion = outVer;
					}
				}
				catch { };
				logEntry.Log("Completed Parsing Organization Instance Details", TraceEventType.Verbose);
			}
		}

		/// <summary>
		/// Get current user info. 
		/// </summary>
		/// <param name="trackingID"></param>
		/// <param name="cdsService"></param>
		internal WhoAmIResponse GetWhoAmIDetails(IOrganizationService cdsService, Guid trackingID = default(Guid))
		{
			if (cdsService != null)
			{
				Stopwatch dtQueryTimer = new Stopwatch();
				dtQueryTimer.Restart();
				try
				{
					if (trackingID == Guid.Empty)
						trackingID = Guid.NewGuid();

					WhoAmIRequest req = new WhoAmIRequest();
					if (trackingID != Guid.Empty) // Add Tracking number of present. 
						req.RequestId = trackingID;

					var resp = (WhoAmIResponse)cdsService.Execute(req);

					// Left in information mode intentionaly. 
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executed Command - WhoAmIRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()));
					return resp;
				}
				catch (Exception ex)
				{
					logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Executed Command - WhoAmIRequest : RequestId={1} : total duration: {0}", dtQueryTimer.Elapsed.ToString(), trackingID.ToString()), TraceEventType.Error);
					logEntry.Log("************ Exception - Failed to lookup current user", TraceEventType.Error, ex);
					throw ex;
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
		internal void SetClonedProperties(CdsServiceClient sourceClient)
		{
			// Sets the cloned properties from the caller. 
			user = sourceClient._SystemUser;
			OrganizationVersion = sourceClient.CdsConnectionSvc.OrganizationVersion;
			ConnectedOrgPublishedEndpoints = sourceClient.ConnectedOrgPublishedEndpoints;
			ConnectedOrgFriendlyName = sourceClient.ConnectedOrgFriendlyName;
			OrganizationId = sourceClient.ConnectedOrgId;
			CustomerOrganization = sourceClient.ConnectedOrgUniqueName;
			_ActualCdsOrgUri = sourceClient.CdsConnectOrgUriActual;
			GetAccessTokenFromParent = () => sourceClient.CurrentAccessToken;
			TenantId = sourceClient.TenantId;
			EnvironmentId = sourceClient.EnvironmentId;
			GetAccessToken = sourceClient.GetAccessToken;
			_authority = sourceClient.CdsConnectionSvc.AuthContext == null
							? string.Empty
							: sourceClient.CdsConnectionSvc.AuthContext.Authority;
		}

		#region WebAPI Interface Utilities

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
		/// <returns></returns>
		internal static async Task<HttpResponseMessage> ExecuteHttpRequestAsync(string uri, HttpMethod method, string body = default(string), Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken), CdsTraceLogger logSink = null, Guid? requestTrackingId = null, string contentType = default(string), Guid? sessionTrackingId = null , bool suppressDebugMessage = false , HttpClient providedHttpClient = null)
		{
			bool isLogEntryCreatedLocaly = false;
			if (logSink == null)
			{
				logSink = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}

			Guid RequestId = Guid.NewGuid();
			if (requestTrackingId.HasValue)
				RequestId = requestTrackingId.Value;

			HttpResponseMessage _httpResponse = null;
			Stopwatch logDt = new Stopwatch();
			try
			{
				using (var _httpRequest = new HttpRequestMessage())
				{
					_httpRequest.Method = method;
					_httpRequest.RequestUri = new System.Uri(uri);

					// Set Headers
					if (customHeaders != null)
					{
						foreach (var _header in customHeaders)
						{
							if (_httpRequest.Headers.Count() > 0)
								if (_httpRequest.Headers.Contains(_header.Key))
								{
									_httpRequest.Headers.Remove(_header.Key);
								}
							_httpRequest.Headers.TryAddWithoutValidation(_header.Key, _header.Value);
						}

						// Add User Agent and request id to send. 
						string Agent = string.Empty;
						if (AppDomain.CurrentDomain != null)
						{
							Agent = AppDomain.CurrentDomain.FriendlyName;
						}
						if (!_httpRequest.Headers.Contains(Utilities.CDSRequestHeaders.USER_AGENT_HTTP_HEADER))
							_httpRequest.Headers.TryAddWithoutValidation(Utilities.CDSRequestHeaders.USER_AGENT_HTTP_HEADER, string.IsNullOrEmpty(Agent) ? "" : Agent);

						if (!_httpRequest.Headers.Contains(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID))
							_httpRequest.Headers.TryAddWithoutValidation(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID, RequestId.ToString());

						if (!_httpRequest.Headers.Contains(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID) && sessionTrackingId.HasValue)
							_httpRequest.Headers.TryAddWithoutValidation(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID, sessionTrackingId.ToString());

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
							if (contentPost.Headers.Contains(Utilities.CDSRequestHeaders.CONTENT_TYPE)) // Remove the default content type if its there. 
								contentPost.Headers.Remove(Utilities.CDSRequestHeaders.CONTENT_TYPE);
							contentPost.Headers.TryAddWithoutValidation(Utilities.CDSRequestHeaders.CONTENT_TYPE, contentType); // Replace with added content type
						}
						else
							contentPost = new StringContent(body, Encoding.UTF8, "application/json");

						_httpRequest.Content = contentPost;
						_requestContent = contentPost.AsString();
					}

					cancellationToken.ThrowIfCancellationRequested();

					if (!suppressDebugMessage)
						logSink.Log(string.Format("Begin Sending request to {3} {0} : {2}RequestID={1}", _httpRequest.RequestUri.AbsolutePath, RequestId, sessionTrackingId.HasValue && sessionTrackingId.Value != Guid.Empty ? $" SessionID={sessionTrackingId.Value.ToString()} : " : "" , method), TraceEventType.Verbose);

					if (providedHttpClient != null)
					{
						logDt.Restart();
						_httpResponse = await providedHttpClient.SendAsync(_httpRequest, cancellationToken).ConfigureAwait(false);
						logDt.Stop();
					}
					else
					{
						// Fall though logic to deal with an Http client not being passed in. 
						using (HttpClient httpCli = new HttpClient())
						{
							logDt.Restart();
							_httpResponse = await httpCli.SendAsync(_httpRequest, cancellationToken).ConfigureAwait(false);
							logDt.Stop();
						}
					}
					HttpStatusCode _statusCode = _httpResponse.StatusCode;
					if (!suppressDebugMessage)
						logSink.Log(string.Format("Response for request to WebAPI {5} {0} : StatusCode={1} : {4}RequestID={2} : Duration={3}", _httpRequest.RequestUri.AbsolutePath, _statusCode, RequestId, logDt.Elapsed.ToString(), sessionTrackingId.HasValue && sessionTrackingId.Value != Guid.Empty ? $" SessionID={sessionTrackingId.Value.ToString()} : " : "" , method));

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
						if (!suppressDebugMessage)
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

		//      /// <summary>
		//      /// Find authority and resources 
		//      /// </summary>
		//      /// <param name="discoveryServiceUri">Service Uri endpoint</param>
		//      /// <param name="resource">Resource to connect to</param>
		//      /// <param name="svcDiscoveryProxy">Discovery Service Proxy</param>
		//      /// <param name="svcWebClientProxy">Organisation Web Proxy</param>
		//      /// <returns></returns>
		//      private static string FindAuthorityAndResource(Uri discoveryServiceUri, out string resource, out DiscoveryWebProxyClient svcDiscoveryProxy, out OrganizationWebProxyClient svcWebClientProxy)
		//{
		//	resource = discoveryServiceUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

		//	UriBuilder versionTaggedUriBuilder = GetUriBuilderWithVersion(discoveryServiceUri);

		//	//discoveryServiceProxy 
		//	svcDiscoveryProxy = new DiscoveryWebProxyClient(versionTaggedUriBuilder.Uri);
		//          svcWebClientProxy = new OrganizationWebProxyClient(versionTaggedUriBuilder.Uri, true);

		//          AuthenticationParameters ap = GetAuthorityFromTargetService(versionTaggedUriBuilder.Uri);
		//	if (ap != null)
		//		return ap.Authority;
		//	else
		//		return null;
		//}

		/// <summary>
		/// Forming version tagged UriBuilder
		/// </summary>
		/// <param name="discoveryServiceUri"></param>
		/// <returns></returns>
		private static UriBuilder GetUriBuilderWithVersion(Uri discoveryServiceUri)
		{
			UriBuilder webUrlBuilder = new UriBuilder(discoveryServiceUri);
			string webPath = "web";

			if (!discoveryServiceUri.AbsolutePath.EndsWith(webPath))
			{
				if (discoveryServiceUri.AbsolutePath.EndsWith("/"))
					webUrlBuilder.Path = string.Concat(webUrlBuilder.Path, webPath);
				else
					webUrlBuilder.Path = string.Concat(webUrlBuilder.Path, "/", webPath);
			}

			UriBuilder versionTaggedUriBuilder = new UriBuilder(webUrlBuilder.Uri);
			string version = FileVersionInfo.GetVersionInfo(typeof(OrganizationWebProxyClient).Assembly.Location).FileVersion;
			string versionQueryStringParameter = string.Format("SDKClientVersion={0}", version);

			if (string.IsNullOrEmpty(versionTaggedUriBuilder.Query))
			{
				versionTaggedUriBuilder.Query = versionQueryStringParameter;
			}
			else if (!versionTaggedUriBuilder.Query.Contains("SDKClientVersion="))
			{
				versionTaggedUriBuilder.Query = string.Format("{0}&{1}", versionTaggedUriBuilder.Query, versionQueryStringParameter);
			}

			return versionTaggedUriBuilder;
		}

		/// <summary>
		/// Obtaining authentication context
		/// </summary>
		private static AuthenticationContext ObtainAuthenticationContext(string Authority, bool requireValidation, string tokenCachePath)
		{
			// Do not need to dispose this here as its added ot the authentication context,  its cleaned up with the authentication context later. 
			CdsServiceClientTokenCache tokenCache = new CdsServiceClientTokenCache(tokenCachePath);

#if DEBUG
			// When in debug mode.. Always disable Authority validation to support NOVA builds. 
			requireValidation = false;
#endif

			// check in cache
			AuthenticationContext authenticationContext = null;
			if (requireValidation == false)
			{
				authenticationContext = new AuthenticationContext(Authority, requireValidation, tokenCache);
			}
			else
			{
				authenticationContext = new AuthenticationContext(Authority, tokenCache);
			}
			return authenticationContext;
		}

#if (NET462 || NET472 || NET48) 
		/// <summary>
		/// Obtain access token for regular popup based authentication
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="redirectUri">Redirect Uri</param>
		/// <param name="promptBehavior">Prompt behavior for connecting</param>
		/// <param name="user">UserIdentifier</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessToken(AuthenticationContext authenticationContext, string resource, string clientId, Uri redirectUri, PromptBehavior promptBehavior, UserIdentifier user)
		{
			PlatformParameters platformParameters = new PlatformParameters(promptBehavior);
			AuthenticationResult _authenticationResult = null;
			if (user != null)//If user enter username and password in connector UX
				_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientId, redirectUri, platformParameters, user).Result;
			else
				_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientId, redirectUri, platformParameters).Result;
			return _authenticationResult;
		}
#endif

#if (NET462 || NET472 || NET48) 
		/// <summary>
		/// Obtain access token for silent login
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="clientCredentials">Credentials passed for creating a connection</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessToken(AuthenticationContext authenticationContext, string resource, string clientId, ClientCredentials clientCredentials)
		{
			AuthenticationResult _authenticationResult = null;
			_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientId, new UserPasswordCredential(clientCredentials.UserName.UserName, clientCredentials.UserName.Password)).Result;
			return _authenticationResult;
		}
#endif

		/// <summary>
		/// Obtain access token for certificate based login
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="clientCert">X509Certificate to use to connect</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessToken(AuthenticationContext authenticationContext, string resource, string clientId, X509Certificate2 clientCert)
		{
			ClientAssertionCertificate cred = new ClientAssertionCertificate(clientId, clientCert);
			AuthenticationResult _authenticationResult = null;
			_authenticationResult = authenticationContext.AcquireTokenAsync(resource, cred).Result;
			return _authenticationResult;
		}

#if (NET462 || NET472 || NET48) 
		/// <summary>
		/// Obtain access token for ClientSecret Based Login. 
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="clientSecret">Client Secret used to connect</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessToken(AuthenticationContext authenticationContext, string resource, string clientId, SecureString clientSecret)
		{
			ClientCredential clientCredential = new ClientCredential(clientId, new SecureClientSecret(clientSecret));
			AuthenticationResult _authenticationResult = null;
			_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientCredential).Result;
			return _authenticationResult;
		}
#else
		/// <summary>
		/// Obtain access token for ClientSecret Based Login. 
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="clientSecret">Client Secret used to connect</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessToken(AuthenticationContext authenticationContext, string resource, string clientId, string clientSecret)
		{
			ClientCredential clientCredential = new ClientCredential(clientId, clientSecret);
			AuthenticationResult _authenticationResult = null;
			_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientCredential).Result;
			return _authenticationResult;
		}
#endif

		/// <summary>
		/// Trues to get the current users login token for the target resource. 
		/// </summary>
		/// <param name="authenticationContext">Authentication Context to be used for connection</param>
		/// <param name="resource">Resource endpoint to connect</param>
		/// <param name="clientId">Registered client Id</param>
		/// <param name="clientCredentials">Credentials passed for creating a connection, username only is honored.</param>
		/// <returns>Authentication result with the access token for the authenticated connection</returns>
		private static AuthenticationResult ObtainAccessTokenCurrentUser(AuthenticationContext authenticationContext, string resource, string clientId, ClientCredentials clientCredentials)
		{
			AuthenticationResult _authenticationResult = null;
			if (clientCredentials != null && clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName))
				_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientId, new UserCredential(clientCredentials.UserName.UserName)).Result;
			else
				_authenticationResult = authenticationContext.AcquireTokenAsync(resource, clientId, new UserCredential()).Result;

			return _authenticationResult;
		}

		/// <summary>
		/// Discovers the organizations (OAuth Specific)
		/// </summary>
		/// <param name="discoveryServiceUri">The discovery service uri.</param>
		/// <param name="clientCredentials">The client credentials.</param>
		/// <param name="user">The user identifier.</param>
		/// <param name="clientId">The client id of registered Azure app.</param>
		/// <param name="redirectUri">The redirect uri.</param>
		/// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
		/// <param name="tokenCachePath">The token cache path.</param>
		/// <param name="isOnPrem">Determines whether onprem or </param>
		/// <param name="authority">The authority identifying the registered tenant</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useGlobalDisco">Use the global disco path. </param>
		/// <param name="useDefaultCreds">(optional) If true attempts login using current user</param>
		/// <returns>The list of organizations discovered.</returns>
		public static OrganizationDetailCollection DiscoverOrganizations(Uri discoveryServiceUri, ClientCredentials clientCredentials, UserIdentifier user, string clientId, Uri redirectUri, PromptBehavior promptBehavior, string tokenCachePath, bool isOnPrem, string authority, CdsTraceLogger logSink = null, bool useGlobalDisco = false, bool useDefaultCreds = false)
		{
			bool isLogEntryCreatedLocaly = false;
			if (logSink == null)
			{
				logSink = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}

			try
			{
				logSink.Log("DiscoverOrganizations - Called using user of MFA Auth for : " + discoveryServiceUri.ToString());
				if (!useGlobalDisco)
					return DiscoverOrganizations_Internal(discoveryServiceUri, clientCredentials, null, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, useDefaultCreds, logSink);
				else
				{
					var a = DiscoverGlobalOrganizations(discoveryServiceUri, clientCredentials, null, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, logSink, useDefaultCreds: useDefaultCreds);
					return a;
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
		/// <param name="tokenCachePath">The token cache path.</param>
		/// <param name="isOnPrem">Determines whether onprem or </param>
		/// <param name="authority">The authority identifying the registered tenant</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useDefaultCreds">(optional) If true, attempts login with current user.</param>
		/// <returns>The list of organizations discovered.</returns>
		public static OrganizationDetailCollection DiscoverOrganizations(Uri discoveryServiceUri, X509Certificate2 loginCertificate, string clientId, string tokenCachePath, bool isOnPrem, string authority, CdsTraceLogger logSink = null, bool useDefaultCreds = false)
		{
			bool isLogEntryCreatedLocaly = false;
			if (logSink == null)
			{
				logSink = new CdsTraceLogger();
				isLogEntryCreatedLocaly = true;
			}
			try
			{
				logSink.Log("DiscoverOrganizations - Called using Certificate Auth for : " + discoveryServiceUri.ToString());
				return DiscoverOrganizations_Internal(discoveryServiceUri, null, loginCertificate, null, clientId, null, PromptBehavior.Never, tokenCachePath, isOnPrem, authority, useDefaultCreds, logSink);
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
		/// <returns>Populated OrganizationDetailCollection or Null.</returns>
		internal static async Task<OrganizationDetailCollection> DiscoverGlobalOrganizations(Uri discoveryServiceUri, Func<string, Task<string>> tokenProviderFunction , CdsTraceLogger logSink = null)
		{
			bool isLogEntryCreatedLocaly = false;
			if (logSink == null)
			{
				logSink = new CdsTraceLogger();
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
				string AuthToken = await tokenProviderFunction(discoveryServiceUri.ToString());
				return await QueryGlobalDiscovery(AuthToken, discoveryServiceUri, logSink); 
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
		/// <param name="user">The user identifier.</param>
		/// <param name="clientId">The client id of registered Azure app.</param>
		/// <param name="redirectUri">The redirect uri.</param>
		/// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
		/// <param name="tokenCachePath">The token cache path.</param>
		/// <param name="isOnPrem">Determines whether onprem or </param>
		/// <param name="authority">The authority identifying the registered tenant</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useDefaultCreds">(optional) If true, tries to login with current users credentials</param>
		/// <returns>The list of organizations discovered.</returns>
		private static OrganizationDetailCollection DiscoverOrganizations_Internal(Uri discoveryServiceUri, ClientCredentials clientCredentials, X509Certificate2 loginCertificate, UserIdentifier user, string clientId, Uri redirectUri, PromptBehavior promptBehavior, string tokenCachePath, bool isOnPrem, string authority, bool useDefaultCreds = false, CdsTraceLogger logSink = null)
		{
			AuthenticationContext authContext = null;
			bool createdLogSource = false;
			Stopwatch dtStartQuery = new Stopwatch();
			try
			{
				if (logSink == null)
				{
					// when set, the log source is locally created. 
					createdLogSource = true;
					logSink = new CdsTraceLogger();
				}


				// Initialize discovery service proxy.
				logSink.Log("DiscoverOrganizations - Initializing Discovery Server Object with " + discoveryServiceUri.ToString());

				DiscoveryWebProxyClient svcDiscoveryProxy = null;
				Uri targetServiceUrl = null;
				string authToken = string.Empty;
				string resource = string.Empty; // not used here..

				// Execute Authentication Request and return token And ServiceURI
				authToken = ExecuteAuthenticateServiceProcess(discoveryServiceUri, clientCredentials, loginCertificate, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, out targetServiceUrl, out authContext, out resource, out user, logSink, useDefaultCreds: useDefaultCreds);

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
					dtStartQuery.Restart();
					RetrieveOrganizationsResponse orgResponse = (RetrieveOrganizationsResponse)svcDiscoveryProxy.Execute(orgRequest);
					dtStartQuery.Stop();

					if (null == orgResponse)
						throw new Exception("Organizations response is not properly initialized.");

					logSink.Log(string.Format(CultureInfo.InvariantCulture, "DiscoverOrganizations - Discovery Server Get Orgs Call Complete - Elapsed:{0}", dtStartQuery.Elapsed.ToString()));

					// Return the collection.
					return orgResponse.Details;
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

				if (authContext != null && authContext.TokenCache is CdsServiceClientTokenCache)
					((CdsServiceClientTokenCache)authContext.TokenCache).Dispose();

				if (createdLogSource) // Only dispose it if it was created localy. 
					logSink.Dispose();
			}
		}

		/// <summary>
		/// Discovers the organizations (OAuth Specific)
		/// </summary>
		/// <param name="discoveryServiceUri">The discovery service uri.</param>
		/// <param name="clientCredentials">The client credentials.</param>
		/// <param name="loginCertificate">The Certificate used to login</param>
		/// <param name="user">The user identifier.</param>
		/// <param name="clientId">The client id of registered Azure app.</param>
		/// <param name="redirectUri">The redirect uri.</param>
		/// <param name="promptBehavior">The prompt behavior for ADAL library.</param>
		/// <param name="tokenCachePath">The token cache path.</param>
		/// <param name="isOnPrem">Determines whether onprem or </param>
		/// <param name="authority">The authority identifying the registered tenant</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useGlobalDisco">(optional) utilize Global discovery service</param>
		/// <param name="useDefaultCreds">(optional) if true, attempts login with the current users credentials</param>
		/// <returns>The list of organizations discovered.</returns>
		private static OrganizationDetailCollection DiscoverGlobalOrganizations(Uri discoveryServiceUri, ClientCredentials clientCredentials, X509Certificate2 loginCertificate, UserIdentifier user, string clientId, Uri redirectUri, PromptBehavior promptBehavior, string tokenCachePath, bool isOnPrem, string authority, CdsTraceLogger logSink = null, bool useGlobalDisco = false, bool useDefaultCreds = false)
		{
			AuthenticationContext authContext = null;
			bool createdLogSource = false;
			try
			{
				if (logSink == null)
				{
					// when set, the log source is locally created. 
					createdLogSource = true;
					logSink = new CdsTraceLogger();
				}

				if (discoveryServiceUri == null)
					throw new ArgumentNullException("discoveryServiceUri", "Discovery service uri cannot be null.");

				// if the discovery URL does not contain api/discovery , base it and use it in the commercial format base. 
				// Check needs to be in 2 places as there are 2 different ways Auth can occur. 
				if(!(discoveryServiceUri.Segments.Contains("api") && discoveryServiceUri.Segments.Contains("discovery")))
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
				authToken = ExecuteAuthenticateServiceProcess(authChallengeUri, clientCredentials, loginCertificate, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, out targetServiceUrl, out authContext, out resource, out user, logSink, useDefaultCreds: useDefaultCreds, addVersionInfoToUri:false);

				// Get the GD Info and return. 
				return QueryGlobalDiscovery(authToken, discoveryServiceUri, logSink).Result;

			}
			finally
			{
				if (authContext != null && authContext.TokenCache is CdsServiceClientTokenCache)
					((CdsServiceClientTokenCache)authContext.TokenCache).Dispose();

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
		/// <returns></returns>
		private static async Task<OrganizationDetailCollection> QueryGlobalDiscovery(string authToken, Uri discoveryServiceUri, CdsTraceLogger logSink = null)
		{
			bool createdLogSource = false;

			if (logSink == null)
			{
				// when set, the log source is locally created. 
				createdLogSource = true;
				logSink = new CdsTraceLogger();
			}

			if (discoveryServiceUri == null)
				throw new ArgumentNullException("discoveryServiceUri", "Discovery service uri cannot be null.");

			Stopwatch dtStartQuery = new Stopwatch();
			dtStartQuery.Start();
			// Initialize discovery service proxy.
			logSink.Log("QueryGlobalDiscovery - Initializing Discovery Server Uri with " + discoveryServiceUri.ToString());

			try
			{
				var headers = new Dictionary<string, List<string>>();
				headers.Add("Authorization", new List<string>());
				headers["Authorization"].Add(string.Format("Bearer {0}", authToken));

				var a = await ExecuteHttpRequestAsync(discoveryServiceUri.ToString(), HttpMethod.Get, customHeaders: headers, logSink: logSink).ConfigureAwait(false);
				string body = await a.Content.ReadAsStringAsync();
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
					System.Reflection.PropertyInfo proInfo = d.GetType().GetProperty("Endpoints");
					if (proInfo != null)
					{
						proInfo.SetValue(d, ep, null);
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
		/// Executes Authentication against a service 
		/// </summary>
		/// <param name="serviceUrl"></param>
		/// <param name="clientCredentials"></param>
		/// <param name="user"></param>
		/// <param name="clientId"></param>
		/// <param name="redirectUri"></param>
		/// <param name="promptBehavior"></param>
		/// <param name="tokenCachePath"></param>
		/// <param name="isOnPrem"></param>
		/// <param name="authority"></param>
		/// <param name="targetServiceUrl"></param>
		/// <param name="authContext"></param>
		/// <param name="resource"></param>
		/// <param name="userCert">Certificate of provided to login with</param>
		/// <param name="userIdent">UserIdent Determined by authentication request</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useDefaultCreds">(optional) if set, tries to login as the current user.</param>
		/// <param name="clientSecret"></param>
		/// <param name="addVersionInfoToUri">indicates if the serviceURI should be updated to include the /web?sdk version</param>
		/// <returns>JWT Token for the requested Resource and user/app</returns>
		private static string ExecuteAuthenticateServiceProcess(Uri serviceUrl, ClientCredentials clientCredentials, X509Certificate2 userCert, UserIdentifier user, string clientId, Uri redirectUri, PromptBehavior promptBehavior, string tokenCachePath, bool isOnPrem, string authority, out Uri targetServiceUrl, out AuthenticationContext authContext, out string resource, out UserIdentifier userIdent, CdsTraceLogger logSink = null, bool useDefaultCreds = false, SecureString clientSecret = null , bool addVersionInfoToUri = true)
		{
			if (!_ADALLoggingSet)
			{
				// Attach Logger
				LoggerCallbackHandler.LogCallback = Utils.ADALLoggerCallBack.Log;
				_ADALLoggingSet = true;

			}

			string authToken = string.Empty;
			authContext = null;
			bool createdLogSource = false;
			userIdent = user; // Set default property. 
			try
			{
				if (logSink == null)
				{
					// when set, the log source is locally created. 
					createdLogSource = true;
					logSink = new CdsTraceLogger();
				}

				string Authority = string.Empty;

				bool clientCredentialsCheck = clientCredentials != null && clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName) && !string.IsNullOrEmpty(clientCredentials.UserName.Password);
				resource = serviceUrl.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
				if (!resource.EndsWith("/"))
					resource += "/";

				if (addVersionInfoToUri)
					targetServiceUrl = GetUriBuilderWithVersion(serviceUrl).Uri;
				else
					targetServiceUrl = serviceUrl;

				if (!clientCredentialsCheck && !string.IsNullOrWhiteSpace(authority))
				{
					//Overriding the tenant specific authority if clientCredentials are null
					Authority = authority;
				}
				else
				{
					AuthenticationParameters ap = GetAuthorityFromTargetService(targetServiceUrl, logSink);
					if (ap != null)
					{
						Authority = ap.Authority;
						if (ap.Resource != null)
							resource = ap.Resource;
						else
							logSink.Log("AuthenticateService - Resource URI is null", TraceEventType.Warning);
					}
					else
						throw new ArgumentNullException("Authority", "Need a non-empty authority");
				}

				logSink.Log("AuthenticateService - found authority with name " + (string.IsNullOrEmpty(Authority) ? "<Not Provided>" : Authority));
				logSink.Log("AuthenticateService - found resource with name " + (string.IsNullOrEmpty(resource) ? "<Not Provided>" : resource));


				authContext = ObtainAuthenticationContext(Authority, !isOnPrem, tokenCachePath);
				if (null == authContext)
				{
					throw new Exception("Organizations response is not properly initialized.");
				}

				AuthenticationResult _authenticationResult = null;

				if (userCert != null || clientSecret != null)
				{
					if (userCert != null)
					{
						// execute certificate flow 
						logSink.Log("ObtainAccessToken - CERT", TraceEventType.Verbose);
						_authenticationResult = ObtainAccessToken(authContext, resource, clientId, userCert);
					}
					else
					{
						if (clientSecret != null)
						{
							logSink.Log("ObtainAccessToken - Client Secret", TraceEventType.Verbose);
#if (NET462 || NET472 || NET48) 
							_authenticationResult = ObtainAccessToken(authContext, resource, clientId, clientSecret);
#else
							_authenticationResult = ObtainAccessToken(authContext, resource, clientId, clientSecret.ToUnsecureString());
#endif
						}
						else
							throw new Exception("Invalid Cert or Client Secret Auth flow");
					}
				}
				else
				{
					// Execute user flows. 
					if (clientCredentialsCheck && !useDefaultCreds)
					{
#if (NET462 || NET472 || NET48) 
						logSink.Log("ObtainAccessToken - CRED", TraceEventType.Verbose);
						_authenticationResult = ObtainAccessToken(authContext, resource, clientId, clientCredentials);
#endif
					}
					else
					{
						if (useDefaultCreds)
						{
							logSink.Log("ObtainAccessToken - DEFAULT CREDS", TraceEventType.Verbose);
							_authenticationResult = ObtainAccessTokenCurrentUser(authContext, resource, clientId, clientCredentials);

						}
						else
						{
#if (NET462 || NET472 || NET48) 
							logSink.Log(string.Format("ObtainAccessToken - PROMPT - Behavior: {0}", promptBehavior), TraceEventType.Verbose);
							_authenticationResult = ObtainAccessToken(authContext, resource, clientId, redirectUri, promptBehavior, user);
#endif
						}
					}
					//Assigning the authority to ref object to pass back to ConnMgr to store the latest Authority in Credential Manager.
					authority = authContext.Authority;
				}

				if (_authenticationResult != null && _authenticationResult.UserInfo != null)
				{
					//To use same userId while connecting to OrgService (ConnectAndInitCrmOrgService)
					_userId = _authenticationResult.UserInfo.DisplayableId;

					// added to deal with N users logging in on the same machine user account. 
					if (user == null)
					{
						if (_authenticationResult.UserInfo.DisplayableId != null)
							userIdent = new UserIdentifier(_authenticationResult.UserInfo.DisplayableId, UserIdentifierType.RequiredDisplayableId);
					}
					else
						userIdent = user;
				}

				_authority = authContext.Authority;//To use same authority while connecting to OrgService (ConnectAndInitCrmOrgService)

				if (null == _authenticationResult)
				{
					throw new ArgumentNullException("AuthenticationResult", "Need a non-empty authority");
				}
				authToken = _authenticationResult.AccessToken;
			}
			catch (AggregateException ex)
			{
				if (ex.InnerException is AdalException)
				{
					ProcessAdalExecption(serviceUrl, clientCredentials, userCert, out user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, out targetServiceUrl, out authContext, out resource, logSink, useDefaultCreds, out authToken, (AdalException)ex.InnerException);
				}
				else
				{
					logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - General ADAL Error", TraceEventType.Error, ex);
					logSink.Log(ex);
					throw;
				}
			}
			catch (AdalException ex)
			{ 
				ProcessAdalExecption(serviceUrl, clientCredentials, userCert, out user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, out targetServiceUrl, out authContext, out resource, logSink, useDefaultCreds, out authToken, ex);
			}
			catch (System.Exception ex)
			{
				logSink.Log("ERROR REQUESTING Token FROM THE Authentication context", TraceEventType.Error);
				logSink.Log(ex);
				throw;
			}
			finally
			{
				// Do not dispose the auth context here as its passed back out. 

				if (createdLogSource) // Only dispose it if it was created locally. 
					logSink.Dispose();
			}
			return authToken;
		}

		/// <summary>
		/// Process ADAL execption and provide common handlers. 
		/// </summary>
		/// <param name="serviceUrl"></param>
		/// <param name="clientCredentials"></param>
		/// <param name="userCert"></param>
		/// <param name="user"></param>
		/// <param name="clientId"></param>
		/// <param name="redirectUri"></param>
		/// <param name="promptBehavior"></param>
		/// <param name="tokenCachePath"></param>
		/// <param name="isOnPrem"></param>
		/// <param name="authority"></param>
		/// <param name="targetServiceUrl"></param>
		/// <param name="authContext"></param>
		/// <param name="resource"></param>
		/// <param name="logSink"></param>
		/// <param name="useDefaultCreds"></param>
		/// <param name="authToken"></param>
		/// <param name="adalEx"></param>
		private static void ProcessAdalExecption(Uri serviceUrl, ClientCredentials clientCredentials, X509Certificate2 userCert, out UserIdentifier user, string clientId, Uri redirectUri, PromptBehavior promptBehavior, string tokenCachePath, bool isOnPrem, string authority, out Uri targetServiceUrl, out AuthenticationContext authContext, out string resource, CdsTraceLogger logSink, bool useDefaultCreds, out string authToken, AdalException adalEx)
		{
			if (adalEx.ErrorCode.Equals("interaction_required", StringComparison.OrdinalIgnoreCase) ||
				adalEx.ErrorCode.Equals("user_password_expired", StringComparison.OrdinalIgnoreCase) ||
				adalEx.ErrorCode.Equals("password_required_for_managed_user", StringComparison.OrdinalIgnoreCase))
			{
				logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - USER intervention required", TraceEventType.Warning);
				// ADAL wants the User to do something,, determine if we are able to see a user
				if (promptBehavior == PromptBehavior.Always || promptBehavior == PromptBehavior.Auto)
				{
					// Switch to MFA user mode..
					user = new UserIdentifier(clientCredentials.UserName.UserName, UserIdentifierType.OptionalDisplayableId);
					authToken = ExecuteAuthenticateServiceProcess(serviceUrl, null, userCert, user, clientId, redirectUri, promptBehavior, tokenCachePath, isOnPrem, authority, out targetServiceUrl, out authContext, out resource, out user, logSink, useDefaultCreds: useDefaultCreds);
				}
				else
				{
					logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - USER intervention required but not permitted by prompt behavior", TraceEventType.Error, adalEx);
					throw adalEx;
				}
			}
			else
			{
				logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - General ADAL Error", TraceEventType.Error, adalEx);
				throw adalEx;
			}
		}

		/// <summary>
		/// Get the Authority and Support data from the requesting system using a sync call. 
		/// </summary>
		/// <param name="targetServiceUrl">Resource URL</param>
		/// <param name="logSink">Log tracer</param>
		/// <returns>Populated AuthenticationParameters or null</returns>
		private static AuthenticationParameters GetAuthorityFromTargetService(Uri targetServiceUrl, CdsTraceLogger logSink)
		{
			try
			{
				// if using ADAL > 4.x  return.. // else remove oauth2/authorize from the authority
				if (_ADALAsmVersion == null)
				{
					// initial setup to get the ADAL version 
					var AdalAsm = System.Reflection.Assembly.GetAssembly(typeof(IPlatformParameters));
					if (AdalAsm != null)
						_ADALAsmVersion = AdalAsm.GetName().Version;
				}

				logSink.Log($"GetAuthorityFromTargetService - ADAL Version : {_ADALAsmVersion.ToString()}");
				AuthenticationParameters foundAuthority;
				if (_ADALAsmVersion != null && _ADALAsmVersion >= Version.Parse("5.0.0.0"))
				{
					foundAuthority = CreateFromUrlAsync(targetServiceUrl);
				}
				else
				{
					foundAuthority = CreateFromResourceUrlAsync(targetServiceUrl);
				}

				if (_ADALAsmVersion != null && _ADALAsmVersion > Version.Parse("4.0.0.0"))
				{
					foundAuthority.Authority = foundAuthority.Authority.Replace("oauth2/authorize", "");
				}

				return foundAuthority;
			}
			catch (Exception ex)
			{
				if (logSink != null)
				{
					logSink.Log(ex);
				}
				throw ex;
			}

			//return null;
		}

		/// <summary>
		/// Creates authentication parameters from the address of the resource.
		/// </summary>
		/// <param name="targetServiceUrl">Resource URL</param>
		/// <returns>AuthenticationParameters object containing authentication parameters</returns>
		private static AuthenticationParameters CreateFromResourceUrlAsync(Uri targetServiceUrl)
		{
			 var result = (Task<AuthenticationParameters>)typeof(AuthenticationParameters)
			   .GetMethod("CreateFromResourceUrlAsync").Invoke(null, new[] { targetServiceUrl });

			return result.ConfigureAwait(false).GetAwaiter().GetResult();
		}

		/// <summary>
		/// Creates authentication parameters from the address of the resource.
		/// Invoked for ADAL 5+ which changed the method used to retrieve authentication parameters.
		/// </summary>
		/// <param name="targetServiceUrl">Resource URL</param>
		/// <returns>AuthenticationParameters object containing authentication parameters</returns>
		private static AuthenticationParameters CreateFromUrlAsync(Uri targetServiceUrl)
		{
			var result = (Task<AuthenticationParameters>)typeof(AuthenticationParameters)
				.GetMethod("CreateFromUrlAsync").Invoke(null, new[] { targetServiceUrl });

			return result.ConfigureAwait(false).GetAwaiter().GetResult(); ;
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
		private static X509Certificate2 FindCertificate(string certificateThumbprint, StoreName storeName, CdsTraceLogger logSink)
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
		/// Connects too and initializes the CDS org Data service. 
		/// </summary>
		/// <param name="orgdata">Organization Data</param>
		/// <param name="IsOnPrem">True if called from the OnPrem Branch</param>
		/// <param name="homeRealmUri"> URI of the users Home Realm or null</param>
		[SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", MessageId = "proxy")]
		private async Task<IOrganizationService> ConnectAndInitCdsOrgService(OrganizationDetail orgdata, bool IsOnPrem, Uri homeRealmUri)
		{
			//_ActualOrgDetailUsed = orgdata; 
			_ActualCdsOrgUri = BuildOrgConnectUri(orgdata);
			logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Organization Service URI is = {0}", _ActualCdsOrgUri.ToString()), TraceEventType.Information);

			// Set the Org into system config
			_organization = orgdata.UniqueName;
			ConnectedOrgFriendlyName = orgdata.FriendlyName;
			ConnectedOrgPublishedEndpoints = orgdata.Endpoints;

			Stopwatch logDt = new Stopwatch();
			logDt.Start();
			// Build User Credential
			logEntry.Log("ConnectAndInitCdsOrgService - Initializing Organization Service Object", TraceEventType.Verbose);
			// this to provide trouble shooting information when determining org connect failures. 
			logEntry.Log(string.Format(CultureInfo.InvariantCulture, "ConnectAndInitCdsOrgService - Requesting connection to Organization with CDS Version: {0}", orgdata.OrganizationVersion == null ? "No organization data available" : orgdata.OrganizationVersion), TraceEventType.Information);

			// try to create a version number from the org. 
			OrganizationVersion = null;
			try
			{
				OrganizationVersion = new Version(orgdata.OrganizationVersion);
			}
			catch { };

			OrganizationWebProxyClient svcWebClientProxy = null;
			if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.Certificate || _eAuthType == AuthenticationType.ExternalTokenManagement || _eAuthType == AuthenticationType.ClientSecret)
			{
				string resource = string.Empty;
				string Authority = string.Empty;

				Uri targetServiceUrl = null;
				string authToken = string.Empty;

				//Creating UserIdentifier with user who got authenticated during DiscoveryService.
				if (_user == null && _userId != null)
				{
					_user = new UserIdentifier(_userId, UserIdentifierType.RequiredDisplayableId);
				}

				if (_eAuthType == AuthenticationType.ExternalTokenManagement)
				{
					// Call External hook here. 
					try
					{
						targetServiceUrl = targetServiceUrl = GetUriBuilderWithVersion(_ActualCdsOrgUri).Uri;
						if (GetAccessToken != null)
							authToken = await GetAccessToken(targetServiceUrl.ToString());

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
					authToken = ExecuteAuthenticateServiceProcess(_ActualCdsOrgUri, _UserClientCred, _certificateOfConnection, _user, _clientId, _redirectUri, _promptBehavior, _tokenCachePath, IsOnPrem, _authority, out targetServiceUrl, out _authenticationContext, out _resource, out _user, logEntry, useDefaultCreds: _isDefaultCredsLoginForOAuth, clientSecret: _eAuthType == AuthenticationType.ClientSecret ? _LivePass : null);
				}
				_ActualCdsOrgUri = targetServiceUrl;
				svcWebClientProxy = new OrganizationWebProxyClient(targetServiceUrl, true);
				svcWebClientProxy.ChannelFactory.Opening += WebProxyChannelFactory_Opening;
				svcWebClientProxy.HeaderToken = authToken;

				if (svcWebClientProxy != null)
				{
					// Set default timeouts
					svcWebClientProxy.InnerChannel.OperationTimeout = _MaxConnectionTimeout;
					svcWebClientProxy.Endpoint.Binding.SendTimeout = _MaxConnectionTimeout;
					svcWebClientProxy.Endpoint.Binding.ReceiveTimeout = _MaxConnectionTimeout;
				}
			}

			logDt.Stop();
			logEntry.Log(string.Format(CultureInfo.InvariantCulture, "ConnectAndInitCdsOrgService - Proxy created, total elapsed time: {0}", logDt.Elapsed.ToString()));

			return svcWebClientProxy;
		}

		/// <summary>
		/// Grab the Channel factory Open event and add the CrmHook Service behaviors. 
		/// </summary>
		/// <param name="sender">incoming ChannelFactory</param>
		/// <param name="e">ignored</param>
		private void WebProxyChannelFactory_Opening(object sender, EventArgs e)
		{

			// Add Connection header support for Organization Web client. 
			ChannelFactory fact = sender as ChannelFactory;
			if (fact != null)
			{
				if (!fact.Endpoint.EndpointBehaviors.Contains(typeof(CdsServiceTelemetryBehaviors)))
				{
					fact.Endpoint.EndpointBehaviors.Add(new CdsServiceTelemetryBehaviors(this));
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
		/// <returns>CRM Connection URI</returns>
		private Uri BuildOrgConnectUri(OrganizationDetail orgdata)
		{

			logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Start);

			// Build connection URL  
			string CrmUrl = string.Empty;
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
				// CRM Online ..> USE PROVIDED URI. 
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

						CrmUrl = String.Format(CultureInfo.InvariantCulture,
							"{0}://{1}:{2}{3}", _InternetProtocalToUse, _hostname, _port, OrgEndPoint.PathAndQuery);
					}
					else
					{
						CrmUrl = String.Format(CultureInfo.InvariantCulture,
							"{0}://{1}{2}", _InternetProtocalToUse, _hostname, OrgEndPoint.PathAndQuery);
					}

					logEntry.Log("BuildOrgConnectUri CoreClass ()", TraceEventType.Stop);
					return new Uri(CrmUrl);
				}
			}


		}

		/// <summary>
		/// Iterates through the list of CRM online Discovery Servers to find one that knows the user. 
		/// </summary>
		/// <param name="onlineServerList"></param>
		/// <param name="useO365Servers"></param>
		private CdsOrgList FindCdsDiscoveryServer(CdsDiscoveryServers onlineServerList)
		{
			CdsOrgList orgsList = new CdsOrgList();
			OrganizationDetailCollection col = null;

			if (_OrgDetail == null)
			{
				// If the user as Specified a server to use, try to get the org from that server. 
				if (!string.IsNullOrWhiteSpace(_CdsOnlineRegion))
				{
					logEntry.Log("Using User Specified Server ", TraceEventType.Information);
					// Server Specified... 
					CdsDiscoveryServer svr = onlineServerList.GetServerByShortName(_CdsOnlineRegion);
					if (svr != null)
					{
						if (_eAuthType == AuthenticationType.OAuth && svr.RequiresRegionalDiscovery)
						{
							if (svr.RegionalGlobalDiscoveryServer == null)
							{
								logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);
								col = QueryLiveDiscoveryServer(svr.DiscoveryServer); // Defaults to not using GD. 
							}
							else
							{
								logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Regional Global Discovery Server, ({1}) URI is = {0}", svr.RegionalGlobalDiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);
								QueryOnlineServersList(onlineServerList.OSDPServers, col, orgsList, svr.DiscoveryServer, svr.RegionalGlobalDiscoveryServer);
								//col = QueryLiveDiscoveryServer(svr.DiscoveryServer); // Defaults to not using GD. 
								return orgsList;
							}
						}
						else
						{
							if (_eAuthType == AuthenticationType.OAuth)
							{
								// OAuth, and GD is allowed. 
								logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Global Discovery Server ({0}) and filtering to region {1}", GlobalDiscoveryAllInstancesUri, _CdsOnlineRegion), TraceEventType.Information);
								QueryOnlineServersList(onlineServerList.OSDPServers, col, orgsList, svr.DiscoveryServer);
								return orgsList;
							}
							else
							{
								col = QueryLiveDiscoveryServer(svr.DiscoveryServer);
								if (col != null)
									AddOrgToOrgList(col, svr.DisplayName, svr.DiscoveryServer, ref orgsList);
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
					col = QueryLiveDiscoveryServer(new Uri(GlobalDiscoveryAllInstancesUri), true);
					if (col != null)
					{
						bool isOnPrem = false;
						foreach (var itm in col)
						{
							var orgObj = Utilities.DeterminDiscoveryDataFromOrgDetail(new Uri(itm.Endpoints[EndpointType.OrganizationService]), out isOnPrem);
							AddOrgToOrgList(itm, orgObj.DisplayName, ref orgsList);
						}
					}
					return orgsList;
				}
				else
					QueryOnlineServersList(onlineServerList.OSDPServers, col, orgsList);
			}
			else
			{
				// the org was preexisting
				logEntry.Log("User Specified Org details are used.", TraceEventType.Information);
				col = new OrganizationDetailCollection();
				col.Add(_OrgDetail);
				AddOrgToOrgList(col, "User Defined Org Detail", new Uri(_OrgDetail.Endpoints[EndpointType.OrganizationService]), ref orgsList);
			}

			return orgsList;
		}

		/// <summary>
		/// Iterate over the discovery servers available. 
		/// </summary>
		/// <param name="svrs"></param>
		/// <param name="col"></param>
		/// <param name="orgsList"></param>
		/// <param name="trimToDiscoveryUri">Forces the results to be trimmed to this region when present</param>
		/// <param name="globalDiscoUriToUse">Overriding Global Discovery URI</param>
		private void QueryOnlineServersList(ObservableCollection<CdsDiscoveryServer> svrs, OrganizationDetailCollection col, CdsOrgList orgsList, Uri trimToDiscoveryUri = null, Uri globalDiscoUriToUse = null)
		{
			// CHANGE HERE FOR GLOBAL DISCO ----
			// Execute Global Discovery
			if (_eAuthType == AuthenticationType.OAuth)
			{
				Uri gdUriToUse = globalDiscoUriToUse != null ? new Uri(string.Format(_baselineGlobalDiscoveryFormater, globalDiscoUriToUse.ToString(), _globlaDiscoVersion, "Instances")) : new Uri(GlobalDiscoveryAllInstancesUri);
				logEntry.Log(string.Format("Trying Global Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Information);
				try
				{
					col = QueryLiveDiscoveryServer(gdUriToUse, true);
				}
				catch (MessageSecurityException)
				{
					logEntry.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Warning);
					col = null;
				}
				catch (Exception ex)
				{
					logEntry.Log(string.Format("Exception while trying to connect Discovery Server, ({1}) URI is = {0}", gdUriToUse.ToString(), "Global Discovery"), TraceEventType.Error, ex);
					col = null;
				}

				// if we have results.. add them to the AddOrgToOrgList object. ( need to iterate over the objects to match region to result. ) 

				if (col != null)
				{
					bool isOnPrem = false;
					foreach (var itm in col)
					{
						var orgObj = Utilities.DeterminDiscoveryDataFromOrgDetail(new Uri(itm.Endpoints[EndpointType.OrganizationService]), out isOnPrem);
						if (trimToDiscoveryUri != null && !trimToDiscoveryUri.Equals(orgObj.DiscoveryServer))
							continue;
						AddOrgToOrgList(itm, orgObj.DisplayName, ref orgsList);
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
						if (svr.DiscoveryServer == null) continue;

						logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Trying Live Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);

						col = QueryLiveDiscoveryServer(svr.DiscoveryServer);
						if (col != null)
							AddOrgToOrgList(col, svr.DisplayName, svr.DiscoveryServer, ref orgsList);
					}
					catch (MessageSecurityException)
					{
						logEntry.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Warning);
						col = null;
					}
					catch (Exception)
					{
						logEntry.Log(string.Format("Exception while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Error);
						col = null;
					}
				}
			}
		}


		/// <summary>
		/// Query an individual Live System
		/// </summary>
		/// <param name="discoServer"></param>
		/// <param name="useGlobal">when try, uses global discovery</param>
		/// <returns></returns>
		private OrganizationDetailCollection QueryLiveDiscoveryServer(Uri discoServer, bool useGlobal = false)
		{
			logEntry.Log("QueryLiveDiscoveryServer()", TraceEventType.Start);
			try
			{
				if (_eAuthType == AuthenticationType.OAuth || _eAuthType == AuthenticationType.ClientSecret)
				{
					return DiscoverOrganizations(discoServer, _UserClientCred, _user, _clientId, _redirectUri, _promptBehavior, _tokenCachePath, false, _authority, logEntry, useGlobalDisco: useGlobal);
				}
				else
				{
					if (_eAuthType == AuthenticationType.Certificate)
					{
						return DiscoverOrganizations(discoServer, _certificateOfConnection, _clientId, _tokenCachePath, false, _authority, logEntry);
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
		private void AddOrgToOrgList(OrganizationDetailCollection organizationDetailList, string discoveryServer, Uri discoveryServerUri, ref CdsOrgList orgList)
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
		private void AddOrgToOrgList(OrganizationDetail organizationDetail, string discoveryServer, ref CdsOrgList orgList)
		{

			if (orgList == null) orgList = new CdsOrgList();
			if (orgList.OrgsList == null) orgList.OrgsList = new ObservableCollection<CdsOrgByServer>();

			orgList.OrgsList.Add(new CdsOrgByServer()
			{
				DiscoveryServerName = discoveryServer,
				OrgDetail = organizationDetail
			});
		}

		/// <summary>
		/// Refresh web proxy client token
		/// </summary>
		internal async Task<string> RefreshWebProxyClientToken()
		{
			if (GetAccessTokenFromParent != null)
				return GetAccessTokenFromParent();

			if (_authenticationContext != null && !string.IsNullOrEmpty(_resource) && !string.IsNullOrEmpty(_clientId))
			{
				if (_isCalledbyExecuteRequest && _promptBehavior != PromptBehavior.Never)
				{
					_isCalledbyExecuteRequest = false;

#if (NET462 || NET472 || NET48) 
					// token cache check is skipped here as all userID based flows could require change in MFA or token status at any time, 
					// thus turning control over to AAD Libs to decide treatment. 
					var ar = ObtainAccessToken(_authenticationContext, _resource, _clientId, _redirectUri, PromptBehavior.Auto, _user);
					_oAuthar = ar;
					_svcWebClientProxy.HeaderToken = ar.AccessToken;
#endif
				}
				else
				{
					// Check to see if the token is still valid, it so , abort and send request back. 
					if (_oAuthar != null && _oAuthar.ExpiresOn.ToUniversalTime() > DateTime.UtcNow.Add(_tokenOffSetTimeSpan))
						return _oAuthar.AccessToken;

					if (_eAuthType == AuthenticationType.Certificate)
					{
						var ar = ObtainAccessToken(_authenticationContext, _resource, _clientId, _certificateOfConnection);
						_svcWebClientProxy.HeaderToken = ar.AccessToken;
						_oAuthar = ar;
					}
					else
					{
						if (_eAuthType == AuthenticationType.ClientSecret)
						{
#if (NET462 || NET472 || NET48) 
							var ar = ObtainAccessToken(_authenticationContext, _resource, _clientId, _LivePass);
#else
							var ar = ObtainAccessToken(_authenticationContext, _resource, _clientId, _LivePass.ToUnsecureString());
#endif
							_svcWebClientProxy.HeaderToken = ar.AccessToken;
							_oAuthar = ar;
						}
						else
						{
#if (NET462 || NET472 || NET48) 
							// If is user ID Auth / and prompt behavior is set to Never, then treat as a 'service' connection. 
							// thus if MFA / or User token status is changed by AAD, then fault out using AAD flows. 
							var ar = ObtainAccessToken(_authenticationContext, _resource, _clientId, _redirectUri, _promptBehavior, _user);
							_svcWebClientProxy.HeaderToken = ar.AccessToken;
							_oAuthar = ar;
#endif
						}
					}
				}
			}

			if (_eAuthType == AuthenticationType.ExternalTokenManagement)
			{
				// Call External hook here. 
				try
				{
					if (GetAccessToken != null)
						_svcWebClientProxy.HeaderToken = await GetAccessToken(_ActualCdsOrgUri.ToString());
					else
						throw new Exception("External Authentication Requested but not configured correctly. Faulted In Request Access Token 004");
									  
				}
				catch (Exception ex)
				{
					throw new Exception("External Authentication Requested but not configured correctly. 005", ex);
				}
			}

			if (_svcWebClientProxy != null)
				return _svcWebClientProxy.HeaderToken;
			else
				return string.Empty; // this can happen when running via tests 

		}

#region IDisposable Support
		/// <summary>
		/// Reset disposed state to handle this object being pulled from cache. 
		/// </summary>
		private void ResetDisposedState()
		{
			// reset the disposed state to deal with the object being pulled from cache. 
			disposedValue = false;
		}
		private bool disposedValue = false; // To detect redundant calls

		void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (isLogEntryCreatedLocaly)
					{
						if (logEntry != null)
							logEntry.Dispose();
					}

					if (_authenticationContext != null && _authenticationContext.TokenCache != null)
					{
						if (_authenticationContext.TokenCache is CdsServiceClientTokenCache)
						{
							((CdsServiceClientTokenCache)_authenticationContext.TokenCache).Dispose();
						}
					}

					if (unqueInstance)
					{
						// Clean the connect out of memory. 
						System.Runtime.Caching.MemoryCache.Default.Remove(_ServiceCACHEName);
					}

					try
					{
						if (_svcWebClientProxy != null)
						{
							if (_svcWebClientProxy.Endpoint.EndpointBehaviors.Contains(typeof(CdsServiceTelemetryBehaviors)))
							{
								_svcWebClientProxy.ChannelFactory.Opening -= WebProxyChannelFactory_Opening;
								_svcWebClientProxy.ChannelFactory.Endpoint.EndpointBehaviors.Remove(typeof(CdsServiceTelemetryBehaviors));
								_svcWebClientProxy.Endpoint.EndpointBehaviors.Remove(typeof(CdsServiceTelemetryBehaviors));
							}
						}
					}
					catch { }; // Failed to dispose.. no way to notifiy this right now.. let it go . 
				}

				disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			Dispose(true);
		}
#endregion
#endregion

	}

#region Extension Methods for SecureString
	/// <summary>
	/// Adds a extension to Secure string
	/// </summary>
	internal static class SecureStringExtensions
	{
		/// <summary>
		/// DeCrypt a Secure password 
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string ToUnsecureString(this SecureString value)
		{
			if (null == value)
				throw new ArgumentNullException("value");

			// Get a pointer to the secure string memory data. 
			IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
			try
			{
				// DeCrypt
				return Marshal.PtrToStringUni(ptr);
			}
			finally
			{
				// release the pointer. 
				Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}
#endregion
}
