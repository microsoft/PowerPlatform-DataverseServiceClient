using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Extensions for interacting with the Dataverse Metadata system. 
    /// </summary>
    public static class MetadataExtensions
    {
        #region Dataverse MetadataService methods


        /// <summary>
        /// Gets a PickList, Status List or StateList from the metadata of an attribute
        /// </summary>
        /// <param name="targetEntity">text name of the entity to query</param>
        /// <param name="attribName">name of the attribute to query</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static PickListMetaElement GetPickListElementFromMetadataEntity(this ServiceClient serviceClient, string targetEntity, string attribName)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService != null)
            {
                List<AttributeData> attribDataList = serviceClient._dynamicAppUtility.GetAttributeDataByEntity(targetEntity, attribName);
                if (attribDataList.Count > 0)
                {
                    // have data..
                    // need to make sure its really a pick list.
                    foreach (AttributeData attributeData in attribDataList)
                    {
                        switch (attributeData.AttributeType)
                        {
                            case AttributeTypeCode.Picklist:
                            case AttributeTypeCode.Status:
                            case AttributeTypeCode.State:
                                PicklistAttributeData pick = (PicklistAttributeData)attributeData;
                                PickListMetaElement resp = new PickListMetaElement((string)pick.ActualValue, pick.AttributeLabel, pick.DisplayValue);
                                if (pick.PicklistOptions != null)
                                {
                                    foreach (OptionMetadata opt in pick.PicklistOptions)
                                    {
                                        PickListItem itm = null;
                                        itm = new PickListItem((string)GetLocalLabel(opt.Label), (int)opt.Value.Value);
                                        resp.Items.Add(itm);
                                    }
                                }
                                return resp;
                            default:
                                break;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a global option set from Dataverse.
        /// </summary>
        /// <param name="globalOptionSetName">Name of the Option Set To get</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>OptionSetMetadata or null</returns>
        public static OptionSetMetadata GetGlobalOptionSetMetadata(this ServiceClient serviceClient, string globalOptionSetName)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            try
            {
                return serviceClient._metadataUtlity.GetGlobalOptionSetMetadata(globalOptionSetName);
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting optionset metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }


        /// <summary>
        /// Returns a list of entities with basic data from Dataverse
        /// </summary>
        /// <param name="onlyPublished">defaults to true, will only return published information</param>
        /// <param name="filter">EntityFilter to apply to this request, note that filters other then Default will consume more time.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static List<EntityMetadata> GetAllEntityMetadata(this ServiceClient serviceClient, bool onlyPublished = true, EntityFilters filter = EntityFilters.Default)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return serviceClient._metadataUtlity.GetAllEntityMetadata(onlyPublished, filter);
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from CDS   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns the Metadata for an entity from Dataverse, defaults to basic data only.
        /// </summary>
        /// <param name="entityLogicalname">Logical name of the entity</param>
        /// <param name="queryFilter">filter to apply to the query, defaults to default entity data.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static EntityMetadata GetEntityMetadata(this ServiceClient serviceClient, string entityLogicalname, EntityFilters queryFilter = EntityFilters.Default)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return serviceClient._metadataUtlity.GetEntityMetadata(queryFilter, entityLogicalname);
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns the Form Entity References for a given form type.
        /// </summary>
        /// <param name="entityLogicalname">logical name of the entity you are querying for form data.</param>
        /// <param name="formTypeId">Form Type you want</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>List of Entity References for the form type requested.</returns>
        public static List<EntityReference> GetEntityFormIdListByType(this ServiceClient serviceClient, string entityLogicalname, FormTypeId formTypeId)
        {
            serviceClient._logEntry.ResetLastError();
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                serviceClient._logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                RetrieveFilteredFormsRequest req = new RetrieveFilteredFormsRequest();
                req.EntityLogicalName = entityLogicalname;
                req.FormType = new OptionSetValue((int)formTypeId);
                RetrieveFilteredFormsResponse resp = (RetrieveFilteredFormsResponse)serviceClient.Command_Execute(req, "GetEntityFormIdListByType");
                if (resp != null)
                    return resp.SystemForms.ToList();
                else
                    return null;
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Returns all attributes on a entity
        /// </summary>
        /// <param name="entityLogicalname">returns all attributes on a entity</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static List<AttributeMetadata> GetAllAttributesForEntity(this ServiceClient serviceClient, string entityLogicalname)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                serviceClient._logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return serviceClient._metadataUtlity.GetAllAttributesMetadataByEntity(entityLogicalname);
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Gets metadata for a specific entity's attribute.
        /// </summary>
        /// <param name="entityLogicalname">Name of the entity</param>
        /// <param name="attribName">Attribute Name</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static AttributeMetadata GetEntityAttributeMetadataForAttribute(this ServiceClient serviceClient, string entityLogicalname, string attribName)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }
            if (string.IsNullOrWhiteSpace(entityLogicalname))
            {
                serviceClient._logEntry.Log("An Entity Name must be supplied", TraceEventType.Error);
                return null;
            }
            #endregion

            try
            {
                return serviceClient._metadataUtlity.GetAttributeMetadata(entityLogicalname, attribName);
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return null;
        }

        /// <summary>
        /// Gets an Entity Name by Logical name or Type code.
        /// </summary>
        /// <param name="entityName">logical name of the entity </param>
        /// <param name="entityTypeCode">Type code for the entity </param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Localized name for the entity in the current users language</returns>
        public static string GetEntityDisplayName(this ServiceClient serviceClient, string entityName, int entityTypeCode = -1)
        {
            return serviceClient.GetEntityDisplayNameImpl(entityName, entityTypeCode);
        }

        /// <summary>
        /// Gets an Entity Name by Logical name or Type code.
        /// </summary>
        /// <param name="entityName">logical name of the entity </param>
        /// <param name="entityTypeCode">Type code for the entity </param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Localized plural name for the entity in the current users language</returns>
        public static string GetEntityDisplayNamePlural(this ServiceClient serviceClient, string entityName, int entityTypeCode = -1)
        {
            return serviceClient.GetEntityDisplayNameImpl(entityName, entityTypeCode, true);
        }

        /// <summary>
        /// This will clear the Metadata cache for either all entities or the specified entity
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="entityName">Optional: name of the entity to clear cached info for</param>
        public static void ResetLocalMetadataCache(this ServiceClient serviceClient, string entityName = "")
        {
            if (serviceClient._metadataUtlity != null)
                serviceClient._metadataUtlity.ClearCachedEntityMetadata(entityName);
        }

        /// <summary>
        /// Gets the Entity Display Name.
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityTypeCode"></param>
        /// <param name="getPlural"></param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        private static string GetEntityDisplayNameImpl(this ServiceClient serviceClient, string entityName, int entityTypeCode = -1, bool getPlural = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return string.Empty;
            }

            if (entityTypeCode == -1 && string.IsNullOrWhiteSpace(entityName))
            {
                serviceClient._logEntry.Log("Target entity or Type code is required", TraceEventType.Error);
                return string.Empty;
            }
            #endregion

            try
            {
                // Get the entity by type code if necessary.
                if (entityTypeCode != -1)
                    entityName = serviceClient._metadataUtlity.GetEntityLogicalName(entityTypeCode);

                if (string.IsNullOrWhiteSpace(entityName))
                {
                    serviceClient._logEntry.Log("Target entity or Type code is required", TraceEventType.Error);
                    return string.Empty;
                }

                // Pull Object type code for this object.
                EntityMetadata entData =
                    serviceClient._metadataUtlity.GetEntityMetadata(EntityFilters.Entity, entityName);

                if (entData != null)
                {
                    if (getPlural)
                    {
                        if (entData.DisplayCollectionName != null && entData.DisplayCollectionName.UserLocalizedLabel != null)
                            return entData.DisplayCollectionName.UserLocalizedLabel.Label;
                        else
                            return entityName; // Default to echo the same name back
                    }
                    else
                    {
                        if (entData.DisplayName != null && entData.DisplayName.UserLocalizedLabel != null)
                            return entData.DisplayName.UserLocalizedLabel.Label;
                        else
                            return entityName; // Default to echo the same name back
                    }
                }

            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the typecode of an entity by name.
        /// </summary>
        /// <param name="entityName">name of the entity to get the type code on</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static string GetEntityTypeCode(this ServiceClient serviceClient, string entityName)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return string.Empty;
            }

            if (string.IsNullOrEmpty(entityName))
            {
                serviceClient._logEntry.Log("Target entity is required", TraceEventType.Error);
                return string.Empty;
            }
            #endregion

            try
            {

                // Pull Object type code for this object.
                EntityMetadata entData =
                    serviceClient._metadataUtlity.GetEntityMetadata(EntityFilters.Entity, entityName);

                if (entData != null)
                {
                    if (entData.ObjectTypeCode != null && entData.ObjectTypeCode.HasValue)
                    {
                        return entData.ObjectTypeCode.Value.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                serviceClient._logEntry.Log("************ Exception getting metadata info from Dataverse   : " + ex.Message, TraceEventType.Error);
            }
            return string.Empty;
        }


        /// <summary>
        /// Returns the Entity name for the given Type code
        /// </summary>
        /// <param name="entityTypeCode"></param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static string GetEntityName(this ServiceClient serviceClient, int entityTypeCode)
        {
            return serviceClient._metadataUtlity.GetEntityLogicalName(entityTypeCode);
        }


        /// <summary>
        /// Adds an option to a pick list on an entity.
        /// </summary>
        /// <param name="targetEntity">Entity Name to Target</param>
        /// <param name="attribName">Attribute Name on the Entity</param>
        /// <param name="locLabelList">List of Localized Labels</param>
        /// <param name="valueData">integer Value</param>
        /// <param name="publishOnComplete">Publishes the Update to the Live system.. note this is a time consuming process.. if you are doing a batch up updates, call PublishEntity Separately when you are finished.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, on fail check last error.</returns>
        public static bool CreateOrUpdatePickListElement(this ServiceClient serviceClient, string targetEntity, string attribName, List<LocalizedLabel> locLabelList, int valueData, bool publishOnComplete)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            #region Basic Checks
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(targetEntity))
            {
                serviceClient._logEntry.Log("Target entity is required", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(attribName))
            {
                serviceClient._logEntry.Log("Target attribute name is required", TraceEventType.Error);
                return false;
            }

            if (locLabelList == null || locLabelList.Count == 0)
            {
                serviceClient._logEntry.Log("Target Labels are required", TraceEventType.Error);
                return false;
            }

            serviceClient.LoadLCIDs(); // Load current languages .

            // Clear out the Metadata for this object.
            if (serviceClient._metadataUtlity != null)
                serviceClient._metadataUtlity.ClearCachedEntityMetadata(targetEntity);

            EntityMetadata entData =
                serviceClient._metadataUtlity.GetEntityMetadata(targetEntity);

            if (!entData.IsCustomEntity.Value)
            {
                // Only apply this if the entity is not a custom entity
                if (valueData <= 199999)
                {
                    serviceClient._logEntry.Log("Option Value must exceed 200000", TraceEventType.Error);
                    return false;
                }
            }
            #endregion

            // get the values for the requested attribute.
            PickListMetaElement listData = serviceClient.GetPickListElementFromMetadataEntity(targetEntity, attribName);
            if (listData == null)
            {
                // error here.
            }

            bool isUpdate = false;
            if (listData.Items != null && listData.Items.Count != 0)
            {
                // Check to see if the value we are looking to insert already exists by name or value.
                List<string> DisplayLabels = new List<string>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (DisplayLabels.Contains(loclbl.Label))
                        continue;
                    else
                        DisplayLabels.Add(loclbl.Label);
                }

                foreach (PickListItem pItem in listData.Items)
                {
                    // check the value by id.
                    if (pItem.PickListItemId == valueData)
                    {
                        if (DisplayLabels.Contains(pItem.DisplayLabel))
                        {
                            DisplayLabels.Clear();
                            serviceClient._logEntry.Log("PickList Element exists, No Change required.", TraceEventType.Error);
                            return false;
                        }
                        isUpdate = true;
                        break;
                    }

                    //// Check the value by name...  by putting this hear, we will handle a label update vs a Duplicate label.
                    if (DisplayLabels.Contains(pItem.DisplayLabel))
                    {
                        // THis is an ERROR State... While Dataverse will allow 2 labels with the same text, it looks weird.
                        DisplayLabels.Clear();
                        serviceClient._logEntry.Log("Label Name exists, Please use a different display name for the label.", TraceEventType.Error);
                        return false;
                    }
                }

                DisplayLabels.Clear();
            }

            if (isUpdate)
            {
                // update request
                UpdateOptionValueRequest updateReq = new UpdateOptionValueRequest();
                updateReq.AttributeLogicalName = attribName;
                updateReq.EntityLogicalName = targetEntity;
                updateReq.Label = new Label();
                List<LocalizedLabel> lblList = new List<LocalizedLabel>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (serviceClient._loadedLCIDList.Contains(loclbl.LanguageCode))
                    {
                        LocalizedLabel lbl = new LocalizedLabel()
                        {
                            Label = loclbl.Label,
                            LanguageCode = loclbl.LanguageCode
                        };
                        lblList.Add(lbl);
                    }
                }
                updateReq.Label.LocalizedLabels.AddRange(lblList.ToArray());
                updateReq.Value = valueData;
                updateReq.MergeLabels = true;

                UpdateOptionValueResponse UpdateResp = (UpdateOptionValueResponse)serviceClient.Command_Execute(updateReq, "Updating a PickList Element in Dataverse");
                if (UpdateResp == null)
                    return false;
            }
            else
            {
                // create request.
                // Create a new insert request
                InsertOptionValueRequest req = new InsertOptionValueRequest();

                req.AttributeLogicalName = attribName;
                req.EntityLogicalName = targetEntity;
                req.Label = new Label();
                List<LocalizedLabel> lblList = new List<LocalizedLabel>();
                foreach (LocalizedLabel loclbl in locLabelList)
                {
                    if (serviceClient._loadedLCIDList.Contains(loclbl.LanguageCode))
                    {
                        LocalizedLabel lbl = new LocalizedLabel()
                        {
                            Label = loclbl.Label,
                            LanguageCode = loclbl.LanguageCode
                        };
                        lblList.Add(lbl);
                    }
                }
                req.Label.LocalizedLabels.AddRange(lblList.ToArray());
                req.Value = valueData;


                InsertOptionValueResponse resp = (InsertOptionValueResponse)serviceClient.Command_Execute(req, "Creating a PickList Element in Dataverse");
                if (resp == null)
                    return false;

            }

            // Publish the update if asked to.
            if (publishOnComplete)
                return serviceClient.PublishEntity(targetEntity);
            else
                return true;
        }

        /// <summary>
        /// Publishes an entity to the production system,
        /// used in conjunction with the Metadata services.
        /// </summary>
        /// <param name="entityName">Name of the entity to publish</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>True on success</returns>
        public static bool PublishEntity(this ServiceClient serviceClient, string entityName)
        {
            // Now Publish the update.
            string sPublishUpdateXml =
                           string.Format(CultureInfo.InvariantCulture, "<importexportxml><entities><entity>{0}</entity></entities><nodes /><securityroles/><settings/><workflows/></importexportxml>",
                           entityName);

            PublishXmlRequest pubReq = new PublishXmlRequest();
            pubReq.ParameterXml = sPublishUpdateXml;

            PublishXmlResponse rsp = (PublishXmlResponse)serviceClient.Command_Execute(pubReq, "Publishing a PickList Element in Dataverse");
            if (rsp != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Loads the Currently loaded languages for Dataverse
        /// </summary>
        /// <returns></returns>
        internal static bool LoadLCIDs(this ServiceClient serviceClient)
        {
            // Now Publish the update.
            // Check to see if the Language ID's are loaded.
            if (serviceClient._loadedLCIDList == null)
            {
                serviceClient._loadedLCIDList = new List<int>();

                // load the Dataverse Language List.
                RetrieveAvailableLanguagesRequest lanReq = new RetrieveAvailableLanguagesRequest();
                RetrieveAvailableLanguagesResponse rsp = (RetrieveAvailableLanguagesResponse)serviceClient.Command_Execute(lanReq, "Reading available languages from Dataverse");
                if (rsp == null)
                    return false;
                if (rsp.LocaleIds != null)
                {
                    foreach (int iLCID in rsp.LocaleIds)
                    {
                        if (serviceClient._loadedLCIDList.Contains(iLCID))
                            continue;
                        else
                            serviceClient._loadedLCIDList.Add(iLCID);
                    }
                }
            }
            return true;
        }

        #endregion

        #region Utilities 

        
        /// <summary>
        /// Adds values for an update to a Dataverse propertyList
        /// </summary>
        /// <param name="Field"></param>
        /// <param name="PropertyList"></param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        internal static void AddValueToPropertyList(this ServiceClient serviceClient, KeyValuePair<string, DataverseDataTypeWrapper> Field, AttributeCollection PropertyList)
        {
            if (string.IsNullOrEmpty(Field.Key))
                // throw exception
                throw new System.ArgumentOutOfRangeException("valueArray", "Missing Dataverse field name");

            try
            {
                switch (Field.Value.Type)
                {

                    case DataverseFieldType.Boolean:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (bool)Field.Value.Value));
                        break;

                    case DataverseFieldType.DateTime:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (DateTime)Field.Value.Value));
                        break;

                    case DataverseFieldType.Decimal:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Convert.ToDecimal(Field.Value.Value)));
                        break;

                    case DataverseFieldType.Float:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Convert.ToDouble(Field.Value.Value)));
                        break;

                    case DataverseFieldType.Money:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new Money(Convert.ToDecimal(Field.Value.Value))));
                        break;

                    case DataverseFieldType.Number:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (int)Field.Value.Value));
                        break;

                    case DataverseFieldType.Customer:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new EntityReference(Field.Value.ReferencedEntity, (Guid)Field.Value.Value)));
                        break;

                    case DataverseFieldType.Lookup:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new EntityReference(Field.Value.ReferencedEntity, (Guid)Field.Value.Value)));
                        break;

                    case DataverseFieldType.Picklist:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, new OptionSetValue((int)Field.Value.Value)));
                        break;

                    case DataverseFieldType.String:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (string)Field.Value.Value));
                        break;

                    case DataverseFieldType.Raw:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, Field.Value.Value));
                        break;

                    case DataverseFieldType.UniqueIdentifier:
                        PropertyList.Add(new KeyValuePair<string, object>(Field.Key, (Guid)Field.Value.Value));
                        break;
                }
            }
            catch (InvalidCastException castEx)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed when casting DataverseDataTypeWrapper wrapped objects to the Dataverse Type. Field : {0}", Field.Key), TraceEventType.Error, castEx);
                throw;
            }
            catch (System.Exception ex)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed when casting DataverseDataTypeWrapper wrapped objects to the Dataverse Type. Field : {0}", Field.Key), TraceEventType.Error, ex);
                throw;
            }

        }

        /// <summary>
        /// Get the localize label from a Dataverse Label.
        /// </summary>
        /// <param name="localLabel"></param>
        /// <returns></returns>
        private static string GetLocalLabel(Label localLabel)
        {
            foreach (LocalizedLabel lbl in localLabel.LocalizedLabels)
            {
                // try to get the current display language code.
                if (lbl.LanguageCode == CultureInfo.CurrentUICulture.LCID)
                {
                    return lbl.Label;
                }
            }
            return localLabel.UserLocalizedLabel.Label;
        }

        #endregion
    }
}