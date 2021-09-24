using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility;
using uiMessages = Microsoft.PowerPlatform.Dataverse.ConnectControl.Properties.Messages;
using uiResources = Microsoft.PowerPlatform.Dataverse.ConnectControl.Properties.Resources;
using Microsoft.PowerPlatform.Dataverse.Ui.Styles;
using System.Globalization;
using System.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
	/// <summary>
	/// This control provides the UI components and interaction logic with the CrmConnectionManager Class.
	/// </summary>
	public partial class ServerLoginControl : UserControl
	{
		#region Vars
		private GridViewColumnHeader _CurSortCol = null;
		private SortAdorner _CurAdorner = null;
		private bool _isSortButtonClicked = false;
		private bool _isConnected = false;
		private bool bOnlineMultiOrgFix = false;
		private bool showtitle = false;
		private double iRow3;
		private double iRow4;
		private double iRow5;
        private double iRow6;
        private double iRow7;
		private double iRow8;
		private double iRow9;
		private double advRow0;
		private double advRow3;
		private bool hideCancel = false;
		/// <summary>
		/// Configures how the control will act in various modes. 
		/// </summary>
		private ServerLoginConfigCtrlMode ControlBehaviorMode;

		/// <summary>
		/// Keys for the Server Config Info.
		/// </summary>
		private Dictionary<Dynamics_ConfigFileServerKeys, object> ServerConfigKeys;

		/// <summary>
		/// Connection manager
		/// </summary>
		private ConnectionManager _connectionManager;

		/// <summary>
		/// Storyboard for going Online
		/// </summary>
		private Storyboard goOnline = null;

		/// <summary>
		/// Storyboard for going OnPrem. 
		/// </summary>
		private Storyboard goPrem = null;

		/// <summary>
		/// Storyboard for going AdvancedCheck
		/// </summary>
		private Storyboard goAdvancedCheck = null;

		/// <summary>
		/// Storyboard for going AdvancedUnCheck
		/// </summary>
		private Storyboard goAdvancedUncheck = null;

		/// <summary>
		/// Switch to tell me if the last storyboard ran was for Online. 
		/// </summary>
		private bool LastBoardWasOnline = false;

		/// <summary>
		/// To get last AuthSource was Oauth or not.
		/// </summary>
		private bool LastSelectionWasOAuth = false;

		/// <summary>
		/// Authentication Server List
		/// </summary>
		private Model.ClaimsHomeRealmOptions AuthTypeListSource = null;

		/// <summary>
		/// Switch to indicate whether coming from Multi Org List. 
		/// </summary>
		private bool bMultiOrg = false;

		/// <summary>
		/// Login Tracing System
		/// </summary>
		private LoginTracer tracer = new LoginTracer();

		#endregion

		#region Events
		/// <summary>
		/// Raised when a Status event is raised
		/// </summary>
		public event EventHandler<ConnectStatusEventArgs> ConnectionStatusEvent;
		/// <summary>
		/// Raised when the user clicked cancel. 
		/// </summary>
		public event EventHandler UserCancelClicked;
		/// <summary>
		/// Raised when the connection process has begun 
		/// </summary>
		public event EventHandler ConnectionCheckBegining;
		/// <summary>
		/// Raised when there is an error
		/// </summary>
		public event EventHandler<ConnectErrorEventArgs> ConnectErrorEvent;

		#endregion

		#region Properties

		///<summary>
		/// Complete Connect Check
		///</summary>
		public bool IsConnected
		{
			get
			{
				return _isConnected;
			}
			private set
			{
				_isConnected = value;
			}
		}
		/// <summary>
		/// Returns the Friendly name of the connected org. 
		/// </summary>
		public string GetConnectedOrgName
		{
			get
			{
				if (_connectionManager != null) return _connectionManager.ConnectedOrgFriendlyName;

				else return string.Empty;
			}
		}
		/// <summary>
		/// Show Cancel Button or Not
		/// </summary>
		/// <remarks>The flag is specified in XAML as a bool.</remarks>
		[Description("Hide Cancel Button"), Category("Common Properties")]
		public bool HideCancel
		{
			get
			{
				return (bool)hideCancel;
			}
			set
			{
				hideCancel = value;
				if (value)
				{
					btn_Cancel.Visibility = Visibility.Collapsed;
					btnCancel.Visibility = Visibility.Collapsed;
					btnCancelOrg.Visibility = Visibility.Collapsed;
				}
			}
		}
		/// <summary>
		/// Show Title or Not
		/// </summary>
		/// <remarks>The flag is specified in XAML as a bool.</remarks>
		[Description("Show Title"), Category("Common Properties")]
		public bool ShowTitle
		{
			get
			{
				return (bool)showtitle;
			}
			set
			{
				showtitle = value;
				if (!value)
				{
					LoginGrid.RowDefinitions[0].Height = new GridLength(0);
					lblSignin.Visibility = Visibility.Collapsed;
				}
			}
		}
		#endregion

		/// <summary>
		/// Default constructor
		/// </summary>
		public ServerLoginControl()
		{
			this.InitializeComponent();
			if (CultureUtils.UICulture.TextInfo.IsRightToLeft)
			{
			this.FlowDirection = System.Windows.FlowDirection.RightToLeft;
			}

			// try to get the HomeRealm Object from the WPF form. 
			object oHr = FindResource("ClaimsHomeRealmOptionsDataSource");
			if (oHr != null && oHr is Model.ClaimsHomeRealmOptions)
			{
				AuthTypeListSource = (Model.ClaimsHomeRealmOptions)oHr;
			}

			// try to get UI Update Storyboards
			object oSb2 = TryFindResource("OnlineChecked");
			if (oSb2 != null && oSb2 is Storyboard)
			{
				goOnline = (Storyboard)oSb2;
			}

			object oSb3 = TryFindResource("OnPremChecked");
			if (oSb3 != null && oSb3 is Storyboard)
			{
				goPrem = (Storyboard)oSb3;
			}

			object oSb4 = TryFindResource("OnAdvancedChecked");
			if (oSb4 != null && oSb4 is Storyboard)
			{
				goAdvancedCheck = (Storyboard)oSb4;
			}

			object oSb5 = TryFindResource("OnAdvancedUnChecked");
			if (oSb5 != null && oSb5 is Storyboard)
			{
				goAdvancedUncheck = (Storyboard)oSb5;
			}
		}

		/// <summary>
		/// Sets the Server storage pointer.
		/// </summary>
		/// <param name="globalStore"></param>
		public void SetGlobalStoreAccess(ConnectionManager globalStore)
		{
			if (globalStore.ParentControl == null)
				globalStore.ParentControl = this;

			_connectionManager = globalStore;

			// Set the CRM Server List here from the UI.. 
			object oCrmDiscoServices = FindResource("OnlineDiscoveryServersDataSource");
			if (oCrmDiscoServices != null && oCrmDiscoServices is Model.OnlineDiscoveryServers)
				_connectionManager.OnlineDiscoveryServerList = (Model.OnlineDiscoveryServers)oCrmDiscoServices;

			if (AuthTypeListSource != null)
			{
				if (AuthTypeListSource.Items.Count == 0)
				{
					if (_connectionManager != null)
					{
						LoadHomeRealmData(); // Load HomeRealm Information from Config.
						ServerConfigKeys = _connectionManager.LoadConfigFromFile();
						if (ServerConfigKeys != null && ServerConfigKeys.Count > 3)
							LoadDisplayWithAppSettingsData();
						else
							SetInitialDefaultData();
					}
				}
				_connectionManager.HomeRealmServersList = AuthTypeListSource;
			}
		}

		/// <summary>
		///  Sets the Mode of the control..
		/// </summary>
		/// <param name="mode"></param>
		public void SetControlMode(ServerLoginConfigCtrlMode mode)
		{
			ControlBehaviorMode = mode;
			if (ControlBehaviorMode == ServerLoginConfigCtrlMode.ConfigPanel)
				btn_Cancel.Visibility = Visibility.Collapsed;
			else
				btn_Cancel.Visibility = Visibility.Visible;
		}
		/// <summary>
		/// Returns true if orgeselect grid visible 
		/// </summary>
		public Visibility IsOrgSelect()
		{
			return OrgSelectGrid.Visibility;
		}
		/// <summary>
		/// Loads the UI with initial Default data from Configuration environment. 
		/// </summary>
		private void SetInitialDefaultData()
		{
			// Set on Prem and Default creds. 
			rbOnPrem.IsChecked = true;
			cbUseDefaultCreds.IsChecked = true;
			cbUseDefaultCreds_Click(this, null);
			rbOnlinePrem_Click(this, null);

			// Set Default focus of the UI 
			if (ddlAuthSource.Items.Count > 0) ddlAuthSource.SelectedIndex = 0;
		}

		/// <summary>
		/// Load Default settings for the UI from the AppConfig and the AuthStores File
		/// </summary>
		private void LoadDisplayWithAppSettingsData()
		{
			tbCrmOrg.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg);
			tbCrmServerName.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName);
			tbCrmServerPort.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort);


			tbUserId.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
			tbDomain.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain);


			// Get Bool Settings
			bool tempBool = true;

			// if set, then the user password is not cached and the system is set to "not use" default credentials. 
			if (bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials), out tempBool))
				if (tempBool)
				{
					SecureString password = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
					if (password != null)
						tbPassword.Password = password.ToUnsecureString();
				}
				else
					tbPassword.Password = string.Empty;
			else
				tempBool = true; // Set to support next step.. check for Default Cred switch. 

			// Default Creds Switch 
			if (tempBool)
			{
				// at this point if tempBool is true, Cache Creds is enabled. 
				if (bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds), out tempBool))
				{
					if (tempBool)
						cbUseDefaultCreds.IsChecked = tempBool;
				}
				else
					cbUseDefaultCreds.IsChecked = false; // Setting was not found.. default to false. 
			}

			// On Prem vs Online vs 365 Switch. 
			// Value is not a bool.. check to see if there is a value. 
			_connectionManager.LastDeploymentType = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType);
			if (string.IsNullOrWhiteSpace(_connectionManager.LastDeploymentType))
				rbOnPrem.IsChecked = true;  // Default ...  
			else
			{
				// there is a value. 
				if (_connectionManager.LastDeploymentType.Equals(CrmDeploymentType.O365.ToString(), StringComparison.OrdinalIgnoreCase))
					rbOn365.IsChecked = true;
				else
					rbOnPrem.IsChecked = true;
			}


			if (bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUseSSL), out tempBool))
				cbUseSSL.IsChecked = tempBool;
			else
				cbUseSSL.IsChecked = false; // Default... 

			SetCurrentOnLineRegionInfo();


			// HomeRealm / Auth SOurce Pick List. 
			if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm)))
			{
				Model.ClaimsHomeRealmOptionsHomeRealm svr = AuthTypeListSource.GetServerByDisplayName((StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm)));
				if (ddlAuthSource.Items.Contains(svr))
					ddlAuthSource.SelectedItem = svr;
			}
			else
				if (ddlAuthSource.Items.Count > 0) ddlAuthSource.SelectedIndex = 0;

			if (bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AdvancedCheck), out tempBool))
				cbAdvanced.IsChecked = tempBool;
			else
				cbAdvanced.IsChecked = false;


			// push an event to fix the UI.
			cbUseDefaultCreds_Click(this, null);
			rbOnlinePrem_Click(this, null);
		}

		/// <summary>
		/// Sets the current UI element for the Online region. 
		/// </summary>
		private void SetCurrentOnLineRegionInfo()
		{
			// Get Drop Down List Settings. 
			// CRM Online Discovery Server Region 
			if (!string.IsNullOrEmpty(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion)))
			{
				// Modify to detect which array to set too.. o365 or Live. 
				if (rbOn365.IsChecked.Value)
				{
					Model.OnlineDiscoveryServer svr = _connectionManager.OnlineDiscoveryServerList.GetServerByShortName(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion), true);
					if (ddlCrmOnlineRegions.Items.Contains(svr))
						ddlCrmOnlineRegions.SelectedItem = svr;
				}
				else
				{
					Model.OnlineDiscoveryServer svr = _connectionManager.OnlineDiscoveryServerList.GetServerByShortName(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion));
					if (ddlCrmOnlineRegions.Items.Contains(svr))
						ddlCrmOnlineRegions.SelectedItem = svr;
				}
			}
			else
				if (ddlCrmOnlineRegions.Items.Count > 0) ddlCrmOnlineRegions.SelectedIndex = 0;
		}

		/// <summary>
		/// Refresh the In memory data store from the UI
		/// </summary>
		private void UpdateServerConfigKeysFromUI()
		{
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmServerName, tbCrmServerName.Text.Trim());
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPort, tbCrmServerPort.Text.Trim());

			// Set Deployment type. 
			if (rbOn365.IsChecked.Value)
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType, CrmDeploymentType.O365.ToString());
			else
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType, CrmDeploymentType.Prem.ToString());

			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName, tbUserId.Text.Trim());
			StorageUtils.SetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword, tbPassword.SecurePassword);
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain, tbDomain.Text.Trim());
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDefaultCreds, cbUseDefaultCreds.IsChecked.Value.ToString());
			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUseSSL, cbUseSSL.IsChecked.Value.ToString());
			if (ddlCrmOnlineRegions.SelectedValue != null && ddlCrmOnlineRegions.SelectedValue is Model.OnlineDiscoveryServer)
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion, ((Model.OnlineDiscoveryServer)ddlCrmOnlineRegions.SelectedValue).ShortName);

			if (ddlAuthSource.SelectedValue != null && ddlAuthSource.SelectedValue is Model.ClaimsHomeRealmOptionsHomeRealm)
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm, ((Model.ClaimsHomeRealmOptionsHomeRealm)ddlAuthSource.SelectedValue).DisplayName);
			else
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AuthHomeRealm, string.Empty);

			if (cbAskforOrg.IsChecked.Value)
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg, string.Empty);
			else
				StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg, tbCrmOrg.Text);

			StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.AdvancedCheck, cbAdvanced.IsChecked.Value.ToString());

			if (!cbAskforOrg.IsChecked.Value) // Skip Auto Assgin of org is the Show all orgs switch is set. 
				if (ddlAuthSource.SelectedIndex == 1 || ddlAuthSource.SelectedIndex == 2)
				{
					string[] crmOrg = tbCrmServerName.Text.Trim().Split('.');
					StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg, crmOrg[0]);
				}


#if DEBUG
			this.UpdateTestHelperData();
#endif
		}

#if DEBUG
		private void UpdateTestHelperData()
		{
			TestingHelper.Instance.SelectedOption = String.Empty;
			string deploymentType = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDeploymentType);
			if (false == deploymentType.Equals(CrmDeploymentType.Prem.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				TestingHelper.Instance.SelectedOption = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOnlineRegion);
			}
			
		}
#endif

		/// <summary>
		/// Populates the Home Realm data from the HomeRealmsStore.xml file. 
		/// Also adds the default entries for Active Directory and IDF
		/// </summary>
		private void LoadHomeRealmData()
		{
			tracer.Log("LoadHomeRealmData", TraceEventType.Start);

			// Add the defaults 
			AuthTypeListSource.Items.Clear();
			AuthTypeListSource.Items.Add(new Model.ClaimsHomeRealmOptionsHomeRealm() { DisplayName = uiResources.LOGIN_FRM_AUTHTYPE_AD, Uri = "" });
			AuthTypeListSource.Items.Add(new Model.ClaimsHomeRealmOptionsHomeRealm() { DisplayName = uiResources.LOGIN_FRM_AUTHTYPE_IFD, Uri = "" });
			AuthTypeListSource.Items.Add(new Model.ClaimsHomeRealmOptionsHomeRealm() { DisplayName = uiResources.LOGIN_FRM_AUTHTYPE_OAUTH, Uri = "" });

			Model.ClaimsHomeRealmOptions AuthTypeDataFile = StorageUtils.ReadHomeRealmConfigFile();
			if (AuthTypeDataFile != null && AuthTypeDataFile.Items.Count > 0)
			{
				tracer.Log(string.Format("Loading Home Realm options from Config. Found {0} entries", AuthTypeDataFile.Items.Count), TraceEventType.Information);
				foreach (var autItem in AuthTypeDataFile.Items)
				{
					AuthTypeListSource.Items.Add(autItem);
				}
			}
			tracer.Log("LoadHomeRealmData", TraceEventType.Stop);
		}

		#region Validate Server Connection calls here.

		/// <summary>
		///  Starts the connect check process
		/// </summary>
		public void StartConnectCheck()
		{

			if (ConnectionCheckBegining != null)
				ConnectionCheckBegining(this, null);
			// hide the error alert if its there.. 
			if (stkMessage.IsVisible) stkMessage.Visibility = Visibility.Collapsed;

			// Check for skip discovery flag 
			if (IsSkipDiscoverySet())
			{
				// If true pop the URL dialog. 
				InstanceUrlCapture captureFrm = new InstanceUrlCapture();
				if (this.Parent is Window)
					captureFrm.Owner = (Window)this.Parent;
				else
					captureFrm.WindowStartupLocation = WindowStartupLocation.CenterScreen; 

				if (captureFrm.ShowDialog().Value)
				{
					StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.UseDirectConnection, true.ToString());
					StorageUtils.SetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.DirectConnectionUri, captureFrm.tbConnectUrl.Text);
				}

			}

			// Update config data. 
			UpdateServerConfigKeysFromUI();
			_connectionManager.SetConfigKeyInformation(ServerConfigKeys);
			_connectionManager.ConnectionCheckComplete += new EventHandler<ServerConnectStatusEventArgs>(storageAccess_ConnectionCheckComplete);
			_connectionManager.ServerConnectionStatusUpdate += new EventHandler<ServerConnectStatusEventArgs>(storageAccess_ServerConnectionStatusUpdate);
			_connectionManager.ForceFirstOAuthPrompt = true; // Forces OAuth to prompt for Creds
			_connectionManager.ConnectToServerCheck();
			
			MessageGrid.Visibility = Visibility.Visible;
			LoginGrid.Visibility = Visibility.Collapsed;
		}

		/// <summary>
		/// Cancel Connect check
		/// </summary>
		private void CancelConnectCheck()
		{
			_connectionManager.CancelConnectToServerCheck();
		}

		/// <summary>
		/// Completed Connection Check
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void storageAccess_ConnectionCheckComplete(object sender, ServerConnectStatusEventArgs e)
		{
			IsConnected = true;
			// More then one Org was found.  Need to display the Org Selection window
			if (e.MultiOrgsFound)
			{
				tracer.Log("Multi Orgs Found. Launching selector", TraceEventType.Stop);
				// Sync to main UI thread. 
				this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
							   new Action(ShowSelectOrgDialog));
				
				// Multi orgs found 
				return;
			}
			//Below is handled as per 
			// Sync to main UI thread. 
			this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
						   new Action<ServerConnectStatusEventArgs>(ConnectionCheckComplete), e);

		}

		/// <summary>
		/// Raises the Show Orgs Dialog. 
		/// </summary>
		private void ShowSelectOrgDialog()
		{
			if (_connectionManager != null && !bOnlineMultiOrgFix)
			{
				MessageGrid.Visibility = Visibility.Collapsed;
				LoginGrid.Visibility = Visibility.Collapsed;
				OrgSelectGrid.Visibility = Visibility.Visible;
				lvOrgList.ItemsSource = _connectionManager.CrmOrgsFoundForUser.OrgsList;
			}
		}

		private void Sort_Click(object sender, RoutedEventArgs e)
		{
			GridViewColumnHeader column = sender as GridViewColumnHeader;
			_isSortButtonClicked = true;

			SetSort(column);
		}

		/// <summary>
		/// Sorts the grid view column
		/// </summary>
		/// <param name="column"></param>
		private void SetSort(GridViewColumnHeader column)
		{
			try
			{

				String field = column.Tag as String;

				if (_CurSortCol != null)
				{
					AdornerLayer.GetAdornerLayer(_CurSortCol).Remove(_CurAdorner);
					lvOrgList.Items.SortDescriptions.Clear();
				}

				ListSortDirection newDir = ListSortDirection.Ascending;
				if (_isSortButtonClicked)
				{
					//Changing sort direction only on sort button click
					if (_CurSortCol == column && _CurAdorner.Direction == newDir)
						newDir = ListSortDirection.Descending;

					_isSortButtonClicked = false;
				}
				else if (_CurAdorner != null)
				{
					newDir = _CurAdorner.Direction;
				}

				_CurSortCol = column;

				_CurAdorner = new SortAdorner(_CurSortCol, newDir, "ConnectControlSortOrderBrush");
				lvOrgList.Items.SortDescriptions.Add(
					new SortDescription(field, newDir));

				// Check to see if the adorner
				var Layer = AdornerLayer.GetAdornerLayer(_CurSortCol);
				if (Layer != null)
					Layer.Add(_CurAdorner);
			}
			catch (Exception)
			{

			}
		}


		/// <summary>
		/// Threaded Handler for the UI. 
		/// </summary>
		/// <param name="e"></param>
		private void ConnectionCheckComplete(ServerConnectStatusEventArgs e)
		{
			// Sync to main UI thread. 

			_connectionManager.ConnectionCheckComplete -= new EventHandler<ServerConnectStatusEventArgs>(storageAccess_ConnectionCheckComplete);
			_connectionManager.ServerConnectionStatusUpdate -= new EventHandler<ServerConnectStatusEventArgs>(storageAccess_ServerConnectionStatusUpdate);

			if (e.Connected)
			{
				// Added here to clear the status message. 
				stkMessage.Visibility = Visibility.Collapsed;
				
				btn_Connect.Visibility = Visibility.Visible;
				btnConnect.IsEnabled = false;
			}
			else
			{
				//show error - two different screens from login or orgselect
				btn_Connect.Visibility = Visibility.Visible;
				btnConnect.IsEnabled = false;
				
				if (MessageGrid.IsVisible)
				{
					if (!bMultiOrg)
					{
						MessageGrid.Visibility = Visibility.Collapsed;
						LoginGrid.Visibility = Visibility.Visible;
					}
					else
					{
						MessageGrid.Visibility = Visibility.Collapsed;
						OrgSelectGrid.Visibility = Visibility.Visible;
						bMultiOrg = false;
					}
				}
			}
			CompleteConnectCheck(e.Connected);
		}


		/// <summary>
		/// Status event update.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void storageAccess_ServerConnectionStatusUpdate(object sender, ServerConnectStatusEventArgs e)
		{
			// Sync to main UI thread. 
			this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
						   new Action<ServerConnectStatusEventArgs>(UpdateConnectStatusText), e);
		}

		/// <summary>
		/// Handle Error Messages
		/// </summary>
		/// <param name="bOrg"></param>
		private void HandleError(bool bOrg)
		{
			if (bOrg)
			{
				stkMessageOrg.Visibility = Visibility.Visible;
				tbConnectStatusOrg.Text = string.Empty;
				tbConnectStatusOrg.Inlines.Add(uiResources.LOGIN_FRM_ERR_FIRST);

				Run linkMsg = new Run(uiResources.LOGIN_FRM_ERR_SECOND);

				linkMsg.TextDecorations = TextDecorations.Underline;
				linkMsg.Cursor = Cursors.Hand;
				linkMsg.Focusable = true;
				tbConnectStatusOrg.Inlines.Add(linkMsg);
				linkMsg.Focus();
				linkMsg.KeyDown += new System.Windows.Input.KeyEventHandler(this.linkMsg_KeyDown);
				linkMsg.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.linkMsg_MouseLeftButtonUp);
				Inline[] inlineArray = new Inline[tbConnectStatusOrg.Inlines.Count];
				tbConnectStatusOrg.Inlines.CopyTo(inlineArray, 0);

				for (int count = 0; count < inlineArray.Length; count++)
				{
					tbConnectStatusOrg.Inlines.Add(inlineArray[count]);
				}
				tbConnectStatusOrg.UpdateLayout();
			}
			else
			{
				stkMessage.Visibility = Visibility.Visible;
				tbConnectStatus.Text = string.Empty;
				tbConnectStatus.Inlines.Add(uiResources.LOGIN_FRM_ERR_FIRST);

				Run linkMsg = new Run(uiResources.LOGIN_FRM_ERR_SECOND);

				linkMsg.TextDecorations = TextDecorations.Underline;
				linkMsg.Cursor = Cursors.Hand;
				linkMsg.Focusable = true;
				tbConnectStatus.Inlines.Add(linkMsg);
				linkMsg.Focus();
				linkMsg.KeyDown += new System.Windows.Input.KeyEventHandler(this.linkMsg_KeyDown);
				linkMsg.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.linkMsg_MouseLeftButtonUp);
				Inline[] inlineArray = new Inline[tbConnectStatus.Inlines.Count];
				tbConnectStatus.Inlines.CopyTo(inlineArray, 0);

				for (int count = 0; count < inlineArray.Length; count++)
				{
					tbConnectStatus.Inlines.Add(inlineArray[count]);
				}
				tbConnectStatus.UpdateLayout();
			}
		}

		/// <summary>
		/// Updates the displayed status 
		/// </summary>
		/// <param name="e"></param>
		private void UpdateConnectStatusText(ServerConnectStatusEventArgs e)
		{
			bOnlineMultiOrgFix = false;
			if (e.exEvent != null)
			{
				//// Error here .. 
				if (!bMultiOrg)
					HandleError(false);
				else
					HandleError(true);

				if (ConnectErrorEvent != null)
					ConnectErrorEvent(this, new ConnectErrorEventArgs() { ErrorMessage = e.ErrorMessage, Ex = e.exEvent });

				//this is to handle extrenal error handlers hooked
				btn_Connect.IsEnabled = true;
			}
			else
			{
				if (!string.IsNullOrEmpty(e.StatusMessage))
				{
					tracer.Log(string.Format("Login Status in Connect is =  {0}", e.StatusMessage));

					//When user don't have permission to selected org.
					if (e.StatusMessage.Equals(string.Format(uiMessages.CRMCONNECT_LOGIN_UNABLE_TO_CONNECT_GENERAL, StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmOrg), StringComparison.CurrentCultureIgnoreCase)))
					{
						if (!LoginGrid.IsVisible)
						{
							LoginGrid.Visibility = System.Windows.Visibility.Visible;
							OrgSelectGrid.Visibility = System.Windows.Visibility.Collapsed;
						}

						if (ConnectErrorEvent != null)
							ConnectErrorEvent(this, new ConnectErrorEventArgs() { ErrorMessage = e.StatusMessage });
					}

					if (MessageGrid.IsVisible)
					{ //Found Single Org
						if (_connectionManager != null && !string.IsNullOrEmpty(_connectionManager.ConnectedOrgFriendlyName))
						{
							if (e.StatusMessage.Equals(uiMessages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING, StringComparison.CurrentCultureIgnoreCase))
							{
								if (!bMultiOrg)
								{
									lblCrmOrg.Text = string.Format(uiResources.LOGIN_FRM_RETRIEVE_DEF, _connectionManager.ConnectedOrgFriendlyName);
								}
							}
							else
							{
								lblCrmOrg.Text = string.Format(uiMessages.CRMCONNECT_SERVER_CONNECT_GOOD + " - {0}", _connectionManager.ConnectedOrgFriendlyName);
								ipb.Visibility = Visibility.Collapsed;
							}
						}
						else
						{
							// Adding logic to not show this if multiple orgs are being requested. 
							if (!e.MultiOrgsFound)
							{
								if (e.StatusMessage.Equals(uiMessages.CRMCONNECT_MSG_WEBSERVICE_CLIENT_MISCONFIGURED, StringComparison.CurrentCultureIgnoreCase) || e.StatusMessage.Equals(uiMessages.CRMCONNECT_MSG_EMPTY_LOGIN_DETAILS, StringComparison.CurrentCultureIgnoreCase) || e.StatusMessage.Equals(uiMessages.CRMCONNECT_MSG_INVALID_LOGIN_DETAILS, StringComparison.CurrentCultureIgnoreCase))
								{
									HandleError(false);
									if (ConnectErrorEvent != null)
										ConnectErrorEvent(this, new ConnectErrorEventArgs() { ErrorMessage = e.ErrorMessage, Ex = new Exception(e.StatusMessage) }); //this is to handle extrenal error handlers hooked
									return; //error happened in CRM Online although exEvent is null
								}
								lblCrmOrg.Text = e.StatusMessage;
							}
						}
					}
				}
				else
				{
					if (!e.MultiOrgsFound)
					{
						// raise error event here. 
						tracer.Log(string.Format("Login Error in Connect is = {0}", e.ErrorMessage), TraceEventType.Error);

						HandleError(false);

						if (ConnectErrorEvent != null)
							ConnectErrorEvent(this, new ConnectErrorEventArgs()
							{
								ErrorMessage = e.ErrorMessage,
								Ex =

									e.exEvent
							});

						btn_Connect.IsEnabled = true;
					}
				}

				if (ConnectionStatusEvent != null)
					ConnectionStatusEvent(this, new ConnectStatusEventArgs(e.Connected) { Status = e.StatusMessage });
			}

		}

		/// <summary>
		/// Handles clicking on the error link in the status text
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void linkMsg_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			try
			{
				ErrorLogger.LaunchLogFile();
			}
			catch (Exception)
			{
			}
		}

		/// <summary>
		/// Handles keydown click on the error link in the status text
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void linkMsg_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			try
			{
				if (e != null && e.Key == Key.Enter)
				{
					ErrorLogger.LaunchLogFile();
					e.Handled = true;
				}
			}
			catch (Exception)
			{
			}
		}

		/// <summary>
		///  Called when the Connect Check Completes.
		/// </summary>
		/// <param name="bSuccess"></param>
		private void CompleteConnectCheck(bool bSuccess)
		{
			if (bSuccess)
			{
				// Save settings
				_connectionManager.SaveConfigToFile(ServerConfigKeys);

				// Force a reload to pick up any special bits. 
				LoadDisplayWithAppSettingsData();

				if (ConnectionStatusEvent != null)
					ConnectionStatusEvent(this, new ConnectStatusEventArgs(bSuccess));
			}
			else
			{
				// failed to connect. 
				if (ConnectionStatusEvent != null)
					ConnectionStatusEvent(this, new ConnectStatusEventArgs(bSuccess));
			}
			btnCancel.Visibility = Visibility.Visible;
		}

		/// <summary>
		/// To Set AdvancedGrid width.
		/// </summary>
		private void SetAdvancedGridWidth()
		{
			//To make sure Inner grid(Advanced Grid) alligmrnt is inline with parent grid(Login Grid)
			if (LastBoardWasOnline)
			{
				//Office365: Reseting width of Advanced Grid
				AdvancedGrid.ColumnDefinitions[0].Width = GridLength.Auto;
			}
			else
			{
				// On-Prem: Seting Login Grid width to Advanced Grid
				GridLength LgnGridColumn0length = new GridLength(LoginGrid.ColumnDefinitions[0].ActualWidth);
				AdvancedGrid.ColumnDefinitions[0].Width = LgnGridColumn0length;
			}
		}

		/// <summary>
		/// Checks to see if the skip discovery flag is set, if so, requires the user to provide a full URI to connect to the remote server. 
		/// </summary>
		/// <returns></returns>
		private bool IsSkipDiscoverySet()
		{
			if (ConfigurationManager.AppSettings != null)
				if (ConfigurationManager.AppSettings["SkipDiscovery"] != null && ConfigurationManager.AppSettings["SkipDiscovery"].Equals("true", StringComparison.OrdinalIgnoreCase))
				{
					return true; 
				};
			return false; 
		}

		#endregion

		#region Events

		/// <summary>
		///  Raised when the Connect to Server Button is Pushed.  
		///  Begins the Server connection process. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btn_ConnectToServer(object sender, System.Windows.RoutedEventArgs e)
		{
			bMultiOrg = false;
			btnCancel.Visibility = Visibility.Collapsed;
			_connectionManager.ServiceClient = null;
			StartConnectCheck();
		}
		/// <summary>
		/// Raised when the Default Credentials Check box State changes
		/// Sets the visible state for using the setting of the Default Credentials control
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cbUseDefaultCreds_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			tracer.Log(string.Format("Use Current User Checkbox State = {0}", cbUseDefaultCreds.IsChecked.Value));

			// set to the opposite of the initial value
			tbUserId.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
			tbPassword.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
			tbDomain.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
			if (rbOn365.IsChecked.Value)
				tbDomain.IsEnabled = false;

		}

		/// <summary>
		/// Raised when the Cancel button is clicked
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btn_CancelSave(object sender, System.Windows.RoutedEventArgs e)
		{
			CancelConnectCheck();
			if (UserCancelClicked != null)
				UserCancelClicked(this, null);
		}

		/// <summary>
		/// Raised when the UI is loaded
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UiiServerConnectionCtrl_Loaded(object sender, RoutedEventArgs e)
		{
			if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
			{
				iRow3 = LoginGrid.RowDefinitions[3].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[3].Height.Value;
				iRow4 = LoginGrid.RowDefinitions[4].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[4].Height.Value;
				iRow5 = LoginGrid.RowDefinitions[5].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[5].Height.Value;
                iRow6 = LoginGrid.RowDefinitions[6].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[6].Height.Value;
                iRow7 = LoginGrid.RowDefinitions[7].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[7].Height.Value;
				iRow8 = LoginGrid.RowDefinitions[8].Height.Value == 93 ? 115.0 : LoginGrid.RowDefinitions[8].Height.Value;
				iRow9 = LoginGrid.RowDefinitions[9].Height.Value == 0 ? 31.0 : LoginGrid.RowDefinitions[9].Height.Value;
				advRow0 = AdvancedGrid.RowDefinitions[0].Height.Value == 0 ? 31.0 : AdvancedGrid.RowDefinitions[0].Height.Value;
				advRow3 = AdvancedGrid.RowDefinitions[3].Height.Value == 0 ? 31.0 : AdvancedGrid.RowDefinitions[3].Height.Value;

				LoginGrid.RowDefinitions[4].Height = new GridLength(0);
				if (LoginGrid.RowDefinitions[5].Height.Value == 0) 
					LoginGrid.RowDefinitions[5].Height = new GridLength(iRow5);
				//In config file if CrmDeploymentType is O365 need to manualy set the height of iRow8
				if ((iRow8 == 0 || iRow8 == 31) && LastBoardWasOnline)
					iRow8 = 115;

				if (iRow5 == 115 && LastBoardWasOnline)
					iRow5 = 31;

				// Load Stored settings here. 
				if (_connectionManager != null)
				{
					LoadHomeRealmData(); // Load HomeRealm Information from Config.
					ServerConfigKeys = _connectionManager.LoadConfigFromFile();
					if (ServerConfigKeys != null && ServerConfigKeys.Count > 3)
						LoadDisplayWithAppSettingsData();
					else
						SetInitialDefaultData();
				}
			}
		}


		/// <summary>
		/// Sets the UI state when either the CRM Online or Prem Radio buttons are checked
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void rbOnlinePrem_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// Storyboard execution moved out of triggers, to here, to support the not unnecessary replaying a storyboard

			// Bind to O365 Servers. 
			ddlCrmOnlineRegions.ItemsSource = _connectionManager.OnlineDiscoveryServerList.OSDPServers;

			if (rbOnPrem.IsChecked.Value)
			{
				LoginGrid.RowDefinitions[3].Height = new GridLength(0);
				LoginGrid.RowDefinitions[4].Height = new GridLength(31);
				LoginGrid.RowDefinitions[5].Height = new GridLength(31);
                LoginGrid.RowDefinitions[6].Height = new GridLength(iRow6);
                LoginGrid.RowDefinitions[7].Height = new GridLength(iRow7);
				LoginGrid.RowDefinitions[8].Height = new GridLength(93);
				LoginGrid.RowDefinitions[9].Height = new GridLength(iRow9);

				AdvancedGrid.RowDefinitions[0].Height = new GridLength(0);
				AdvancedGrid.RowDefinitions[3].Height = new GridLength(advRow3);
				GbAdvanced.Margin = new Thickness(-6, -20, -6, -20);

				tbUserId.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
				tbPassword.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
				tbDomain.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;

				GbAdvanced.Visibility = Visibility.Visible;
				GbAdvanced.Header = "";
				GbAdvanced.BorderThickness = new Thickness(0);

				Grid.SetRow(GbAdvanced, 8);
				Grid.SetRow(stkOrg, 9);

				if (goPrem != null)
					goPrem.Begin();
				LastBoardWasOnline = false;
			}
			else
			{
                LoginGrid.RowDefinitions[3].Height = new GridLength(22);
                LoginGrid.RowDefinitions[4].Height = new GridLength(22);
                LoginGrid.RowDefinitions[5].Height = new GridLength(22);
				LoginGrid.RowDefinitions[7].Height = new GridLength(0);
				LoginGrid.RowDefinitions[9].Height = new GridLength(0);

                Grid.SetRow(stkUseDefaultCreds, 3);
                Grid.SetRow(stkOrg, 4);
                Grid.SetRow(stkAdvanced, 5);

				// Do UI.. 
				if (!LastBoardWasOnline)
				{
					if (goOnline != null)
						goOnline.Begin();
					LastBoardWasOnline = true;
				}

				SetCurrentOnLineRegionInfo(); // Set the currently focused Online Region. 
				cbAdvanced_Checked(this, null);

			}
			SetAdvancedGridWidth();
			SetAdvancedGroupBoxVisibility();
		}

		private void SetAdvancedGroupBoxVisibility()
		{
			if (rbOnPrem.IsChecked.Value && ddlAuthSource.SelectedValue != null
				&& ddlAuthSource.SelectedValue is Model.ClaimsHomeRealmOptionsHomeRealm
				&& ((Model.ClaimsHomeRealmOptionsHomeRealm)ddlAuthSource.SelectedValue).DisplayName.Equals(uiResources.LOGIN_FRM_AUTHTYPE_OAUTH))
			{
				//Disabling Username, Password and Domain textboxes on select of OAuth (On-Prem).
				GbAdvanced.IsEnabled = false;
				tbUserId.Clear();
				tbPassword.Clear();
				tbDomain.Clear();
				LastSelectionWasOAuth = true;
			}
			else if (LastSelectionWasOAuth)
			{
				GbAdvanced.IsEnabled = true;

				//Reading Username, Password and Domain from config file
				tbUserId.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmUserName);
				tbDomain.Text = StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmDomain);

				bool tempBool = true;

				if (bool.TryParse(StorageUtils.GetConfigKey<string>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CacheCredentials), out tempBool))
				{
					if (tempBool)
					{
						SecureString password = StorageUtils.GetConfigKey<SecureString>(ServerConfigKeys, Dynamics_ConfigFileServerKeys.CrmPassword);
						if (password != null)
							tbPassword.Password = password.ToUnsecureString();
					}
					else
						tbPassword.Password = string.Empty;
				}
				LastSelectionWasOAuth = false;
			}
		}

		private void tbCrmServerPort_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.D0 ||
			   e.Key == Key.D1 ||
			   e.Key == Key.D2 ||
			   e.Key == Key.D3 ||
			   e.Key == Key.D4 ||
			   e.Key == Key.D5 ||
			   e.Key == Key.D6 ||
			   e.Key == Key.D7 ||
			   e.Key == Key.D8 ||
			   e.Key == Key.D9 ||
			   e.Key == Key.NumPad0 ||
			   e.Key == Key.NumPad1 ||
			   e.Key == Key.NumPad2 ||
			   e.Key == Key.NumPad3 ||
			   e.Key == Key.NumPad4 ||
			   e.Key == Key.NumPad5 ||
			   e.Key == Key.NumPad6 ||
			   e.Key == Key.NumPad7 ||
			   e.Key == Key.NumPad8 ||
			   e.Key == Key.NumPad9 ||
			   e.Key == Key.Tab)
				return;
			else
				e.Handled = true;

		}

		#endregion

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			CancelConnectCheck();
			if (UserCancelClicked != null)
				UserCancelClicked(this, null);
		}

		/// <summary>
		/// Handles the List view Mouse Double Click
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mouseConnectOrg_DoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				ConnectToSelectedOrg();
			}
		}

		/// <summary>
		/// Handles the Connect to Org click. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnConnectOrg_Click(object sender, RoutedEventArgs e)
		{
			ConnectToSelectedOrg();
		}

		/// <summary>
		/// To Connect to Selected Org
		/// </summary>
		private void ConnectToSelectedOrg()
		{
			bMultiOrg = true;
			// User Selected a CRM Server. 
			if (lvOrgList.SelectedItem != null && lvOrgList.SelectedItem is OrgByServer)
			{
				// Organization is selected. 
				OrgByServer selectedorg = (OrgByServer)lvOrgList.SelectedItem;
				if (selectedorg != null)
				{
					// ReRun Server Auth with selected order passed in. 
					if (ConnectionCheckBegining != null)
						ConnectionCheckBegining(this, null);

					OrgSelectGrid.Visibility = Visibility.Collapsed;
					MessageGrid.Visibility = Visibility.Visible;
					lblCrmOrg.Text = string.Format(uiMessages.CRMCONNECT_LOGIN_PROCESS_CONNNECTING + " - {0}", selectedorg.FriendlyName);
					ipb.Visibility = Visibility.Visible;
					btnCancel.Visibility = Visibility.Collapsed;

					_connectionManager.ConnectionCheckComplete -= storageAccess_ConnectionCheckComplete;
					_connectionManager.ServerConnectionStatusUpdate -= storageAccess_ServerConnectionStatusUpdate;

					_connectionManager.ConnectionCheckComplete += storageAccess_ConnectionCheckComplete;
					_connectionManager.ServerConnectionStatusUpdate += storageAccess_ServerConnectionStatusUpdate;
					_connectionManager.ConnectToServerCheck(selectedorg);
					if (stkMessageOrg.IsVisible) stkMessageOrg.Visibility = Visibility.Collapsed;
					return;
				}
			}
			if (_connectionManager != null)
			{
				stkMessageOrg.Visibility = Visibility.Visible;
				tbConnectStatusOrg.Text = uiMessages.CRMCONNECT_NOORG_SEL;
				storageAccess_ServerConnectionStatusUpdate(this, new ServerConnectStatusEventArgs(uiMessages.CRMCONNECT_NOORG_SEL, false));
				ConnectionCheckComplete(new ServerConnectStatusEventArgs(uiMessages.CRMCONNECT_NOORG_SEL, false));
			}
		}

		private void btnCancelOrg_Click(object sender, RoutedEventArgs e)
		{
			CancelConnectCheck();
			if (UserCancelClicked != null)
				UserCancelClicked(this, null);
		}

		private void lvOrgList_Loaded(object sender, RoutedEventArgs e)
		{
			SetSort((GridViewColumnHeader)OrgCol.Header);
		}

		/// <summary>
		/// LoginGrid load event to set wid
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void LoginGrid_Loaded(object sender, RoutedEventArgs e)
		{
			SetAdvancedGridWidth();
		}
		/// <summary>
		///  API Called when Client wants to go back to login and cancel connect.
		/// </summary>
		public void GoBackToLogin()
		{
			StartCancelSave();
			IsConnected = false;
			OrgSelectGrid.Visibility = Visibility.Collapsed;
			MessageGrid.Visibility = Visibility.Collapsed;
			LoginGrid.Visibility = Visibility.Visible;
		}
		/// <summary>
		///  API Called when Client wants to cancel connect.
		/// </summary>
		public void StartCancelSave()
		{
			CancelConnectCheck();
			if (!IsConnected)
			{
				if (UserCancelClicked != null)
					UserCancelClicked(this, null);
			}
		}
		/// <summary>
		/// Called when the client wants to show the message grid. 
		/// </summary>
		public void ShowMessageGrid()
		{
			MessageGrid.Visibility = Visibility.Visible;
			LoginGrid.Visibility = Visibility.Collapsed;
		}

		private void cbAdvanced_Checked(object sender, RoutedEventArgs e)
		{
			if (cbAdvanced.IsChecked.Value)
			{
				if (goAdvancedCheck != null)
					goAdvancedCheck.Begin();

				GbAdvanced.Visibility = Visibility.Visible;
				LoginGrid.RowDefinitions[6].Height = new GridLength(iRow8);
				LoginGrid.RowDefinitions[8].Height = new GridLength(iRow5);
				AdvancedGrid.RowDefinitions[0].Height = new GridLength(advRow0);
				AdvancedGrid.RowDefinitions[3].Height = new GridLength(0);

				GbAdvanced.BorderThickness = new Thickness(1);
				GbAdvanced.Header = uiResources.LOGIN_FRM_GB_HEADER;
				GbAdvanced.Margin = new Thickness(0, 0, 0, 0);
				Grid.SetRow(GbAdvanced, 6);

                tbUserId.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
                tbPassword.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
                tbDomain.IsEnabled = !cbUseDefaultCreds.IsChecked.Value;
                if (rbOn365.IsChecked.Value)
                    tbDomain.IsEnabled = false;
            }
			else
			{
				GbAdvanced.Header = "";
				GbAdvanced.BorderThickness = new Thickness(0);
				AdvancedGrid.RowDefinitions[0].Height = new GridLength(advRow0);
				AdvancedGrid.RowDefinitions[3].Height = new GridLength(0);
				if (goAdvancedUncheck != null)
					goAdvancedUncheck.Begin();
				GbAdvanced.Visibility = Visibility.Collapsed;
			}
		}
		
		private void ddlAuthSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//if ((ddlAuthSource.SelectedIndex == 1 && cbUseSSL.IsChecked == true) || (ddlAuthSource.SelectedIndex == 2 && cbUseSSL.IsChecked == true))
			//{
			//	cbAskforOrg.IsChecked = false;
			//	//cbAskforOrg.Visibility = Visibility.Hidden;
			//}
			////else
			//	//cbAskforOrg.Visibility = Visibility.Visible;

			SetAdvancedGroupBoxVisibility();
		}

		private void cbUseSSL_Click(object sender, RoutedEventArgs e)
		{
			//if ((ddlAuthSource.SelectedIndex == 1 && cbUseSSL.IsChecked == true) || (ddlAuthSource.SelectedIndex == 2 && cbUseSSL.IsChecked == true))
			//{
			//	cbAskforOrg.IsChecked = false;
			//	cbAskforOrg.Visibility = Visibility.Visible;
			//}
			//else
			//	cbAskforOrg.Visibility = Visibility.Visible;
		}
	}
}