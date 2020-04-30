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

namespace CdsClient_Core_Tests
{
    public partial class CdsClientTests
    {
        #region SharedVars

        TestSupport testSupport = new TestSupport();

        #endregion

        [Fact]
        public void ExecuteCrmOrganizationRequest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            testSupport.SetupWhoAmIHandlers(orgSvc); 
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            var rsp = (WhoAmIResponse)cli.ExecuteCdsOrganizationRequest(new WhoAmIRequest());

            // Validate that the user ID sent in is the UserID that comes out. 
            Assert.Equal(rsp.UserId, testSupport._UserId);
        }

        [Fact]
        public void DeleteRequestTests()
        {
            var orgSvc = new Mock<IOrganizationService>();
            testSupport.SetupWhoAmIHandlers(orgSvc);
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);

            orgSvc.Setup(f => f.Execute(It.Is<DeleteRequest>(p => p.Target.LogicalName.Equals("account") && p.Target.Id.Equals(testSupport._DefaultId)))).Returns(new DeleteResponse());

            bool rslt = cli.ExecuteCdsEntityDeleteRequest("account", testSupport._DefaultId);
            Assert.True(rslt);

            rslt = cli.DeleteEntity("account", testSupport._DefaultId);
            Assert.True(rslt);

        }

        [Fact]
        public void GetCurrentUser()
        {
            var orgSvc = new Mock<IOrganizationService>();
            testSupport.SetupWhoAmIHandlers(orgSvc);

            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            var rsp01 = cli.GetMyCdsUserId();

            // Validate that the user ID sent in is the UserID that comes out. 
            Assert.Equal(rsp01, testSupport._UserId);
        }

        [Fact]
        public void BatchTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);

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
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);

            AssociateEntitiesResponse associateEntitiesResponse = new AssociateEntitiesResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<AssociateEntitiesRequest>())).Returns(associateEntitiesResponse);

            bool result = cli.CreateEntityAssociation("account", testSupport._DefaultId, "contact", testSupport._DefaultId, "somerelation");
            Assert.True(result);
        }

        [Fact]
        public void CreateMultiEntityAssociationTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);
            
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
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);

            AssignResponse assignResponse = new AssignResponse(); 
            orgSvc.Setup(f => f.Execute(It.IsAny<AssignRequest>())).Returns(assignResponse);

            bool result = cli.AssignEntityToUser(testSupport._DefaultId, "account", testSupport._DefaultId);
            Assert.True(result);

        }

        [Fact]
        public void SendSingleEmailTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);

            SendEmailResponse sendEmailResponse = new SendEmailResponse();
            orgSvc.Setup(f => f.Execute(It.IsAny<SendEmailRequest>())).Returns(sendEmailResponse);

            bool result = cli.SendSingleEmail(testSupport._DefaultId, "tokn");
            Assert.True(result);
        }

        [Fact]
        public void GetEntityDisplayNameTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);
            testSupport.SetupMetadataHandlersForAccount(orgSvc);

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
        public void GetEntityTypeCodeTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);
            testSupport.SetupMetadataHandlersForAccount(orgSvc);

            string entityName = "account";
            string result = cli.GetEntityTypeCode(entityName);

            Assert.Equal("1", result);
        }

        [Fact]
        public void ResetLocalMetadataCacheTest()
        {
            var orgSvc = new Mock<IOrganizationService>();
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object);
            testSupport.SetupWhoAmIHandlers(orgSvc);
            testSupport.SetupMetadataHandlersForAccount(orgSvc);

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
    }
}
