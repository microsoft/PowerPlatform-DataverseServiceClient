using Microsoft.PowerPlatform.Cds.Client;
using Microsoft.PowerPlatform.Cds.Client.Dynamics;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using System.Net.Http;

namespace CdsClient_Core_UnitTests
{
    public class CdsClientDynamicsExtensionsTests
    {
        TestSupport testSupport = new TestSupport();


        [Fact]
        public void CloseQuoteTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            orgSvc.Setup(f => f.Execute(It.Is<CloseQuoteRequest>(p => p.QuoteClose is Entity && ((Entity)p.QuoteClose).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new CloseQuoteResponse());
            orgSvc.Setup(f => f.Execute(It.Is<WinQuoteRequest>(p => p.QuoteClose is Entity && ((Entity)p.QuoteClose).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new WinQuoteResponse());

            Dictionary<string, CdsDataTypeWrapper> inboundData = new Dictionary<string, CdsDataTypeWrapper>();
            inboundData.Add("name", new CdsDataTypeWrapper("MyName", CdsFieldType.String));

            Guid rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 3);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseQuote(Guid.Empty, inboundData, quoteStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 1);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            // Test with projected Activity ID
            inboundData.Add("activityid", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Key));
            rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);
            Assert.Equal<Guid>(testSupport._DefaultId, rslt);

            // try with batch 
            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 4, batchId: batchid);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);

        }

        [Fact]
        public void CloseOpportunityTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            orgSvc.Setup(f => f.Execute(It.Is<WinOpportunityRequest>(p => p.OpportunityClose is Entity && ((Entity)p.OpportunityClose).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new WinOpportunityResponse());
            orgSvc.Setup(f => f.Execute(It.Is<LoseOpportunityRequest>(p => p.OpportunityClose is Entity && ((Entity)p.OpportunityClose).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new LoseOpportunityResponse());

            Dictionary<string, CdsDataTypeWrapper> inboundData = new Dictionary<string, CdsDataTypeWrapper>();
            inboundData.Add("name", new CdsDataTypeWrapper("MyName", CdsFieldType.String));

            Guid rslt = cli.CloseOpportunity(testSupport._DefaultId, inboundData, opportunityStatusCode: 3);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseOpportunity(Guid.Empty, inboundData, opportunityStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseOpportunity(testSupport._DefaultId, inboundData, opportunityStatusCode: 2);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseOpportunity(testSupport._DefaultId, inboundData, opportunityStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            // Test with projected Activity ID
            inboundData.Add("activityid", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Key));
            rslt = cli.CloseOpportunity(testSupport._DefaultId, inboundData, opportunityStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);
            Assert.Equal<Guid>(testSupport._DefaultId, rslt);

            // try with batch 
            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            rslt = cli.CloseQuote(testSupport._DefaultId, inboundData, quoteStatusCode: 4, batchId: batchid);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);
        }

        [Fact]
        public void CloseIncidentTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            orgSvc.Setup(f => f.Execute(It.Is<CloseIncidentRequest>(p => p.IncidentResolution is Entity && ((Entity)p.IncidentResolution).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new CloseIncidentResponse());

            Dictionary<string, CdsDataTypeWrapper> inboundData = new Dictionary<string, CdsDataTypeWrapper>();
            inboundData.Add("name", new CdsDataTypeWrapper("MyName", CdsFieldType.String));

            Guid rslt = cli.CloseIncident(testSupport._DefaultId, inboundData, incidentStatusCode: 5);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseIncident(Guid.Empty, inboundData, incidentStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseIncident(testSupport._DefaultId, inboundData, incidentStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            // Test with projected Activity ID
            inboundData.Add("activityid", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Key));
            rslt = cli.CloseIncident(testSupport._DefaultId, inboundData, incidentStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);
            Assert.Equal<Guid>(testSupport._DefaultId, rslt);

            // try with batch 
            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            rslt = cli.CloseIncident(testSupport._DefaultId, inboundData, incidentStatusCode: 4, batchId: batchid);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);
        }


        [Fact]
        public void CancelSalesOrderTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            orgSvc.Setup(f => f.Execute(It.Is<CancelSalesOrderRequest>(p => p.OrderClose is Entity && ((Entity)p.OrderClose).GetAttributeValue<string>("name").Equals("MyName", StringComparison.OrdinalIgnoreCase)))).Returns(new CancelSalesOrderResponse());

            Dictionary<string, CdsDataTypeWrapper> inboundData = new Dictionary<string, CdsDataTypeWrapper>();
            inboundData.Add("name", new CdsDataTypeWrapper("MyName", CdsFieldType.String));

            Guid rslt = cli.CancelSalesOrder(Guid.Empty, inboundData, orderStatusCode: 5);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CancelSalesOrder(testSupport._DefaultId, inboundData, orderStatusCode: 2);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            rslt = cli.CancelSalesOrder(testSupport._DefaultId, inboundData, orderStatusCode: 5);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            rslt = cli.CancelSalesOrder(testSupport._DefaultId, inboundData, orderStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            // Test with projected Activity ID
            inboundData.Add("activityid", new CdsDataTypeWrapper(testSupport._DefaultId, CdsFieldType.Key));
            rslt = cli.CancelSalesOrder(testSupport._DefaultId, inboundData, orderStatusCode: 4);
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);
            Assert.Equal<Guid>(testSupport._DefaultId, rslt);

            // try with batch 
            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            rslt = cli.CancelSalesOrder(testSupport._DefaultId, inboundData, orderStatusCode: 4, batchId: batchid);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);

        }


        [Fact]
        public void CloseTroubleTicketTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            CdsServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            orgSvc.Setup(f => f.Execute(It.Is<CloseIncidentRequest>(p => p.IncidentResolution is Entity && ((Entity)p.IncidentResolution).GetAttributeValue<string>("subject").Equals("Subject", StringComparison.OrdinalIgnoreCase)))).Returns(new CloseIncidentResponse());

            Guid rslt = cli.CloseTroubleTicket(testSupport._DefaultId, "Subject" , "Description");
            Assert.IsType<Guid>(rslt);
            Assert.NotEqual<Guid>(Guid.Empty, rslt);

            rslt = cli.CloseTroubleTicket(Guid.Empty, "Subject", "Description");
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // try with batch 
            // Setup a batch
            string BatchRequestName = "TestBatch";
            Guid batchid = cli.CreateBatchOperationRequest(BatchRequestName);

            rslt = cli.CloseTroubleTicket(testSupport._DefaultId, "Subject", "Description", batchId: batchid);
            Assert.IsType<Guid>(rslt);
            Assert.Equal<Guid>(Guid.Empty, rslt);

            // Release batch request
            cli.ReleaseBatchInfoById(batchid);

        }
    }
}
