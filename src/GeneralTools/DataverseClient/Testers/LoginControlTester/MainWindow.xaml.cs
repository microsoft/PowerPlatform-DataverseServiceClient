//===============================================================================
// MICROSOFT SAMPLE
// Microsoft Dynamics CRM 2010
// Project: Dynamics CRM Connect Control Login Control Tester
// PURPOSE: Example project for a Login Dialog 
//===============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.PowerPlatform.Dataverse.ConnectControl;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Threading;
using System.Windows.Threading;
using System.Net;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;

namespace LoginControlTester
{
	/// <summary>
	/// Connection Control tester.... 
	/// </summary>
	public partial class MainWindow : Window
	{

		/// <summary>
		/// Microsoft.PowerPlatform.Dataverse.Connector services
		/// </summary>
		private ServiceClient _serviceClient;

		/// <summary>
		/// Bool flag to determine if there is a connection 
		/// </summary>
		private bool bIsConnectedComplete = false;

		/// <summary>
		/// <summary>
		/// CRM Connection Manager component. 
		/// </summary>
		private ConnectionManager _connectionManager;

		/// <summary>
		///  This is used to allow the UI to reset w/out closing 
		/// </summary>
		private bool resetUiFlag = false; 

		public MainWindow()
		{
			InitializeComponent();
			// CodeQL [SM02184] intent is to debug the login process
			ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
			{
				//MessageBox.Show("CertError");
				return true;
			};
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			/*
				This is the setup process for the login control, 
				The login control uses a class called CrmConnectionManager to manage the interaction with CRM, this class and also be queried as later points for information about the current connection. 
				In this case, the login control is referred to as CrmLoginCtrl
			 */

			// Set off flag. 
			bIsConnectedComplete = false; 
			// Init the CRM Connection manager.. 
			_connectionManager = new ConnectionManager();
			// Azure registered ClientId for PD, HAT and other tools
			_connectionManager.ClientId = "2ad88395-b77d-4561-9441-d0e40824f9bc";
			// Azure registered RedirectUri for PD, HAT and other tools
			_connectionManager.RedirectUri = new Uri("app://5d3e90d6-aa8e-48a8-8f2c-58b45cc67315");
			// Pass a reference to the current UI or container control,  this is used to synchronize UI threads In the login control
			_connectionManager.ParentControl = CrmLoginCtrl;
			// if you are using an unmanaged client, excel for example, and need to store the config in the users local directory
			// set this option to true. 
			_connectionManager.UseUserLocalDirectoryForConfigStore = true;

			var TitleBarResource = CrmLoginCtrl.FindName("lblCrmMultOrg");
			if (TitleBarResource is TextBlock)
			{
				((TextBlock)TitleBarResource).MaxWidth = double.PositiveInfinity;
			}

			// if you are using an unmanaged client,  you need to provide the name of an exe to use to create app config key's for. 
			//mgr.HostApplicatioNameOveride = "TEST.exe";

			// CrmLoginCtrl is the Login control,  this sets the CrmConnection Manager into it. 
			CrmLoginCtrl.SetGlobalStoreAccess(_connectionManager);
			// There are several modes to the login control UI
			CrmLoginCtrl.SetControlMode(ServerLoginConfigCtrlMode.FullLoginPanel);
			// this wires an event that is raised when the login button is pressed. 
			CrmLoginCtrl.ConnectionCheckBegining += new EventHandler(CrmLoginCtrl_ConnectionCheckBegining);
			// this wires an event that is raised when an error in the connect process occurs. 
			CrmLoginCtrl.ConnectErrorEvent += new EventHandler<ConnectErrorEventArgs>(CrmLoginCtrl_ConnectErrorEvent);
			// this wires an event that is raised when a status event is returned. 
			CrmLoginCtrl.ConnectionStatusEvent += new EventHandler<ConnectStatusEventArgs>(CrmLoginCtrl_ConnectionStatusEvent);
			// this wires an event that is raised when the user clicks the cancel button. 
			CrmLoginCtrl.UserCancelClicked += new EventHandler(CrmLoginCtrl_UserCancelClicked);
			// Check to see if its possible to do an Auto Login 
			if (!_connectionManager.RequireUserLogin())
			{
				// If RequireUserLogin is false, it means that there has been a successful login here before and the credentials are cached. 
				if (MessageBox.Show(LoginControlTester.Resources.Resources.CREDENTIALS_ALREADY_SAVED_IN_CONFIGURATION, LoginControlTester.Resources.Resources.AUTO_LOGIN, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
					CrmLoginCtrl.IsEnabled = false;

					// When running an auto login,  you need to wire and listen to the events from the connection manager.
					// Run Auto User Login process, Wire events. 
					_connectionManager.ServerConnectionStatusUpdate += new EventHandler<ServerConnectStatusEventArgs>(mgr_ServerConnectionStatusUpdate);
					_connectionManager.ConnectionCheckComplete += new EventHandler<ServerConnectStatusEventArgs>(mgr_ConnectionCheckComplete);
					// Start the connection process. 
					_connectionManager.ConnectToServerCheck();

					// Show the message grid. 
					CrmLoginCtrl.ShowMessageGrid(); 
				}
			}
		}

		/// <summary>
		/// Updates from the Auto Login process. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mgr_ServerConnectionStatusUpdate(object sender, ServerConnectStatusEventArgs e)
		{
			// The Status event will contain information about the current login process,  if Connected is false, then there is not yet a connection. 
			// Set the updated status of the loading process. 
			Dispatcher.Invoke(DispatcherPriority.Normal,
							   new System.Action(() =>
							   {
								   this.Title = string.IsNullOrWhiteSpace(e.StatusMessage) ? e.ErrorMessage : e.StatusMessage;
							   }
								   ));

		}

		/// <summary>
		/// Complete Event from the Auto Login process
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mgr_ConnectionCheckComplete(object sender, ServerConnectStatusEventArgs e)
		{
			// The Status event will contain information about the current login process,  if Connected is false, then there is not yet a connection. 
			// Unwire events that we are not using anymore, this prevents issues if the user uses the control after a failed login. 
			((ConnectionManager)sender).ConnectionCheckComplete -= mgr_ConnectionCheckComplete;
			((ConnectionManager)sender).ServerConnectionStatusUpdate -= mgr_ServerConnectionStatusUpdate;

			if (!e.Connected)
			{
				// if its not connected pop the login screen here. 
				if (e.MultiOrgsFound)
					MessageBox.Show(LoginControlTester.Resources.Resources.UNABLE_TO_LOGIN_TO_CRM_ORGANIZATION_NOT_FOUND, LoginControlTester.Resources.Resources.LOGIN_FAILURE);
				else
					MessageBox.Show(LoginControlTester.Resources.Resources.UNABLE_TO_LOGIN_TO_CRM, LoginControlTester.Resources.Resources.LOGIN_FAILURE);

				resetUiFlag = true;
				CrmLoginCtrl.GoBackToLogin();
				// Bad Login Get back on the UI. 
				Dispatcher.Invoke(DispatcherPriority.Normal,
					   new System.Action(() =>
					   {
						   this.Title = LoginControlTester.Resources.Resources.FAILED_TO_LOGIN;
						   MessageBox.Show(this.Title , LoginControlTester.Resources.Resources.NOTIFICATION_FROM_CONNECTION_MANAGER, MessageBoxButton.OK, MessageBoxImage.Error);
						   CrmLoginCtrl.IsEnabled = true;
					   }
						));
				resetUiFlag = false; 
			}
			else
			{
				// Good Login Get back on the UI 
				if (e.Connected && !bIsConnectedComplete)
					ProcessSuccess();
			}

		}

		/// <summary>
		///  Login control connect check starting. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CrmLoginCtrl_ConnectionCheckBegining(object sender, EventArgs e)
		{
			bIsConnectedComplete = false;
			Dispatcher.Invoke(DispatcherPriority.Normal,
							   new System.Action(() =>
							   {
								   this.Title = "Starting Login Process. ";
								   CrmLoginCtrl.IsEnabled = true;
							   }
								   ));
		}

		/// <summary>
		/// Login control connect check status event. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CrmLoginCtrl_ConnectionStatusEvent(object sender, ConnectStatusEventArgs e)
		{
			//Here we are using the bIsConnectedComplete bool to check to make sure we only process this call once. 
			if (e.ConnectSucceeded && !bIsConnectedComplete)
				ProcessSuccess();

		}

		/// <summary>
		/// This raises and processes Success
		/// </summary>
		private void ProcessSuccess()
		{
			resetUiFlag = true;
			bIsConnectedComplete = true;
			_serviceClient = _connectionManager.ServiceClient;
			CrmLoginCtrl.GoBackToLogin();
			MessageBox.Show(LoginControlTester.Resources.Resources.CONNECTED_LOGIN_EXPERIENCE, LoginControlTester.Resources.Resources.NOTIFICATION_FROM_PARENT, MessageBoxButton.OK, MessageBoxImage.Information);
			Dispatcher.Invoke(DispatcherPriority.Normal,
			   new System.Action(() =>
			   {
				   this.Title = LoginControlTester.Resources.Resources.CONNECTED_FROM_PARENT_MAIN_WINDOW;
				   CrmLoginCtrl.IsEnabled = true;

				   // Run query 
				   var a = _serviceClient.GetEntityDisplayName("account");
				   if (a == null)
					   MessageBox.Show(_serviceClient.LastError);

				   MessageBox.Show(a + LoginControlTester.Resources.Resources.DISPLAY_NAME_ENTITY, LoginControlTester.Resources.Resources.NOTIFICATION_FROM_PARENT, MessageBoxButton.OK, MessageBoxImage.Information);
			   }
				));
			resetUiFlag = false;
		}

		/// <summary>
		/// Login control Error event. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CrmLoginCtrl_ConnectErrorEvent(object sender, ConnectErrorEventArgs e)
		{
			string errorMessage = e.ErrorMessage;
			if (!string.IsNullOrWhiteSpace(e.Ex?.Message) && !e.Ex.Message.StartsWith(e.ErrorMessage, StringComparison.OrdinalIgnoreCase))
            {
				errorMessage += Environment.NewLine + e.Ex.Message;
			}
			MessageBox.Show(errorMessage, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		/// <summary>
		/// Login Control Cancel event raised. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CrmLoginCtrl_UserCancelClicked(object sender, EventArgs e)
		{
			if ( !resetUiFlag) 
				this.Close();
		}
	}
}
