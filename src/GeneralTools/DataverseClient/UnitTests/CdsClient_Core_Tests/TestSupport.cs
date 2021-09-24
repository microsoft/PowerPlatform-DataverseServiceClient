using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.WebServiceClient;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client_Core_UnitTests
{
    public class TestSupport
    {
        #region SharedVars
        public Guid _UserId = Guid.Parse("22C50B6B-37E5-4E71-86EC-A062D42F38A6");
        public Guid _BusinessUnitId = Guid.Parse("6446AAF1-8435-4CBD-8872-F81BD0D69444");
        public Guid _OrganizationId = Guid.Parse("D6A74182-AD30-4879-99B4-F85BAA2909E9");
        public Guid _DefaultId = Guid.Parse("0A2E187D-0946-4FF8-B894-3391D4569CCC");
        public Guid _SampleAppID = Guid.Parse("51f81489-12ee-4a9e-aaae-a2591f45987d");
        public Uri _SampleAppRedirect = new Uri("app://58145B91-0C36-4500-8554-080854F2AC97");
        #endregion

        public ILogger logger { get; set; }

        #region BoilerPlate
        public void SetupMockAndSupport( out Mock<IOrganizationService> moqOrgSvc , out Mock<MoqHttpMessagehander> moqHttpHandler , out ServiceClient cdsServiceClient , Version requestedCdsVersion = null)
        {
            if (requestedCdsVersion is null)
                requestedCdsVersion = new Version("9.1.2.0");

            var orgSvc = new Mock<IOrganizationService>();
            var fakHttpMethodHander = new Mock<MoqHttpMessagehander>() { CallBase = true };
            string baseTestUrl = "https://testorg.crm.dynamics.com";
            SetupWhoAmIHandlers(orgSvc , fakHttpMethodHander, baseTestUrl, requestedCdsVersion);
            SetupMetadataHandlersForAccount(orgSvc);

            var httpClientHandeler = new HttpClient(fakHttpMethodHander.Object, false);
            ServiceClient cli = new ServiceClient(orgSvc.Object, httpClientHandeler, baseTestUrl, requestedCdsVersion , logger);

            moqOrgSvc = orgSvc;
            moqHttpHandler = fakHttpMethodHander;
            cdsServiceClient = cli;
        }
        #endregion

        #region PreSetupResponses
        private void SetupWhoAmIHandlers(Mock<IOrganizationService> orgSvc , Mock<MoqHttpMessagehander> moqHttpHandler, string baseTestUrl , Version requestedCdsVersion)
        {
            // Who Am I Response
            var whoAmIResponse = new WhoAmIResponse();
            whoAmIResponse.Results = new ParameterCollection();
            whoAmIResponse.Results.Add("UserId", _UserId);
            whoAmIResponse.Results.Add("BusinessUnitId", _BusinessUnitId);
            whoAmIResponse.Results.Add("OrganizationId", _OrganizationId);
            orgSvc.Setup(req => req.Execute(It.IsAny<WhoAmIRequest>())).Returns(whoAmIResponse);

            HttpResponseMessage createWhoAmIRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createWhoAmIRespMsg.Content = new StringContent(string.Format(DataverseClient_Core_UnitTests.Properties.Resources.WhoAmIResponse, "TestOrg", _BusinessUnitId, _UserId, _OrganizationId));
            moqHttpHandler.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("get", StringComparison.OrdinalIgnoreCase) && f.RequestUri.ToString().Contains("WhoAmI")))).Returns(createWhoAmIRespMsg);

            string _baseWebApiUriFormat = @"{0}/api/data/v{1}/";
            string _baseSoapOrgUriFormat = @"{0}/XRMServices/2011/Organization.svc";
            string directConnectUri = baseTestUrl;

            EndpointCollection ep = new EndpointCollection();
            ep.Add(EndpointType.WebApplication, directConnectUri);
            ep.Add(EndpointType.OrganizationDataService, string.Format(_baseWebApiUriFormat, directConnectUri, $"{requestedCdsVersion.Major}.{requestedCdsVersion.Minor}"));
            ep.Add(EndpointType.OrganizationService, string.Format(_baseSoapOrgUriFormat, directConnectUri));

            OrganizationDetail d = new OrganizationDetail();
            d.FriendlyName = "DIRECTSET";
            d.OrganizationId = _OrganizationId;
            d.OrganizationVersion = requestedCdsVersion.ToString();
            d.Geo = "NAM";
            d.State = OrganizationState.Enabled;
            d.UniqueName = "HOLD";
            d.UrlName = "HOLD";
            System.Reflection.PropertyInfo proInfo = d.GetType().GetProperty("Endpoints");
            if (proInfo != null)
            {
                proInfo.SetValue(d, ep, null);
            }

            string httpResponseVersion = string.Format(DataverseClient_Core_UnitTests.Properties.Resources.RetrieveCurrentOrg,
                directConnectUri,
                d.OrganizationId,
                d.FriendlyName,
                d.OrganizationVersion,
                d.EnvironmentId,
                d.DatacenterId,
                d.Geo,
                d.TenantId,
                d.UrlName,
                d.UniqueName,
                d.State,
                directConnectUri,
                string.Format(_baseWebApiUriFormat, directConnectUri, $"{requestedCdsVersion.Major}.{requestedCdsVersion.Minor}"),
                 string.Format(_baseSoapOrgUriFormat, directConnectUri));

            HttpResponseMessage createRetrieveCurrentOrganizationRespMsg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            createRetrieveCurrentOrganizationRespMsg.Content = new StringContent(httpResponseVersion);

            RetrieveCurrentOrganizationResponse rawResp = new RetrieveCurrentOrganizationResponse();
            rawResp.ResponseName = "RetrieveCurrentOrganization";
            rawResp.Results.AddOrUpdateIfNotNull("Detail", d);
            RetrieveCurrentOrganizationResponse CurrentOrgResp = (RetrieveCurrentOrganizationResponse)rawResp;

            moqHttpHandler.Setup(s => s.Send(It.Is<HttpRequestMessage>(f => f.Method.ToString().Equals("GET", StringComparison.OrdinalIgnoreCase) && f.RequestUri.ToString().Contains("RetrieveCurrentOrganization")))).Returns(createRetrieveCurrentOrganizationRespMsg);
            orgSvc.Setup(f => f.Execute(It.IsAny<RetrieveCurrentOrganizationRequest>())).Returns(CurrentOrgResp);
        }

        private void SetupMetadataHandlersForAccount(Mock<IOrganizationService> orgSvc)
        {
            EntityMetadata entityMetadata = new EntityMetadata();
            entityMetadata.LogicalName = "account";
            entityMetadata.SchemaName = "account";
            entityMetadata.EntitySetName = "accounts";
            entityMetadata.DisplayName = new Label("Account", 1033);
            entityMetadata.DisplayName.UserLocalizedLabel = new LocalizedLabel("Account", 1033);
            entityMetadata.DisplayCollectionName = new Label("Accounts", 1033);
            entityMetadata.DisplayCollectionName.UserLocalizedLabel = new LocalizedLabel("Accounts", 1033);

            var attribmetadata = new List<AttributeMetadata>()
            {
                new DateTimeAttributeMetadata(DateTimeFormat.DateOnly , "dateonlyfield" ),
                new DateTimeAttributeMetadata(DateTimeFormat.DateAndTime , "datetimeNormal"),
                new DateTimeAttributeMetadata(DateTimeFormat.DateAndTime , "datetimeTZindependant")
                {
                    DateTimeBehavior = new DateTimeBehavior(){ Value = "TimeZoneIndependent" }
                }
            };

            //entityMetadata.ManyToOneRelationships
            var ManyToOneRels = new List<OneToManyRelationshipMetadata>() {
            new OneToManyRelationshipMetadata()
            {
                ReferencingAttribute = "field02",
                ReferencedEntity = "account",
                ReferencingEntityNavigationPropertyName = "field02_account"
            },
            new OneToManyRelationshipMetadata()
            {
                ReferencingAttribute = "field02",
                ReferencedEntity = "contact",
                ReferencingEntityNavigationPropertyName = "field02_contact"
            },
            new OneToManyRelationshipMetadata()
            {
                ReferencingAttribute = "field07",
                ReferencedEntity = "account",
                ReferencingEntityNavigationPropertyName = "field07account"
            }
            };

            System.Reflection.PropertyInfo proInfo = entityMetadata.GetType().GetProperty("ManyToOneRelationships");
            if (proInfo != null)
            {
                proInfo.SetValue(entityMetadata, ManyToOneRels.ToArray(), null);
            };

            System.Reflection.PropertyInfo proInfo1 = entityMetadata.GetType().GetProperty("ObjectTypeCode");
            if (proInfo1 != null)
            {
                proInfo1.SetValue(entityMetadata, 1, null);
            }

            System.Reflection.PropertyInfo proInfo3 = entityMetadata.GetType().GetProperty("Attributes");
            if (proInfo3 != null)
            {
                proInfo3.SetValue(entityMetadata, attribmetadata.ToArray(), null);
            }

            System.Reflection.PropertyInfo proInfo4 = entityMetadata.GetType().GetProperty("PrimaryIdAttribute");
            if (proInfo4 != null)
            {
                proInfo4.SetValue(entityMetadata, "accountid", null);
            }

            RetrieveEntityResponse retrieveEntityResponse = new RetrieveEntityResponse();
            retrieveEntityResponse.Results.Add("EntityMetadata", entityMetadata);

            List<EntityMetadata> entities = new List<EntityMetadata>();
            entities.Add(entityMetadata);

            RetrieveAllEntitiesResponse retrieveAllEntitiesResponse = new RetrieveAllEntitiesResponse();
            retrieveAllEntitiesResponse.Results.Add("EntityMetadata", entities.ToArray());

            orgSvc.Setup(f => f.Execute(It.IsAny<RetrieveEntityRequest>())).Returns(retrieveEntityResponse);
            orgSvc.Setup(f => f.Execute(It.IsAny<RetrieveAllEntitiesRequest>())).Returns(retrieveAllEntitiesResponse);

        }

        #endregion

    }
}
