using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.ConnectControl.Model;
using Microsoft.PowerPlatform.Dataverse.ConnectControl.Properties;
using Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility;
using Microsoft.Xrm.Sdk.Discovery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using System.Web.Services.Protocols;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
    /// <summary>
    ///  Provides Connection logic and error handling for connecting to CRM2011.  This class is designed to operate in the background, off the primary user UI thread.
    ///  This class raises events that can be used to update the user with progress reports. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
	public class ConnectionManager : IDisposable
	{
		#region vars
		/// <summary>
		/// Background worker process for loading Connection To the server. 
		/// </summary>
		private BackgroundWorker _bgWorker = null;

		/// <summary>
		/// Keys for the Server Configuration Info.
		/// </summary>
		private Dictionary<Dynamics_ConfigFileServerKeys, object> ServerConfigKeys;

        /// <summary>
        /// Dataverse Connection Object. 
        /// </summary>
        public ServiceClient ServiceClient { get; set; }

        /// <summary>
        /// Login Tracing System
        /// </summary>
        private LoginTracer _tracer = new LoginTracer();

        /// <summary>
        /// contains the current deployment type. 
        /// </summary>
        private CrmDeploymentType _deploymentType = CrmDeploymentType.O365;

        /// <summary>
        /// determines if SSL Is required for Login
        /// </summary>
        private bool IsSSLReq = false;

        /// <summary>
        /// determines if the current Authentication type is OAuth
        /// </summary>
        private bool IsOAuth = false;

		/// <summary>
		/// determines if the Default Credentials can be used
		/// </summary>
		private bool UseDefaultCreds = false;

		/// <summary>
		/// determines if Advanced Check box is enabled
		/// </summary>
		private bool IsAdvancedCheckEnabled = false;

		/// <summary>
		/// Prompt Behavior to work on
		/// </summary>
		private Client.Auth.PromptBehavior _promptBehavior = Client.Auth.PromptBehavior.Auto;

		/// <summary>
		/// Home Realm of the user.  if Present, it is provided for Claims Auth
		/// </summary>
		private Uri uUserHomeRealm = null;

		/// <summary>
		/// Use Creds for Login to CRM for Prem Solutions 
		/// </summary>
		private NetworkCredential _userCred;

		/// <summary>
		/// Describes the user Client Credential 
		/// </summary>
		private ClientCredentials _userClientCred;

		/// <summary>
		/// Device Credentials for Connecting to 
		/// </summary>
		private ClientCredentials DeviceCredentials;

		/// <summary>
		/// Org List View that will be assigned to the Select Org Dialog box. 
		/// this is created here to limit the impact on user XAML code. 
		/// </summary>
		private CrmOrgList _orgListView = new CrmOrgList();

		/// <summary>
		/// Private variable that contains the CRM Discovery Server List. 
		/// </summary>
		private OnlineDiscoveryServers _onlineDiscoveryServerList = null;

		/// <summary>
		/// Private variable that contains the HomeRealmList
		/// </summary>
		private ClaimsHomeRealmOptions _homeRealmServersList = null;

		/// <summary>
		/// Profile name to use for this connection, only valid if UseUserLocalDirectory is set to true
		/// </summary>
		private string _profileName;

		/// <summary>
		/// flag indicating that the last error was sent though a status update. 
		/// </summary>
		private bool _lastErrorSent = false;

		/// <summary>
		/// Private variable that contains cached authority (to store in) \(fetch from)  credential managaer
		/// </summary>
		private string _cachedAuthorityName = null;

		/// <summary>
		/// Private variable that contains cached userId (to store in) \(fetch from) config file
		/// </summary>
		private string _cachedUserId = null;

		/// <summary>
		/// if true, Skip discovery is in focus
		/// </summary>
		private bool _isSkipDiscovery = false;

		/// <summary>
		/// if populated, contains the URL to try a direct connect too. 
		/// </summary>
		private string _directConnectUri = string.Empty;

		/// <summary>
		/// format string for the global discovery service
		/// </summary>
		private static readonly string _globalDiscoBaseWebAPIUriFormat = "https://globaldisco.crm.dynamics.com/api/discovery/v{0}/{1}";

		/// <summary>
		/// version of the globaldiscovery service. 
		/// </summary>
		private static readonly string _globlaDiscoVersion = "2.0";

		#endregion

		#region Events

		/// <summary>
		/// Raised when a status is updated
		/// </summary>
		public event EventHandler<ServerConnectStatusEventArgs> ServerConnectionStatusUpdate;
		/// <summary>
		/// Raised when the connection process completes
		/// </summary>
		public event EventHandler<ServerConnectStatusEventArgs> ConnectionCheckComplete;

		#endregion

		#region Properties

		/// <summary>
		/// Collection of CRM Discovery Servers
		/// if a server list has not been submitted to this property, and get is called, a new instance of the OnlineDiscoveryServers is created.
		/// </summary>
		public OnlineDiscoveryServers OnlineDiscoveryServerList { get { if (_onlineDiscoveryServerList == null) _onlineDiscoveryServerList = new OnlineDiscoveryServers(); return _onlineDiscoveryServerList; } set { _onlineDiscoveryServerList = value; } }

		/// <summary>
		/// Collection of HomeRealms Loaded by Configuration 
		/// </summary>
		public ClaimsHomeRealmOptions HomeRealmServersList { get { if (_homeRealmServersList == null) _homeRealmServersList = new ClaimsHomeRealmOptions(); return _homeRealmServersList; } set { _homeRealmServersList = value; } }

		/// <summary>
		/// User Identifier as a login hint
		/// </summary>
		public UserIdentifier UserId { get; set; }

		/// <summary>
		/// ClientId for the client
		/// </summary>
		public string ClientId { get; set; }

		/// <summary>
		/// Resource for the resource
		/// </summary>
		public Uri RedirectUri { get; set; }

		/// <summary>
		/// Token Cache Path where tokencache file will be stored.
		/// </summary>
		public string TokenCachePath { get; set; }

		/// <summary>
		/// List of Organizations that have been requested by the user. 
		/// </summary>
		public CrmOrgList CrmOrgsFoundForUser { get { return _orgListView; } }

		/// <summary>
		/// This the parent control that invoked me. 
		/// </summary>
		public UserControl ParentControl { get; set; }

		/// <summary>
		/// Returns the friendly name of the connected org. 
		/// </summary>
		public string ConnectedOrgFriendlyName { get; private set; }
		/// <summary>
		/// Returns the unique name for the org that has been connected. 
		/// </summary>
		public string ConnectedOrgUniqueName { get; private set; }
		/// <summary>
		/// Returns the endpoint collection for the connected org. 
		/// </summary>
		public EndpointCollection ConnectedOrgPublishedEndpoints { get; private set; }
		
		/// <summary>
		/// Returns the unique Id of the connected organization.
		/// </summary>
		public Guid ConnectedOrgId { get; private set; } 

		/// <summary>
		/// Tells the system to store the user config in the users local app directory instead of the Exe directory. 
		/// </summary>
		public bool UseUserLocalDirectoryForConfigStore { get; set; }
		/// <summary>
		/// Used in conjunction with the UseUserLocalDirecotryForConfigStore,  Allows you to set a name for the config application to use. 
		/// This is used when the host application cannot provide a proper AppDomain.Current.FriendlyName
		/// </summary>
		public string HostApplicatioNameOveride { get; set; }

		/// <summary>
		/// Returns Last successful DeploymentType.
		/// </summary>
		internal string LastDeploymentType { get; set; }

		/// <summary>
		/// Profile name to use for this login process.
		/// </summary>
		public string ProfileName
		{
			get
			{
				return _profileName;
			}
			set
			{
				// Check for validSet
				bool bFail = false;
				if (value.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
					bFail = true;
				if (value.IndexOfAny(Path.GetInvalidPathChars()) != -1)
					bFail = true;
				if (bFail)
					throw new System.ArgumentOutOfRangeException(Messages.CRMCONNECT_PROFILENAME_INVALID);
				_profileName = value;
			}
		}

		/// <summary>
		/// last error from the connection manager
		/// </summary>
		public string LastError { get { return _tracer != null ? _tracer.LastError : string.Empty; } }

		/// <summary>
		/// Last Exception from the connection manager. 
		/// </summary>
		public Exception LastException { get { return _tracer != null ? _tracer.LastException : null; } }

		/// <summary>
		/// Forces an OAuth Prompt on the first request for this connection. 
		/// </summary>
		public bool ForceFirstOAuthPrompt { get; set; }

		/// <summary>
		/// returns the URL to global discovery for querying all instances. 
		/// </summary>
		private string GlobalDiscoveryAllInstancesUri { get { return string.Format(_globalDiscoBaseWebAPIUriFormat, _globlaDiscoVersion, "Instances"); } }
		/// <summary>
		/// Format string for calling global disco for a specific instance. 
		/// </summary>
		private string GlobalDiscoveryInstanceUriFormat { get { return string.Format(_globalDiscoBaseWebAPIUriFormat, _globlaDiscoVersion, "Instances({0})"); } }

		#endregion

		/// <summary>
		/// Default constructor. 
		/// </summary>
		public ConnectionManager()
		{
			// Default Profile setting 
			ProfileName = "Default";

			// Configure the background worker.
			InitBackgroundWorker();
			_tracer.Log("Created CrmConnectionManager ", TraceEventType.Information);
		}

		/// <summary>
		/// Clean up background worker on exit. 
		/// </summary>
		~ConnectionManager()
		{
			if (_bgWorker != null)
				_bgWorker.Dispose();
		}

		/// <summary>
		/// Begins the authentication process for CRM. 
		/// </summary>
		public void ConnectToServerCheck()
		{
			_tracer.Log("ConnectToServerCheck()", TraceEventType.Start);

			if (_bgWorker != null)
				_bgWorker.RunWorkerAsync(new object());

			_tracer.Log("ConnectToServerCheck()", TraceEventType.Stop);
		}

		/// <summary>
		/// Begin Connect check to server
		/// </summary>
		/// <param name="selectedOrgToConnectTo"></param>
		public void ConnectToServerCheck(OrgByServer selectedOrgToConnectTo)
		{
			_tracer.Log("ConnectToServerCheck( OrganizationDetail selectedOrgToConnectTo)", TraceEventType.Start);

			if (_bgWorker != null)
				_bgWorker.RunWorkerAsync(selectedOrgToConnectTo);

			_tracer.Log("ConnectToServerCheck( OrganizationDetail selectedOrgToConnectTo)", TraceEventType.Stop);
		}

		/// <summary>
		/// Cancel Connection process. 
		/// </summary>
		public void CancelConnectToServerCheck()
		{
			_tracer.Log("CancelConnectToServerCheck()", TraceEventType.Start);
			if (_bgWorker != null)
				_bgWorker.CancelAsync();

			_tracer.Log("CancelConnectToServerCheck()", TraceEventType.Stop);
		}

		/// <summary>
		/// This will check the configuration to determine if a user login is required. and if so, return true. 
		/// </summary>
		/// <returns></returns>
		public bool RequireUserLogin()
		{
			_tracer.Log("RequireUserLogin()", TraceEventType.Start);

			if (ServerConfigKeys == null || ServerConfigKeys.Count <= 4)
				LoadConfigFromFile();

			// Default to Require login. 
			bool isUserLoginRequired = true;

			if (ValidateUserSpecifiedData())
				isUserLoginRequired = false;

			// Handle the switch being set in configuration, outside of the UI. 
			bool cacheCreds = string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials)) ? true :
				StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials).Equals(true.ToString(), StringComparison.CurrentCultureIgnoreCase);

			if (!cacheCreds)
				isUserLoginRequired = true;

			_tracer.Log("RequireUserLogin()", TraceEventType.Stop);
			// If the cache credentials is true, then we want to allow for a direct login,  else we want to force a user login. 
			return isUserLoginRequired;
		}

		/// <summary>
		/// Sets the current connection information for the server. 
		/// <para>this can be used to pass in a preconfigured list of keys</para>
		/// </summary>
		/// <param name="configKeys"></param>
		public void SetConfigKeyInformation(Dictionary<Dynamics_ConfigFileServerKeys, object> configKeys)
		{
			_tracer.Log(string.Format("SetConfigKeyInfo, Key Count = {0}", configKeys.Count), TraceEventType.Information);
			ServerConfigKeys = configKeys;
		}

		#region Threaded handlers

		/// <summary>
		/// Initializes the worker process
		/// </summary>
		/// <returns></returns>
		private void InitBackgroundWorker()
		{
			_tracer.Log("InitBackgroundWorker()", TraceEventType.Start);
			_bgWorker = new BackgroundWorker();
			_bgWorker.WorkerReportsProgress = true;
			_bgWorker.WorkerSupportsCancellation = true;
			_bgWorker.DoWork += new DoWorkEventHandler(bgWorker_DoWork);
			_bgWorker.ProgressChanged += new ProgressChangedEventHandler(bgWorker_ProgressChanged);
			_bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorker_RunWorkerCompleted);
			_tracer.Log("InitBackgroundWorker()", TraceEventType.Stop);
		}

		/// <summary>
		/// Raised when the connection is complete.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			_tracer.Log("bgWorker_RunWorkerCompleted()", TraceEventType.Start);
			if (e != null)
			{
				if (e.Result is bool)
				{
					if (ConnectionCheckComplete != null)
						if (_orgListView != null && _orgListView.OrgsList != null && _orgListView.OrgsList.Count > 1)
							ConnectionCheckComplete(this, new ServerConnectStatusEventArgs(string.Empty, (bool)e.Result, true));
						else
							if ((bool)e.Result)
							{
								// Good Connect... 
								ConnectionCheckComplete(this, new ServerConnectStatusEventArgs(string.Empty, (bool)e.Result)
								{
									StatusMessage = (bool)e.Result ? Messages.CRMCONNECT_SERVER_CONNECT_GOOD :
									  ServiceClient != null ? ServiceClient.LastError :
									  string.Empty
								});
							}
							else
							{
								// Bad Connect
								if (!_lastErrorSent)
								{
									if (ServerConnectionStatusUpdate != null)
									{
										ServerConnectionStatusUpdate(this,
											new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), false,
												new Exception(
													ServiceClient != null && string.IsNullOrWhiteSpace(ServiceClient.LastError) ? ServiceClient.LastError : Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL
													)))
												);
									}
								}

								ConnectionCheckComplete(this, new ServerConnectStatusEventArgs(string.Empty, (bool)e.Result)
								{
									StatusMessage = (bool)e.Result ? Messages.CRMCONNECT_SERVER_CONNECT_GOOD :
									  ServiceClient != null ? ServiceClient.LastError :
									  "BadConnect"
								});
							}
				}
			}
			else
				if (ConnectionCheckComplete != null)
					ConnectionCheckComplete(this, new ServerConnectStatusEventArgs(string.Empty, false));

			_tracer.Log("bgWorker_RunWorkerCompleted()", TraceEventType.Stop);
		}

		/// <summary>
		/// Raised for status events from the worker process 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			_lastErrorSent = false;
			_tracer.Log("bgWorker_ProgressChanged()", TraceEventType.Start);
			if (e != null)
			{
				if (e.UserState is ServerConnectStatusEventArgs)
				{
					if (e.ProgressPercentage == 100)
					{
						// Log last error here...
						if (!string.IsNullOrWhiteSpace(((ServerConnectStatusEventArgs)e.UserState).StatusMessage) ||
							!string.IsNullOrWhiteSpace(((ServerConnectStatusEventArgs)e.UserState).ErrorMessage) ||
							((ServerConnectStatusEventArgs)e.UserState).exEvent != null)
						{
							_lastErrorSent = true;
						}
					}
					// in Connection Validation... 
					if (ServerConnectionStatusUpdate != null)
					{
						ServerConnectionStatusUpdate(this, (ServerConnectStatusEventArgs)e.UserState);
						_tracer.Log(string.Format("Progress changed to: {0}", ((ServerConnectStatusEventArgs)e.UserState).StatusMessage), TraceEventType.Stop);
					}
				}
			}
			else
				if (ServerConnectionStatusUpdate != null)
					ServerConnectionStatusUpdate(this, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_ERR_UNEXPECTED_STATUS));
			_tracer.Log("bgWorker_ProgressChanged()", TraceEventType.Stop);
		}

		/// <summary>
		/// Handler that executes Background worker.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			_tracer.Log("bgWorker_DoWork()", TraceEventType.Start);
			_bgWorker.ReportProgress(1, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNECT_TO_UII));

			// Run Connect
			bool bServerGood = false;
			if (e.Argument != null && e.Argument is OrgByServer)
				bServerGood = ValidateServerConnection((OrgByServer)e.Argument);
			else
				bServerGood = ValidateServerConnection(null);
			e.Result = bServerGood;

			if (_orgListView != null && _orgListView.OrgsList != null && _orgListView.OrgsList.Count > 1)
				// Orgs found in the last run... Raise the MultiOrg Event.
				_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs("User attention required", false, true));


			if ((bool)e.Result)
				_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_SERVER_CONNECT_GOOD));

			_tracer.Log("bgWorker_DoWork()", TraceEventType.Stop);
		}

		#endregion


		/// <summary>
		/// Validate and connect to the server. 
		/// </summary>
		/// <param name="selectedOrg">User Selected Org.</param>
		/// <returns></returns>
		private bool ValidateServerConnection(OrgByServer selectedOrg)
		{
			_tracer.Log("ValidateServerConnection()", TraceEventType.Start);

			if (ServerConfigKeys == null || ServerConfigKeys.Count <= 4)
				LoadConfigFromFile();

			string sErrorMessage = string.Empty;

			// Clears out the connected org information. 
			ConnectedOrgFriendlyName = string.Empty;
			// Clear out the OrgList
			ClearOrgList();

			//Check for and handle ForceOSDP flag in app.config of supporting program, mostly used for PD, DMT and other tools consuming tooling connector. 
			ResetOAuthIfOSDPOverrideSwitch();

			// URI check..
			// Make sure there is a value
			if (!ValidateUserSpecifiedData()) return false;

			// Check to see if its a direct connect. 
			bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDirectConnection), out _isSkipDiscovery);
			if (_isSkipDiscovery)
				_directConnectUri = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.DirectConnectionUri);

			// Value is not a bool.. check to see if there is a value. 
			string sDeploymentType = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType);
			if (string.IsNullOrWhiteSpace(sDeploymentType))
				_deploymentType = CrmDeploymentType.Prem;
			else
			{
				// there is a value. 
				if (sDeploymentType.Equals(CrmDeploymentType.O365.ToString(), StringComparison.OrdinalIgnoreCase))
					_deploymentType = CrmDeploymentType.O365;
				else
					if (sDeploymentType.Equals(CrmDeploymentType.Online.ToString(), StringComparison.OrdinalIgnoreCase))
					_deploymentType = CrmDeploymentType.Online;
				else
					_deploymentType = CrmDeploymentType.Prem;
			}
			_tracer.Log($"Using CRM deployment type {_deploymentType}", TraceEventType.Information);

			if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUseSSL), out IsSSLReq))
				IsSSLReq = false;

			_tracer.Log($"SSL Connection = {IsSSLReq}", TraceEventType.Information);

			// Flush out ServiceClient Connection
			ServiceClient = null;

			// Check for saving login creds:
			if (RequireUserLogin())
				TokenCachePath = string.Empty;

			_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_INIT_UII_CONNECTION));

			try
			{
				uUserHomeRealm = null;  // Reset HomeRealm Info
				IsOAuth = false; // Reset OAuth Information
				if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds), out UseDefaultCreds))
				{
					UseDefaultCreds = false;
				}

				// Get Orgs and Direct ( single org ) login. 
				if (_deploymentType == CrmDeploymentType.Prem)
				{
					#region On-PremCode
					// On Premise - Build Discovery Connection string. 

					// You cannot use Default config for anything other then AD or IFD Setting's in the Auth Realm. 
					if ((StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_AD) ||
						 StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_IFD)))
					{

						// Credentials support Default Credentials... 
						if (UseDefaultCreds)
							// Use default creds..
							_userCred = System.Net.CredentialCache.DefaultNetworkCredentials;
						else
						{
							// Build Creds
							_userCred =
								new System.Net.NetworkCredential(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName),
									StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword),
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)
									);

							if (!string.IsNullOrWhiteSpace(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)) &&
								StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_IFD))
							{
								// Domain is present... see if the user typed it into the user name box too...
								if (!StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName).Contains(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)))
								{
									// they don't match.. prepend it.. 

									string overrideUserName = string.Format("{0}\\{1}",
										StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain),
										StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName));

									if (!string.IsNullOrWhiteSpace(overrideUserName))
										_userCred.UserName = overrideUserName;
								}
							}
						}
					}
					else if (StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_OAUTH))
					{
						IsOAuth = true;
						if (UseDefaultCreds)
							// Use default creds..
							_userCred = System.Net.CredentialCache.DefaultNetworkCredentials;
						else
						{
							// Build Creds
							_userCred =
								new System.Net.NetworkCredential(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName),
									StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword),
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)
									);
						}

						if (_userClientCred == null)
							_userClientCred = new ClientCredentials();

						string overrideUserName = string.Empty;
						if (!string.IsNullOrWhiteSpace(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)))
						{
							// Domain is present... see if the user typed it into the user name box too...
							if (!StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName).Contains(
								StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)))
							{
								// they don't match.. prepend it.. 
								overrideUserName = string.Format("{0}\\{1}",
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain),
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName));

							}
						}

						if (string.IsNullOrWhiteSpace(overrideUserName))
							_userClientCred.UserName.UserName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
						else
							_userClientCred.UserName.UserName = overrideUserName;
						SecureString password = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
						if (password != null)
							_userClientCred.UserName.Password = password.ToUnsecureString();
					}
					else
					{
						// Credentials Required.
						ClaimsHomeRealmOptionsHomeRealm realm = HomeRealmServersList.GetServerByDisplayName(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm));
						if (realm != null && !string.IsNullOrEmpty(realm.Uri))
						{
							uUserHomeRealm = new Uri(realm.Uri);

							_tracer.Log(string.Format("HomeRealm is = {0}", uUserHomeRealm.ToString()), TraceEventType.Information);

							DeviceCredentials = new ClientCredentials();

							DeviceCredentials.UserName.UserName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
							var securePassword = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
							if (securePassword != null)
								DeviceCredentials.UserName.Password = securePassword.ToUnsecureString();

							if (_userClientCred == null)
								_userClientCred = new ClientCredentials();

							string overrideUserName = string.Empty;
							if (!string.IsNullOrWhiteSpace(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)))
							{
								// Domain is present... see if the user typed it into the user name box too...
								if (!StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName).Contains(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain)))
								{
									// they don't match.. prepend it.. 
									overrideUserName = string.Format("{0}\\{1}",
										StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain),
										StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName));

								}
							}

							if (string.IsNullOrWhiteSpace(overrideUserName))
								_userClientCred.UserName.UserName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
							else
								_userClientCred.UserName.UserName = overrideUserName;
							SecureString password = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
							if (password != null)
								_userClientCred.UserName.Password = password.ToUnsecureString();
						}
						else
							uUserHomeRealm = null;
					}

					// This path Attempts to login in the user
					if (selectedOrg != null)
					{
						return ConnectAndInitOrgService(selectedOrg);
					}

					// Connecting to an OnPrem Server here. 
					_bgWorker.ReportProgress(30, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNECT_TO_UII));

					// Build Discovery Server connection URL  
					string CrmUrl = string.Empty;
					if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort)))
					{
						CrmUrl = String.Format(CultureInfo.InvariantCulture,
							"{0}://{1}:{2}/XRMServices/2011/Discovery.svc",
							IsSSLReq ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
							StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName),
							StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort));
					}
					else
					{
						CrmUrl = String.Format(CultureInfo.InvariantCulture,
							"{0}://{1}/XRMServices/2011/Discovery.svc",
							IsSSLReq ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
							StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName));
					}

					_tracer.Log($"Discovery URI is = {CrmUrl}", TraceEventType.Information);

					// Discover Orgs Url. 
					Uri uCrmUrl = new Uri(CrmUrl);

					// Create the CRM Service Connection. 
					_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_GET_ORGS));


					// This will try to discover any organizations that the user has access too,  one way supports AD / IFD and the other supports Claims  
					DiscoverOrganizationsResult discoverOrganizationsResult = null;

					if (IsOAuth == true)
					{
						//Reading authority and userId from Config file
						if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.Authority)))
							_cachedAuthorityName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.Authority);

						if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UserId)))
							_cachedUserId = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UserId);

						if (ForceFirstOAuthPrompt)
						{
							_cachedAuthorityName = null;
							_cachedUserId = null;
							ForceFirstOAuthPrompt = false;
							discoverOrganizationsResult = ServiceClient.DiscoverOnPremiseOrganizationsAsync(uCrmUrl, _userClientCred, ClientId, RedirectUri, _cachedAuthorityName, Client.Auth.PromptBehavior.Always, false, TokenCachePath).ConfigureAwait(false).GetAwaiter().GetResult();
						}
						else
						{
							if (UserId == null && _cachedUserId != null)
								UserId = new UserIdentifier(_cachedUserId, UserIdentifierType.RequiredDisplayableId);
							discoverOrganizationsResult = ServiceClient.DiscoverOnPremiseOrganizationsAsync(uCrmUrl, _userClientCred, ClientId, RedirectUri, _cachedAuthorityName, _promptBehavior, false, TokenCachePath).ConfigureAwait(false).GetAwaiter().GetResult();
						}
					}
					else
                    {
						throw new NotSupportedException();
					}
					/* Not supported
					else if (uUserHomeRealm == null)
						orgs = ServiceClient.DiscoverOnPremiseOrganizationsAsync(uCrmUrl, null, userCred).ConfigureAwait(false).GetAwaiter().GetResult();
					else
						orgs = ServiceClient.DiscoverOnPremiseOrganizationsAsync(uCrmUrl, uUserHomeRealm, userClientCred, DeviceCredentials).ConfigureAwait(false).GetAwaiter().GetResult(); ;
					*/

					// Check the Result to see if we have Orgs back 
					if (discoverOrganizationsResult.OrganizationDetailCollection != null && discoverOrganizationsResult.OrganizationDetailCollection.Count > 0)
					{
						_tracer.Log($"Found {discoverOrganizationsResult.OrganizationDetailCollection.Count} Org(s)", TraceEventType.Information);
						if (discoverOrganizationsResult.OrganizationDetailCollection.Count == 1)
						{
							OrgByServer orgByServer = new OrgByServer() { DiscoveryServerName = "Premise", OrgDetail = discoverOrganizationsResult.OrganizationDetailCollection.First() };
							AddOrgToOrgList(discoverOrganizationsResult.OrganizationDetailCollection.First(), Resources.LOGIN_FRM_CRM_PREM_NAME, uCrmUrl);
							return ConnectAndInitOrgService(orgByServer, discoverOrganizationsResult.Account);
						}
						else
						{
							string userOrg = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg);
							if (!string.IsNullOrEmpty(userOrg))
							{
								_tracer.Log($"looking for Org = {userOrg} in the results from CRM's Discovery server list.", TraceEventType.Information);
								// Find the Stored org in the returned collection..
								OrganizationDetail orgDetail = Utilities.DeterminOrgDataFromOrgInfo(discoverOrganizationsResult.OrganizationDetailCollection, userOrg);

								if (orgDetail != null && !string.IsNullOrEmpty(orgDetail.UniqueName))
								{
									// Found it .. 
									_tracer.Log($"found User Org = {userOrg} in results", TraceEventType.Information);
									// Good Find, Clear org list.  
									ClearOrgList();
									OrgByServer orgByServer = new OrgByServer() { DiscoveryServerName = "Premise", OrgDetail = orgDetail };
									return ConnectAndInitOrgService(orgByServer, discoverOrganizationsResult.Account);
								}
							}

							// Didn't find it in the list.. ask the user for the org. 
							foreach (OrganizationDetail od in discoverOrganizationsResult.OrganizationDetailCollection)
							{
								AddOrgToOrgList(od, Resources.LOGIN_FRM_CRM_PREM_NAME, uCrmUrl);
							}

							return false;
						}
					}
					else
					{
						// No Orgs detected. 
						ClearOrgList();
						_tracer.Log("No Orgs Found", TraceEventType.Information);
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_PREM, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName)));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_PREM, false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_PREM, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName))));
						_tracer.Log(string.Format("No Orgs Found. Server Setting = {0}", StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName))
							, TraceEventType.Error);
						return false;

					}

					#endregion
				}
				else
				{
					#region OnLine / Live Code / OAuth
					// Generate Live ID Connection Info
					// GenerateDeviceCreds();

					var liveCreds = new ClientCredentials();
					if (UseDefaultCreds)
					{
						_tracer.Log("Utilizing Current user to attempt login", TraceEventType.Information);
						//Setting Use Default Credentials to false if this info is required further in code.
						//StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds, "False");
					}
					else
					{
						liveCreds.UserName.UserName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
						SecureString password = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
						if (password != null)
							liveCreds.UserName.Password = password.ToUnsecureString();
					}

					if (!(string.IsNullOrEmpty(ClientId) || RedirectUri == null))
					{
						//Switching to online when clientId & redirectUri are null/empty and Username and password are present, otherwise OAuth
						IsOAuth = true;
					}

					if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AdvancedCheck), out IsAdvancedCheckEnabled))
					{
						IsAdvancedCheckEnabled = false;
					}
					if (!IsAdvancedCheckEnabled)
					{
						//Setting Use Default Credentials to false if this info is required further in code.
						StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion, "");
						liveCreds.UserName.Password = string.Empty;
						liveCreds.UserName.UserName = string.Empty;
					}

					// THIS IS FOR CONNECTING TO AN ON-LINE SERVER 
					_bgWorker.ReportProgress(30, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNECT_TO_UII));

					if (_isSkipDiscovery)
					{
						string _baseWebApiUriFormat = @"{0}/api/data/v{1}/";
						string _baseSoapOrgUriFormat = @"{0}/XRMServices/2011/Organization.svc";

						EndpointCollection ep = new EndpointCollection();
						ep.Add(EndpointType.WebApplication, _directConnectUri);
						ep.Add(EndpointType.OrganizationDataService, string.Format(_baseWebApiUriFormat, _directConnectUri, "8.0"));
						ep.Add(EndpointType.OrganizationService, string.Format(_baseSoapOrgUriFormat, _directConnectUri));

						OrganizationDetail d = new OrganizationDetail();
						d.FriendlyName = "DIRECTSET";
						d.OrganizationId = Guid.Empty;
						d.OrganizationVersion = "0.0.0.0";
						d.State = OrganizationState.Enabled;
						d.UniqueName = "HOLD";
						d.UrlName = "HOLD";
						System.Reflection.PropertyInfo proInfo = d.GetType().GetProperty("Endpoints");
						if (proInfo != null)
						{
							proInfo.SetValue(d, ep, null);
						}

						selectedOrg = new OrgByServer();
						selectedOrg.OrgDetail = d;

						return ConnectAndInitOrgService(selectedOrg);
					}

					// This path Attempts to login in the user
					if (selectedOrg != null)
					{
						return ConnectAndInitOrgService(selectedOrg);
					}

					var discoverResult = FindOnlineDiscoveryServer(liveCreds);

					if (_orgListView.OrgsList != null && _orgListView.OrgsList.Count > 0)
					{
						_tracer.Log($"Found {_orgListView.OrgsList.Count} Org(s)", TraceEventType.Information);
						if (_orgListView.OrgsList.Count == 1)
						{
							bool success = ConnectAndInitOrgService(_orgListView.OrgsList.First(), discoverResult.Account);
							if (success)
								StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion,
									OnlineDiscoveryServerList.GetServerShortNameByDisplayName(_orgListView.OrgsList.First().DiscoveryServerName, _deploymentType == CrmDeploymentType.O365));
#if DEBUG
							TestingHelper.Instance.SelectedOption = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion);
#endif
							return success;
						}
						else
						{
							string userOrg = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg);
							if (!string.IsNullOrEmpty(userOrg))
							{
								_tracer.Log($"looking for Org = {userOrg} in the results from CRM's Discovery server list.", TraceEventType.Information);
								// Find the Stored org in the returned collection..
								var orgDetail = CrmOrgList.DeterminOrgDataFromOrgInfo(_orgListView, userOrg);

								if (orgDetail != null && !string.IsNullOrEmpty(orgDetail.OrgDetail.UniqueName))
								{
									// Found it .. 
									_tracer.Log($"found User Org = {userOrg} in results", TraceEventType.Information);
									bool success = ConnectAndInitOrgService(orgDetail, discoverResult.Account);
									if (success)
									{
										StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion,
											OnlineDiscoveryServerList.GetServerShortNameByDisplayName(orgDetail.DiscoveryServerName, _deploymentType == CrmDeploymentType.O365));
#if DEBUG
										TestingHelper.Instance.SelectedOption = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion);
#endif
										// Good connect.. clear out the org list. 
										ClearOrgList();
									}

									return success;

								}
							}
							return false;
						}
					}
					else
					{
						// Error here. 
						_tracer.Log("No Orgs Found", TraceEventType.Information);

						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_ONLINE, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion)));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_ONLINE, false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_NO_ORGS_FOUND_ONLINE, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion))));
						_tracer.Log(string.Format("No Orgs Found, Searched Online. Region Setting = {0}", StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion))
							, TraceEventType.Error);
						return false;
					}
					#endregion
				}
			}

			#region Login / Discovery Server Exception handlers

			catch (MessageSecurityException ex)
			{
				// Login to Live Failed. 
				ErrorLogger.WriteToFile(ex);
				_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_MSG_INVALID_LOGIN_DETAILS, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), false, ex.InnerException == null ? ex : ex.InnerException)));
				_tracer.Log(string.Format(Messages.CRMCONNECT_MSG_INVALID_LOGIN_DETAILS, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), false, ex), TraceEventType.Error, ex);
				return false;

			}
			catch (SoapException exp)
			{
				// Log Error to file. 
				ErrorLogger.WriteToFile(exp);
				_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), false, exp.InnerException == null ? exp : exp.InnerException)));
				_tracer.Log(string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), false, exp), TraceEventType.Error, exp);
				return false;
			}
			catch (WebException webEx)
			{
				// Log Error to file. 
				ErrorLogger.WriteToFile(webEx);
				// Check the result for Errors.
				if (!string.IsNullOrEmpty(webEx.Message) && webEx.Message.Contains("HTTP status 401"))
				{
					// Login Error. 
					Debug.WriteLine(string.Format("Exception in Login handler : {0} \n{1}", webEx.Message, webEx.InnerException != null ? webEx.InnerException.StackTrace : string.Empty));
					_bgWorker.ReportProgress(100,
						new ServerConnectStatusEventArgs(
							string.Format(Messages.CRMCONNECT_MSG_INVALID_LOGIN_DETAILS_PARAMS, webEx.Message).Replace("\\n", Environment.NewLine), false, webEx.InnerException != null ? webEx.InnerException : webEx));
					_tracer.Log(string.Format(Messages.CRMCONNECT_MSG_INVALID_LOGIN_DETAILS_PARAMS, webEx.Message).Replace("\\n", Environment.NewLine),
						TraceEventType.Error, webEx);
					return false;
				}
				else
				{
					_bgWorker.ReportProgress(100,
						new ServerConnectStatusEventArgs(
							string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg)), false, webEx.InnerException != null ? webEx.InnerException : webEx));
					_tracer.Log(string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg)),
						TraceEventType.Error, webEx);
					return false;
				}
			}
			catch (InvalidOperationException ex)
			{
				ErrorLogger.WriteToFile(ex);
				_bgWorker.ReportProgress(100,
					new ServerConnectStatusEventArgs(
						string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg)), false, ex.InnerException != null ? ex.InnerException : ex));
				_tracer.Log(
					string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg)),
					TraceEventType.Error, ex);
				return false;
			}
			catch (Exception ex)
			{
				ErrorLogger.WriteToFile(ex);

				var crmDeploymentType = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType);
				string message;
				if (crmDeploymentType != null && crmDeploymentType.Equals(CrmDeploymentType.Prem.ToString(), StringComparison.OrdinalIgnoreCase))
					message = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg);
				else
					message = crmDeploymentType;
				_bgWorker.ReportProgress(100,
					new ServerConnectStatusEventArgs(
						string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, message), false, ex.InnerException != null ? ex.InnerException : ex));
				_tracer.Log(string.Format(Messages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg)),
					   TraceEventType.Error, ex);
				return false;
			}
			#endregion
		}

		/// <summary>
		/// Check for an Override OAuth Settings of the ForceOSDP flag is present in the supporting applications app.config file. 
		/// </summary>
		internal void ResetOAuthIfOSDPOverrideSwitch()
		{
			if (ConfigurationManager.AppSettings != null)
				if (ConfigurationManager.AppSettings["ForceOSDP"] != null && ConfigurationManager.AppSettings["ForceOSDP"].Equals("true", StringComparison.OrdinalIgnoreCase))
				{
					// invalidate the current client ID and redirect URI settings. 
					ClientId = string.Empty;
					RedirectUri = null;
				};
		}

		/// <summary>
		/// Clears the Found Organizations List
		/// </summary>
		private void ClearOrgList()
		{
			// Clear out existing Orgs. 
			if (ParentControl != null)
				// try to clear it on the parent control. 
				ParentControl.Dispatcher.Invoke(DispatcherPriority.Normal,
							new Action(_orgListView.OrgsList.Clear)
								  );
			else
			{
				try
				{
					_orgListView.OrgsList.Clear();
				}
				catch (Exception ex)
				{
					// this will most likely occur if the OrgsList is attached to a different thread. 
					_tracer.Log(string.Format("Cannot clear the OrgsList object. Exception is = {0}", ex.Message), TraceEventType.Error);
				}
			}
		}


		/// <summary>
		/// Performs Validation on the authentication request, looking for problems that can be solved before the initial connect attempt  
		/// </summary>
		/// <returns>True if Ok, False if not</returns>
		private bool ValidateUserSpecifiedData()
		{
			// Check Login Type vs Required CRM Connection Information. 
			if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType)) &&
				StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType).Equals(CrmDeploymentType.Prem.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				// if the on Prem Bit is checked make sure there server name is there. 
				if (string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName)))
				{
					ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_SERVER, Dynamics_ConfigFileServerKeys.CrmServerName.ToString()));
					_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_SERVER_MSG, false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_SERVER)));
					_tracer.Log("You must specify a CRM Server to connect too", TraceEventType.Information);
					return false;
				}
			}

			// Check Login flag vs Required Information based on connection Type. 
			if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds)) &&
						StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds).Equals("false", StringComparison.CurrentCultureIgnoreCase))
			{
				bool IsClientIdOrRedirectUriEmpty = string.IsNullOrEmpty(ClientId) || RedirectUri == null;
				bool IsUserNameNull = string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName));
				var passwordSecureString = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
				bool IsPasswordNull = (passwordSecureString == null) || (passwordSecureString.Length == 0);
				if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AdvancedCheck), out IsAdvancedCheckEnabled))
				{
					IsAdvancedCheckEnabled = false;
				}

				if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm)) && 
						StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_OAUTH))
				{
					// Use Default is not checked and Auth type is OAuth.		
					if (IsUserNameNull && !IsPasswordNull)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID, Dynamics_ConfigFileServerKeys.CrmUserName.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID)));
						_tracer.Log("You must specify both User Name and Password or both are required to be null", TraceEventType.Information);
						return false;
					}
					else if (!IsUserNameNull && IsPasswordNull)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD)));
						_tracer.Log("You must specify both User Name and Password or both are required to be null", TraceEventType.Information);
						return false;
					}

					if (IsClientIdOrRedirectUriEmpty)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID, Dynamics_ConfigFileServerKeys.CrmUserName.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID)));
						_tracer.Log("You must specify both User Name and Password", TraceEventType.Information);
						return false;
					}
				}
				else if(!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType)) &&
								StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType).Equals(CrmDeploymentType.O365.ToString(), StringComparison.OrdinalIgnoreCase) &&
								IsAdvancedCheckEnabled)
				{
					// Use Default is not checked and Auth type is OAuth.		
					if (IsUserNameNull && !IsPasswordNull)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID, Dynamics_ConfigFileServerKeys.CrmUserName.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID)));
						_tracer.Log("You must specify both User Name and Password or both are required to be null", TraceEventType.Information);
						return false;
					}
					else if (!IsUserNameNull && IsPasswordNull)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD)));
						_tracer.Log("You must specify both User Name and Password or both are required to be null", TraceEventType.Information);
						return false;
					}

					if (IsClientIdOrRedirectUriEmpty && IsUserNameNull && IsPasswordNull)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID, Dynamics_ConfigFileServerKeys.CrmUserName.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID)));
						_tracer.Log("You must specify both User Name and Password", TraceEventType.Information);
						return false;
					}
				}
				else if((!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType)) &&
								StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType).Equals(CrmDeploymentType.O365.ToString(), StringComparison.OrdinalIgnoreCase)) &&
								!IsAdvancedCheckEnabled)
				{
					if (IsClientIdOrRedirectUriEmpty)
					{
						//need to add a constant for a new error message, get it scrubbed and localise it.
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD)));
						_tracer.Log("You must specify both clientId and RedirectUri", TraceEventType.Information);
						return false;
					}
				}
				else
				{
					// Use Default is not checked.. 
					// if the on useDefualt Creds is not checked make sure the user name is there. 
					if (string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName)))
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID, Dynamics_ConfigFileServerKeys.CrmUserName.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_USERID)));
						_tracer.Log("You must specify a User Name", TraceEventType.Information);
						return false;
					}

					if (StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword) == null)
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD_MSG,
									false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_PASSWORD)));
						_tracer.Log("You must specify a Password", TraceEventType.Information);
						return false;
					}
				}
			}
			else
			{
				// Use Default is checked. 
				// if Auth Type is not AD,IFD or OAuth Then Alert the user. 
				if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm)))
				{
					if (!(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_AD) ||
						 StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_IFD) ||
						 StorageUtils.GetConfigKey<string>(ServerConfigKeys,Dynamics_ConfigFileServerKeys.AuthHomeRealm).Equals(Resources.LOGIN_FRM_AUTHTYPE_OAUTH)))
					{
						ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_LOGINTYPE, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
						_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_LOGINTYPE_INCOMPATIBLE_LOGIN_TYPE, false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_LOGINTYPE)));
						_tracer.Log("Invalid Login Type and Credentials", TraceEventType.Information);
						return false;
					}
				}
				else
				{
					// No Auth type specified.. 
					ErrorLogger.WriteToFile(new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_AUTHTYPE, Dynamics_ConfigFileServerKeys.CrmPassword.ToString()));
					_bgWorker.ReportProgress(100, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_AUTHTYPE_MSG, false, new System.ArgumentException(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_AUTHTYPE)));
					_tracer.Log("Invalid Auth Type and Credentials", TraceEventType.Information);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Adds an Org to the List of Orgs
		/// </summary>
		/// <param name="organizationDetail"></param>
		/// <param name="discoveryServer"></param>
		/// <param name="discoveryServerUri"></param>
		private void AddOrgToOrgList(OrganizationDetail organizationDetail, string discoveryServer, Uri discoveryServerUri)
		{
			_tracer.Log("AddOrgToOrgList()", TraceEventType.Start);
			// Not in the UI Thread. 
			if (_orgListView == null) _orgListView = new CrmOrgList();
			if (_orgListView.OrgsList == null) _orgListView.OrgsList = new ObservableCollection<OrgByServer>();

			if (ParentControl != null)
				ParentControl.Dispatcher.Invoke((Action)(() =>
				{
					_orgListView.OrgsList.Add(new OrgByServer()
					{
						DiscoveryServerName = discoveryServer,
						OrgDetail = organizationDetail
					});
				}), DispatcherPriority.Send);
			else
				_orgListView.OrgsList.Add(new OrgByServer()
				{
					DiscoveryServerName = discoveryServer,
					OrgDetail = organizationDetail
				});

			_tracer.Log("AddOrgToOrgList()", TraceEventType.Stop);
		}

		/// <summary>
		/// Adds an Org to the List of Orgs
		/// </summary>
		/// <param name="organizationDetailList"></param>
		/// <param name="discoveryServer"></param>
		/// <param name="discoveryServerUri"></param>
		private void AddOrgToOrgList(OrganizationDetailCollection organizationDetailList, string discoveryServer, Uri discoveryServerUri)
		{
			foreach (OrganizationDetail o in organizationDetailList)
			{
				AddOrgToOrgList(o, discoveryServer, discoveryServerUri);
			}
		}

		/// <summary>
		/// Iterates through the list of CRM online Discovery Servers to find one that knows the user. 
		/// </summary>
		/// <param name="liveCreds"></param>
		private DiscoverOrganizationsResult FindOnlineDiscoveryServer(ClientCredentials liveCreds)
        {
            _tracer.Log("FindCrmOnlineDiscoveryServer()", TraceEventType.Start);
            if (IsOAuth == true)
            {
                //Reading authority and userId from Config file
                if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.Authority)))
                    _cachedAuthorityName = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.Authority);

                if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UserId)))
                    _cachedUserId = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UserId);
            }

            DiscoverOrganizationsResult discoverResult = null;
            // If the user as specified a server to use, try to get the org from that server. 
            if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion)))
            {
                _tracer.Log("Using User Specified Server ", TraceEventType.Information);
                // Server specified... 
                OnlineDiscoveryServer svr = OnlineDiscoveryServerList.GetServerByShortName(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion), _deploymentType == CrmDeploymentType.O365);
                if (svr != null)
                {
                    if (IsOAuth == true && svr.RequiresRegionalDiscovery)
                    {
                        if (svr.RegionalGlobalDiscoveryServer == null)
                        {
                            _tracer.Log(string.Format("Trying Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);
                            _bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_PROCESS_GET_ORGS_LIVE, svr.DisplayName)));
                            discoverResult = QueryOAuthDiscoveryServer(svr.DiscoveryServer, liveCreds, UserId, ClientId, RedirectUri, _promptBehavior, TokenCachePath);
                        }
                        else
                        {
                            // using regional version of a GD. 
                            _tracer.Log(string.Format(CultureInfo.InvariantCulture, "Trying Regional Global Discovery Server, ({1}) URI is = {0}", svr.RegionalGlobalDiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);
                            _bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_PROCESS_GET_ORGS_LIVE, svr.DisplayName)));
                            return QueryOnlineServerList(OnlineDiscoveryServerList.OSDPServers, liveCreds, trimToDiscoveryUri: svr.DiscoveryServer, globalDiscoUriToUse: svr.RegionalGlobalDiscoveryServer); // using regional disco server. 
                        }
                    }
                    else
                    {
                        if (IsOAuth)
                        {
                            return QueryOnlineServerList(OnlineDiscoveryServerList.OSDPServers, liveCreds, trimToDiscoveryUri: svr.DiscoveryServer);
                        }
                    }
                    if (discoverResult.OrganizationDetailCollection != null)
                        AddOrgToOrgList(discoverResult.OrganizationDetailCollection, svr.DisplayName, svr.DiscoveryServer);

                    return discoverResult;
                }

            }

            // Server is unspecified or the user chose ‘don’t know’
            if (_deploymentType == CrmDeploymentType.Online)
                discoverResult = QueryOnlineServerList(OnlineDiscoveryServerList.Servers, liveCreds);
            else
                discoverResult = QueryOnlineServerList(OnlineDiscoveryServerList.OSDPServers, liveCreds);

            _tracer.Log("FindCrmOnlineDiscoveryServer()", TraceEventType.Stop);
            return discoverResult;
        }

        /// <summary>
        /// Iterate over each discovery server in the collection
        /// </summary>
        /// <param name="svrs">Collection of discovery servers</param>
        /// <param name="liveCreds">Credential object</param>
        /// <param name="trimToDiscoveryUri">Forces the results to be trimed to this region when present</param>
        /// <param name="globalDiscoUriToUse">Overriding Global Discovery URI</param>
        private DiscoverOrganizationsResult QueryOnlineServerList(ObservableCollection<OnlineDiscoveryServer> svrs, ClientCredentials liveCreds, Uri trimToDiscoveryUri = null, Uri globalDiscoUriToUse = null)
		{
			DiscoverOrganizationsResult discoverResult = null;
			// Execute Global Discovery
			if (IsOAuth == true) 
			{
				Uri globalDisoSvcUri = globalDiscoUriToUse != null ? new Uri(string.Format("{0}api/discovery/v{1}/{2}", globalDiscoUriToUse.ToString(), _globlaDiscoVersion, "Instances")) : new Uri(GlobalDiscoveryAllInstancesUri);
				_tracer.Log(string.Format("Trying Global Discovery Server, ({1}) URI is = {0}", globalDisoSvcUri.ToString(), "Global Discovery"), TraceEventType.Information);
				_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_PROCESS_GET_ORGS_LIVE, "Global Discovery")));

				try
				{
					discoverResult = QueryOAuthDiscoveryServer(globalDisoSvcUri, liveCreds, UserId, ClientId, RedirectUri, _promptBehavior, TokenCachePath, useGlobalDisco:true);
				}
				catch (MessageSecurityException)
				{
					_tracer.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", globalDisoSvcUri.ToString(), "Global Discovery"), TraceEventType.Warning);
					return null;
				}
				catch (Exception ex)
				{
					_tracer.Log($"Exception while trying to connect Discovery Server, (Global Discovery) URI is = {globalDisoSvcUri}. Exception message: {ex.Message}", TraceEventType.Error);
					throw;
				}

				// if we have results.. add them to the AddOrgToOrgList object. ( need to iterate over the objects to match region to result. ) 

				if (discoverResult.OrganizationDetailCollection != null)
				{
					bool isOnPrem = false; 
					foreach (var itm in discoverResult.OrganizationDetailCollection)
					{
						var orgObj = Utilities.DeterminDiscoveryDataFromOrgDetail(new Uri(itm.Endpoints[EndpointType.OrganizationService]), out isOnPrem, Geo: itm.Geo);
						if (trimToDiscoveryUri != null && !trimToDiscoveryUri.Equals(orgObj.DiscoveryServerUri))
							continue;
						AddOrgToOrgList(itm, orgObj.DisplayName, orgObj.DiscoveryServerUri);
					}
				}
			}
			else
            {
                foreach (var svr in svrs)
                {
                    // Covers the "dont know" setting.
                    if (svr.DiscoveryServer == null) continue;

                    _tracer.Log(string.Format("Trying Live Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Information);

                    _bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(string.Format(Messages.CRMCONNECT_LOGIN_PROCESS_GET_ORGS_LIVE, svr.DisplayName)));

                    try
                    {
                        if (IsOAuth == true)
                        {
                            discoverResult = QueryOAuthDiscoveryServer(svr.DiscoveryServer, liveCreds, UserId, ClientId, RedirectUri, _promptBehavior, TokenCachePath);
                        }
                    }
                    catch (MessageSecurityException)
                    {
                        _tracer.Log(string.Format("MessageSecurityException while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Warning);
                        return null;
                    }
                    catch (Exception)
                    {
                        _tracer.Log(string.Format("Exception while trying to connect Discovery Server, ({1}) URI is = {0}", svr.DiscoveryServer.ToString(), svr.DisplayName), TraceEventType.Error);
                        return null;
                    }
                    if (discoverResult.OrganizationDetailCollection != null)
                        AddOrgToOrgList(discoverResult.OrganizationDetailCollection, svr.DisplayName, svr.DiscoveryServer);
                }
            }

            return discoverResult;
		}

		/// <summary>
		/// Query an individual OAuth server
		/// </summary>
		/// <param name="discoServer">Discovery Service Uri</param>
		/// <param name="liveCreds">Credentials supplied for login</param>
		/// <param name="user">User identifier</param>
		/// <param name="clientId">Registered Client Id of application trying for OAuth</param>
		/// <param name="redirectUri">Uri to redirect the application</param>
		/// <param name="promptBehavior">Prompt behavior defining ADAL login popup</param>
		/// <param name="tokenCachePath">Token cache path supplied by user for storing bearer tokens</param>
		/// <param name="useGlobalDisco">if true, calls global discovery path</param>
		/// <returns></returns>
		private DiscoverOrganizationsResult QueryOAuthDiscoveryServer(Uri discoServer, ClientCredentials liveCreds, UserIdentifier user, string clientId, Uri redirectUri, Client.Auth.PromptBehavior promptBehavior, string tokenCachePath, bool useGlobalDisco = false)
		{
			_tracer.Log($"{nameof(QueryOAuthDiscoveryServer)}", TraceEventType.Start);

			DiscoverOrganizationsResult result = null;
			try
			{
				if (ForceFirstOAuthPrompt)
				{
					ForceFirstOAuthPrompt = false;
					_cachedAuthorityName = null;
					_cachedUserId = null;
					promptBehavior = Client.Auth.PromptBehavior.Always;

					if (UseDefaultCreds)
						promptBehavior = Client.Auth.PromptBehavior.SelectAccount;

					result = ServiceClient.DiscoverOnlineOrganizationsAsync(discoServer, liveCreds, clientId, redirectUri, false, _cachedAuthorityName, promptBehavior, UseDefaultCreds, tokenCachePath).ConfigureAwait(false).GetAwaiter().GetResult();
				}
				else
				{
					if (user == null && _cachedUserId != null)
						user = new UserIdentifier(_cachedUserId, UserIdentifierType.RequiredDisplayableId);
					result = ServiceClient.DiscoverOnlineOrganizationsAsync(discoServer, liveCreds, clientId, redirectUri, false, _cachedAuthorityName, promptBehavior, tokenCacheStorePath:tokenCachePath).ConfigureAwait(false).GetAwaiter().GetResult();
				}
				return result;
			}
			catch (SecurityAccessDeniedException securEx)
			{
				// User Does not have any orgs on this server. 
				_tracer.Log("User does not have access to this discovery server", TraceEventType.Warning, securEx);
            }
            _tracer.Log($"{nameof(QueryOAuthDiscoveryServer)}", TraceEventType.Stop);

			return result;
        }

		/// <summary>
		/// Connects too and inits the org Data service. 
		/// </summary>
		/// <param name="orgdata">Organization to use when connecting to CRM</param>
		/// <param name="account">account hint</param>
		private bool ConnectAndInitOrgService(OrgByServer orgdata, IAccount account = null)
		{
			_tracer.Log($"{nameof(ConnectAndInitOrgService)}", TraceEventType.Start);

			Uri oOrgSvc = BuildOrgConnectUri(orgdata.OrgDetail);
			_tracer.Log(string.Format("Organization Service URI is = {0}", oOrgSvc.ToString()), TraceEventType.Information);

			// Set the Org into system config
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg, orgdata.OrgDetail.UniqueName);

			// Build User Credential
			if (_deploymentType == CrmDeploymentType.Prem)
			{
				if (uUserHomeRealm != null)
				{
					// Claims server... 
					_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING));
					_tracer.Log(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, TraceEventType.Information);

					ServiceClient = new ServiceClient(_userClientCred.UserName.UserName, ServiceClient.MakeSecureString(_userClientCred.UserName.Password), 
						uUserHomeRealm.ToString(), oOrgSvc.Host, oOrgSvc.Port.ToString(), orgdata.OrgDetail.UniqueName, IsSSLReq, useUniqueInstance: true, orgDetail: orgdata.OrgDetail, ClientId, RedirectUri);

					PopulateOrgProperties(orgdata);

					return ServiceClient.IsReady;
				}
				else if (IsOAuth == true)
				{
					_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING));
					_tracer.Log(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, TraceEventType.Information);

					ServiceClient = new ServiceClient(_userClientCred.UserName.UserName, ServiceClient.MakeSecureString(_userClientCred.UserName.Password), 
						string.Empty, oOrgSvc.Host, oOrgSvc.Port.ToString(),
						orgdata.OrgDetail.UniqueName, true, true, orgdata.OrgDetail, ClientId, RedirectUri, _promptBehavior, TokenCachePath);

					PopulateOrgProperties(orgdata);

					return ServiceClient.IsReady;
				}
				else
				{
					throw new NotSupportedException();
					/*
					// Create the CRM Service Connection.  Non-Claims
					bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING));
					tracer.Log(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, TraceEventType.Information);

					// Connect to CRM Directly
					CrmSvc = new ServiceClient(userCred, oOrgSvc.Host, oOrgSvc.Port.ToString(), orgdata.OrgDetail.UniqueName, useSsl: IsSSLReq, orgDetail: orgdata.OrgDetail, useUniqueInstance: true);

					PopulateOrgProperties(orgdata);
					return CrmSvc.IsReady;
					*/
				}
			}
			else
			{
				//if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds), out UseDefaultCreds))
				//{
				//	UseDefaultCreds = false;
				//}
				// Connecting to Online via Live. 
				//if (UseDefaultCreds)
				//{
				//	// Error here .. Cannot use Default with Online. 
				//	bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_LOGINTYPE_INCOMPATIBLE_LOGIN_TYPE));
				//	tracer.Log(Messages.CRMCONNECT_LOGIN_VALIDATION_ERR_LOGINTYPE_INCOMPATIBLE_LOGIN_TYPE, TraceEventType.Error);
				//	PopulateOrgProperties(orgdata);
				//	return false;
				//}
				//else 
				if (IsOAuth == true)
				{
					_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING));
					_tracer.Log(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, TraceEventType.Information);

					string toSendUserId = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
					SecureString toSendSecurePW = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);

					// if the useDefaultCred is checked, and IsAdvanced is Checked.   OR UseDefaultCreds is not checked and IsAdvanceds is not checked -
					//  Dont send UID / PW/ 
					if ((UseDefaultCreds && IsAdvancedCheckEnabled) || (!UseDefaultCreds && !IsAdvancedCheckEnabled))
					{
						// Pass account as a hint
						toSendUserId = account != null ? account.Username : string.Empty ;
						toSendSecurePW = null;
					}

					ServiceClient = new ServiceClient(
							toSendUserId,
							toSendSecurePW,
							StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion),
							orgdata.OrgDetail.UniqueName, true, orgdata.OrgDetail, ClientId, RedirectUri, _promptBehavior, useDefaultCreds: UseDefaultCreds, TokenCachePath);
					PopulateOrgProperties(orgdata);
					return ServiceClient.IsReady;
				}
				else
				{
					_bgWorker.ReportProgress(25, new ServerConnectStatusEventArgs(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING));
					_tracer.Log(Messages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, TraceEventType.Information);

					if (_deploymentType == CrmDeploymentType.O365)
					{
						// Connect via o365 
						ServiceClient = new ServiceClient(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName),
									StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword),
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion),
									orgdata.OrgDetail.UniqueName, useUniqueInstance: true, orgDetail: orgdata.OrgDetail, ClientId, RedirectUri);
						if (ServiceClient.IsReady)
						{
							// Good connect Set Online Info for later recovery. 
							try
							{
								StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion,
									OnlineDiscoveryServerList.GetServerShortNameByDisplayName(orgdata.DiscoveryServerName, true));
							}
							catch { } // Catch unknown errors here. 
						}
					}
					else
					{
						// Connect via Live
						ServiceClient = new ServiceClient(
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName),
									StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword),
									StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion),
									orgdata.OrgDetail.UniqueName, useUniqueInstance: true, orgDetail: orgdata.OrgDetail, ClientId, RedirectUri);
						if (ServiceClient.IsReady)
						{
							// Good connect Set Online Info for later recovery. 
							try
							{
								StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion,
									OnlineDiscoveryServerList.GetServerShortNameByDisplayName(orgdata.DiscoveryServerName));
							}
							catch { } // Catch unknown errors here. 
						}
					}
					PopulateOrgProperties(orgdata);
					return ServiceClient.IsReady;
				}
			}
		}

		/// <summary>
		/// Populates the local properties of the connector.
		/// </summary>
		/// <param name="orgdata"></param>
		private void PopulateOrgProperties(OrgByServer orgdata)
		{
			// Set Org name. 
			ConnectedOrgFriendlyName = ServiceClient != null && ServiceClient.IsReady && !string.IsNullOrWhiteSpace(ServiceClient.ConnectedOrgFriendlyName) ? ServiceClient.ConnectedOrgFriendlyName : orgdata.FriendlyName;
			ConnectedOrgUniqueName = ServiceClient != null && ServiceClient.IsReady &&  !string.IsNullOrWhiteSpace(ServiceClient.ConnectedOrgUniqueName) ? ServiceClient.ConnectedOrgUniqueName : orgdata.OrgDetail.UniqueName;
			ConnectedOrgPublishedEndpoints = ServiceClient != null && ServiceClient.IsReady && ServiceClient.ConnectedOrgPublishedEndpoints != null ? ServiceClient.ConnectedOrgPublishedEndpoints : orgdata.OrgDetail.Endpoints; 
			ConnectedOrgId = ServiceClient != null && ServiceClient.IsReady && ServiceClient.ConnectedOrgId != Guid.Empty? ServiceClient.ConnectedOrgId : orgdata.OrgDetail.OrganizationId;  
		}

		/// <summary>
		/// Builds the Organization Service Connect URI
		/// - This is done, potentially replacing the original string, to deal with the discovery service returning an unusable string, for example, a DNS name that does not resolve. 
		/// </summary>
		/// <param name="orgdata">Org Data found from the Discovery Service.</param>
		/// <returns>CRM Connection URI</returns>
		private Uri BuildOrgConnectUri(OrganizationDetail orgdata)
		{
			_tracer.Log("BuildOrgConnectUri()", TraceEventType.Start);
			// determine if SSL is required to connect. 
			bool IsSSLReq = false;
			if (!bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUseSSL), out IsSSLReq))
				IsSSLReq = false;

			// Build connection URL  
			string CrmUrl = string.Empty;
			Uri OrgEndPoint = new Uri(orgdata.Endpoints[EndpointType.OrganizationService]);
			_tracer.Log("DiscoveryServer indicated organization service location = " + OrgEndPoint.ToString(), TraceEventType.Verbose);
#if DEBUG
			if (TestingHelper.Instance.IsDebugEnvSelected())
			{
				return OrgEndPoint;
			}
#endif
			if (Utilities.IsValidOnlineHost(OrgEndPoint))
			{
				// CRM Online ..> USE PROVIDED URI. 
				_tracer.Log("BuildOrgConnectUri()", TraceEventType.Stop);
				return OrgEndPoint;
			}
			else
			{
				if (_deploymentType == CrmDeploymentType.O365)
				{
					return OrgEndPoint; // O365 returns direct org end point. 
				}

				// Need to come up with a way to support a Redirect from the Discovery Server,,,  IE: the discovery server is in one location, the server in another. 
				// and continue to support the concept of a rewrite of the URL.
				//  perhaps try to resolve the URL the discovery server returns? 
				//      On Fail,  do the URL Rewrite. 

				string _InternetProtocalToUse = IsSSLReq ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
				if (!OrgEndPoint.Scheme.Equals(_InternetProtocalToUse, StringComparison.OrdinalIgnoreCase))
				{
					_tracer.Log("Organization Services is using a different URI Scheme then requested,  switching to Discovery server specified scheme = " + OrgEndPoint.Scheme, TraceEventType.Stop);
					_InternetProtocalToUse = OrgEndPoint.Scheme;
				}

				if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort)))
				{
					CrmUrl = String.Format(CultureInfo.InvariantCulture,
						"{0}://{1}:{2}{3}",
						_InternetProtocalToUse,
						StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName),
						StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort),
						OrgEndPoint.PathAndQuery
						);
				}
				else
				{
					CrmUrl = String.Format(CultureInfo.InvariantCulture,
						"{0}://{1}{2}",
						_InternetProtocalToUse,
						StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName),
						OrgEndPoint.PathAndQuery);
				}
			}

			_tracer.Log("BuildOrgConnectUri()", TraceEventType.Stop);
			return new Uri(CrmUrl);
		}




		#region ConfigFile Commands

		/// <summary>
		/// Will remove the user connection data settings from the users app config directory,  useable only when the UseUserLocalDirectoryForConfigStore is set to true. 
		/// this should be called after the connection has been established, or before the connect attempt in order to be effective. 
		/// </summary>
		/// <returns>true on success, false on fail.</returns>
		public bool RemoveUserLocalDirectoryConfigFile()
		{
			try
			{
				// 
				if (UseUserLocalDirectoryForConfigStore)
				{
					// User Specified to use the users app config directory to store the user connect info. 
					string sPotentialPath = string.Empty;
					if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
						sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
									AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", ""));
					else
						sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
									HostApplicatioNameOveride.Replace(".vshost", "").Replace(".exe", ""));

					string ConfigPath = string.Empty;
					if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
						ConfigPath = Path.Combine(sPotentialPath, AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", ""));
					else
						ConfigPath = Path.Combine(sPotentialPath, HostApplicatioNameOveride.Replace(".vshost", ""));

					if (File.Exists(ConfigPath))
					{
						File.Delete(ConfigPath);
						return true;
					}
				}
				else
				{
					_tracer.Log("Cannot call RemoveUserLocalDirectoryConfigFile when UseUserLocalDirectoryForConfigStore is false", TraceEventType.Warning);
					return false;
				}
			}
			catch (Exception ex)
			{
				_tracer.Log("Exception raised in RemoveUserLocalDirectoryConfigFile", TraceEventType.Error, ex);
			}
			return false;
		}

		/// <summary>
		/// Loads the Configuration key's from file b/c some things are missing
		/// </summary>
		public Dictionary<Dynamics_ConfigFileServerKeys, object> LoadConfigFromFile(bool readLocalFirst = false)
		{
			_tracer.Log("LoadConfigFromFile()", TraceEventType.Start);
			if (ServerConfigKeys != null)
				ServerConfigKeys.Clear();
			else
				ServerConfigKeys = new Dictionary<Dynamics_ConfigFileServerKeys, object>();
			try
			{
				// Get a handle to the Configuration
				string ConfigPath = string.Empty;
				bool iCreatedFile = false;
				if (UseUserLocalDirectoryForConfigStore && !readLocalFirst)
				{
					// User Specified to use the users app config directory to store the user connect info. 
					string sPotentialPath = string.Empty;
					if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
						sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
									AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", ""));
					else
						sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
									HostApplicatioNameOveride.Replace(".vshost", "").Replace(".exe", ""));

					if (!Directory.Exists(sPotentialPath))
					{
						Directory.CreateDirectory(sPotentialPath);
					}

					// Set logging directory for errors. 
					ErrorLogger.LogfileDirectoryOverride = sPotentialPath;

					if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
					{
						ConfigPath = Path.Combine(sPotentialPath, string.Format("{0}_{1}", ProfileName, AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Trim()));
						TokenCachePath = Path.Combine(sPotentialPath, string.Format("{0}_{1}.{2}", ProfileName, AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", "").Trim(), "tokens.dat"));
					}
					else
					{
						ConfigPath = Path.Combine(sPotentialPath, string.Format("{0}_{1}", ProfileName, HostApplicatioNameOveride.Replace(".vshost", "").Trim()));
						TokenCachePath = Path.Combine(sPotentialPath, string.Format("{0}_{1}.{2}", ProfileName, HostApplicatioNameOveride.Replace(".vshost", "").Replace(".exe", "").Trim(), "tokens.dat"));
					}
				}
				else
				{
					ConfigPath = Environment.CommandLine.Replace("\"", "").Replace(".vshost", "").Trim();
					// when local directory store is not used, application executable path shall be used to store the token cache. Inline with the config file 
					TokenCachePath = string.Format("{0}.{1}", Environment.CommandLine.Replace("\"", "").Replace(".vshost", "").Replace(".exe", "").Trim(),"tokens.dat");
				}

				if (!File.Exists(ConfigPath) && !readLocalFirst)
				{
					// Need to create the file .. 
					// This is to allow the configuration reader to work right  ( because I didn't want to rewrite the load / save system for this use case yet. ) 
					using (StreamWriter cfgFileWr = File.CreateText(ConfigPath))
					{
						cfgFileWr.Flush();
					}

					if (UseUserLocalDirectoryForConfigStore)
					{
						try
						{
							// user is using a specified file directory... encrypt file to user using Machine / FS Locking. 
							// this will lock / prevent users other then the current user from accessing this file. 
							FileInfo fi = new FileInfo(ConfigPath);
							fi.Encrypt();
						}
						catch (IOException)
						{
							// This can happen when a certificate system on the host has failed. 
							// usually this can be fixed with the steps in this article : http://support.microsoft.com/kb/937536
							//tracer.Log("Failed to Encrypt Configuration File!", TraceEventType.Error, encrEX);
							//tracer.Log("This problem may be related to a domain certificate in windows being out of sync with the domain, please read http://support.microsoft.com/kb/937536");
						}
						catch (Exception)
						{
							//tracer.Log("Failed to Encrypt Configuration File!", TraceEventType.Error, genEX);
						}

					}
					iCreatedFile = true;
				}

				if (UseUserLocalDirectoryForConfigStore && !readLocalFirst)
				{
					// User Specified to use the users app config directory to store the user connect Info. 
					// Map Local file only if I created it. 
					if (iCreatedFile)
					{
						// Read the config settings right now.. 
						ServerConfigKeys = LoadConfigFromFile(true);
						if (ServerConfigKeys == null) // Deal with failure. 
							ServerConfigKeys = new Dictionary<Dynamics_ConfigFileServerKeys, object>();

						// Save the template. 
						if (ServerConfigKeys != null)
							SaveConfigToFile(ServerConfigKeys);
					}
				}


				if (File.Exists(ConfigPath))
				{
					Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigPath);
					if (config != null)
					{
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmOrg, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmServerName, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmPort, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmOnlineRegion, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.AuthHomeRealm, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmDeploymentType, CrmDeploymentType.O365.ToString(), overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmUseSSL, bool.FalseString, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.AdvancedCheck, bool.FalseString, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.Authority, overrideDefaultSet: readLocalFirst);
						SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.UserId, overrideDefaultSet: readLocalFirst);

						if (config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.UseDefaultCreds.ToString()] != null)
						{
							SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.UseDefaultCreds, overrideDefaultSet: readLocalFirst);

							if (!readLocalFirst)
							{
								if (StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds)
									.Equals("true", StringComparison.CurrentCultureIgnoreCase))
								{
									StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName, string.Empty);
									StorageUtils.SetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword, null);
									StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain, string.Empty);
								}
								else
								{
									// Read domain from disc
									SetServerConfigKey(config, Dynamics_ConfigFileServerKeys.CrmDomain, overrideDefaultSet: readLocalFirst);

									// Read from PW Vault
									// Build HostName
									StringBuilder credName = new StringBuilder();
									if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
										credName.Append(AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", ""));
									else
										credName.Append(HostApplicatioNameOveride.ToLowerInvariant().Replace(".vshost", "").Replace(".exe", ""));

									// Add ProfileName
									if (UseUserLocalDirectoryForConfigStore)
										credName.AppendFormat("_{0}", ProfileName);

									try
									{
										// Read Credentials from the Vault. 
										SavedCredentials creds = CredentialManager.ReadCredentials(credName.ToString());
										StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName, creds.UserName);
										StorageUtils.SetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword, creds.Password);
									}
									catch (Win32Exception)
									{
										// Failed to read exception
										// This is possible on a first read or where the vault has been cleared. logging set to Verbose so that it does not report an error in the logs under normal circumstances. 
										_tracer.Log("Failed to get credentials from Windows Vault", TraceEventType.Verbose);
										StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName, string.Empty);
									}
								}
							}
						}
						if (config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.CacheCredentials.ToString()] != null)
						{
							// Get a switch to determine if we are in a non user caching mode. 
							// if set, then the user password is not cached and the system is set to "not use" default credentials. 
							StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials,
								config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.CacheCredentials.ToString()].Value);
						}
						else
							StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials, true.ToString());

						if (!readLocalFirst)
						{

							// Handle the switch being set in configuration, outside of the UI. 
							bool cacheCreds = string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials)) ? true :
								StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials).Equals("true", StringComparison.CurrentCultureIgnoreCase);
							if (!cacheCreds)
							{
								// Do not cache...
								ForceFirstOAuthPrompt = true;
								StorageUtils.SetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword, null);
								StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg, string.Empty);  // Flush ORG so it can be reset. 
							}
						}

						// Get query user for Org choice Setting.. defaults to off. 
						if (config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.AskForOrg.ToString()] != null)
						{
							if (config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.CrmPassword.ToString()] != null)
								StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AskForOrg,
									config.AppSettings.Settings[Dynamics_ConfigFileServerKeys.AskForOrg.ToString()].Value);
						}
						else
							StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AskForOrg, false.ToString());

					}
				}
				_tracer.Log("LoadConfigFromFile()", TraceEventType.Stop);
				return ServerConfigKeys;
			}
			catch (Exception ex)
			{
				_tracer.Log("LoadConfigFromFile() - Except - fail", TraceEventType.Error, ex);
				_tracer.Log("LoadConfigFromFile() - Except - fail", TraceEventType.Stop);
				return null;
			}
		}

		/// <summary>
		/// Sets a key in the configuration
		/// </summary>
		/// <param name="config">Configuration File</param>
		/// <param name="key">Key to set</param>
		/// <param name="defaultValue">Default Value to set</param>
		/// <param name="overrideDefaultSet">If true, overrides default value logic.</param>
		private void SetServerConfigKey(Configuration config, Dynamics_ConfigFileServerKeys key, string defaultValue = null, bool overrideDefaultSet = false)
		{
			if (config.AppSettings.Settings[key.ToString()] != null)
			{
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, key,
					config.AppSettings.Settings[key.ToString()].Value);
			}
			else
				if (!string.IsNullOrEmpty(defaultValue))
					StorageUtils.SetConfigKey<string>(ServerConfigKeys, key, defaultValue);
		}

		/// <summary>
		/// Save the configuration Keys to the configuration file. 
		/// </summary>
		/// <param name="configToSave">Config key dictionary</param>
		/// <returns>true on success</returns>
		public bool SaveConfigToFile(Dictionary<Dynamics_ConfigFileServerKeys, object> configToSave)
		{
			_tracer.Log("SaveConfigToFile()", TraceEventType.Start);
			string ConfigPath = string.Empty;
			try
			{
				if (configToSave != null)
				{
					// Get a handle to the configuration 
					if (UseUserLocalDirectoryForConfigStore)
					{
						string sPotentialPath = string.Empty;
						if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
							sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
										AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", ""));
						else
							sPotentialPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft",
										HostApplicatioNameOveride.ToLowerInvariant().Replace(".vshost", "").Replace(".exe", ""));


						if (!Directory.Exists(sPotentialPath))
						{
							Directory.CreateDirectory(sPotentialPath);
						}

						// Set logging directory for errors. 
						ErrorLogger.LogfileDirectoryOverride = sPotentialPath;

						if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
							ConfigPath = Path.Combine(sPotentialPath, string.Format("{0}_{1}", ProfileName, AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "")));
						else
							ConfigPath = Path.Combine(sPotentialPath, string.Format("{0}_{1}", ProfileName, HostApplicatioNameOveride.Replace(".vshost", "")));
					}
					else
						ConfigPath = Environment.CommandLine.Replace("\"", "").Replace(".vshost", "");


					if (File.Exists(ConfigPath))
					{
						Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigPath);
						if (config != null)
						{
							// Clear the configuration data out.. 
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmDeploymentType.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmUseSSL.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmOrg.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmPort.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmServerName.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.UseDefaultCreds.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmUserName.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmPassword.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmDomain.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CacheCredentials.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.CrmOnlineRegion.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.AuthHomeRealm.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.AskForOrg.ToString());
							config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.AdvancedCheck.ToString());

							// Create new data. 
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmDeploymentType.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmDeploymentType));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmUseSSL.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmUseSSL));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmOrg.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmOrg));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmPort.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmPort));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmServerName.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmServerName));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.UseDefaultCreds.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.UseDefaultCreds));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CacheCredentials.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CacheCredentials));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmOnlineRegion.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmOnlineRegion));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.AuthHomeRealm.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.AuthHomeRealm));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.AskForOrg.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.AskForOrg));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.CrmDomain.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmDomain));
							config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.AdvancedCheck.ToString(), StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.AdvancedCheck));

							if (ServiceClient != null && ServiceClient.ActiveAuthenticationType == AuthenticationType.OAuth)
							{
								//stroring userid in config file after successful login
								config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.UserId.ToString());
								config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.UserId.ToString(), ServiceClient.OAuthUserId);

								if (!string.IsNullOrWhiteSpace(ServiceClient.Authority))
								{
									//Storing authority in config file after validating the connection with OAuth
									config.AppSettings.Settings.Remove(Dynamics_ConfigFileServerKeys.Authority.ToString());
									config.AppSettings.Settings.Add(Dynamics_ConfigFileServerKeys.Authority.ToString(), ServiceClient.Authority);
								}
							}
							config.Save(ConfigurationSaveMode.Modified); // Save the changes. 
							ConfigurationManager.RefreshSection("appSettings"); // Force the configuration to Refresh the App Settings. 

							// Only Run UID/PW process if Not using default PW / user account
							if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.UseDefaultCreds)) &&
								!StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.UseDefaultCreds).Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
							{
								// Use Windows Value to store passwords and such. 
								// Build HostName
								StringBuilder credName = new StringBuilder();
								if (string.IsNullOrWhiteSpace(HostApplicatioNameOveride))
									credName.Append(AppDomain.CurrentDomain.FriendlyName.Replace(".vshost", "").Replace(".exe", ""));
								else
									credName.Append(HostApplicatioNameOveride.ToLowerInvariant().Replace(".vshost", "").Replace(".exe", ""));

								if (UseUserLocalDirectoryForConfigStore) // Add ProfileName if the UseUserLocalData is configured. 
									credName.AppendFormat("_{0}", ProfileName);

								string userName = StorageUtils.GetConfigKey<string>(configToSave, Dynamics_ConfigFileServerKeys.CrmUserName);
								SecureString password = StorageUtils.GetConfigKey<SecureString>(configToSave, Dynamics_ConfigFileServerKeys.CrmPassword);
								// Build credential array.
								//OAuth: Skipping this when user login without entering credentials in UX.
								if (!string.IsNullOrWhiteSpace(userName) && password != null)
								{
									SavedCredentials creds = new SavedCredentials(userName, password);
									// Write creds to Vault. 
									try
									{
										CredentialManager.WriteCredentials(credName.ToString(), creds, true);
									}
									catch (Win32Exception ex)
									{
										// Failed to write exception
										// This is possible if the windows vault is not present or the user does not have permissions to write to it. 
										_tracer.Log("Failed to write credentials to Windows Vault", TraceEventType.Verbose, ex);
									}
								}
								else
								{
									try
									{
										CredentialManager.DeleteCredentials(credName.ToString(), false);
									}
									catch (Win32Exception ex)
									{
										// Failed to write exception
										// This is possible if the windows vault is not present or the user does not have permissions to write to it. 
										_tracer.Log("Failed to remove unneeded credentials from Windows Vault", TraceEventType.Verbose, ex);
									}
								}
							}

							if (UseUserLocalDirectoryForConfigStore)
							{
								try
								{
									// Encrypt the config file. 
									FileInfo fi = new FileInfo(config.FilePath);
									fi.Encrypt();
								}
								catch (IOException)
								{
									// This can happen when a certificate system on the host has failed. 
									// usually this can be fixed with the steps in this article : http://support.microsoft.com/kb/937536
									//tracer.Log("Failed to Encrypt Configuration File!", TraceEventType.Error, encrEX);
									//tracer.Log("This problem may be related to a domain certificate in windows being out of sync with the domain, please read http://support.microsoft.com/kb/937536");
								}
								catch (Exception)
								{
									//tracer.Log("Failed to Encrypt Configuration File!", TraceEventType.Error, genEX);
								}
							}

							_tracer.Log("SaveConfigToFile()", TraceEventType.Stop);
							return true;
						}
					}
					else
					{
						_tracer.Log(string.Format("SaveConfigToFile() - fail - cannot find file {0}", ConfigPath), TraceEventType.Error);
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				_tracer.Log("SaveConfigToFile() - fail - " + ConfigPath,
					 TraceEventType.Error, ex);
			}
			_tracer.Log("SaveConfigToFile() - fail", TraceEventType.Stop);
			return false;
		}

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		/// <summary>
		/// Clean up 
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (_onlineDiscoveryServerList != null)
					{
						_onlineDiscoveryServerList.Dispose();
						_onlineDiscoveryServerList = null;
					}

					if (_tracer != null)
					{
						_tracer.Dispose();
						_tracer = null; 
					}

					//if (CrmSvc != null)
					//{
					//    CrmSvc.Dispose();
					//    CrmSvc = null; 
					//}
				}
				disposedValue = true;
			}
		}


		/// <summary>
		/// Clean up 
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion

		#endregion
	}

	/// <summary>
	/// Event Raised when the Server Validate Connection Command operates. 
	/// </summary>
	public class ServerConnectStatusEventArgs : EventArgs
	{
		/// <summary>
		/// Error Message from the originating source
		/// </summary>
		public string ErrorMessage { get; private set; }
		/// <summary>
		/// if true, connected to CRM, else not connected to CRM
		/// </summary>
		public bool Connected { get; private set; }
		/// <summary>
		/// Exception that goes with the error message
		/// </summary>
		public Exception exEvent { get; private set; }
		/// <summary>
		/// Text status message, usually communicating the current state of the login process.
		/// </summary>
		public string StatusMessage { get; set; }
		/// <summary>
		/// If true, there were multiple organizations found,  the user will need to select the correct org and re logon.
		/// </summary>
		public bool MultiOrgsFound { get; private set; }

		/// <summary></summary>
		public ServerConnectStatusEventArgs(string statusMsg)
		{
			StatusMessage = statusMsg;
		}

		/// <summary></summary>
		public ServerConnectStatusEventArgs(string errMsg, bool connected)
		{
			ErrorMessage = errMsg;
			Connected = connected;
			exEvent = null;
		}

		/// <summary></summary>
		public ServerConnectStatusEventArgs(string errMsg, bool connected, bool multiOrgFound)
		{
			ErrorMessage = errMsg;
			Connected = connected;
			exEvent = null;
			MultiOrgsFound = multiOrgFound;
		}


		/// <summary></summary>
		public ServerConnectStatusEventArgs(string errMsg, bool connected, Exception except)
		{
			ErrorMessage = errMsg;
			Connected = connected;
			exEvent = except;
		}

		/// <summary></summary>
		public ServerConnectStatusEventArgs()
		{

		}
	}

}
