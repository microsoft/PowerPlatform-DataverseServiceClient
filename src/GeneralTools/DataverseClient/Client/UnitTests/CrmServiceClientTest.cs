[assembly: Microsoft.Moles.Framework.MoledType(typeof(System.IO.File))]
namespace Microsoft.Xrm.Tooling.Connector.UnitTests
{
	using System;
	using System.Collections.Generic;
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using Microsoft.Xrm.Sdk.Client.Moles;
	using Microsoft.Xrm.Tooling.Connector.Moles;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Messages.Moles;
	using Microsoft.Crm.Sdk.Messages.Moles;
	using Microsoft.Crm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Query;
	using Microsoft.Xrm.Sdk.Metadata;
	using System.IO.Moles;
	using Microsoft.Xrm.Sdk.Metadata.Moles;
	using Microsoft.Xrm.Tooling.Connector.Behaviors;
	using Microsoft.Crm.UnitTest.Framework;


	[TestClass]
	public class CrmServiceClientTest
	{
		private MOrganizationServiceProxy orgProxy;
		CrmServiceClient crmaction;

		[TestInitialize]
		public void TestInitialize()
		{
			this.orgProxy = new MOrganizationServiceProxy();
			orgProxy.BehaveAsDefaultValue();
			BCrmWebSvc.MockCrmSvc(orgProxy);
			BCrmWebSvc.MockDoLogin();
			BCrmWebSvc.MockOrganizationVersion();
			crmaction = new CrmServiceClient(this.orgProxy);
			MDynamicEntityUtility.AllInstances.GetAttributeDataByEntityStringGuidStringArray = (dynamicEntUtl, str, inguid, arrstr) =>
			{
				return new List<AttributeData>();
			};
			MetadataUtility.ClearCachedEntityMetadata("account");
		}

		[TestCleanup]
		public void TestCleanUp()
		{
			BCrmServiceClient.ClearResponses();
			BCrmServiceClient.ClearRequest();
		}

		[TestMethod]
		public void ReleaseBatchInfoByIdTest()
		{
			var px = new MOrganizationServiceProxy();
			px.BehaveAsDefaultValue();
			CrmServiceClient crmaction = new CrmServiceClient(px);
			Guid sampleGuid = Guid.NewGuid();
			bool methodCalled = false;
			MBatchManager.AllInstances.RemoveBatchGuid = (objBatchManager, guid) => { methodCalled = true; Assert.AreEqual(guid, sampleGuid); };
			crmaction.ReleaseBatchInfoById(sampleGuid);
			Assert.IsTrue(methodCalled);
		}

		[TestMethod]
		public void CreateNewRecordTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MCreateResponse orgResp = new MCreateResponse();
			orgResp.idGet = () => respId;
			BCrmServiceClient.AddResponse(typeof(CreateRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("name", new CrmDataTypeWrapper("CrudTestAccount", CrmFieldType.String));
			newFields.Add("accountnumber", new CrmDataTypeWrapper("12345", CrmFieldType.String));
			newFields.Add("telephone1", new CrmDataTypeWrapper("555-555-5555", CrmFieldType.String));
			newFields.Add("donotpostalmail", new CrmDataTypeWrapper(true, CrmFieldType.CrmBoolean));
			Guid result = crmaction.CreateNewRecord("account", newFields);
			Assert.AreEqual(result, respId);
			result = crmaction.CreateNewRecord("", new Dictionary<string, CrmDataTypeWrapper>());
			Assert.AreNotEqual(result, respId);
			orgReq = BCrmServiceClient.GetRequest(typeof(CreateRequest));
			Assert.IsNotNull(((CreateRequest)orgReq).Target);
		}

		[TestMethod]
		public void CreateAnnotationTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MCreateResponse orgResp = new MCreateResponse();
			orgResp.idGet = () => respId;
			BCrmServiceClient.AddResponse(typeof(CreateRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			MCrmWebSvc.AllInstances.CrmUserGet = (websvc) => new WhoAmIResponse();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("name", new CrmDataTypeWrapper("CrudTestAccount", CrmFieldType.String));
			newFields.Add("accountnumber", new CrmDataTypeWrapper("12345", CrmFieldType.String));
			newFields.Add("telephone1", new CrmDataTypeWrapper("555-555-5555", CrmFieldType.String));
			newFields.Add("donotpostalmail", new CrmDataTypeWrapper(true, CrmFieldType.CrmBoolean));
			Guid result = crmaction.CreateAnnotation("account", respId, newFields);
			Assert.AreEqual(result, respId);
			result = crmaction.CreateAnnotation("", respId, new Dictionary<string, CrmDataTypeWrapper>());
			Assert.AreNotEqual(result, respId);
			orgReq = BCrmServiceClient.GetRequest(typeof(CreateRequest));
			Assert.IsNotNull(((CreateRequest)orgReq).Target);
		}

		[TestMethod]
		public void CreateBatchOperationRequestTest()
		{
			Guid respId = Guid.NewGuid();
			MBatchManager.AllInstances.CreateNewBatchStringBooleanBoolean = (objbatchManager, str, bln, bln2) => { return respId; };
			MCrmServiceClient.AllInstances.IsBatchOperationsAvailableGet = (serviceActionObj) => { return true; };
			Guid result = crmaction.CreateBatchOperationRequest("TestBatch");
			Assert.AreEqual(result, respId);
		}
		[TestMethod]
		public void CreateEntityAssociationTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			bool result = crmaction.CreateEntityAssociation("account", respId, "contact", respId, "somerelation");
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(AssociateEntitiesRequest));
			Assert.IsNotNull(((AssociateEntitiesRequest)orgReq).Moniker1);
			Assert.IsNotNull(((AssociateEntitiesRequest)orgReq).Moniker2);
			Assert.AreEqual(((AssociateEntitiesRequest)orgReq).RelationshipName, "somerelation");
			result = crmaction.CreateEntityAssociation("", respId, "", respId, "");
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void CreateMultiEntityAssociationTest()
		{
			OrganizationRequest orgReq = null;
			Guid accountId = Guid.NewGuid();
			Guid contactId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			List<Guid> lst = new List<Guid>();
			lst.Add(accountId);
			lst.Add(contactId);
			bool result = crmaction.CreateMultiEntityAssociation("account", contactId, "contact", lst, "some rel");
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(AssociateRequest));
			Assert.IsNotNull(((AssociateRequest)orgReq).RelatedEntities);
			Assert.IsNotNull(((AssociateRequest)orgReq).Target);
		}

		[TestMethod]
		public void AssignEntityToUserTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			bool result = crmaction.AssignEntityToUser(respId, "account", respId);
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(AssignRequest));
			Assert.IsNotNull(((AssignRequest)orgReq).Assignee);
			Assert.IsNotNull(((AssignRequest)orgReq).Target);

		}

		[TestMethod]
		public void SendSingleEmailTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			bool result = crmaction.SendSingleEmail(respId, "tokn");
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(SendEmailRequest));
			Assert.IsNotNull(((SendEmailRequest)orgReq).EmailId);
		}

		[TestMethod]
		public void CancelSalesOrderTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("name", new CrmDataTypeWrapper("CrudTestAccount", CrmFieldType.String));
			newFields.Add("accountnumber", new CrmDataTypeWrapper("12345", CrmFieldType.String));
			Guid result = crmaction.CancelSalesOrder(respId, newFields);
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(CancelSalesOrderRequest));
			Assert.IsNotNull(((CancelSalesOrderRequest)orgReq).OrderClose);
		}

		[TestMethod]
		public void GetEntityDisplayNamePluralTest()
		{
			Guid respId = Guid.NewGuid();
			MRetrieveEntityResponse orgResp = new MRetrieveEntityResponse();
			orgResp.EntityMetadataGet = () => { return new EntityMetadata(); };
			BCrmServiceClient.AddResponse(typeof(RetrieveEntityRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			string result = crmaction.GetEntityDisplayNamePlural("account");
			Assert.IsNotNull(result);
		}

		[TestMethod]
		public void CreateOrUpdatePickListElementTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MRetrieveEntityResponse rtventResp = new MRetrieveEntityResponse();
			MEntityMetadata entmt = new MEntityMetadata();
			entmt.IsCustomEntityGet = () => { return true; };
			rtventResp.EntityMetadataGet = () => { return entmt; };
			BCrmServiceClient.AddResponse(typeof(RetrieveEntityRequest), rtventResp);
			BCrmServiceClient.MockCrmCommandExecute();
			List<AttributeData> lst1 = new List<AttributeData>();
			lst1.Add(new AttributeData());
			MDynamicEntityUtility.AllInstances.GetAttributeDataByEntityStringStringArray = (obj, str, arrstr) => { return lst1; };
			List<LocalizedLabel> lst = new List<LocalizedLabel>();
			lst.Add(new LocalizedLabel());
			BCrmServiceClient.MockGetPickListElementFromMetadataEntity();
			bool result = crmaction.CreateOrUpdatePickListElement("account", "name", lst, 1, true);
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(InsertOptionValueRequest));
			Assert.IsNotNull(((InsertOptionValueRequest)orgReq).Value);
		}

		[TestMethod]
		public void AddEntityToQueueTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MRetrieveMultipleResponse rtvResp = new MRetrieveMultipleResponse();
			EntityCollection entcol = new EntityCollection();
			entcol.Entities.Add(new Entity());
			rtvResp.EntityCollectionGet = () => { return entcol; };
			BCrmServiceClient.AddResponse(typeof(RetrieveMultipleRequest), rtvResp);
			BCrmServiceClient.MockCrmCommandExecute();
			MCrmServiceClient.AllInstances.GetDataByKeyFromResultsSetDictionaryOfStringObjectString<Guid>((objsvcact, dct, str) => { return respId; });
			bool result = crmaction.AddEntityToQueue(respId, "account", "que", respId);
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(AddToQueueRequest));
			Assert.IsNotNull(((AddToQueueRequest)orgReq).DestinationQueueId);
			Assert.IsNotNull(((AddToQueueRequest)orgReq).Target);
		}

		[TestMethod]
		public void DeleteEntityAssociationTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			List<Guid> lst = new List<Guid>();
			lst.Add(respId);
			bool result = crmaction.DeleteEntityAssociation("account", respId, "contact", respId, "somerelation");
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(DisassociateEntitiesRequest));
			Assert.IsNotNull(((DisassociateEntitiesRequest)orgReq).Moniker1);
			Assert.IsNotNull(((DisassociateEntitiesRequest)orgReq).Moniker2);
			Assert.AreEqual(((DisassociateEntitiesRequest)orgReq).RelationshipName, "somerelation");
		}

		[TestMethod]
		public void GetBatchOperationIdRequestByNameTest()
		{
			Guid respId = Guid.NewGuid();
			RequestBatch rqBatch = new RequestBatch();
			rqBatch.BatchId = respId;
			MBatchManager.AllInstances.GetRequestBatchByNameString = (objsvcmgr, str) => { return rqBatch; };
			Guid result = crmaction.GetBatchOperationIdRequestByName("TestBatch");
			Assert.AreEqual(result, respId);
		}


		[TestMethod]
		public void GetBatchRequestAtPositionTest()
		{
			Guid respId = Guid.NewGuid();
			RequestBatch rqBatch = new RequestBatch();
			rqBatch.BatchId = respId;
			List<BatchItemOrganizationRequest> batchItems = new List<BatchItemOrganizationRequest>();
			BatchItemOrganizationRequest itmOrgRqs = new BatchItemOrganizationRequest();
			itmOrgRqs.Request = new OrganizationRequest();
			batchItems.Add(itmOrgRqs);
			rqBatch.BatchItems = batchItems;
			MCrmServiceClient.AllInstances.GetBatchByIdGuid = (objsvcAction, guid) => { return rqBatch; };
			OrganizationRequest result = crmaction.GetBatchRequestAtPosition(respId, 0);
			Assert.IsNotNull(result);
		}


		[TestMethod]
		public void GetActivitiesByTest()
		{
			OrganizationRequest orgReq = null;
			MRollupResponse orgResp = new MRollupResponse();
			EntityCollection entcol = new EntityCollection();
			entcol.Entities.Add(new Entity());
			orgResp.EntityCollectionGet = () => { return entcol; };
			BCrmServiceClient.AddResponse(typeof(RollupRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			Guid respId = Guid.NewGuid();
			Guid entId = Guid.NewGuid();
			List<string> fldList = new List<string>();
			fldList.Add("name");
			fldList.Add("source");
			fldList.Add("importmapid");
			CrmServiceClient.LogicalSearchOperator lgsrc = new CrmServiceClient.LogicalSearchOperator();
			List<CrmServiceClient.CrmSearchFilter> fltList = new List<CrmServiceClient.CrmSearchFilter>();
			fltList.Add(new CrmServiceClient.CrmSearchFilter());
			Dictionary<string, CrmServiceClient.LogicalSortOrder> dct = new Dictionary<string, CrmServiceClient.LogicalSortOrder>();
			Dictionary<string, string> lg = new Dictionary<string, string>();
			string pagecookie;
			bool ismore;
			Dictionary<string, Dictionary<string, object>> result = crmaction.GetActivitiesBy("account", entId, fldList, lgsrc, fltList, dct, 1, 1, "", out pagecookie, out ismore);
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(RollupRequest));
			Assert.IsNotNull(((RollupRequest)orgReq).Query);
			Assert.IsNotNull(((RollupRequest)orgReq).Target);
			Assert.AreEqual(RollupType.Related, ((RollupRequest)orgReq).RollupType);
			BCrmServiceClient.ClearRequest();
			result = crmaction.GetActivitiesBy("account", entId, fldList, lgsrc, lg, dct, 1, 1, "", out pagecookie, out ismore);
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(RollupRequest));
			Assert.IsNotNull(((RollupRequest)orgReq).Query);
			Assert.IsNotNull(((RollupRequest)orgReq).Target);
			Assert.AreEqual(RollupType.Related, ((RollupRequest)orgReq).RollupType);
		}

		[TestMethod]
		public void ExecuteWorkflowOnEntityTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MExecuteWorkflowResponse exwResp = new MExecuteWorkflowResponse();
			exwResp.IdGet = () => { return respId; };
			MRetrieveMultipleResponse rtvresp = new MRetrieveMultipleResponse();
			EntityCollection entcoll = new EntityCollection();
			entcoll.Entities.Add(new Entity());
			rtvresp.EntityCollectionGet = () => { return entcoll; };
			BCrmServiceClient.AddResponse(typeof(ExecuteWorkflowRequest), exwResp);
			BCrmServiceClient.AddResponse(typeof(RetrieveMultipleRequest), rtvresp);
			BCrmServiceClient.MockCrmCommandExecute();
			MCrmServiceClient.AllInstances.GetDataByKeyFromResultsSetDictionaryOfStringObjectString<Guid>((objwbsvcaction, dict, str) =>
			{
				if (str == "parentworkflowid")
					return Guid.Empty;
				else
					return (Guid)respId;
			});
			Guid result = crmaction.ExecuteWorkflowOnEntity("workflow1", respId);
			Assert.AreEqual(respId, result);
			orgReq = BCrmServiceClient.GetRequest(typeof(ExecuteWorkflowRequest));
			Assert.IsNotNull(((ExecuteWorkflowRequest)orgReq).WorkflowId);
		}

		[TestMethod]
		public void ExecuteBatchTest()
		{
			ExecuteMultipleRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			RequestBatch rqBatch = new RequestBatch();
			rqBatch.BatchId = respId;
			List<BatchItemOrganizationRequest> batchItems = new List<BatchItemOrganizationRequest>();
			BatchItemOrganizationRequest itmOrgRqs = new BatchItemOrganizationRequest();
			itmOrgRqs.Request = new OrganizationRequest();
			batchItems.Add(itmOrgRqs);
			rqBatch.BatchItems = batchItems;
			MBatchManager.AllInstances.GetRequestBatchByIdGuid = (objbatchmgr, guid) => { return rqBatch; };
			ExecuteMultipleResponse rsp = crmaction.ExecuteBatch(respId);
			Assert.AreEqual(rsp, BCrmServiceClient.GetResponse(typeof(ExecuteMultipleRequest)));
			orgReq = (ExecuteMultipleRequest)BCrmServiceClient.GetRequest(typeof(ExecuteMultipleRequest));
			Assert.IsNotNull(((ExecuteMultipleRequest)orgReq).Requests);
		}

		[TestMethod]
		public void ImportDataMapToCrmTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MImportMappingsImportMapResponse orgResp = new MImportMappingsImportMapResponse();
			orgResp.ImportMapIdGet = () => { return respId; };
			BCrmServiceClient.AddResponse(typeof(ImportMappingsImportMapRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			Guid result = crmaction.ImportDataMapToCrm("samplexml");
			Assert.AreEqual(respId, result);
			orgReq = BCrmServiceClient.GetRequest(typeof(ImportMappingsImportMapRequest));
			Assert.IsNotNull(((ImportMappingsImportMapRequest)orgReq).MappingsXml);
		}

		[TestMethod]
		public void InstallSampleDataToCrm()
		{
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Guid result = crmaction.InstallSampleDataToCrm();
			Assert.AreEqual(BCrmServiceClient.GetRequest(typeof(InstallSampleDataRequest)).RequestId.Value, result);
		}

		[TestMethod]
		public void ImportSolutionToCrmTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			//file related moling
			MFile.ExistsString = (str) => { return true; };
			byte[] fileData = new byte[1000 * 1000 * 3];
			MFile.ReadAllBytesString = (str) => { return fileData; };
			Guid result = crmaction.ImportSolutionToCrm("solutionpath", out respId);
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(ImportSolutionRequest));
			Assert.IsNotNull(((ImportSolutionRequest)orgReq).CustomizationFile);
		}

		[TestMethod]
		public void UninstallSampleDataFromCrmTest()
		{
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			MCrmServiceClient.AllInstances.IsSampleDataInstalled = (objwbsvcaction) => { return CrmServiceClient.ImportStatus.Completed; };
			Guid result = crmaction.UninstallSampleDataFromCrm();
			Assert.AreEqual(BCrmServiceClient.GetRequest(typeof(UninstallSampleDataRequest)).RequestId.Value, result);
		}


		[TestMethod]
		public void UpdateStateAndStatusForEntityTest()
		{
			OrganizationRequest orgReq = null;
			BCrmServiceClient.MockCrmCommandExecute();
			BCrmServiceClient.MockGetPickListElementFromMetadataEntity();
			Guid toupdateId = Guid.NewGuid();
			bool result = crmaction.UpdateStateAndStatusForEntity("account", toupdateId, 1, 1);
			Assert.IsTrue(result);
			result = crmaction.UpdateStateAndStatusForEntity("account", toupdateId, "Completed", "2");
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(SetStateRequest));
			Assert.IsNotNull(((SetStateRequest)orgReq).State);
		}

		[TestMethod]
		public void SubmitImportRequestTest()
		{
			Guid respid = Guid.NewGuid();
			OrganizationRequest orgReq = null;
			Guid asncId = Guid.NewGuid();
			MCrmServiceClient.AllInstances.CreateNewRecordStringDictionaryOfStringCrmDataTypeWrapperStringBooleanGuid = (objwbsvcact, dict, str, bln, guid, guid1) =>
			{ return respid; };
			MParseImportResponse prsResponse = new MParseImportResponse();
			prsResponse.AsyncOperationIdGet = () => asncId;
			BCrmServiceClient.AddResponse(typeof(ParseImportRequest), prsResponse);
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("name", new CrmDataTypeWrapper("CrudTestAccount", CrmFieldType.String));
			newFields.Add("accountnumber", new CrmDataTypeWrapper("12345", CrmFieldType.String));
			newFields.Add("telephone1", new CrmDataTypeWrapper("555-555-5555", CrmFieldType.String));
			newFields.Add("donotpostalmail", new CrmDataTypeWrapper(true, CrmFieldType.CrmBoolean));
			Guid toupdateId = Guid.NewGuid();
			CrmServiceClient.ImportRequest importRequest = new CrmServiceClient.ImportRequest();
			importRequest.Files.Add(new CrmServiceClient.ImportFileItem());
			DateTime dt = new DateTime();
			Guid result = crmaction.SubmitImportRequest(importRequest, dt);
			Assert.AreEqual(respid, result);
			orgReq = BCrmServiceClient.GetRequest(typeof(ImportRecordsImportRequest));
			Assert.IsNotNull(((ImportRecordsImportRequest)orgReq).ImportId);
			orgReq = BCrmServiceClient.GetRequest(typeof(ParseImportRequest));
			Assert.IsNotNull(((ParseImportRequest)orgReq).ImportId);
		}

		[TestMethod]
		public void CloseIncidentTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			Guid incdId = Guid.NewGuid();
			Guid actId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("incidentid", new CrmDataTypeWrapper(incdId, CrmFieldType.UniqueIdentifier));
			newFields.Add("activityid", new CrmDataTypeWrapper(actId, CrmFieldType.UniqueIdentifier));
			Guid guid = crmaction.CloseIncident(respId, newFields);
			Assert.IsNotNull(guid);
			orgReq = BCrmServiceClient.GetRequest(typeof(CloseIncidentRequest));
			Assert.IsNotNull(((CloseIncidentRequest)orgReq).IncidentResolution);

		}

		[TestMethod]
		public void CloseQuoteTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			Guid quotId = Guid.NewGuid();
			Guid actId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("quoteid", new CrmDataTypeWrapper(quotId, CrmFieldType.UniqueIdentifier));
			newFields.Add("activityid", new CrmDataTypeWrapper(actId, CrmFieldType.UniqueIdentifier));
			Guid guid = crmaction.CloseQuote(respId, newFields);
			Assert.IsNotNull(guid);
			orgReq = BCrmServiceClient.GetRequest(typeof(CloseQuoteRequest));
			Assert.IsNotNull(((CloseQuoteRequest)orgReq).QuoteClose);
		}

		[TestMethod]
		public void CloseOpportunityTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			Guid oprtId = Guid.NewGuid();
			Guid actId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, CrmDataTypeWrapper> newFields = new Dictionary<string, CrmDataTypeWrapper>();
			newFields.Add("opportunityid", new CrmDataTypeWrapper(oprtId, CrmFieldType.UniqueIdentifier));
			newFields.Add("activityid", new CrmDataTypeWrapper(actId, CrmFieldType.UniqueIdentifier));
			Guid guid = crmaction.CloseOpportunity(respId, newFields);
			Assert.IsNotNull(guid);
			orgReq = BCrmServiceClient.GetRequest(typeof(WinOpportunityRequest));
			Assert.IsNotNull(((WinOpportunityRequest)orgReq).OpportunityClose);
		}

		[TestMethod]
		public void CloseTroubleTicketTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			Guid guid = crmaction.CloseTroubleTicket(respId, "", "");
			Assert.IsNotNull(guid);
			orgReq = BCrmServiceClient.GetRequest(typeof(CloseIncidentRequest));
			Assert.IsNotNull(((CloseIncidentRequest)orgReq).IncidentResolution);
		}

		[TestMethod]
		public void CloseActivityTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			BCrmServiceClient.MockCrmCommandExecute();
			BCrmServiceClient.MockGetPickListElementFromMetadataEntity();
			bool bln = crmaction.CloseActivity("", respId);
			Assert.IsTrue(bln);
			orgReq = BCrmServiceClient.GetRequest(typeof(SetStateRequest));
			Assert.IsNotNull(((SetStateRequest)orgReq).State);
		}

		[TestMethod]
		public void GetEntityDataByIdTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			Dictionary<string, object> MapData = null;
			MRetrieveResponse updResp = new MRetrieveResponse();
			updResp.EntityGet = () => new Entity();
			BCrmServiceClient.AddResponse(typeof(RetrieveRequest), updResp);
			BCrmServiceClient.MockCrmCommandExecute();
			List<string> fldList = new List<string>();
			fldList.Add("name");
			fldList.Add("source");
			fldList.Add("importmapid");
			MapData = crmaction.GetEntityDataById("account", respId, fldList);
			Assert.IsNotNull(MapData);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveRequest));
			Assert.IsNotNull(((RetrieveRequest)orgReq).ColumnSet);
		}

		[TestMethod]
		public void GetEntityDataByFetchSearchTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MRetrieveMultipleResponse rtrResp = new MRetrieveMultipleResponse();
			EntityCollection entColl = new EntityCollection();
			entColl.Entities.Add(new Entity());
			rtrResp.EntityCollectionGet = () => { return entColl; };
			MFetchXmlToQueryExpressionResponse ftcrsp = new MFetchXmlToQueryExpressionResponse();
			ftcrsp.QueryGet = () => { return new QueryExpression(); };
			BCrmServiceClient.AddResponse(typeof(FetchXmlToQueryExpressionRequest), ftcrsp);
			BCrmServiceClient.AddResponse(typeof(RetrieveMultipleRequest), rtrResp);
			BCrmServiceClient.MockCrmCommandExecute();
			string fetch = "<fetch mapping='logical'>";
			fetch += "<entity name='account'><all-attributes/>";
			fetch += "</entity></fetch>";
			Dictionary<string, Dictionary<string, object>> Results = crmaction.GetEntityDataByFetchSearch(fetch);
			Assert.IsNotNull(Results);
			string strCookie;
			bool blnmoreRecord;
			Results = crmaction.GetEntityDataByFetchSearch(fetch, 1, 1, "", out strCookie, out blnmoreRecord);
			Assert.IsNotNull(Results);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveMultipleRequest));
			Assert.IsNotNull(((RetrieveMultipleRequest)orgReq).Query);
		}

		[TestMethod]
		public void GetGlobalOptionSetMetadata()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MRetrieveOptionSetResponse resp = new MRetrieveOptionSetResponse();
			resp.BehaveAsDefaultValue();
			resp.OptionSetMetadataGet = () => { return new OptionSetMetadata(); };
			MRetrieveResponse Rresp = new MRetrieveResponse();
			Rresp.EntityGet = () => new Entity();
			Microsoft.Xrm.Sdk.Metadata.OptionSetMetadata M = null;
			BCrmServiceClient.AddResponse(typeof(RetrieveOptionSetRequest), resp);
			BCrmServiceClient.AddResponse(typeof(RetrieveRequest), Rresp);
			BCrmServiceClient.MockCrmCommandExecute();
			M = crmaction.GetGlobalOptionSetMetadata("online");
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveOptionSetRequest));
			Assert.IsNotNull(((RetrieveOptionSetRequest)orgReq).Name);
		}

		[TestMethod]
		public void GetAllEntityMetadata()
		{
			Guid respId = Guid.NewGuid();
			MRetrieveAllEntitiesResponse resp = new MRetrieveAllEntitiesResponse();
			MEntityMetadata mtdata = new MEntityMetadata();
			mtdata.ObjectTypeCodeGet = () => 1;
			mtdata.LogicalNameGet = () => "account";
			resp.EntityMetadataGet = () => new EntityMetadata[1] { mtdata };
			BCrmServiceClient.AddResponse(typeof(RetrieveAllEntitiesRequest), resp);
			BCrmServiceClient.MockCrmCommandExecute();
			List<EntityMetadata> LM = new List<EntityMetadata>();
			LM = crmaction.GetAllEntityMetadata();
			Assert.IsNotNull(LM);
		}

		[TestMethod]
		public void GetEntityMetadata()
		{
			OrganizationRequest orgReq = null;
			EntityMetadata EM = new EntityMetadata();
			MRetrieveEntityResponse resp = new MRetrieveEntityResponse();
			resp.EntityMetadataGet = () => new EntityMetadata();
			BCrmServiceClient.AddResponse(typeof(RetrieveEntityRequest), resp);
			BCrmServiceClient.MockCrmCommandExecute();
			EM = crmaction.GetEntityMetadata("Account", EntityFilters.Default);
			Assert.IsNotNull(EM);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveEntityRequest));
			Assert.IsNotNull(((RetrieveEntityRequest)orgReq).LogicalName);
		}

		[TestMethod]
		public void GetEntityFormIdListByType()
		{
			OrganizationRequest orgReq = null;
			List<EntityReference> lstRef = new List<EntityReference>();
			MRetrieveFilteredFormsResponse resp = new MRetrieveFilteredFormsResponse();
			resp.SystemFormsGet = () => { return new EntityReferenceCollection(); };
			BCrmServiceClient.AddResponse(typeof(RetrieveFilteredFormsRequest), resp);
			BCrmServiceClient.MockCrmCommandExecute();
			lstRef = crmaction.GetEntityFormIdListByType("contact", CrmServiceClient.FormTypeId.AppointmentBook);
			Assert.IsNotNull(lstRef);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveFilteredFormsRequest));
			Assert.IsNotNull(((RetrieveFilteredFormsRequest)orgReq).EntityLogicalName);
		}

		[TestMethod]
		public void GetAllAttributesForEntity()
		{
			OrganizationRequest orgReq = null;
			List<AttributeMetadata> AM = new List<AttributeMetadata>();
			MRetrieveAttributeResponse resp = new MRetrieveAttributeResponse();
			resp.AttributeMetadataGet = () => new AttributeMetadata();
			MEntityMetadata metadata = new MEntityMetadata();
			metadata.AttributesGet = () => new AttributeMetadata[1] { new AttributeMetadata() };
			MMetadataUtility.AllInstances.GetEntityMetadataString = (objMutility, str) => { return metadata; };
			BCrmServiceClient.AddResponse(typeof(RetrieveAttributeRequest), resp);
			BCrmServiceClient.MockCrmCommandExecute();
			AM = crmaction.GetAllAttributesForEntity("contact");
			Assert.IsNotNull(AM);
		}

		[TestMethod]
		public void GetEntityAttributeMetadataForAttribute()
		{
			OrganizationRequest orgReq = null;
			AttributeMetadata AM = new AttributeMetadata();
			MRetrieveAttributeResponse resp = new MRetrieveAttributeResponse();
			BCrmServiceClient.AddResponse(typeof(RetrieveAttributeRequest), resp);
			resp.AttributeMetadataGet = () => new AttributeMetadata();
			BCrmServiceClient.MockCrmCommandExecute();
			AM = crmaction.GetEntityAttributeMetadataForAttribute("contact", "attribute");
			Assert.IsNotNull(AM);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveAttributeRequest));
			Assert.IsNotNull(((RetrieveAttributeRequest)orgReq).LogicalName);
			Assert.IsNotNull(((RetrieveAttributeRequest)orgReq).EntityLogicalName);
		}

		[TestMethod]
		public void GetEntityDisplayName()
		{
			OrganizationRequest orgReq = null;
			MRetrieveEntityResponse resp = new MRetrieveEntityResponse();
			resp.EntityMetadataGet = () => new EntityMetadata();
			BCrmServiceClient.AddResponse(typeof(RetrieveEntityRequest), resp);
			BCrmServiceClient.MockCrmCommandExecute();
			string entityName = "account";
			string displayName = crmaction.GetEntityDisplayName(entityName);
			Assert.AreEqual(entityName, displayName);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveEntityRequest));
			Assert.AreEqual(entityName, ((RetrieveEntityRequest)orgReq).LogicalName);
		}

		[TestMethod]
		public void GetEntityDataByLinkedSearchTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();

			MRetrieveMultipleResponse rtrResp = new MRetrieveMultipleResponse();
			EntityCollection entColl = new EntityCollection();
			entColl.Entities.Add(new Entity());
			rtrResp.EntityCollectionGet = () => { return entColl; };
			BCrmServiceClient.AddResponse(typeof(RetrieveMultipleRequest), rtrResp);
			BCrmServiceClient.MockCrmCommandExecute();
			Dictionary<string, Dictionary<string, object>> result = crmaction.GetEntityDataByLinkedSearch("account", new Dictionary<string, string>(), "contact", new Dictionary<string, string>(), "", "", "", new CrmServiceClient.LogicalSearchOperator(), new List<string>());
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveMultipleRequest));
			Assert.IsNotNull(((RetrieveMultipleRequest)orgReq).Query);
		}

		[TestMethod]
		public void CreateNewActivityEntryTest()
		{
			OrganizationRequest orgReq = null;
			Guid respId = Guid.NewGuid();
			MCreateResponse orgResp = new MCreateResponse();
			orgResp.idGet = () => respId;
			BCrmServiceClient.AddResponse(typeof(CreateRequest), orgResp);
			BCrmServiceClient.MockCrmCommandExecute();
			string activityName = "fax";
			Guid id = crmaction.CreateNewActivityEntry(activityName, "", respId, "testmail", "", "");
			Assert.AreEqual(respId, id);
			orgReq = BCrmServiceClient.GetRequest(typeof(CreateRequest));
			Assert.AreEqual(activityName, ((CreateRequest)orgReq).Target.LogicalName);
		}

		[TestMethod]
		public void DeleteEntityTest()
		{
			OrganizationRequest orgReq = null;
			BCrmServiceClient.MockCrmCommandExecute();
			Guid toDeleteId = Guid.NewGuid();
			string entityDelete = "account";
			bool result = crmaction.DeleteEntity(entityDelete, toDeleteId);
			Assert.IsTrue(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(DeleteRequest));
			Assert.AreEqual(entityDelete, (((EntityReference)((DeleteRequest)orgReq).Target).LogicalName));
		}


		[TestMethod]
		public void GetMyCrmUserIdTest()
		{
			Guid respId = Guid.NewGuid();
			MWhoAmIResponse response = new MWhoAmIResponse();
			MCrmWebSvc.AllInstances.CrmUserGet = (obj) => response;
			MWhoAmIResponse.AllInstances.UserIdGet = (obj) => respId;
			Guid result = crmaction.GetMyCrmUserId();
			Assert.AreEqual(result, respId);
		}

		[TestMethod]
		public void ExecuteCrmEntityDeleteRequestTest()
		{
			Guid toDeleteId = Guid.NewGuid();
			bool result = crmaction.ExecuteCrmEntityDeleteRequest("account", toDeleteId);
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void ExecuteCrmOrganizationRequestTest()
		{
			OrganizationRequest orgReq = new OrganizationRequest();
			BCrmServiceClient.MockCrmCommandExecute();
			OrganizationResponse orgRes = crmaction.ExecuteCrmOrganizationRequest(orgReq);
			Assert.IsNotNull(orgRes);
		}

		[TestMethod]
		public void GetEntityNameTest()
		{
			MRetrieveAllEntitiesResponse retriveResp = new MRetrieveAllEntitiesResponse();
			MEntityMetadata ent = new MEntityMetadata();

			Nullable<int> no = 1;
			ent.ObjectTypeCodeGet = () => { return no; };

			string st = "";
			ent.LogicalNameGet = () => { return st; };

			EntityMetadata[] entmt = { ent };
			retriveResp.EntityMetadataGet = () => { return entmt; };
			BCrmServiceClient.AddResponse(typeof(RetrieveAllEntitiesRequest), retriveResp);
			BCrmServiceClient.MockCrmCommandExecute();

			string result = crmaction.GetEntityName(1);
			Assert.IsNotNull(result);
		}

		[TestMethod]
		public void GetEntityTypeCodeTest()
		{
			OrganizationRequest orgReq = null;
			MRetrieveEntityResponse retriveResp = new MRetrieveEntityResponse();
			MEntityMetadata ent = new MEntityMetadata();
			ent.ObjectTypeCodeGet = () => { return 1; };
			retriveResp.EntityMetadataGet = () => { return ent; };
			BCrmServiceClient.AddResponse(typeof(RetrieveEntityRequest), retriveResp);
			BCrmServiceClient.MockCrmCommandExecute();
			string entityName = "account";
			string result = crmaction.GetEntityTypeCode(entityName);
			Assert.IsNotNull(result);
			orgReq = BCrmServiceClient.GetRequest(typeof(RetrieveEntityRequest));
			Assert.AreEqual(entityName, ((RetrieveEntityRequest)orgReq).LogicalName);
		}

		[TestMethod]
		public void ResetLocalMetadataCacheTest()
		{
			bool methodCalled = false;
			string entityName = "account";
			MMetadataUtility.ClearCachedEntityMetadataString = (str) => { methodCalled = true; Assert.AreEqual(entityName, str); };
			crmaction.ResetLocalMetadataCache("account");
			Assert.IsTrue(methodCalled);
		}
	}
}