//===================================================================================
// Microsoft – subject to the terms of the Microsoft EULA and other agreements
// Microsoft.PowerPlatform.Dataverse.WebResourceUtility
// copyright 2003-2012 Microsoft Corp.
//
// Retrieve Xml resources from CRM
//
//===================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Diagnostics;

namespace Microsoft.PowerPlatform.Dataverse.WebResourceUtility
{
	/// <summary>
	/// This class is used to access and retrieve web 
	/// </summary>
	public class XmlResources
	{
		#region Vars

		/// <summary>
		/// Dataverse Connection
		/// </summary>
		private ServiceClient _serviceClient;

		/// <summary>
		/// Tracer
		/// </summary>
		private TraceLogger _logEntry;

		#endregion

		/// <summary>
		/// Constructs a class used to retrieve an XML resources from Dataverse..
		/// </summary>
		/// <param name="serviceClient">Initialized copy of a ServiceClient object</param>
		public XmlResources(ServiceClient serviceClient)
		{
			_serviceClient = serviceClient;
			_logEntry = new TraceLogger(string.Empty);
		}

		/// <summary>
		/// Returns Xml Resource from Dataverse 
		/// </summary>
		/// <param name="webResourceName">Xml Resource Name requested</param>
		/// <returns>Returns Null if the Xml is not found, or Xml document as text.</returns>
		public string GetXmlFromCRMWebResource(string webResourceName)
		{
			#region PreCheck
			_logEntry.ResetLastError();  // Reset Last Error 
			if (_serviceClient == null || string.IsNullOrWhiteSpace(webResourceName))
			{
				return null;
			}
			#endregion

			string outData = null;
			// Get the Web Resources from CRM 
			var SearchFilter = new List<ServiceClient.DataverseSearchFilter>();
			var filter1 = new ServiceClient.DataverseSearchFilter()
			{
				SearchConditions = new List<ServiceClient.DataverseFilterConditionItem>()
					{
						new ServiceClient.DataverseFilterConditionItem() { FieldName = "name", FieldOperator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, FieldValue=webResourceName },
						new ServiceClient.DataverseFilterConditionItem() { FieldName = "webresourcetype", FieldOperator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, FieldValue=4 }
					},
				FilterOperator = Microsoft.Xrm.Sdk.Query.LogicalOperator.And
			};

			SearchFilter.Add(filter1);
			var rslts = _serviceClient.GetEntityDataBySearchParams("webresource", SearchFilter, ServiceClient.LogicalSearchOperator.None,
				new List<string>() { "content", "webresourcetype" });
			if (rslts != null && rslts.Count > 0)
			{
				// Found it.. Get the first one. 
				var workingWith = rslts.FirstOrDefault().Value;
				return _serviceClient.GetDataByKeyFromResultsSet<string>(workingWith, "content");
			}
			else
				_logEntry.Log(string.Format("Web Resource Xml file not found, Looking for : {0}", webResourceName), TraceEventType.Error);
			return outData;
		}

	}
}
