using System;
using System.Collections.Generic;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Helper class that gets/sets the data for connecting to debug online env.
	/// </summary>
	public sealed class TestingHelper
	{
		/// <summary>
		/// Stores the string identifier for the currently selected online region(the one selected from online region drop down).
		/// </summary>
		public string SelectedOption
		{
			get
			{
				return _selectedOption;
			}
			set
			{
				_selectedOption = (value != null) ? value.ToLowerInvariant() : value;
			}
		}

		/// <summary>
		/// Returns an instance of this class.
		/// </summary>
		public static TestingHelper Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new TestingHelper();
				}

				return _instance;
			}
		}

		/// <summary>
		/// Method to check if currently selected online region in UI is custom debug env. or not.
		/// </summary>
		/// <returns></returns>
		public bool IsDebugEnvSelected()
		{
			return !String.IsNullOrEmpty(this.SelectedOption)
				&& (this._servers.ContainsKey(this.SelectedOption) || this._OSDPservers.ContainsKey(this.SelectedOption));
		}

		/// <summary>
		/// Gets the issuer Uri for the selected debug env.
		/// </summary>
		/// <returns></returns>
		public string GetIssuerUriForSelectedEnv()
		{
			if (!String.IsNullOrEmpty(this.SelectedOption))
			{
				if (_servers.ContainsKey(this.SelectedOption))
				{
					return _servers[this.SelectedOption];
				}
				else if (_OSDPservers.ContainsKey(this.SelectedOption))
				{
					return _OSDPservers[this.SelectedOption];
				}
			}

			return this.defaultIssuerUri;
		}

		#region Private
		private TestingHelper()
		{
			this.Initialize();
		}

		private void Initialize()
		{
			if (_servers == null)
			{
				_servers = new Dictionary<string, string>();
			}

			if (_OSDPservers == null)
			{
				_OSDPservers = new Dictionary<string, string>();
			}
#if DEBUG
			// The key in below dict(s) should be in sync with environment info(see class InternalEnvInfo below).
			_servers.Add("crmlivedebug", "login.live-int.com");
			_servers.Add("crmlivetielive", this.defaultIssuerUri);

			_OSDPservers.Add("crmlivetieosdp", this.defaultIssuerUri);
			_OSDPservers.Add("crmintosdp", this.defaultIssuerUri);
#endif
		}

		/// <summary>
		/// These dictionaries contain the mapping of shortName to the issuerUri for Live/OSDP servers.
		/// </summary>
		private Dictionary<string, string> _servers;
		private Dictionary<string, string> _OSDPservers;

		private string _selectedOption;
		private readonly string defaultIssuerUri = "login.live.com";
		private static TestingHelper _instance = null;
		#endregion Private
	}

	/// <summary>
	/// Data container for Live/OSDP debug env.
	/// </summary>
	public sealed class ServerInfo
	{
		/// <summary>
		/// Gives the discovery server url
		/// </summary>
		public string DiscoveryServer { get; set; }
		/// <summary>
		/// Gets/Sets the display name.
		/// </summary>
		public string DisplayName { get; set; }
		/// <summary>
		/// Gets/Sets the shortname(should be unique).
		/// </summary>
		public string ShortName { get; set; }

		/// <summary>
		/// Sets the restricted status of the instance. ( restricted means it is not in the global discovery servers )
		/// </summary>
		public bool RequiresRegionalDiscovery { get; set; }

		/// <summary>
		/// regional global discovery server
		/// </summary>
		public Uri RegionalGlobalDiscoveryUri { get; set; }

		/// <summary>
		/// Geo Code
		/// </summary>
		public string GeoCode { get; set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		public ServerInfo()
		{
			// Defaulting to true for Restricted access
			RequiresRegionalDiscovery = true;
			// defaulting to null 
			RegionalGlobalDiscoveryUri = null;
			// Default to null. 
			GeoCode = null;
		}
	}

#if DEBUG
	/// <summary>
	/// Class containing information about various debug environments
	/// </summary>
	public sealed class InternalEnvInfo
	{
		private ServerInfo _crmLiveTie;
		private ServerInfo _crmLiveDebug;
		private ServerInfo _crmLiveTieOSDP;
		private ServerInfo _crmIntOSDP;
		private ServerInfo _discocrmlivetodaydebugOSDP;
		private ServerInfo _discocrmlivetodaydebugLIVE;
		private ServerInfo _discocrm1boxtestOSDP;
		private ServerInfo _discocrmtipOSDP;
		private ServerInfo _discocrm2LiveTieOSDP;

		#region Live Specific
		/// <summary>
		/// Returns settings for CrmLiveTie
		/// </summary>
		public ServerInfo CrmLiveTie
		{
			get
			{
				if(_crmLiveTie == null)
				{
					_crmLiveTie = new ServerInfo()
					{
						DiscoveryServer = "https://dev.crm.crmlivetie.com/XRMServices/2011/Discovery.svc",
						DisplayName = "CrmLiveTIE LIVE",
						ShortName = "crmlivetielive"
					};
				}

				return _crmLiveTie;
			}
		}

		/// <summary>
		/// Returns settings for CrmLiveDebug
		/// </summary>
		public ServerInfo CrmLiveDebug
		{
			get
			{
				if (_crmLiveDebug == null)
				{
					_crmLiveDebug = new ServerInfo()
					{
						DiscoveryServer = "https://dev.crm.crmlivetoday.com/XRMServices/2011/Discovery.svc",
						DisplayName = "Crm Live(Debug)",
						ShortName = "crmlivedebug"
					};
				}

				return _crmLiveDebug;
			}
		}
		/// <summary>
		/// InternalDebug
		/// </summary>
		public ServerInfo CrmLiveTodayDebugLIVE
		{
			get
			{
				if (this._discocrmlivetodaydebugLIVE == null)
				{
					ServerInfo info = new ServerInfo
					{
						DiscoveryServer = "https://dev.crm.crmlivetoday.com/XRMServices/2011/Discovery.svc",
						DisplayName = "dev.crm.crmlivetoday.com(Debug)",
						ShortName = "devcrmlivetodaydebug"
					};
					this._discocrmlivetodaydebugLIVE = info;
				}
				return this._discocrmlivetodaydebugLIVE;
			}
		}

		#endregion Live Specific

		#region OSDP Specific
		/// <summary>
		/// Returns settings for CrmLiveTieOSDP
		/// </summary>
		public ServerInfo CrmLiveTieOSDP
		{
			get
			{
				if (_crmLiveTieOSDP == null)
				{
					_crmLiveTieOSDP = new ServerInfo()
					{
						DiscoveryServer = "https://disco.crm.crmlivetie.com/XRMServices/2011/Discovery.svc",
						DisplayName = "CrmLiveTIE OSDP",
						ShortName = "crmlivetieosdp"
					};
				}

				return _crmLiveTieOSDP;
			}
		}

		/// <summary>
		/// Returns settings for CrmIntOSDP
		/// </summary>
		public ServerInfo CrmIntOSDP
		{
			get
			{
				if (_crmIntOSDP == null)
				{
					_crmIntOSDP = new ServerInfo()
					{
						DiscoveryServer = "https://disco.crm.dynamics-int.com/XRMServices/2011/Discovery.svc",
						DisplayName = "Crm Int OSDP",
						ShortName = "crmintosdp"
					};
				}

				return _crmIntOSDP;
			}
		}
		/// <summary>
		/// InternalDebug
		/// </summary>
		public ServerInfo CrmLiveTodayDebugOSDP
		{
			get
			{
				if (this._discocrmlivetodaydebugOSDP == null)
				{
					ServerInfo info = new ServerInfo
					{
						DiscoveryServer = "https://disco.crm.crmlivetoday.com/XRMServices/2011/Discovery.svc",
						DisplayName = "disco.crm.crmlivetoday.com(Debug)",
						ShortName = "discocrmlivetodaydebug"
					};
					this._discocrmlivetodaydebugOSDP = info;
				}
				return this._discocrmlivetodaydebugOSDP;
			}
		}

		/// <summary>
		/// this is the connect infor for the 1box config for EDOG env.
		/// </summary>
		public ServerInfo Crm1BoxTest
		{
			get
			{
				if (this._discocrm1boxtestOSDP == null)
				{
					ServerInfo info = new ServerInfo
					{
						DiscoveryServer = "https://disco.crm.1boxtest.com/XRMServices/2011/Discovery.svc",
						DisplayName = "disco.crm.1boxtest.com(Debug)",
						ShortName = "discocrm1boxtestdebug"
					};
					this._discocrm1boxtestOSDP = info;
				}
				return this._discocrm1boxtestOSDP;
			}
		}

		/// <summary>
		/// This is support the TIP environment.
		/// </summary>
		public ServerInfo CrmTIP
		{
			get
			{
				if (this._discocrmtipOSDP == null)
				{
					ServerInfo info = new ServerInfo
					{
						DiscoveryServer = "https://disco.crm10.dynamics.com/XRMServices/2011/Discovery.svc",
						DisplayName = "Production Test Region",
						ShortName = "tip",
						RequiresRegionalDiscovery = true,
						RegionalGlobalDiscoveryUri = new Uri("https://globaldisco.crm10.dynamics.com"),
						GeoCode="TIP"
					};
					this._discocrmtipOSDP = info;
				}
				return this._discocrmtipOSDP;
			}
		}

		/// <summary>
		/// This is support the CRM2.CRMLiveTie environment.
		/// </summary>
		public ServerInfo Crm2LiveTie
		{
			get
			{
				if (this._discocrm2LiveTieOSDP == null)
				{
					ServerInfo info = new ServerInfo
					{
						DiscoveryServer = "https://disco.crm2.crmlivetie.com/XRMServices/2011/Discovery.svc",
						DisplayName = "disco.crm2.crmlivetie.com(Debug)",
						ShortName = "discocrm2crmlivetiecomdebug"
					};
					this._discocrm2LiveTieOSDP = info;
				}
				return this._discocrm2LiveTieOSDP;
			}
		}

		#endregion OSDP Specific
	}
#endif
}
