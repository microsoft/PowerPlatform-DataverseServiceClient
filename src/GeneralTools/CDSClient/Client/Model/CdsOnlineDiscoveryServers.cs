using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.PowerPlatform.Cds.Client;

namespace Microsoft.PowerPlatform.Cds.Client.Model
{
	/// <summary>
	/// CDS online Discovery server enumeration
	/// </summary>
	public class CdsDiscoveryServers : INotifyPropertyChanged , IDisposable
	{
		/// <summary>
		/// CRM Log entry
		/// </summary>
		CdsTraceLogger logger = null;

		/// <summary>
		/// Contains the list of Office 365 CRM enabled Discovery Servers
		/// </summary>
		private ObservableCollection<CdsDiscoveryServer> _OSDPServers = new ObservableCollection<CdsDiscoveryServer>();

		/// <summary>
		/// Public Property to Access Office 365 discovery servers
		/// </summary>
		public ObservableCollection<CdsDiscoveryServer> OSDPServers { get { return _OSDPServers; } set { if (value != _OSDPServers) _OSDPServers = value; NotifyPropertyChanged("OSDPServers"); } }

		/// <summary>
		/// Default constructor,  Builds baseline data for the Servers.
		/// </summary>
		public CdsDiscoveryServers()
		{
			if ( logger == null )
				logger = new CdsTraceLogger();

			if (_OSDPServers == null)
				_OSDPServers = new ObservableCollection<CdsDiscoveryServer>();

			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = null, DisplayName = "Don’t Know", ShortName = "" });

			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm5.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Asia Pacific Area", ShortName = "APAC" , GeoCode="APAC" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm3.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Canada", ShortName = "CAN" , GeoCode="CAN" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm.dynamics.cn/XRMServices/2011/Discovery.svc"), DisplayName = "China", ShortName = "CHN", GeoCode="CHN", RequiresRegionalDiscovery = true, RegionalGlobalDiscoveryServer = new Uri("https://globaldisco.crm.dynamics.cn") });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm4.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Europe, Middle East and Africa", ShortName = "EMEA" , GeoCode= "EMEA" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm12.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "France", ShortName = "FRA" , GeoCode= "FRA" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm16.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Germany (Go Local)", ShortName = "GER" , GeoCode= "GER" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm.Microsoftdynamics.de/XRMServices/2011/Discovery.svc"), DisplayName = "Germany", ShortName = "DEU", RequiresRegionalDiscovery = true , GeoCode="DEU"});
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm8.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "India", ShortName = "IND" , GeoCode="IND" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm7.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Japan", ShortName = "JPN" , GeoCode ="JPN" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "North America", ShortName = "NorthAmerica" });  // Do not add Geo code to NAM or GCC,  as they use the same server level GEO code. 
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm9.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "North America 2", ShortName = "NorthAmerica2", RequiresRegionalDiscovery = true, RegionalGlobalDiscoveryServer = new Uri("https://globaldisco.crm9.dynamics.com")});
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm6.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Oceania", ShortName = "Oceania" , GeoCode="OCE" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm14.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "South Africa", ShortName = "ZAF" , GeoCode= "ZAF" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm2.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "South America", ShortName = "SouthAmerica" , GeoCode="LATAM" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm17.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "Switzerland", ShortName = "Switzerland", GeoCode = "CHE" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm15.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "United Arab Emirates", ShortName = "UAE" , GeoCode="UAE" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm11.dynamics.com/XRMServices/2011/Discovery.svc"), DisplayName = "United Kingdom", ShortName = "GBR" , GeoCode = "GBR" });
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm.appsplatform.us/XRMServices/2011/Discovery.svc"), DisplayName = "US Gov DoD", ShortName = "DoD", GeoCode= "DOD", RequiresRegionalDiscovery = true, RegionalGlobalDiscoveryServer = new Uri("https://globaldisco.crm.appsplatform.us")});
			_OSDPServers.Add(new CdsDiscoveryServer() { DiscoveryServer = new Uri("https://disco.crm.microsoftdynamics.us/XRMServices/2011/Discovery.svc"), DisplayName = "US Gov High", ShortName = "USG", GeoCode = "USG" , RequiresRegionalDiscovery = true, RegionalGlobalDiscoveryServer = new Uri("https://globaldisco.crm.microsoftdynamics.us")});

#if DEBUG
				var internalEnvInfo = new InternalEnvInfo();
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.CrmLiveTieOSDP));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.CrmIntOSDP));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.CrmLiveTodayDebugOSDP));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.CrmLiveTodayDebugLIVE));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.Crm1BoxTest));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.CrmTIP));
			_OSDPServers.Add(new CdsDiscoveryServer(internalEnvInfo.Crm2LiveTie));
#endif
		}

		/// <summary>
		/// Parses an OrgURI to determine what the supporting discovery server is.
		/// </summary>
		/// <param name="orgUri"></param>
		/// <returns></returns>
		public CdsDiscoveryServer GetServerByOrgUrl ( Uri orgUri )
		{
			if (orgUri == null)
				return null;
			// remove the https://disco from the request. 
			string domainUri = orgUri.GetComponents(UriComponents.Host, UriFormat.UriEscaped);
			if (_OSDPServers != null)
			{
				var rslts = _OSDPServers.Where(w => w.DiscoveryServer.ToString().Contains(domainUri));
				if (rslts.Count() > 0)
				{
					return rslts.FirstOrDefault();
				}
				else
					return null;
			}
			else
				return null;
		}

		/// <summary>
		/// Finds a Server by Name in the List or return null.
		/// </summary>
		/// <param name="shortName">Short Name of the server you are looking for</param>
		/// <returns>CdsDiscoveryServer Data or Null</returns>
		public CdsDiscoveryServer GetServerByShortName(string shortName)
		{
			if (_OSDPServers != null)
				return _OSDPServers.FirstOrDefault(i => i.ShortName.Equals(shortName, StringComparison.CurrentCultureIgnoreCase));
			else
				return null;
		}

		/// <summary>
		/// Finds a Server Info by GEO Code.  Note NorthAmerica and GCC cannot be located this way.
		/// to use with NA you must short name,  NorthAmerica for NAM and NorthAmerica2 for GCC.
		/// </summary>
		/// <param name="geoCode">GEO of Discovery Instance you are looking for</param>
		/// <returns>CdsDiscoveryServer Data or Null</returns>
		public CdsDiscoveryServer GetServerByGeo(string geoCode)
		{
			if (_OSDPServers != null)
				return _OSDPServers.FirstOrDefault(i => i.GeoCode.Equals(geoCode, StringComparison.CurrentCultureIgnoreCase));
			else
				return null;
		}

		/// <summary>
		/// Finds the server short name by server uri
		/// </summary>
		/// <param name="serverDisplayName">Name of the Server to find</param>
		/// <returns></returns>
		public string GetServerShortNameByDisplayName(string serverDisplayName)
		{
			try
			{
				if (serverDisplayName.Equals("User Defined Org Detail"))
					return null;

				if (_OSDPServers != null)
					return _OSDPServers.FirstOrDefault(i => i.DisplayName.Equals(serverDisplayName, StringComparison.CurrentCultureIgnoreCase)).ShortName;
			}
			catch (Exception Ex)
			{
				logger.Log(string.Format(CultureInfo.InvariantCulture, "Failed to find Short Name for {0}", serverDisplayName), System.Diagnostics.TraceEventType.Error, Ex);
			}
			return null;
		}

		#region INotifyPropertyChanged
		/// <summary>
		/// Raised when a property changes
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged(String info)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(info));
			}
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
					if (logger != null)
						logger.Dispose();

					if (_OSDPServers != null)
						_OSDPServers.Clear();

				}

				_OSDPServers = null;

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

	#region CrmOnlineDiscoServerListing for WPF Controls

	/// <summary>
	/// Describes a discovery server that can be used to determine what organizations a user is a member of.
	/// </summary>
	public class CdsDiscoveryServer : INotifyPropertyChanged
	{
		#region Properties

		private string _DisplayName = string.Empty;
		private string _ShortName = string.Empty;
		private Uri _DiscoveryServer = null;
		private bool _RequiresRegionalDiscovery = false;
		private Uri _RegionalGlobalDiscovery = null;
		private string _GeoCode = string.Empty;

		/// <summary>
		/// Display name of the Discovery Server
		/// </summary>
		public string DisplayName { get { return _DisplayName; } set { if (value != _DisplayName) _DisplayName = value; NotifyPropertyChanged("DisplayName"); } }
		/// <summary>
		/// Short name of the Discovery Server, this is used to store the server in the users config for later use.
		/// </summary>
		public string ShortName { get { return _ShortName; } set { if (value != _ShortName) _ShortName = value; NotifyPropertyChanged("ShortName"); } }
		/// <summary>
		/// Discovery server Uri, this is the URI necessary to connect to the Discovery server
		/// </summary>
		public Uri DiscoveryServer { get { return _DiscoveryServer; } set { if (value != _DiscoveryServer) _DiscoveryServer = value; NotifyPropertyChanged("DiscoveryServer"); } }
		/// <summary>
		/// When true, the global discovery server cannot be used to locate this instance, it must be a regional discovery query
		/// </summary>
		public bool RequiresRegionalDiscovery { get { return _RequiresRegionalDiscovery; } set { if (value != _RequiresRegionalDiscovery) _RequiresRegionalDiscovery = value; NotifyPropertyChanged("IsRestricted"); } }
		/// <summary>
		/// Server used to override the regional discovery server, if present its treated as using the Global Discovery server
		/// </summary>
		public Uri RegionalGlobalDiscoveryServer { get { return _RegionalGlobalDiscovery; } set { if (value != _RegionalGlobalDiscovery) _RegionalGlobalDiscovery = value; NotifyPropertyChanged("RegionalGlobalDiscoveryServer"); } }
		/// <summary>
		/// Geo that hosts this Disco endpoint
		/// </summary>
		public string GeoCode { get { return _GeoCode; } set { if (value != _GeoCode) _GeoCode = value; NotifyPropertyChanged("GeoCode"); } }
		#endregion

		/// <summary>
		/// Default constructor
		/// </summary>
		public CdsDiscoveryServer()
		{ }

		/// <summary>
		/// Accepts a Server Info object
		/// </summary>
		public CdsDiscoveryServer(ServerInfo serverInfo)
		{
			// load info from a server Info object. 
			DiscoveryServer = new Uri(serverInfo.DiscoveryServer);
			DisplayName = serverInfo.DisplayName;
			ShortName = serverInfo.ShortName;
			RequiresRegionalDiscovery = serverInfo.RequiresRegionalDiscovery;
			RegionalGlobalDiscoveryServer = serverInfo.RegionalGlobalDiscoveryUri;
			GeoCode = serverInfo.GeoCode;
		}

		#region INotifyPropertyChanged
		/// <summary>
		/// Raised when a property changes
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged(String info)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(info));
			}
		}
		#endregion
	}

	#endregion
}
