using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Tooling.Connector.Moles;
using Microsoft.Xrm.Sdk.Messages.Moles;
using Microsoft.Crm.Sdk.Messages.Moles;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Moles;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.Xrm.Tooling.Connector.Behaviors
{
	public static class BCrmWebSvc
	{
		public static void MockCrmSvc(OrganizationServiceProxy proxy)
		{
			MCrmWebSvc.AllInstances.CrmSvcGet = (obj) => proxy;           
		}
		public static void MockDoLogin()
		{
			MCrmWebSvc.AllInstances.DoLogin = (obj) => { return true; };
		}
		public static void MockOrganizationVersion()
		{
			MCrmWebSvc.AllInstances.OrganizationVersionGet = (objWebsvc) => { return new Version("5.0.9690.3000"); }; 
		}
	}
}
