using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk.Discovery;
using System.Windows;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Model
{
	/// <summary>
	/// Describes the Collection of Orgs that a user may select from. This is used to display the list to the user
	/// </summary>
	public class CrmOrgList : INotifyPropertyChanged
	{
		private ObservableCollection<OrgByServer> _orgsList = new ObservableCollection<OrgByServer>();
		/// <summary>
		/// List of Orgs
		/// </summary>
		public ObservableCollection<OrgByServer> OrgsList { get { return _orgsList; } set { if (value != _orgsList) _orgsList = value; NotifyPropertyChanged("OrgsList"); } }

		/// <summary>
		/// Dictionary of Orgs. 
		/// </summary>
		public CrmOrgList()
		{
			if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
			{
				// sample mode
				if (_orgsList == null)
					_orgsList = new ObservableCollection<OrgByServer>();

				_orgsList.Add(new OrgByServer() { DiscoveryServerName = "North America", OrgDetail = new OrganizationDetail() { FriendlyName = "MyOrg1", UniqueName = "" } });
				_orgsList.Add(new OrgByServer() { DiscoveryServerName = "North America", OrgDetail = new OrganizationDetail() { FriendlyName = "MyOrg2", UniqueName = "" } });
				_orgsList.Add(new OrgByServer() { DiscoveryServerName = "EMEA", OrgDetail = new OrganizationDetail() { FriendlyName = "MyOrg3", UniqueName = "" } });
				_orgsList.Add(new OrgByServer() { DiscoveryServerName = "APAC", OrgDetail = new OrganizationDetail() { FriendlyName = "MyOrg4", UniqueName = "" } });
				_orgsList.Add(new OrgByServer() { DiscoveryServerName = "PREMISE", OrgDetail = new OrganizationDetail() { FriendlyName = "MyOrg5", UniqueName = "" } });
			}
		}
		
		/// <summary>
		/// returns ( if possible ) the org detail for a given organization name from the list of orgs in discovery 
		/// </summary>
		/// <param name="orgList">OrgList to Parse though</param>
		/// <param name="organizationName">Name to find</param>
		/// <returns>Found Organization Instance or Null</returns>
		public static OrgByServer DeterminOrgDataFromOrgInfo(CrmOrgList orgList, string organizationName)
		{
			OrgByServer orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.UniqueName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
			if (orgDetail == null)
				orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.FriendlyName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

			// still not found... try by URI name. 
			if (orgDetail == null)
			{
				string formatedOrgName = string.Format("://{0}.", organizationName).ToLowerInvariant();
				orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.Endpoints[EndpointType.WebApplication].Contains(formatedOrgName)).FirstOrDefault();
			}
			return orgDetail;
		}

		#region INotifyPropertyChanged
		/// <summary/>
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
