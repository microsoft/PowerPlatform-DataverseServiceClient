using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Cds.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.WebServiceClient;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace CdsClient_Core_UnitTests
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

        #region BoilerPlate
        public void SetupMockAndSupport( out Mock<IOrganizationService> moqOrgSvc , out Mock<MoqHttpMessagehander> moqHttpHandler , out CdsServiceClient cdsServiceClient )
        {
            var orgSvc = new Mock<IOrganizationService>();
            var fakHttpMethodHander = new Mock<MoqHttpMessagehander> { CallBase = true };
            var httpClientHandeler = new HttpClient(fakHttpMethodHander.Object, false);
            SetupWhoAmIHandlers(orgSvc);
            SetupMetadataHandlersForAccount(orgSvc);
            CdsServiceClient cli = new CdsServiceClient(orgSvc.Object, httpClientHandeler, new Version("9.1.2.0"));

            moqOrgSvc = orgSvc;
            moqHttpHandler = fakHttpMethodHander;
            cdsServiceClient = cli;
        }
        #endregion

        #region PreSetupResponses
        private void SetupWhoAmIHandlers(Mock<IOrganizationService> orgSvc)
        {
            // Who Am I Response 
            var whoAmIResponse = new WhoAmIResponse();
            whoAmIResponse.Results = new ParameterCollection();
            whoAmIResponse.Results.Add("UserId", _UserId);
            whoAmIResponse.Results.Add("BusinessUnitId", _BusinessUnitId);
            whoAmIResponse.Results.Add("OrganizationId", _OrganizationId);

            orgSvc.Setup(req => req.Execute(It.IsAny<WhoAmIRequest>())).Returns(whoAmIResponse);

            string _baseWebApiUriFormat = @"{0}/api/data/v{1}/";
            string _baseSoapOrgUriFormat = @"{0}/XRMServices/2011/Organization.svc";
            string directConnectUri = "https://testorg.crm.dynamics.com";

            EndpointCollection ep = new EndpointCollection();
            ep.Add(EndpointType.WebApplication, directConnectUri);
            ep.Add(EndpointType.OrganizationDataService, string.Format(_baseWebApiUriFormat, directConnectUri, "9.1"));
            ep.Add(EndpointType.OrganizationService, string.Format(_baseSoapOrgUriFormat, directConnectUri));

            OrganizationDetail d = new OrganizationDetail();
            d.FriendlyName = "DIRECTSET";
            d.OrganizationId = _OrganizationId;
            d.OrganizationVersion = "9.1.2.0";
            d.Geo = "NAM";
            d.State = OrganizationState.Enabled;
            d.UniqueName = "HOLD";
            d.UrlName = "HOLD";
            System.Reflection.PropertyInfo proInfo = d.GetType().GetProperty("Endpoints");
            if (proInfo != null)
            {
                proInfo.SetValue(d, ep, null);
            }

            RetrieveCurrentOrganizationResponse rawResp = new RetrieveCurrentOrganizationResponse();
            rawResp.ResponseName = "RetrieveCurrentOrganization";
            rawResp.Results.AddOrUpdateIfNotNull("Detail", d);
            RetrieveCurrentOrganizationResponse CurrentOrgResp = (RetrieveCurrentOrganizationResponse)rawResp;

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
