using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Moq;
using System;
using System.Collections.Generic;
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
        #endregion


        #region PreSetupResponses
        public void SetupWhoAmIHandlers(Mock<IOrganizationService> orgSvc)
        {
            // Who Am I Response 
            var whoAmIResponse = new WhoAmIResponse();
            whoAmIResponse.Results = new ParameterCollection();
            whoAmIResponse.Results.Add("UserId", _UserId);
            whoAmIResponse.Results.Add("BusinessUnitId", _BusinessUnitId);
            whoAmIResponse.Results.Add("OrganizationId", _OrganizationId);

            orgSvc.Setup(req => req.Execute(It.IsAny<WhoAmIRequest>())).Returns(whoAmIResponse);
        }

        public void SetupMetadataHandlersForAccount(Mock<IOrganizationService> orgSvc)
        {
            EntityMetadata entityMetadata = new EntityMetadata();
            entityMetadata.LogicalName = "account";
            entityMetadata.SchemaName = "account";
            entityMetadata.DisplayName = new Label("Account", 1033);
            entityMetadata.DisplayName.UserLocalizedLabel = new LocalizedLabel("Account", 1033);
            entityMetadata.DisplayCollectionName = new Label("Accounts", 1033);
            entityMetadata.DisplayCollectionName.UserLocalizedLabel = new LocalizedLabel("Accounts", 1033);
            System.Reflection.PropertyInfo proInfo = entityMetadata.GetType().GetProperty("ObjectTypeCode");
            if (proInfo != null)
            {
                proInfo.SetValue(entityMetadata, 1, null);
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
