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

namespace Microsoft.Xrm.Tooling.Connector.Behaviors
{
	public static class BCrmServiceClient
	{
		private static Dictionary<Type, OrganizationResponse> lstOrgResp = new Dictionary<Type, OrganizationResponse>();
		private static Dictionary<Type, OrganizationRequest> lstOrgReq = new Dictionary<Type, OrganizationRequest>();

		public static void MockCrmCommandExecute()
		{
			MCrmServiceClient.AllInstances.CrmCommand_ExecuteOrganizationRequestString =
				(serviceActionObj, req, str) =>
				{
					if (!lstOrgReq.ContainsKey(req.GetType()))
						lstOrgReq.Add(req.GetType(), req);
					if (lstOrgResp.ContainsKey(req.GetType()))
						return lstOrgResp[req.GetType()];

					string reqtype = req.GetType().Name;
					switch (reqtype)
					{
						case "UpdateRequest":
							return new UpdateResponse();
						case "ParseImportRequest":
							return new ParseImportResponse();
						case "ImportRecordsImportRequest":
							return new ImportRecordsImportResponse();
						case "TransformImportRequest":
							return new TransformImportResponse();
						case "CloseQuoteRequest":
							return new CloseQuoteResponse();
						case "WinOpportunityRequest":
							return new WinOpportunityResponse();
						case "CloseIncidentRequest":
							return new CloseIncidentResponse();
						case "RetrieveEntityRequest":
							return new RetrieveEntityResponse();
						case "SetStateRequest":
							return new SetStateResponse();
						case "UpdateOptionValueRequest":
							return new UpdateOptionValueResponse();
						case "RetrieveAvailableLanguagesRequest":
							return new RetrieveAvailableLanguagesResponse();
						case "InsertOptionValueRequest":
							return new InsertOptionValueResponse();
						case "PublishXmlRequest":
							return new PublishXmlResponse();
						case "AddToQueueRequest":
							return new AddToQueueResponse();
						case "AssociateEntitiesRequest":
							return new AssociateEntitiesResponse();
						case "AssociateRequest":
							return new AssociateResponse();
						case "AssignRequest":
							return new AssignResponse();
						case "SendEmailRequest":
							return new SendEmailResponse();
						case "CancelSalesOrderRequest":
							return new CancelSalesOrderResponse();
						case "DisassociateEntitiesRequest":
							return new DisassociateEntitiesResponse();
						case "InstallSampleDataRequest":
							return new InstallSampleDataResponse();
						case "ImportSolutionRequest":
							return new ImportSolutionResponse();
						case "UninstallSampleDataRequest":
							return new UninstallSampleDataResponse();
						case "DeleteRequest":
							return new DeleteResponse();
						case "CreateRequest":
							return new CreateResponse();
						case "RetrieveMultipleRequest":
							return new RetrieveMultipleResponse();
						case "ExecuteMultipleRequest":
							AddResponse(typeof(ExecuteMultipleRequest), new ExecuteMultipleResponse());
							return lstOrgResp[typeof(ExecuteMultipleRequest)];
						default:
							return new OrganizationResponse();
					}
				};
		}

		public static void AddResponse(Type type, OrganizationResponse response)
		{
			lstOrgResp.Add(type, response);
		}
		public static OrganizationResponse GetResponse(Type type)
		{
			if (lstOrgResp.ContainsKey(type))
				return lstOrgResp[type];
			return null;
		}

		public static OrganizationRequest GetRequest(Type type)
		{
			if (lstOrgReq.ContainsKey(type))
				return lstOrgReq[type];
			return null;
		}

		public static void ClearResponses()
		{
			lstOrgResp.Clear();
		}
		public static void ClearRequest()
		{
			lstOrgReq.Clear();
		}

		public static void MockGetPickListElementFromMetadataEntity()
		{
			MCrmServiceClient.AllInstances.GetPickListElementFromMetadataEntityStringString = (serviceActionObj, str, str1) =>
			{
				CrmServiceClient.PickListMetaElement metadata = new CrmServiceClient.PickListMetaElement();
				CrmServiceClient.PickListItem item = new CrmServiceClient.PickListItem();
				item.DisplayLabel = "completed";
				metadata.Items.Add(item);
				return metadata;
			};
		}

	}
}
