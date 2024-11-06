#region using
using Client_Core_UnitTests;
using DataverseClient_Core_UnitTests;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using Microsoft.PowerPlatform.Dataverse.Client.Exceptions;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using Microsoft.PowerPlatform.Dataverse.Client.HttpUtils;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
#endregion

namespace Client_Core_Tests
{
    [Collection("NonParallelCollection")]
    public class ServiceClientTests
    {
        #region SharedVars

        TestSupport testSupport = new TestSupport();
        ITestOutputHelper outputListner;
        ILogger<ServiceClientTests> Ilogger = null;
        #endregion

        public ServiceClientTests(ITestOutputHelper output)
        {
            outputListner = output;
            //TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Verbose;
            //TraceConsoleSupport traceConsoleSupport = new TraceConsoleSupport(outputListner);
            //TraceControlSettings.CloseListeners();
            //TraceControlSettings.AddTraceListener(traceConsoleSupport);

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();



            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    })
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddProvider(new TraceConsoleLoggingProvider(output)));
            Ilogger = loggerFactory.CreateLogger<ServiceClientTests>();
            testSupport.logger = Ilogger;
        }

        [Fact]
        public void ExecuteCrmOrganizationRequest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            var rsp = (WhoAmIResponse)cli.ExecuteOrganizationRequest(new WhoAmIRequest());

            // Validate that the user ID sent in is the UserID that comes out.
            Assert.Equal(rsp.UserId, testSupport._UserId);
        }

        [Fact]
        public void TestThrowDisposedOperationCheck()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                cli.Dispose();
                _ = (WhoAmIResponse)cli.Execute(new WhoAmIRequest());
            });

            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                cli.Dispose();
                _ = (WhoAmIResponse)await cli.ExecuteAsync(new WhoAmIRequest()).ConfigureAwait(false);
            });
        }

        [Fact]
        public void ExecuteMessageTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Test that Retrieve Org is working .
            var orgData = cli.Execute(new RetrieveCurrentOrganizationRequest() { AccessType = Microsoft.Xrm.Sdk.Organization.EndpointAccessType.Default });
            Assert.NotNull(orgData);
        }

        [Fact]
        public void LogWriteTest()
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    }));
            ILogger<ServiceClientTests> Ilogger = loggerFactory.CreateLogger<ServiceClientTests>();

            DataverseTraceLogger logger = new DataverseTraceLogger(Ilogger);
            logger.EnabledInMemoryLogCapture = true;

            logger.Log("TEST INFO MESSAGE");
            logger.Log("TEST WARNING MESSAGE", TraceEventType.Warning);
            logger.Log("TEST VERBOSE MESSAGE", TraceEventType.Verbose);
            logger.Log("TEST ERROR MESSAGE", TraceEventType.Error);
            logger.Log("TEST CRITICAL MESSAGE", TraceEventType.Critical);


            // error throw.
            HttpOperationException operationException = new HttpOperationException("HTTPOPEXC");
            HttpResponseMessage Resp500 = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            Resp500.Headers.Add("REQ_ID", "39393F77-8F8B-4416-846E-28B4D2AA5667");
            operationException.Response = new HttpResponseMessageWrapper(Resp500, "{\"error\":{\"code\":\"0x80040203\",\"message\":\"Communication activity cannot have more than one Sender party\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiExceptionSourceKey\":\"Plugin/Microsoft.Crm.Common.ObjectModel.PhoneCallService\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiStepKey\":\"3ccabb1b-ea3e-db11-86a7-000a3a5473e8\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiDepthKey\":\"1\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiActivityIdKey\":\"1736f387-e025-4828-a2bb-74ea8ac768a2\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiPluginSolutionNameKey\":\"System\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiStepSolutionNameKey\":\"System\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiExceptionCategory\":\"ClientError\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiExceptionMesageName\":\"InvalidArgument\",\"@Microsoft.PowerApps.CDS.ErrorDetails.ApiExceptionHttpStatusCode\":\"400\",\"@Microsoft.PowerApps.CDS.HelpLink\":\"http://go.microsoft.com/fwlink/?LinkID=398563&error=Microsoft.Crm.CrmException%3a80040203&client=platform\",\"@Microsoft.PowerApps.CDS.InnerError.Message\":\"Communication activity cannot have more than one Sender party\"}}");
            logger.Log(operationException);

            Assert.NotNull(logger.LastError);
            Exception exOut = logger.LastException;

        }

        [Fact]
        public void DeleteRequestTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Setup handlers to deal with both orgRequest and WebAPI request.
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("delete", StringComparison.OrdinalIgnoreCase)))).Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            orgSvc.Setup(f => f.Execute(It.Is<DeleteRequest>(p => p.Target.LogicalName.Equals("account") && p.Target.Id.Equals(testSupport._DefaultId)))).Returns(new DeleteResponse());

            bool rslt = cli.DeleteEntity("account", testSupport._DefaultId);
            Assert.True(rslt);
        }


        [Fact]
        public void CreateRequestTests()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Set up Responses
            CreateResponse testCreate = new CreateResponse();
            testCreate.Results.AddOrUpdateIfNotNull("accountid", testSupport._DefaultId);
            testCreate.Results.AddOrUpdateIfNotNull("id", testSupport._DefaultId);

            StringAttributeMetadata stringAttributeData = new StringAttributeMetadata();
            stringAttributeData.LogicalName = "name";
            RetrieveAttributeResponse stringAttributeDataResp = new RetrieveAttributeResponse();
            stringAttributeDataResp.Results.AddOrUpdateIfNotNull("AttributeMetadata", stringAttributeData);

            DateTimeAttributeMetadata dateTimeAttributeMetadata = new DateTimeAttributeMetadata();
            dateTimeAttributeMetadata.LogicalName = "dateonlyfield";
            dateTimeAttributeMetadata.DateTimeBehavior = DateTimeBehavior.DateOnly;
            RetrieveAttributeResponse attribdateonlyfieldResp = new RetrieveAttributeResponse();
            attribdateonlyfieldResp.Results.AddOrUpdateIfNotNull("AttributeMetadata", dateTimeAttributeMetadata);

            DateTimeAttributeMetadata dateTimeAttributeMetadata1 = new DateTimeAttributeMetadata();
            dateTimeAttributeMetadata1.LogicalName = "datetimeNormal";
            dateTimeAttributeMetadata1.DateTimeBehavior = DateTimeBehavior.UserLocal;
            RetrieveAttributeResponse attribdatetimeNormalfieldResp = new RetrieveAttributeResponse();
            attribdatetimeNormalfieldResp.Results.AddOrUpdateIfNotNull("AttributeMetadata", dateTimeAttributeMetadata1);

            DateTimeAttributeMetadata dateTimeAttributeMetadata2 = new DateTimeAttributeMetadata();
            dateTimeAttributeMetadata2.LogicalName = "datetimeTZindependant";
            dateTimeAttributeMetadata2.DateTimeBehavior = DateTimeBehavior.TimeZoneIndependent;
            RetrieveAttributeResponse attribdatetimeTZindependantfieldResp = new RetrieveAttributeResponse();
            attribdatetimeTZindependantfieldResp.Results.AddOrUpdateIfNotNull("AttributeMetadata", dateTimeAttributeMetadata2);

            HttpResponseMessage createRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createRespMsg.Headers.Add("Location", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");
            createRespMsg.Headers.Add("OData-EntityId", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");

            // Setup handlers to deal with both orgRequest and WebAPI request.
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("post", StringComparison.OrdinalIgnoreCase)))).Returns(createRespMsg);
            orgSvc.Setup(f => f.Execute(It.Is<CreateRequest>(p => p.Target.LogicalName.Equals("account")))).Returns(testCreate);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("dateonlyfield", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribdateonlyfieldResp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("datetimeNormal", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribdatetimeNormalfieldResp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("datetimeTZindependant", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribdatetimeTZindependantfieldResp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("name", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(stringAttributeDataResp);

            // Setup request
            // use create operation to setup request
            Dictionary<string, DataverseDataTypeWrapper> newFields = new Dictionary<string, DataverseDataTypeWrapper>();
            newFields.Add("name", new DataverseDataTypeWrapper("CrudTestAccount", DataverseFieldType.String));
            newFields.Add("dateonlyfield", new DataverseDataTypeWrapper(new DateTime(2000, 01, 01), DataverseFieldType.DateTime));
            newFields.Add("datetimeNormal", new DataverseDataTypeWrapper(new DateTime(2000, 01, 01, 12, 01, 00, DateTimeKind.Local), DataverseFieldType.DateTime));
            newFields.Add("datetimeTZindependant", new DataverseDataTypeWrapper(new DateTime(2000, 01, 01, 13, 01, 00, DateTimeKind.Local), DataverseFieldType.DateTime));

            Entity acctEntity = new Entity("account");
            acctEntity.Attributes.Add("name", "CrudTestAccount");
            acctEntity.Attributes.Add("dateonlyfield", new DateTime(2000, 01, 01));
            acctEntity.Attributes.Add("datetimeNormal", new DateTime(2000, 01, 01, 12, 01, 00, DateTimeKind.Local));
            acctEntity.Attributes.Add("datetimeTZindependant", new DateTime(2000, 01, 01, 13, 01, 00, DateTimeKind.Local));

            Guid respId = Guid.Empty;

            // Test entity create
            var response = cli.ExecuteOrganizationRequest(new CreateRequest() { Target = acctEntity }, useWebAPI: false);
            Assert.NotNull(response);
            respId = ((CreateResponse)response).id;
            Assert.Equal(testSupport._DefaultId, respId);

            // Test low level create
            respId = cli.Create(acctEntity);
            Assert.Equal(testSupport._DefaultId, respId);

            // Test low level createAsync
            respId = cli.CreateAsync(acctEntity).GetAwaiter().GetResult();
            Assert.Equal(testSupport._DefaultId, respId);

            try
            {
                // Test low level createAsyncwithCancelationToken
                System.Threading.CancellationToken tok = new System.Threading.CancellationToken(true);
                respId = cli.CreateAsync(acctEntity, tok).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Assert.IsType<OperationCanceledException>(ex);
            }

            // Test Helper create
            respId = cli.CreateNewRecord("account", newFields);
            Assert.Equal(testSupport._DefaultId, respId);
        }

        [Fact]
        public void DataTypeParsingTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Set up Responses
            CreateResponse testCreate = new CreateResponse();
            testCreate.Results.AddOrUpdateIfNotNull("accountid", testSupport._DefaultId);
            testCreate.Results.AddOrUpdateIfNotNull("id", testSupport._DefaultId);

            StringAttributeMetadata stringAttributeData = new StringAttributeMetadata();
            stringAttributeData.LogicalName = "name";
            RetrieveAttributeResponse stringAttributeDataResp = new RetrieveAttributeResponse();
            stringAttributeDataResp.Results.AddOrUpdateIfNotNull("AttributeMetadata", stringAttributeData);

            BooleanAttributeMetadata field01booleanAttribute = new BooleanAttributeMetadata();
            field01booleanAttribute.LogicalName = "field01";
            RetrieveAttributeResponse booleanAttributefield01Resp = new RetrieveAttributeResponse();
            booleanAttributefield01Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field01booleanAttribute);

            LookupAttributeMetadata lookupAttributeMeta1 = new LookupAttributeMetadata();
            lookupAttributeMeta1.LogicalName = "field02";
            lookupAttributeMeta1.Targets = new List<string>() { "account", "contact" }.ToArray();
            RetrieveAttributeResponse attribfield02Resp = new RetrieveAttributeResponse();
            attribfield02Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", lookupAttributeMeta1);

            DecimalAttributeMetadata field04decimalAttribute = new DecimalAttributeMetadata();
            field04decimalAttribute.LogicalName = "field04";
            RetrieveAttributeResponse decimalAttributefield04Resp = new RetrieveAttributeResponse();
            decimalAttributefield04Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field04decimalAttribute);

            LookupAttributeMetadata lookupAttributeMeta2 = new LookupAttributeMetadata();
            lookupAttributeMeta2.LogicalName = "field07";
            lookupAttributeMeta2.Targets = new List<string>() { "account" }.ToArray();
            RetrieveAttributeResponse attribfield07Resp = new RetrieveAttributeResponse();
            attribfield07Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", lookupAttributeMeta2);

            DateTimeAttributeMetadata dateTimeAttributeMetadata = new DateTimeAttributeMetadata();
            dateTimeAttributeMetadata.LogicalName = "field03";
            dateTimeAttributeMetadata.Format = DateTimeFormat.DateAndTime;
            RetrieveAttributeResponse attribfield03Resp = new RetrieveAttributeResponse();
            attribfield03Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", dateTimeAttributeMetadata);

            DecimalAttributeMetadata field05AttributeMetadata = new DecimalAttributeMetadata();
            field05AttributeMetadata.LogicalName = "field05";
            field05AttributeMetadata.Precision = 3;
            RetrieveAttributeResponse attribfield05Resp = new RetrieveAttributeResponse();
            attribfield05Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field05AttributeMetadata);

            EntityKeyMetadata field06Metadata = new EntityKeyMetadata();
            field06Metadata.LogicalName = "field06";
            RetrieveAttributeResponse attribfield06Resp = new RetrieveAttributeResponse();
            attribfield06Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field06Metadata);

            MoneyAttributeMetadata field08AttributeMetadata = new MoneyAttributeMetadata();
            field08AttributeMetadata.LogicalName = "field08";
            field08AttributeMetadata.Precision = 2;
            RetrieveAttributeResponse attribfield08Resp = new RetrieveAttributeResponse();
            attribfield08Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field08AttributeMetadata);

            DecimalAttributeMetadata field09AttributeMetadata = new DecimalAttributeMetadata();
            field09AttributeMetadata.LogicalName = "field09";
            field09AttributeMetadata.Precision = 0;
            RetrieveAttributeResponse attribfield09Resp = new RetrieveAttributeResponse();
            attribfield06Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field09AttributeMetadata);

            PicklistAttributeMetadata field10AttributeMetadata = new PicklistAttributeMetadata();
            field10AttributeMetadata.LogicalName = "field010";
            RetrieveAttributeResponse attribfield10Resp = new RetrieveAttributeResponse();
            attribfield10Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field10AttributeMetadata);

            StringAttributeMetadata field011AttributeData = new StringAttributeMetadata();
            stringAttributeData.LogicalName = "field011";
            RetrieveAttributeResponse attribfield011Resp = new RetrieveAttributeResponse();
            attribfield011Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", stringAttributeData);

            EntityKeyMetadata field012Metadata = new EntityKeyMetadata();
            field06Metadata.LogicalName = "field012";
            RetrieveAttributeResponse attribfield012Resp = new RetrieveAttributeResponse();
            attribfield012Resp.Results.AddOrUpdateIfNotNull("AttributeMetadata", field012Metadata);

            HttpResponseMessage createRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createRespMsg.Headers.Add("Location", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");
            createRespMsg.Headers.Add("OData-EntityId", $"https://deploymenttarget02.crm.dynamics.com/api/data/v9.1/accounts({testSupport._DefaultId})");

            // Setup handlers to deal with both orgRequest and WebAPI request.
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("post", StringComparison.OrdinalIgnoreCase)))).Returns(createRespMsg);
            orgSvc.Setup(f => f.Execute(It.Is<CreateRequest>(p => p.Target.LogicalName.Equals("account")))).Returns(testCreate);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("name", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(stringAttributeDataResp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field01", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(booleanAttributefield01Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field02", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield02Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field03", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield03Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field04", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(decimalAttributefield04Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field05", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield05Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field06", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield06Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field07", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield07Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field08", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield08Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field09", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield06Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field010", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield10Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field011", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield011Resp);
            orgSvc.Setup(f => f.Execute(It.Is<RetrieveAttributeRequest>(p => p.LogicalName.Equals("field012", StringComparison.OrdinalIgnoreCase) && p.EntityLogicalName.Equals("account", StringComparison.OrdinalIgnoreCase)))).Returns(attribfield012Resp);

            // Setup request for all datatypes
            // use create operation to setup request
            Dictionary<string, DataverseDataTypeWrapper> newFields = new Dictionary<string, DataverseDataTypeWrapper>();
            newFields.Add("name", new DataverseDataTypeWrapper("CrudTestAccount", DataverseFieldType.String));
            newFields.Add("Field01", new DataverseDataTypeWrapper(false, DataverseFieldType.Boolean));
            newFields.Add("Field02", new DataverseDataTypeWrapper(testSupport._DefaultId, DataverseFieldType.Customer, "account"));
            newFields.Add("Field03", new DataverseDataTypeWrapper(DateTime.UtcNow, DataverseFieldType.DateTime));
            newFields.Add("Field04", new DataverseDataTypeWrapper(64, DataverseFieldType.Decimal));
            newFields.Add("Field05", new DataverseDataTypeWrapper(1.001, DataverseFieldType.Float));
            newFields.Add("Field06", new DataverseDataTypeWrapper(testSupport._DefaultId, DataverseFieldType.Key));
            newFields.Add("Field07", new DataverseDataTypeWrapper(testSupport._DefaultId, DataverseFieldType.Lookup, "account"));
            newFields.Add("Field08", new DataverseDataTypeWrapper(50, DataverseFieldType.Money));
            newFields.Add("Field09", new DataverseDataTypeWrapper(100, DataverseFieldType.Number));
            newFields.Add("Field010", new DataverseDataTypeWrapper(20, DataverseFieldType.Picklist));
            newFields.Add("Field011", new DataverseDataTypeWrapper("RawValue", DataverseFieldType.Raw));
            newFields.Add("Field012", new DataverseDataTypeWrapper(testSupport._DefaultId, DataverseFieldType.UniqueIdentifier));

            Entity acctEntity = new Entity("account");
            acctEntity.Attributes.Add("name", "CrudTestAccount");
            acctEntity.Attributes.Add("Field01", false);
            acctEntity.Attributes.Add("Field02", new EntityReference("parentaccount", testSupport._DefaultId));
            acctEntity.Attributes.Add("Field03", DateTime.UtcNow);
            acctEntity.Attributes.Add("Field04", 64);
            acctEntity.Attributes.Add("Field05", 1.001);
            acctEntity.Attributes.Add("Field08", 50);
            acctEntity.Attributes.Add("Field09", 100);
            acctEntity.Attributes.Add("Field010", new OptionSetValue(20));

            // Test Helper create
            var respId = cli.CreateNewRecord("account", newFields);
            Assert.Equal(testSupport._DefaultId, respId);

            // Test entity create
            var response = cli.ExecuteOrganizationRequest(new CreateRequest() { Target = acctEntity }, useWebAPI: false);
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
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            var rsp01 = cli.GetMyUserId();

            // Validate that the user ID sent in is the UserID that comes out.
            Assert.Equal(rsp01, testSupport._UserId);
        }

        [Fact]
        public void BatchTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
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
            Dictionary<string, DataverseDataTypeWrapper> newFields = new Dictionary<string, DataverseDataTypeWrapper>();
            newFields.Add("name", new DataverseDataTypeWrapper("CrudTestAccount", DataverseFieldType.String));
            newFields.Add("accountnumber", new DataverseDataTypeWrapper("12345", DataverseFieldType.String));
            newFields.Add("telephone1", new DataverseDataTypeWrapper("555-555-5555", DataverseFieldType.String));
            newFields.Add("donotpostalmail", new DataverseDataTypeWrapper(true, DataverseFieldType.Boolean));

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
            ServiceClient cli = null;
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
            ServiceClient cli = null;
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
            ServiceClient cli = null;
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
            ServiceClient cli = null;
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
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Test for plural name
            string response = cli.GetEntityDisplayNamePlural("account");
            Assert.Equal("Accounts", response);

            // Test for plural name ETC
            response = cli.GetEntityDisplayNamePlural("account", 1);
            Assert.Equal("Accounts", response);

            // Test for non plural name
            response = cli.GetEntityDisplayName("account");
            Assert.Equal("Account", response);

            // Test for non plural name ETC
            response = cli.GetEntityDisplayName("account", 1);
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
            ServiceClient cli = null;
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
            var result = cli.ImportSolution(SampleSolutionPath, out importId, activatePlugIns: true, extraParameters: importParams);

            Assert.NotEqual(result, Guid.Empty);
        }


        [Fact]
        public void ImportSolutionTest_AsyncRibbon_ComponetData()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli, new Version("9.2.21013.117"));

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
            var result = cli.ImportSolution(SampleSolutionPath, out importId, activatePlugIns: true, extraParameters: importParams);

            Assert.NotEqual(result, Guid.Empty);
        }

        [Fact]
        public void GetEntityTypeCodeTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
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
            ServiceClient cli = null;
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

        [Fact]
        public void TestResponseHeaderWebAPIBehavior()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            // Setup handlers to deal with both orgRequest and WebAPI request.
            int baseTestDOP = 10;
            var httpResp = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            httpResp.Headers.Add(Utilities.ResponseHeaders.RECOMMENDEDDEGREESOFPARALLELISM, baseTestDOP.ToString());
            orgSvc.Setup(f => f.Execute(It.Is<DeleteRequest>(p => p.Target.LogicalName.Equals("account") && p.Target.Id.Equals(testSupport._DefaultId)))).Returns(new DeleteResponse());
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("delete", StringComparison.OrdinalIgnoreCase)))).Returns(httpResp);

            // Tests/ 
            cli.UseWebApi = true;
            bool rslt = cli.DeleteEntity("account", testSupport._DefaultId);
            Assert.True(rslt);
            Assert.Equal(baseTestDOP, cli.RecommendedDegreesOfParallelism);

            cli.Delete("account", testSupport._DefaultId);
            Assert.Equal(baseTestDOP, cli.RecommendedDegreesOfParallelism);

            Guid requestId = Guid.NewGuid();
            cli._logEntry.Log($"New Request ID is {requestId}", TraceEventType.Information);
            DeleteRequest deleteRequest = new DeleteRequest()
            {
                Target = new EntityReference("account", testSupport._DefaultId),
                RequestId = requestId
            };
            cli.ExecuteOrganizationRequest(deleteRequest, useWebAPI: true);
            Assert.Equal(baseTestDOP, cli.RecommendedDegreesOfParallelism);


        }

        [Fact]
        public void TestResponseHeaderBehavior()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);


            cli.UseWebApi = false;
            // Setup handlers to deal with both orgRequest and WebAPI request.
            int defaultDOP = 5;
            var httpResp = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            httpResp.Headers.Add(Utilities.ResponseHeaders.RECOMMENDEDDEGREESOFPARALLELISM, defaultDOP.ToString());
            orgSvc.Setup(f => f.Execute(It.Is<DeleteRequest>(p => p.Target.LogicalName.Equals("account") && p.Target.Id.Equals(testSupport._DefaultId)))).Returns(new DeleteResponse());
            fakHttpMethodHander.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("delete", StringComparison.OrdinalIgnoreCase)))).Returns(httpResp);

            // Tests/ 
            cli.UseWebApi = false;

            bool rslt = cli.DeleteEntity("account", testSupport._DefaultId);
            Assert.True(rslt);
            Assert.Equal(defaultDOP, cli.RecommendedDegreesOfParallelism);

            cli.UseWebApi = false;
            cli.Delete("account", testSupport._DefaultId);
            Assert.Equal(defaultDOP, cli.RecommendedDegreesOfParallelism);
        }

        [Fact]
        public void RetryOperationTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            Guid testGuid = Guid.NewGuid();

            CreateRequest exampleRequest = new CreateRequest();
            exampleRequest.Target = new Entity("account");
            exampleRequest.Target.Attributes.Add("id", testGuid);

            Stopwatch testwatch = Stopwatch.StartNew();
            int retrycount = 0;

            Task.Run(async () =>
            {
                retrycount = await Utilities.RetryRequest(exampleRequest, testGuid, new TimeSpan(0), testwatch, cli._logEntry, null, false, new TimeSpan(0, 0, 1), new Exception("Fake_TEST_MSG"), "test retry logic", retrycount, false, null).ConfigureAwait(false);
            }).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.True(retrycount == 1);
            Task.Run(async () =>
            {
                retrycount = await Utilities.RetryRequest(exampleRequest, testGuid, new TimeSpan(0), testwatch, cli._logEntry, null, false, new TimeSpan(0, 0, 1), new Exception("Fake_TEST_MSG"), "test retry logic", retrycount, false, null).ConfigureAwait(false);
            }).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.True(retrycount == 2);
        }

        [Fact]
        public async void RetryOperationAyncTest()
        {
            Mock<IOrganizationService> orgSvc = null;
            Mock<MoqHttpMessagehander> fakHttpMethodHander = null;
            ServiceClient cli = null;
            testSupport.SetupMockAndSupport(out orgSvc, out fakHttpMethodHander, out cli);

            Guid testGuid = Guid.NewGuid();

            CreateRequest exampleRequest = new CreateRequest();
            exampleRequest.Target = new Entity("account");
            exampleRequest.Target.Attributes.Add("id", testGuid);

            Stopwatch testwatch = Stopwatch.StartNew();
            int retrycount = 0;

            retrycount = await Utilities.RetryRequest(exampleRequest, testGuid, new TimeSpan(0), testwatch, cli._logEntry, null, false, new TimeSpan(0, 0, 1), new Exception("Fake_TEST_MSG"), "test retry logic", retrycount, false, null).ConfigureAwait(false);
            Assert.True(retrycount == 1);
            retrycount = await Utilities.RetryRequest(exampleRequest, testGuid, new TimeSpan(0), testwatch, cli._logEntry, null, false, new TimeSpan(0, 0, 1), new Exception("Fake_TEST_MSG"), "test retry logic", retrycount, false, null).ConfigureAwait(false);
            Assert.True(retrycount == 2);
        }

        #region LiveConnectedTests

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void RetrieveSolutionImportResultAsyncTestWithSyncImport()
        {
            var client = CreateServiceClient();
            if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(client._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult))
            {
                // Not supported on this version of Dataverse
                client._logEntry.Log($"RetrieveSolutionImportResultAsync request is calling RetrieveSolutionImportResult API. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult.ToString()} or above. The current Dataverse version is {client._connectionSvc?.OrganizationVersion}. This request cannot be made", TraceEventType.Warning);
                return;
            }

            // import solution without async
            client.ImportSolution(Path.Combine("TestData", "TestSolution_1_0_0_1.zip"), out var importId);

            // Response doesn't include formatted results 
            var resWithoutFormatted = client.RetrieveSolutionImportResultAsync(importId);
            resWithoutFormatted.Should().NotBeNull();

            // Response include formatted results
            var resWithFormatted = client.RetrieveSolutionImportResultAsync(importId, true);
            resWithFormatted.Should().NotBeNull();
            resWithFormatted.FormattedResults.Should().NotBeEmpty();
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void RetrieveSolutionImportResultAsyncTestWithAsyncImport()
        {
            var client = CreateServiceClient();
            if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(client._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult))
            {
                // Not supported on this version of Dataverse
                client._logEntry.Log($"RetrieveSolutionImportResultAsync request is calling RetrieveSolutionImportResult API. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult.ToString()} or above. The current Dataverse version is {client._connectionSvc?.OrganizationVersion}. This request cannot be made", TraceEventType.Warning);
                return;
            }
            // import solution with async
            client.ImportSolutionAsync(Path.Combine("TestData", "TestSolution_1_0_0_1.zip"), out var asyncImportId);

            // Wait a little bit because solution might not be immediately available
            System.Threading.Thread.Sleep(30000);

            // Response doesn't include formatted results 
            var resWithoutFormatted = client.RetrieveSolutionImportResultAsync(asyncImportId);
            resWithoutFormatted.Should().NotBeNull();

            // Response include formatted results
            var resWithFormatted = client.RetrieveSolutionImportResultAsync(asyncImportId, true);
            resWithFormatted.Should().NotBeNull();
            resWithFormatted.FormattedResults.Should().NotBeEmpty();
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_CtorV1()
        {
           // System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params.
            var client = new ServiceClient(new Uri(Conn_Url), Conn_AppID, Conn_Secret, true, Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);

        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_CtorV2()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // connection params + secure string.
            var client = new ServiceClient(new Uri(Conn_Url), Conn_AppID, ServiceClient.MakeSecureString(Conn_Secret), true, logger: Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_ConStr()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            ServiceClient client = null;
            try
            {
                // Connection string;
                var connStr = $"AuthType=ClientSecret;AppId={Conn_AppID};ClientSecret={Conn_Secret};Url={Conn_Url}";
                client = new ServiceClient(connStr, Ilogger);
                Assert.True(client.IsReady, "Failed to Create Connection via Connection string");
            }
            catch (Exception ex)
            {
                Assert.Null(ex);
            }
            // Check user before we validate connection
            client._connectionSvc.AuthenticationTypeInUse.Should().Be(Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.ClientSecret);
            client._connectionSvc.AuthContext.Scopes.Should().BeEquivalentTo(new string[] { $"{Conn_Url}/.default" });
            client._connectionSvc.AuthContext.Account.Should().BeNull();
            client._connectionSvc.AuthContext.AccessToken.Should().NotBeNull();
            client._connectionSvc.AuthContext.IdToken.Should().BeNull();

            // Validate connection
            ValidateConnection(client);

            // IdToken should stay null
            client._connectionSvc.AuthContext.IdToken.Should().BeNull();
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_Consetup()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            ConnectionOptions connectionOptions = new ConnectionOptions()
            {
                AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.ClientSecret,
                ClientId = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID"),
                ClientSecret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET"),
                ServiceUri = new Uri(System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI")),
                Logger = Ilogger
            };


            // Connection params.
            var client = new ServiceClient(connectionOptions, deferConnection: true);
            Assert.NotNull(client);
            Assert.False(client.IsReady, "Client is showing True on Deferred Connection.");
            Assert.True(client.Connect(), "Connection was not activated");
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);
        }

		public Task<Dictionary<string, string>> GetAdditionalHeadersAsync()
		{
			var headers = new Dictionary<string, string>();
			headers.Add("User-Agent", "abc");
			return Task.FromResult(headers);
		}

		[SkippableConnectionTest]
		[Trait("Category", "Live Connect Required")]
		public void ConnectUsingUserIdentity_UIDPWHeaders_Consetup()
		{
			System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
			ConnectionOptions connectionOptions = new ConnectionOptions()
			{
				ServiceUri = new Uri(System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI")),
				AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth,
				UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID"),
				Password = ServiceClient.MakeSecureString(System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW")),
				ClientId = DataverseConnectionStringProcessor.sampleClientId,
				RedirectUri = new Uri(DataverseConnectionStringProcessor.sampleRedirectUrl),
				LoginPrompt = PromptBehavior.Auto,
				RequestAdditionalHeadersAsync = GetAdditionalHeadersAsync,
				Logger = Ilogger
			};

			// Connection params.
			var client = new ServiceClient(connectionOptions, deferConnection: true);
			Assert.NotNull(client);
			Assert.False(client.IsReady, "Client is showing True on Deferred Connection.");
			Assert.True(client.Connect(), "Connection was not activated");
			Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

			// Validate connection
			ValidateConnection(client);
			client._connectionSvc.RequestAdditionalHeadersAsync.Should().NotBeNull();

			var clientClone = client.Clone();
			ValidateConnection(clientClone, usingExternalAuth: true);
			clientClone._connectionSvc.RequestAdditionalHeadersAsync.Should().NotBeNull();
		}

		[SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_ExternalAuth_CtorV1()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params.
            var client = new ServiceClient(new Uri(Conn_Url), testSupport.GetS2SAccessTokenForRequest, true, Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client, usingExternalAuth: true);
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingServiceIdentity_ClientSecret_ExternalAuth_Consetup()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            ConnectionOptions connectionOptions = new ConnectionOptions()
            {
                AccessTokenProviderFunctionAsync = testSupport.GetS2SAccessTokenForRequest,
                ServiceUri = new Uri(System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI")),
                Logger = Ilogger
            };

            // Connection params.
            var client = new ServiceClient(connectionOptions, deferConnection: true);
            Assert.NotNull(client);
            Assert.False(client.IsReady, "Client is showing True on Deferred Connection.");
            Assert.True(client.Connect(), "Connection was not activated");
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client, usingExternalAuth: true);

            // test clone
            var clientClone = client.Clone();
            ValidateConnection(clientClone, usingExternalAuth: true);
        }


        /// <summary>
        /// This Tests connection for UID/PW via connection string - direct connect.
        /// </summary>
        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_ConStr()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            var Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection string - Direct connect using Sample ApplicationID's
            var connStr = $"AuthType=OAuth;Username={Conn_UserName};Password={Conn_PW};Url={Conn_Url};AppId={testSupport._SampleAppID.ToString()};RedirectUri={testSupport._SampleAppRedirect.ToString()};LoginPrompt=Never";
            var client = new ServiceClient(connStr, Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Connection string");

            // Check user before we validate connection
            client._connectionSvc.AuthenticationTypeInUse.Should().Be(Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth);
            client._connectionSvc.AuthContext.Scopes.Should().BeEquivalentTo(new string[] { $"{Conn_Url}//user_impersonation" });
            client._connectionSvc.AuthContext.Account.Should().NotBeNull();
            client._connectionSvc.AuthContext.IdToken.Should().NotBeEmpty();
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);

            // Validate connection
            ValidateConnection(client);

            // Check user after we validate connection again as it gets it from cached token
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);
        }

        /// <summary>
        /// This Tests connection for UID/PW via constructor - uses discovery to locate instance.
        /// </summary>
        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_CtorV1_Discovery()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            var Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            //Connection params.
            string onlineRegion = string.Empty;
            string orgName = string.Empty;
            bool isOnPrem = false;
            Utilities.GetOrgnameAndOnlineRegionFromServiceUri(new Uri(Conn_Url), out onlineRegion, out orgName, out isOnPrem);

            Assert.NotNull(onlineRegion);
            Assert.NotNull(orgName);
            Assert.False(isOnPrem);

            var orgUri = new Uri(Conn_Url);
            var hostName = orgUri.Host.Split('.')[0];

            var client = new ServiceClient(Conn_UserName, ServiceClient.MakeSecureString(Conn_PW), onlineRegion, hostName, true, null, DataverseConnectionStringProcessor.sampleClientId, new Uri(DataverseConnectionStringProcessor.sampleRedirectUrl), PromptBehavior.Never);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor - Discovery");

            // Check user before we validate connection
            client._connectionSvc.AuthenticationTypeInUse.Should().Be(Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth);
            client._connectionSvc.AuthContext.Account.Should().NotBeNull();
            client._connectionSvc.AuthContext.IdToken.Should().NotBeEmpty();
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);

            // Validate connection
            ValidateConnection(client);

            // Check user after we validate connection again as it gets it from cached token
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);
        }

        /// <summary>
        /// This Tests connection for UID/PW via constructor - uses Direct Connect.
        /// </summary>
        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_CtorV2()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            var Conn_PW = System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params.
            var client = new ServiceClient(Conn_UserName, ServiceClient.MakeSecureString(Conn_PW), new Uri(Conn_Url), true, DataverseConnectionStringProcessor.sampleClientId, new Uri(DataverseConnectionStringProcessor.sampleRedirectUrl), PromptBehavior.Never);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor - Direct Connect");

            // Check user before we validate connection
            client._connectionSvc.AuthenticationTypeInUse.Should().Be(Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth);
            client._connectionSvc.AuthContext.Scopes.Should().BeEquivalentTo(new string[] { $"{Conn_Url}//user_impersonation" });
            client._connectionSvc.AuthContext.Account.Should().NotBeNull();
            client._connectionSvc.AuthContext.IdToken.Should().NotBeEmpty();
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);

            // Validate connection
            ValidateConnection(client);

            // Check user after we validate connection again as it gets it from cached token
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);
        }

        /// <summary>
        /// This Tests connection for UID/PW via connection string - direct connect.
        /// </summary>
        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ConnectUsingUserIdentity_UIDPW_ConSetup()
        {
            string UrlProspect = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");
            if (UrlProspect.EndsWith("/") || UrlProspect.EndsWith("\\"))
            {
                UrlProspect = UrlProspect.Substring(0, UrlProspect.Length - 1);
            }

            Uri Conn_Url = new Uri(UrlProspect);
            string Conn_UserName = System.Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            ConnectionOptions connectionOptions = new ConnectionOptions()
            {
                AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth,
                ClientId = testSupport._SampleAppID.ToString(),
                RedirectUri = testSupport._SampleAppRedirect,
                ServiceUri = Conn_Url,
                UserName = Conn_UserName,
                Password = ServiceClient.MakeSecureString(System.Environment.GetEnvironmentVariable("XUNITCONNTESTPW")),
                LoginPrompt = PromptBehavior.Never,
                Logger = Ilogger
            };


            // Connection params.
            var client = new ServiceClient(connectionOptions, deferConnection: true);
            Assert.NotNull(client);
            Assert.False(client.IsReady, "Client is showing True on Deferred Connection.");
            Assert.True(client.Connect(), "Connection was not activated");
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            // Validate connection
            ValidateConnection(client);

            // Check user before we validate connection
            client._connectionSvc.AuthenticationTypeInUse.Should().Be(Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth);
            client._connectionSvc.AuthContext.Scopes.Should().BeEquivalentTo(new string[] { $"{Conn_Url}/user_impersonation" });
            client._connectionSvc.AuthContext.Account.Should().NotBeNull();
            client._connectionSvc.AuthContext.IdToken.Should().NotBeEmpty();
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);

            // Validate connection
            ValidateConnection(client);

            // Check user after we validate connection again as it gets it from cached token
            client._connectionSvc.AuthContext.Account.Username.Should().BeEquivalentTo(Conn_UserName);
        }

        #region connectionValidationHelper

        private void ValidateConnection(ServiceClient client, bool usingExternalAuth = false)
        {
            if (!usingExternalAuth)
                client._connectionSvc.AuthContext.Should().NotBeNull();

            // Validate it
            var rslt = client.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "hh:mm:ss ";
            }));

            ILogger<ServiceClientTests> locallogger = loggerFactory.CreateLogger<ServiceClientTests>();
            locallogger.BeginScope("Beginning CloneLogger");
            // Clone it. - Validate use
            using (var client2 = client.Clone(locallogger))
            {
                rslt = client2.Execute(new WhoAmIRequest());
                Assert.IsType<WhoAmIResponse>(rslt);
            }

            // Create clone chain an break linkage.
            var client3 = client.Clone();
            var client4 = client3.Clone();
            rslt = client3.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);
            // dispose client3 explicitly
            client3.Dispose();
            rslt = client4.Execute(new WhoAmIRequest());
            Assert.IsType<WhoAmIResponse>(rslt);
            client4.Dispose();
        }

        #endregion


        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void RelatedEntityLiveTest()
        {
            // Live test is required due to support on server side being required to parse various configurations.
            // use ClientSecretConnection
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // connection params + secure string.
            var client = new ServiceClient(new Uri(Conn_Url), Conn_AppID, ServiceClient.MakeSecureString(Conn_Secret), true, logger: Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");


            // Use Entity class with entity logical name
            var account = new Entity("account");

            // Set attribute values
            // string primary name
            account["name"] = "Sample Account";

            // Create Primary contact
            var primaryContact = new Entity("contact");
            primaryContact["firstname"] = "James";
            primaryContact["lastname"] = "Kirk";

            EntityCollection contactCollection = new EntityCollection();
            var contact01 = new Entity("contact");
            contact01["lastname"] = "Spock";
            contactCollection.Entities.Add(contact01);

            var contact05 = new Entity("contact");
            contact05["firstname"] = "Lennard";
            contact05["lastname"] = "McCoy";
            contactCollection.Entities.Add(contact05);

            EntityCollection ec = new EntityCollection();
            var task1 = new Entity("task");
            task1["subject"] = "task1";
            task1["description"] = "task1-description";
            ec.Entities.Add(task1);
            primaryContact.RelatedEntities.Add(new Relationship("Contact_Tasks"), ec);

            // Add the contact to an EntityCollection
            EntityCollection primaryContactCollection = new EntityCollection();
            primaryContactCollection.Entities.Add(primaryContact);

            // Set the value to the relationship
            account.RelatedEntities[new Relationship("account_primary_contact")] = primaryContactCollection;
            account.RelatedEntities[new Relationship("contact_customer_accounts")] = contactCollection;

            // Create the account
            Guid accountid = client.Create(account);
            Assert.True(accountid != Guid.Empty);

            // Now delete
            client.Delete("account", accountid);
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void WhoAmITest()
        {
            try
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = "true";

                var client = CreateServiceClient();

                var whoAmIResponse = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
                whoAmIResponse.Should().NotBeNull();
                whoAmIResponse.OrganizationId.Should().NotBeEmpty();
                whoAmIResponse.UserId.Should().NotBeEmpty();
            }
            finally
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = null;
            }
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void RetrieveOrganizationInfoRequest_Test()
        {
            try
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = "true";

                var client = CreateServiceClient();

                var request = new RetrieveOrganizationInfoRequest();
                var response = client.Execute(request) as RetrieveOrganizationInfoResponse;
                response.Should().NotBeNull();
                response.organizationInfo.Should().NotBeNull();
                response.organizationInfo.Solutions.Should().NotBeEmpty();
            }
            finally
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = null;
            }
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ImportListDelete_Solution_Test()
        {
            try
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = "true";

                // Import
                var client = CreateServiceClient();
                client.ImportSolution(Path.Combine("TestMaterial", "TestSolution_1_0_0_1.zip"), out var importId);

                // List solutions and find the one that was imported
                client.ForceServerMetadataCacheConsistency = true;
                var listRequest = new RetrieveOrganizationInfoRequest();
                var listResponse = client.Execute(listRequest) as RetrieveOrganizationInfoResponse;
                client.ForceServerMetadataCacheConsistency = false;

                listResponse.Should().NotBeNull();
                listResponse.organizationInfo.Should().NotBeNull();
                listResponse.organizationInfo.Solutions.Should().NotBeEmpty();

                var solution = listResponse.organizationInfo.Solutions.Find(s => string.Compare(s.FriendlyName, "TestSolution", StringComparison.OrdinalIgnoreCase) == 0);
                solution.Should().NotBeNull();

                // Delete it
                var deleteRequest = new DeleteRequest() { Target = new EntityReference("solution", solution.Id) };
                var deleteResponse = client.Execute(deleteRequest) as DeleteResponse;
                deleteResponse.Should().NotBeNull();
            }
            finally
            {
                System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = null;
            }
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void ImportListDelete_Solution_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();
                var asyncTrackingId = client.ImportSolutionAsync(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                System.Threading.Thread.Sleep(10000);
                // List solutions and find the one that was imported
                client.ForceServerMetadataCacheConsistency = true;
                var listRequest = new RetrieveOrganizationInfoRequest();
                var listResponse = client.Execute(listRequest) as RetrieveOrganizationInfoResponse;
                client.ForceServerMetadataCacheConsistency = false;
                listResponse.Should().NotBeNull();
                listResponse.organizationInfo.Should().NotBeNull();
                listResponse.organizationInfo.Solutions.Should().NotBeEmpty();

                var solution = listResponse.organizationInfo.Solutions.Find(s => string.Compare(s.SolutionUniqueName, "PowerPlatformIPManagement", StringComparison.OrdinalIgnoreCase) == 0);
                solution.Should().NotBeNull();

                // Delete it
                var deleteRequest = new DeleteRequest() { Target = new EntityReference("solution", solution.Id) };
                var deleteResponse = client.Execute(deleteRequest) as DeleteResponse;
                deleteResponse.Should().NotBeNull();
                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest(true, "Test Does not work underload")]
        [Trait("Category", "Live Connect Required")]
        public void ImportAndUpgradePromote_Delete_Solution_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();

                // Import
                var client = CreateServiceClient();

                Ilogger.LogInformation("SETTING UP INTIAL SOLUTION TO UPGRADE");
                var asyncTrackingId = client.ImportSolutionAsync(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                Ilogger.LogInformation("SET UP INTIAL SOLUTION TO UPGRADE COMPLETE");

                _RunTime.Start();
                asyncTrackingId = client.ImportSolutionAsync(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_1_0_managed.zip"), out importId, importAsHoldingSolution: true);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                System.Threading.Thread.Sleep(10000);

                var listRequest = new RetrieveOrganizationInfoRequest();
                client.ForceServerMetadataCacheConsistency = true;
                var listResponse = client.Execute(listRequest) as RetrieveOrganizationInfoResponse;
                client.ForceServerMetadataCacheConsistency = false;
                listResponse.Should().NotBeNull();
                listResponse.organizationInfo.Should().NotBeNull();
                listResponse.organizationInfo.Solutions.Should().NotBeEmpty();
                var solution = listResponse.organizationInfo.Solutions.Find(s => string.Compare(s.SolutionUniqueName, "PowerPlatformIPManagement", StringComparison.OrdinalIgnoreCase) == 0);
                solution.Should().NotBeNull();

                System.Threading.Thread.Sleep(10000);
                _RunTime.Start();
                asyncTrackingId = client.DeleteAndPromoteSolutionAsync("PowerPlatformIPManagement");
                Assert.NotEqual(asyncTrackingId, Guid.Empty);
                _RunTime.Stop();

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);
                listRequest = new RetrieveOrganizationInfoRequest();
                client.ForceServerMetadataCacheConsistency = true;
                listResponse = client.Execute(listRequest) as RetrieveOrganizationInfoResponse;
                client.ForceServerMetadataCacheConsistency = false;
                listResponse.Should().NotBeNull();
                listResponse.organizationInfo.Should().NotBeNull();
                listResponse.organizationInfo.Solutions.Should().NotBeEmpty();
                solution = listResponse.organizationInfo.Solutions.Find(s => string.Compare(s.SolutionUniqueName, "PowerPlatformIPManagement", StringComparison.OrdinalIgnoreCase) == 0);
                solution.Should().NotBeNull();

                System.Threading.Thread.Sleep(10000);
                // Delete it - CLEAN IT UP 
                var deleteRequest = new DeleteRequest() { Target = new EntityReference("solution", solution.Id) };
                var deleteResponse = client.Execute(deleteRequest) as DeleteResponse;
                deleteResponse.Should().NotBeNull();

                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest(true, "Test Does not work underload")]
        [Trait("Category", "Live Connect Required")]
        public void ImportAndStageAndUpgrade_Solution_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();

                var asyncTrackingId = client.ImportSolutionAsync(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);
                // List solutions and find the one that was imported
                System.Threading.Thread.Sleep(10000);

                _RunTime.Start();
                client.ForceServerMetadataCacheConsistency = true;
                Dictionary<string, object> solutionPrams = new Dictionary<string, object>();
                solutionPrams.Add(ImportSolutionProperties.USESTAGEANDUPGRADEMODE, true);
                asyncTrackingId = client.ImportSolutionAsync(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_1_0_managed.zip"), out importId, extraParameters: solutionPrams);
                client.ForceServerMetadataCacheConsistency = false;

                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);
                _RunTime.Stop();

                System.Threading.Thread.Sleep(10000);
                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");

                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest(true, "Test Does not work underload")]
        [Trait("Category", "Live Connect Required")]
        public void ImportAndStageAndUpgrade_Solution_TestSync()
        {
            try
            {
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();

                var ImportJobId = client.ImportSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);

                _RunTime.Start();
                // List solutions and find the one that was imported
                client.ForceServerMetadataCacheConsistency = true;
                Dictionary<string, object> solutionPrams = new Dictionary<string, object>();
                solutionPrams.Add(ImportSolutionProperties.USESTAGEANDUPGRADEMODE, true);
                ImportJobId = client.ImportSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_1_0_managed.zip"), out importId, extraParameters: solutionPrams);
                client.ForceServerMetadataCacheConsistency = false;
                _RunTime.Stop();

                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");

                Assert.NotEqual(ImportJobId, Guid.Empty);

                Ilogger.LogInformation($"Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void StageSolution_File_TestSync()
        {
            Stopwatch _RunTime = new Stopwatch();
            // Import
            var client = CreateServiceClient();
            var StageSolutionResults = client.StageSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip")).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.NotNull(StageSolutionResults);
            Assert.NotEqual(StageSolutionResults.StageSolutionUploadId, Guid.Empty);

            // Clean up stages import
            client.Delete("stagesolutionupload", StageSolutionResults.StageSolutionUploadId);
        }

        //[SkippableConnectionTest]
        [Fact(Skip = "Broken API")]
        // [Trait("Category", "Live Connect Required")]
        public void StageSolution_File_Import_TestSync()
        {
            Stopwatch _RunTime = new Stopwatch();
            // Import
            var client = CreateServiceClient();

            // clean up existing solution if present
            DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");

            var StageSolutionResults = client.StageSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip")).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.NotNull(StageSolutionResults);
            if (!ValidateSolutionStageResults(StageSolutionResults))
            {
                throw new OperationCanceledException("StageSolution Failed due to Validation Error");
            }
            Assert.NotEqual(StageSolutionResults.StageSolutionUploadId, Guid.Empty);

            // Import Staged Solution Sync. 
            Guid returnedImportId = client.ImportSolution(StageSolutionResults.StageSolutionUploadId, out Guid guImportId);
            Assert.Null(client.LastException);
            Assert.NotEqual(returnedImportId, Guid.Empty);

            // Clean up Solution: 
            DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public void StageSolution_File_Import_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();

                // clean up existing solution if present
                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");

                _RunTime.Start();
                var StageSolutionResults = client.StageSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip")).ConfigureAwait(false).GetAwaiter().GetResult();
                Assert.NotNull(StageSolutionResults);
                if (!ValidateSolutionStageResults(StageSolutionResults))
                {
                    throw new OperationCanceledException("StageSolution Failed due to Validation Error");
                }
                Assert.NotEqual(StageSolutionResults.StageSolutionUploadId, Guid.Empty);

                var asyncTrackingId = client.ImportSolutionAsync(StageSolutionResults.StageSolutionUploadId, out var importId);
                Assert.Null(client.LastException);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                // Clean up Solution: 
                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");
                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest(true, "Test Does not work underload")]
        [Trait("Category", "Live Connect Required")]
        public void StageSolution_File_StageAndUpgrade_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();

                // clean up existing solution if present
                var guTrackingId = client.ImportSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);


                _RunTime.Start();
                var StageSolutionResults = client.StageSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_1_0_managed.zip")).ConfigureAwait(false).GetAwaiter().GetResult();
                Assert.NotNull(StageSolutionResults);
                if (!ValidateSolutionStageResults(StageSolutionResults))
                {
                    throw new OperationCanceledException("StageSolution Failed due to Validation Error");
                }
                Assert.NotEqual(StageSolutionResults.StageSolutionUploadId, Guid.Empty);


                Dictionary<string, object> solutionPrams = new Dictionary<string, object>();
                solutionPrams.Add(ImportSolutionProperties.USESTAGEANDUPGRADEMODE, true);
                var asyncTrackingId = client.ImportSolutionAsync(StageSolutionResults.StageSolutionUploadId, out importId, extraParameters: solutionPrams);
                Assert.Null(client.LastException);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                // Clean up Solution: 
                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");
                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest(true, "Test Does not work underload")]
        [Trait("Category", "Live Connect Required")]
        public void StageSolution_File_DeleteAndPromote_TestAsync()
        {
            try
            {
                Stopwatch _HoldTime = new Stopwatch();
                Stopwatch _RunTime = new Stopwatch();
                // Import
                var client = CreateServiceClient();

                client.ForceServerMetadataCacheConsistency = true;
                // clean up existing solution if present
                var guTrackingId = client.ImportSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_0_6_managed.zip"), out var importId);
                System.Threading.Thread.Sleep(10000);

                _RunTime.Start();
                var StageSolutionResults = client.StageSolution(Path.Combine("TestMaterial", "PowerPlatformIPManagement_1_0_1_0_managed.zip")).ConfigureAwait(false).GetAwaiter().GetResult();
                Assert.NotNull(StageSolutionResults);
                if (!ValidateSolutionStageResults(StageSolutionResults))
                {
                    throw new OperationCanceledException("StageSolution Failed due to Validation Error");
                }
                Assert.NotEqual(StageSolutionResults.StageSolutionUploadId, Guid.Empty);
                System.Threading.Thread.Sleep(10000);


                Dictionary<string, object> solutionPrams = new Dictionary<string, object>();
                //solutionPrams.Add(ImportSolutionProperties.USESTAGEANDUPGRADEMODE, true); // Cannot use stage Mode + staged solutionid for delete and promote. 
                var asyncTrackingId = client.ImportSolutionAsync(StageSolutionResults.StageSolutionUploadId, out importId, importAsHoldingSolution: true, extraParameters: solutionPrams);
                Assert.Null(client.LastException);
                Assert.NotEqual(asyncTrackingId, Guid.Empty);

                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                System.Threading.Thread.Sleep(10000);

                _RunTime.Start();
                asyncTrackingId = client.DeleteAndPromoteSolutionAsync("PowerPlatformIPManagement");
                Assert.NotEqual(asyncTrackingId, Guid.Empty);
                // Wait for Operation to complete
                WaitForAsyncOperationToComplete(_HoldTime, _RunTime, client, asyncTrackingId);

                System.Threading.Thread.Sleep(10000);
                // Clean up Solution: 
                DeleteSolutionFromSystem(client, "PowerPlatformIPManagement");
                Ilogger.LogInformation($"Hold Time: {_HoldTime.Elapsed}.  Run Time:{_RunTime.Elapsed}");
            }
            finally
            {
            }
        }

        [SkippableConnectionTest]
        [Trait("Category", "Live Connect Required")]
        public async void RequestBuilder_Execute()
        {
            // System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var Conn_AppID = System.Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var Conn_Secret = System.Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var Conn_Url = System.Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // Connection params.
            var client = new ServiceClient(new Uri(Conn_Url), Conn_AppID, Conn_Secret, true, Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            Entity a = new Entity("account");
            a["name"] = "Test Account"; 
            a.Id = Guid.NewGuid();

            var trackingId = a.Id;

            var rslt = await client.CreateRequestBuilder()
                .WithCorrelationId(Guid.NewGuid())
                .WithHeader("User-Agent", "TEST")
                .WithHeader("Foo", "TEST1")
                .CreateAsync(a).ConfigureAwait(false);
            Assert.IsType<Guid>(rslt);

            a["name"] = "Test Account - step 2";
            await client.CreateRequestBuilder().WithCorrelationId(Guid.NewGuid()).WithHeader("User-Agent", "TEST").WithHeader("Foo", "TEST1").UpdateAsync(a).ConfigureAwait(false);

            a["name"] = "Test Account - step 3";
            UpsertRequest upsert = new UpsertRequest();
            upsert.Target = a;
            var upResp = (UpsertResponse) await client.CreateRequestBuilder().WithCorrelationId(Guid.NewGuid()).WithHeader("User-Agent", "TEST").WithHeader("Foo", "TEST1").ExecuteAsync(upsert).ConfigureAwait(false);
            Assert.IsType<UpsertResponse>(upResp);
            Assert.False(upResp.RecordCreated);

            // retrieve
            var ret = (Entity)await client.CreateRequestBuilder().WithCorrelationId(Guid.NewGuid()).WithHeader("User-Agent", "TEST").WithHeader("Foo", "TEST1").RetrieveAsync("account", trackingId, new ColumnSet(true)).ConfigureAwait(false);
            ret.Should().NotBeNull();
            ret.Id.Should().Be(trackingId);
            ret["name"].Should().Be("Test Account - step 3");

            // delete
            await client.CreateRequestBuilder().WithCorrelationId(Guid.NewGuid()).WithHeader("User-Agent", "TEST").WithHeader("Foo", "TEST1").DeleteAsync("account", trackingId).ConfigureAwait(false);
            
        }

        // Not yet implemented
        //[SkippableConnectionTest]
        //[Trait("Category", "Live Connect Required")]
        private void RetrieveUserLicenseInfoTest()
        {
            System.Configuration.ConfigurationManager.AppSettings["UseWebApi"] = "true";

            var client = CreateServiceClient();

            client.UseWebApi = true;

            // First get user id
            var whoAmIResponse = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            whoAmIResponse.Should().NotBeNull();
            whoAmIResponse.OrganizationId.Should().NotBeEmpty();
            whoAmIResponse.UserId.Should().NotBeEmpty();

            // RetrieveUserLicenseInfo
            var retrieveUserLicenseInfoRequest = new RetrieveUserLicenseInfoRequest() { SystemUserId = whoAmIResponse.UserId };
            var retrieveUserLicenseInfoResponse = client.Execute(retrieveUserLicenseInfoRequest) as RetrieveUserLicenseInfoResponse;
            retrieveUserLicenseInfoResponse.Should().NotBeNull();
            retrieveUserLicenseInfoResponse.licenseInfo.Should().NotBeNull();
        }

        private ServiceClient CreateServiceClient()
        {
            var appID = Environment.GetEnvironmentVariable("XUNITCONNTESTAPPID");
            var secret = Environment.GetEnvironmentVariable("XUNITCONNTESTSECRET");
            var url = Environment.GetEnvironmentVariable("XUNITCONNTESTURI");

            // connection params + secure string.
            var client = new ServiceClient(new Uri(url), appID, ServiceClient.MakeSecureString(secret), true, logger: Ilogger);
            Assert.True(client.IsReady, "Failed to Create Connection via Constructor");

            return client;
        }

		private void WaitForAsyncOperationToComplete(Stopwatch _HoldTime, Stopwatch _RunTime, ServiceClient client, Guid? asyncTrackingId)
        {
            if (asyncTrackingId != null && asyncTrackingId != Guid.Empty)
            {
                // poll for status 
                var resp = client.GetAsyncOperationStatus(asyncTrackingId.Value).GetAwaiter().GetResult();
                if (resp != null && resp.State != AsyncStatusResponse.AsyncStatusResponse_statecode.FailedParse)
                {
                    while (resp.State != AsyncStatusResponse.AsyncStatusResponse_statecode.Completed)
                    {
                        // Wait a little bit because solution might not be immediately available
                        Ilogger.LogInformation($"Operation progress: {resp.State} - {resp.StatusCode_Localized}");
                        if (resp.State != AsyncStatusResponse.AsyncStatusResponse_statecode.Suspended && resp.State != AsyncStatusResponse.AsyncStatusResponse_statecode.Ready)
                        {
                            _HoldTime.Stop();
                            _RunTime.Start();
                        }
                        else
                        {
                            _HoldTime.Start();
                            _RunTime.Stop();
                        }
                        System.Threading.Thread.Sleep(5000);
                        resp = client.GetAsyncOperationStatus(asyncTrackingId.Value).GetAwaiter().GetResult();
                    }
                }
                Ilogger.LogInformation($"Operation Completed: {resp.State} - {resp.StatusCode_Localized}");
                _HoldTime.Stop();
                _RunTime.Stop();

                if (resp.StatusCode == AsyncStatusResponse.AsyncStatusResponse_statuscode.Failed)
                {
                    Ilogger.LogError(resp.Message);
                    throw new OperationCanceledException("Operation failed");
                }

            }
        }

        private void DeleteSolutionFromSystem(ServiceClient client, string solutionUnqiueName)
        {
            client.ForceServerMetadataCacheConsistency = true;
            var listRequest = new RetrieveOrganizationInfoRequest();
            var listResponse = client.Execute(listRequest) as RetrieveOrganizationInfoResponse;
            listResponse.Should().NotBeNull();
            listResponse.organizationInfo.Should().NotBeNull();
            listResponse.organizationInfo.Solutions.Should().NotBeEmpty();

            var solution = listResponse.organizationInfo.Solutions.Find(s => string.Compare(s.SolutionUniqueName, solutionUnqiueName, StringComparison.OrdinalIgnoreCase) == 0);
            if (solution != null)
            {
                // Delete it
                var deleteRequest = new DeleteRequest() { Target = new EntityReference("solution", solution.Id) };
                var deleteResponse = client.Execute(deleteRequest) as DeleteResponse;
                deleteResponse.Should().NotBeNull();
            }
            client.ForceServerMetadataCacheConsistency = false;
        }


        private bool ValidateSolutionStageResults(StageSolutionResults StageSolutionResults)
        {
            if (StageSolutionResults.SolutionValidationResults != null
                && StageSolutionResults.SolutionValidationResults.Any())
            {
                // Found validation errors. Emit them and throw. 
                foreach (var itm in StageSolutionResults.SolutionValidationResults)
                {
                    string msg = $"ErrorCode={itm.ErrorCode} | Msg={itm.Message} | AddInfo={itm.AdditionalInfo}";

                    switch (itm.SolutionValidationResultType)
                    {
                        case SolutionValidationResultType.Info:
                            Ilogger.LogInformation(msg);
                            break;
                        case SolutionValidationResultType.Warning:
                            Ilogger.LogWarning(msg);
                            break;
                        case SolutionValidationResultType.Error:
                            Ilogger.LogError(msg);
                            break;
                        default:
                            Ilogger.LogInformation(msg);
                            break;
                    }
                }
                if (StageSolutionResults.SolutionValidationResults.Where(w => w.SolutionValidationResultType.Equals(SolutionValidationResultType.Error)).Any())
                    return false;
            }
            return true;
        }

        #endregion

    }
}
