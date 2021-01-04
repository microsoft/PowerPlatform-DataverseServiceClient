using System;
using System.Linq;
using Xunit;
using Microsoft.Xrm.Sdk;
using Moq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Cds.Client;
using Microsoft.PowerPlatform.Cds.Client.Dynamics;
using Microsoft.Xrm.Sdk.WebServiceClient;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using CdsClient_Core_UnitTests;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit.Abstractions;
using System.IO;
using Microsoft.PowerPlatform.Cds.Client.Auth;

namespace CdsClient_Core_Tests
{
    public partial class CdsClientTests
    {
        #region SharedVars

        TestSupport testSupport = new TestSupport();
        ITestOutputHelper outputListner;
        #endregion

        public CdsClientTests(ITestOutputHelper output)
        {
            outputListner = output;

            TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Verbose;
            TraceConsoleSupport traceConsoleSupport = new TraceConsoleSupport(outputListner);
            TraceControlSettings.CloseListeners();
            TraceControlSettings.AddTraceListener(traceConsoleSupport);
        }

        [Fact]
        public void ExecuteCrmOrganizationRequest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            var rsp = (WhoAmIResponse)cli.ExecuteCdsOrganizationRequest(new WhoAmIRequest());

            // Validate that the user ID sent in is the UserID that comes out. 
            Assert.Equal(rsp.UserId, testSupport._UserId);
        }

        [Fact]
        public void ExecuteMessageTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Test that Retrieve Org is working .
            var orgData = cli.Execute(new RetrieveCurrentOrganizationRequest() { AccessType = Microsoft.Xrm.Sdk.Organization.EndpointAccessType.Default });
            Assert.NotNull(orgData);
        }


        [Fact]
        public void DeleteRequestTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Setup handlers to deal with both orgRequest and WebAPI request.             
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("delete", StringComparison.OrdinalIgnoreCase)))).Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            orgSvc.Setup(f => f.Execute(It.Is<DeleteRequest>(p => p.Target.LogicalName.Equals("account") && p.Target.Id.Equals(testSupport._DefaultId)))).Returns(new DeleteResponse());

            bool rslt = cli.ExecuteCdsEntityDeleteRequest("account", testSupport._DefaultId);
            Assert.True(rslt);

            rslt = cli.DeleteEntity("account", testSupport._DefaultId);
            Assert.True(rslt);
        }


        [Fact]
        public void CreateRequestTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Set up Responses 
            CreateResponse testCreate = new CreateResponse();
            testCreate.Results.AddOrUpdateIfNotNull("accountid", testSupport._DefaultId);
            testCreate.Results.AddOrUpdateIfNotNull("id", testSupport._DefaultId);


            HttpResponseMessage createRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createRespMsg.Headers.Add("Location", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");
            createRespMsg.Headers.Add("OData-EntityId", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");

            // Setup handlers to deal with both orgRequest and WebAPI request.             
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("post", StringComparison.OrdinalIgnoreCase)))).Returns(createRespMsg);
            orgSvc.Setup(f => f.Execute(It.Is<CreateRequest>(p => p.Target.LogicalName.Equals("account")))).Returns(testCreate);


            // Setup request 
            // use create operation to setup request 
            Dictionary<string, CdsDataTypeWrapper> newFields = new Dictionary<string, CdsDataTypeWrapper>();
            newFields.Add("name", new CdsDataTypeWrapper("CrudTestAccount", CdsFieldType.String));

            Entity acctEntity = new Entity("account");
            acctEntity.Attributes.Add("name", "CrudTestAccount");

            Guid respId = Guid.Empty;

            // Test entity create
            var response = cli.ExecuteCdsOrganizationRequest(new CreateRequest() { Target = acctEntity }, useWebAPI: false);
            Assert.NotNull(response);
            respId = ((CreateResponse)response).id;
            Assert.Equal(testSupport._DefaultId, respId);

            // Test low level create
            respId = cli.Create(acctEntity);
            Assert.Equal(testSupport._DefaultId, respId);

            // Test Helper create
            respId = cli.CreateNewRecord("account", newFields);
            Assert.Equal(testSupport._DefaultId, respId);
        }

        [Fact]
        public void DataTypeParsingTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Set up Responses 
            CreateResponse testCreate = new CreateResponse();
            testCreate.Results.AddOrUpdateIfNotNull("accountid", testSupport._DefaultId);
            testCreate.Results.AddOrUpdateIfNotNull("id", testSupport._DefaultId);

            LookupAttributeMetadata lookupAttributeMeta1 = new LookupAttributeMetadata();
            lookupAttributeMeta1.LogicalName = "field02";
            lookupAttributeMeta1.Targets = new List<string>() { "account", "contact" }.ToArray();
            RetrieveAttributeResponse attribfield02Resp = new RetrieveAttributeResponse();
            attribfield02Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", lookupAttributeMeta1);

            LookupAttributeMetadata lookupAttributeMeta2 = new LookupAttributeMetadata();
            lookupAttributeMeta2.LogicalName = "field07";
            lookupAttributeMeta2.Targets = new List<string>() { "account" }.ToArray();
            RetrieveAttributeResponse attribfield07Resp = new RetrieveAttributeResponse();
            attribfield07Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", lookupAttributeMeta2);


            HttpResponseMessage createRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createRespMsg.Headers.Add("Location", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");
            createRespMsg.Headers.Add("OData-EntityId", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");

            // Setup handlers to deal with both orgRequest and WebAPI request.             
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("post", StringComparison.OrdinalIgnoreCase)))).Returns(createRespMsg);
            orgSvc.Setup(f => f.Execute(It.Is<CreateRequest>(p => p.Target.LogicalName.Equals("account")))).Returns(testCreate);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field02", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield02Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field07", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield07Resp);

            // Setup request for all datatypes
            // use create operation to setup request 
            Dictionary<string, CdsDataTypeWrapper> newFields = new Dictionary<string, CdsDataTypeWrapper>();
            newFields.Add("name", new CdsDataTypeWrapper("CrudTestAccount", CdsFieldType.String));
            newFields.Add("Field01", new CdsDataTypeWrapper(false, CdsFieldType.Boolean));
            newFields.Add("Field02", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Customer , "account"));
            newFields.Add("Field03", new CdsDataTypeWrapper(DateTime.UtcNow, CdsFieldType.DateTime));
            newFields.Add("Field04", new CdsDataTypeWrapper(64, CdsFieldType.Decimal));
            newFields.Add("Field05", new CdsDataTypeWrapper(1.001, CdsFieldType.Float));
            newFields.Add("Field06", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Key));
            newFields.Add("Field07", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Lookup, "account"));
            newFields.Add("Field08", new CdsDataTypeWrapper(50, CdsFieldType.Money));
            newFields.Add("Field09", new CdsDataTypeWrapper(100, CdsFieldType.Number));
            newFields.Add("Field010", new CdsDataTypeWrapper(20, CdsFieldType.Picklist));
            newFields.Add("Field011", new CdsDataTypeWrapper("RawValue", CdsFieldType.Raw));
            newFields.Add("Field012", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.UniqueIdentifier));

            Entity acctEntity = new Entity("account");
            acctEntity.Attributes.Add("name", "CrudTestAccount");
            acctEntity.Attributes.Add("Field01",  false);
            acctEntity.Attributes.Add("Field02",  new EntityReference("parentaccount" , testSupport._DefaultId));
            acctEntity.Attributes.Add("Field03",  DateTime.UtcNow);
            acctEntity.Attributes.Add("Field04",  64);
            acctEntity.Attributes.Add("Field05",  1.001);
            acctEntity.Attributes.Add("Field08",  50);
            acctEntity.Attributes.Add("Field09",  100);
            acctEntity.Attributes.Add("Field010", new OptionSetValue(20));

            // Test Helper create
            var respId = cli.CreateNewRecord("account", newFields);
            Assert.Equal(testSupport._DefaultId, respId);

            // Test entity create
            var response = cli.ExecuteCdsOrganizationRequest(new CreateRequest() { Target = acctEntity }, useWebAPI: false);
            Assert.NotNull(response);
            respId = ((CreateResponse)response).id;
            Assert.Equal(testSupport._DefaultId, respId);

            // Test low level create
            respId = cli.Create(acctEntity);
            Assert.Equal(testSupport._DefaultId, respId);


        }


        [Fact]
        public void GetCurrentUser()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            var rsp01 = cli.GetMyCdsUserId();

            // Validate that the user ID sent in is the UserID that comes out. 
            Assert.Equal(rsp01, testSupport._UserId);
        }

        [Fact]
        public void BatchTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            CreateResponse createResponse = new CreateResponse();
            createResponse.Results = new ParameterCollection();
            createResponse.Results.Add("annotationid", testSupport._DefaultId);

            ExecuteMultipleResponseItem responseItem = new ExecuteMultipleResponseItem();
            responseItem.Response = createResponse;
            responseItem.RequestIndex = 0;

            ExecuteMultipleResponseItemCollection responseItems = new ExecuteMultipleResponseItemCollection();
            responseItems.Add(responseItem);

            ExecuteMultipleResponse executeMultipleResponse = new ExecuteMultipleResponse();
            executeMultipleResponse.Results = new ParameterCollection();
            executeMultipleResponse.Results.Add("Responses", responseItems);

            orgSvc.Setup(req1 => req1.Execute(It.IsAny<ExecuteMultipleRequest>())).Returns(executeMultipleResponse);


            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            // use create operation to setup request 
            Dictionary<string, CdsDataTypeWrapper> newFields = new Dictionary<string, CdsDataTypeWrapper>();
            newFields.Add("name", new CdsDataTypeWrapper("CrudTestAccount", CdsFieldType.String));
            newFields.Add("accountnumber", new CdsDataTypeWrapper("12345", CdsFieldType.String));
            newFields.Add("telephone1", new CdsDataTypeWrapper("555-555-5555", CdsFieldType.String));
            newFields.Add("donotpostalmail", new CdsDataTypeWrapper(true, CdsFieldType.Boolean));

            // issue request as a batch: 
            Guid result = cli.CreateAnnotation("account", testSupport._DefaultId, newFields, batchid);
            Assert.Equal<Guid>(Guid.Empty, result);

            OrganizationRequest req = cli.GetBatchRequestAtPosition(batchid, 0);

            // Executes the batch request. 
            cli.ExecuteBatch(batchid);

            // Request Batch by name 
            Guid OperationId = cli.GetBatchOperationIdRequestByName(BatchRequestName);

            // Request batch back 
            RequestBatch reqBatch = cli.GetBatchById(batchid);
            Assert.NotNull(reqBatch);
            Assert.Equal(BatchRequestName, reqBatch.BatchName);
            Assert.True(reqBatch.BatchItems.Count == 1);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);
        }

        [Fact]
        public void CreateEntityAssociationTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            AssociateEntitiesResponse associateEntitiesResponse = new AssociateEntitiesResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<AssociateEntitiesRequest>())).Returns(associateEntitiesResponse);

            bool result = cli.CreateEntityAssociation("account", testSupport._DefaultId, "contact", testSupport._DefaultId, "somerelation");
            Assert.True(result);
        }

        [Fact]
        public void CreateMultiEntityAssociationTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            AssociateResponse associateEntitiesResponse = new AssociateResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<AssociateRequest>())).Returns(associateEntitiesResponse);

            Guid accountId = Guid.NewGuid();
            Guid contactId = Guid.NewGuid();
            List<Guid> lst = new List<Guid>();
            lst.Add(accountId);
            lst.Add(contactId);
            bool result = cli.CreateMultiEntityAssociation("account", contactId, "contact", lst, "some rel");
            Assert.True(result);
        }

        [Fact]
        public void AssignEntityToUserTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            AssignResponse assignResponse = new AssignResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<AssignRequest>())).Returns(assignResponse);

            bool result = cli.AssignEntityToUser(testSupport._DefaultId, "account", testSupport._DefaultId);
            Assert.True(result);

        }

        [Fact]
        public void SendSingleEmailTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            SendEmailResponse sendEmailResponse = new SendEmailResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<SendEmailRequest>())).Returns(sendEmailResponse);

            bool result = cli.SendSingleEmail(testSupport._DefaultId, "tokn");
            Assert.True(result);
        }

        [Fact]
        public void GetEntityDisplayNameTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Test for plural name 
            string response = cli.GetEntityDisplayNamePlural("account");
            Assert.Equal("Accounts", response);

            // Test for plural name ETC
            response = cli.GetEntityDisplayNamePlural("account" , 1);
            Assert.Equal("Accounts", response);

            // Test for non plural name 
            response = cli.GetEntityDisplayName("account");
            Assert.Equal("Account", response);

            // Test for non plural name ETC 
            response = cli.GetEntityDisplayName("account" , 1);
            Assert.Equal("Account", response);

            // Test base function
            response = cli.GetEntityName(1);
            Assert.Equal("account", response);

        }


        [Fact]
        public void ImportSolutionTest_AsyncRibbon_ComponetProcessor()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            ImportSolutionResponse importResponse = new ImportSolutionResponse();
            orgSvc.Setup(f => f.Execute(It.Is<ImportSolutionRequest>(
                (p) =>
                    p.CustomizationFile != null &&
                    p.AsyncRibbonProcessing.Equals(true) &&
                    p.ComponentParameters != null))).Returns(importResponse);

            string SampleSolutionPath = Path.Combine("TestMaterial", "EnvVarsSample_1_0_0_2.zip");

            EntityCollection entCollection = new EntityCollection();

            Dictionary<string, object> importParams = new Dictionary<string, object>();
            importParams.Add(ImportSolutionProperties.ASYNCRIBBONPROCESSING, true);
            importParams.Add(ImportSolutionProperties.COMPONENTPARAMETERSPARAM, entCollection);
            Guid importId = Guid.Empty;
            var result = cli.ImportSolutionToCds(SampleSolutionPath, out importId, activatePlugIns: true, extraParameters: importParams);

            Assert.NotEqual(result, Guid.Empty);
        }


        [Fact]
        public void GetEntityTypeCodeTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            string entityName = "account";
            string result = cli.GetEntityTypeCode(entityName);

            Assert.Equal("1", result);
        }

        [Fact]
        public void ResetLocalMetadataCacheTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            cli.ResetLocalMetadataCache("account");
        }

        //public void CreateOrUpdatePickListElementTest()
        //{
        //    var orgSvc = new Mock<IOrganizationService>();
        //    CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
        //    SetupWhoAmIHandlers(orgSvc);
        //    SetupMetadataHandlers(orgSvc);

        //    List<LocalizedLabel> lst = new List<LocalizedLabel>();
        //    lst.Add(new LocalizedLabel());

        //    bool result = cli.CreateOrUpdatePickListElement("account", "name", lst, 1, true);

        //}

        #region LiveConnectedTests

        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_CtorV1()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            string Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params. 
            CdsServiceClient client = new CdsServiceClient(new Uri(Conn_Url), Conn_AppID, Conn_Secret, true);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);
        }

        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_CtorV2()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            string Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // connection params + secure string. 
            CdsServiceClient client = new CdsServiceClient(new Uri(Conn_Url), Conn_AppID, CdsServiceClient.MakeSecureString(Conn_Secret), true);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);
        }

        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_ConStr()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            string Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection string; 
            string connStr = $"AuthType=ClientSecret;AppId={Conn_AppID};ClientSecret={Conn_Secret};Url={Conn_Url}";
            CdsServiceClient client = new CdsServiceClient(connStr);
            Assert.True(client.IsReady, "Failed to Create Connection via Connection string");

            // Validate connection
            ValidateConnection(client);
        }




        /// <summary>
        /// This Tests connection for UID/PW via connection string - direct connect.
        /// </summary>
        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_ConStr()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            string Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection string - Direct connect using Sample ApplicationID's
            string connStr = $"AuthType=OAuth;Username={Conn_UserName};Password={Conn_PW};Url={Conn_Url};AppId={testSupport._SampleAppID.ToString()};RedirectUri={testSupport._SampleAppRedirect.ToString()};TokenCacheStorePath=c:\\MyTokenCache;LoginPrompt=Never";
            CdsServiceClient client = new CdsServiceClient(connStr);
            Assert.True(client.IsReady, "Failed to Create Connection via Connection string");

            // Validate connection
            ValidateConnection(client);
        }

        /// <summary>
        /// This Tests connection for UID/PW via constructor - uses discovery to locate instance.
        /// </summary>
        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_CtorV1_Discovery()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            string Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            //Connection params. 
            string onlineRegion = string.Empty;
            string orgName = string.Empty;
            bool isOnPrem = false;
            Utilities.GetOrgnameAndOnlineRegionFromServiceUri(new Uri(Conn_Url), out onlineRegion, out orgName, out isOnPrem);

            Assert.NotNull(onlineRegion);
            Assert.NotNull(orgName);
            Assert.False(isOnPrem);

            Uri orgUri = new Uri(Conn_Url);
            string hostName = orgUri.Host.Split('.')[0];

            CdsServiceClient client = new CdsServiceClient(Conn_UserName, CdsServiceClient.MakeSecureString(Conn_PW), onlineRegion, hostName, true,null, CdsConnectionStringProcessor.sampleClientId, new Uri(CdsConnectionStringProcessor.sampleRedirectUrl), PromptBehavior.Never);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor - Discovery");

            // Validate connection
            ValidateConnection(client);
        }

        /// <summary>
        /// This Tests connection for UID/PW via constructor - uses Direct Connect.
        /// </summary>
        [SkippableConnectionTestAttribute]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_CtorV2()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            string Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            string Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            string Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params. 
            CdsServiceClient client = new CdsServiceClient(Conn_UserName, CdsServiceClient.MakeSecureString(Conn_PW),new Uri(Conn_Url), true, CdsConnectionStringProcessor.sampleClientId, new Uri(CdsConnectionStringProcessor.sampleRedirectUrl), PromptBehavior.Never);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor - Direct Connect");

            // Validate connection
            ValidateConnection(client);
        }


        #region connectionValidationHelper

        private void ValidateConnection(CdsServiceClient client)
        {
            // Validate it 
            var rslt = client.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);

            // Clone it. - Validate use
            using (CdsServiceClient client2 = client.Clone())
            {
                rslt = client2.Execute(new WhoAmIRequest());
                Assert.IsType<WhoAmIResponse>(rslt);
            }

            // Create clone chain an break linkage. 
            CdsServiceClient client3 = client.Clone();
            CdsServiceClient client4 = client3.Clone();
            rslt = client3.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);
            // dispose client3 explicitly
            client3.Dispose();
            rslt = client4.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);
            client4.Dispose();
        }

        #endregion

        #endregion

    }
}
