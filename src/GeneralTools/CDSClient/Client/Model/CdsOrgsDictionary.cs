using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk.Discovery;


namespace Microsoft.PowerPlatform.Cds.Client.Model
{
	/// <summary>
	/// Describes a Single Organization returned from a CRM Discovery server
	/// </summary>
	public sealed class CdsOrgByServer : INotifyPropertyChanged
	{
		#region Properties

		private OrganizationDetail _OrgDetail = new OrganizationDetail();
		private string _DiscoveryServerName = string.Empty;


		/// <summary>
		/// This is the display name for the organization that a user sees when working in CRM
		/// </summary>
		public string FriendlyName { get { return _OrgDetail != null ? _OrgDetail.FriendlyName : string.Empty; } }

		/// <summary>
		/// This is the actual name for the organization in CRM, and is required to connect to CRM
		/// </summary>
		public string UniqueOrgName { get { return _OrgDetail != null ? _OrgDetail.UniqueName : string.Empty; } }

		/// <summary>
		/// This is the actual name for the organization in CRM, and is required to connect to CRM
		/// </summary>
		public string UrlHostName { get { return _OrgDetail != null ? _OrgDetail.UrlName : string.Empty; } }

		/// <summary>
		/// This is the details of the Organization, returned directly from CRM
		/// </summary>
		public OrganizationDetail OrgDetail { get { return _OrgDetail; } set { if (value != _OrgDetail) _OrgDetail = value; NotifyPropertyChanged("OrdDetail"); } }

		/// <summary>
		/// This is the name assigned to the Discovery Server, this is used to visual separate organizations returned by Discovery server used, or Premise solutions.
		/// </summary>
		public string DiscoveryServerName { get { return _DiscoveryServerName; } set { if (value != _DiscoveryServerName) _DiscoveryServerName = value; NotifyPropertyChanged("DiscoveryServerName"); } }

		/// <summary>
		/// This is the URI needed to connect to the Organization
		/// </summary>
		public Uri DiscoveryServer
		{
			get
			{
				if (_OrgDetail != null)
					if (Uri.IsWellFormedUriString(_OrgDetail.Endpoints[EndpointType.OrganizationService], UriKind.RelativeOrAbsolute))
						return new Uri(_OrgDetail.Endpoints[EndpointType.OrganizationService]);
				return null;
			}
		}


		#endregion


		#region INotifyPropertyChanged
		/// <summary>
		/// WCF EVENT hook
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

	/// <summary>
	/// Describes the Collection of Orgs that a user may select from. This is used to display the list to the user
	/// </summary>
	public sealed class CdsOrgList : INotifyPropertyChanged
	{
		private ObservableCollection<CdsOrgByServer> _orgsList = new ObservableCollection<CdsOrgByServer>();
		/// <summary>
		/// List of Orgs
		/// </summary>
		public ObservableCollection<CdsOrgByServer> OrgsList { get { return _orgsList; } internal set { if (value != _orgsList) _orgsList = value; NotifyPropertyChanged("OrgsList"); } }

		/// <summary>
		/// Container for CRM Orgs List.
		/// </summary>
		public CdsOrgList()
		{ }

		#region INotifyPropertyChanged
		/// <summary>
		/// WPF EVENT HOOK
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
}
