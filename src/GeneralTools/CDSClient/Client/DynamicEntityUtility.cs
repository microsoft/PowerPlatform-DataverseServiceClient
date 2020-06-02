using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Summary description for DynamicEntityUtility
	/// </summary>
	internal sealed class DynamicEntityUtility
	{
		CdsServiceClient svcAct = null;
		MetadataUtility metadataUtil = null;
		public DynamicEntityUtility(CdsServiceClient svcActions, MetadataUtility metaUtility)
		{
			svcAct = svcActions;
			metadataUtil = metaUtility;
		}

		/// <summary>
		/// Internal Use only
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="attributes"></param>
		/// <returns></returns>
		internal List<AttributeData> GetAttributeDataByEntity(string entityName, params string[] attributes)
		{
			return GetAttributeDataByEntity(entityName, Guid.Empty, attributes);
		}

		/// <summary>
		/// Retrieve metadata for a single CRM record
		/// </summary>
		/// <param name="entityName">A string name of the entity type being retrieved e.g. contact</param>
		/// <param name="entityId">A Guid representing the record id we want to retrieve</param>
		/// <param name="attributes">An array of strings representing the list of attributes we want to retrieve</param>
		/// <returns></returns>
		internal List<AttributeData> GetAttributeDataByEntity(string entityName, Guid entityId, params string[] attributes)
		{
			List<AttributeData> attributeData = new List<AttributeData>();
			Boolean isExistingEntity = false;

			Entity existingRecord = null;
			// OPTION: Metadata can be cached for better performance.
			List<AttributeMetadata> allAttributesMetadata = metadataUtil.GetAllAttributesMetadataByEntity(entityName);
			//if the guid has been supplied then try and retrieve the record
			if (entityId != Guid.Empty)
			{
				existingRecord = RetrieveByIdAsDynamicEntity(entityName, entityId, attributes);
				isExistingEntity = true;
			}

			//for each attribute
			foreach (String attribute in attributes)
			{
				AttributeData data = new AttributeData();
				data.IsUnsupported = false;

				// Attribute label and type apply to all attributes, as they are metadata info.
				AttributeMetadata metadata = allAttributesMetadata.Find(delegate(AttributeMetadata a) { return (a.SchemaName.Equals(attribute, StringComparison.CurrentCultureIgnoreCase)); });
				if (metadata != null)
				{
					switch (metadata.AttributeType.Value)
					{
						case AttributeTypeCode.Boolean:
							BooleanAttributeData booleanData = new BooleanAttributeData();
							booleanData.BooleanOptions = new OptionMetadata[] { new OptionMetadata(new Label("true", 1033), 1), new OptionMetadata(new Label("false", 1033), 0) };
							data = booleanData;
							break;
						case AttributeTypeCode.Picklist:
							PicklistAttributeData picklistData = new PicklistAttributeData();
							picklistData.PicklistOptions = ((PicklistAttributeMetadata)metadata).OptionSet.Options.ToArray();
							data = picklistData;
							break;
						case AttributeTypeCode.Status:
							PicklistAttributeData statusData = new PicklistAttributeData();
							List<OptionMetadata> options = new List<OptionMetadata>();
							foreach (OptionMetadata option in ((StatusAttributeMetadata)metadata).OptionSet.Options)
							{
								options.Add(option);
							}
							statusData.PicklistOptions = options.ToArray();
							data = statusData;
							break;
						case AttributeTypeCode.State:
							PicklistAttributeData stateData = new PicklistAttributeData();
							List<OptionMetadata> Stateoptions = new List<OptionMetadata>();
							foreach (OptionMetadata option in ((StateAttributeMetadata)metadata).OptionSet.Options)
							{
								Stateoptions.Add(option);
							}
							stateData.PicklistOptions = Stateoptions.ToArray();
							data = stateData;
							break;

						case AttributeTypeCode.String:
							StringAttributeData stringData = new StringAttributeData();
							stringData.MaxLength = ((StringAttributeMetadata)metadata).MaxLength.Value;

							data = stringData;
							break;
						case AttributeTypeCode.Customer:
						case AttributeTypeCode.Lookup:
						case AttributeTypeCode.Owner:
						case AttributeTypeCode.PartyList:
						case AttributeTypeCode.Virtual:
							data.IsUnsupported = true;
							break;
					}

					data.SchemaName = attribute;
					data.AttributeLabel = metadata.DisplayName.UserLocalizedLabel.Label;
					data.AttributeType = metadata.AttributeType.Value;
				}

				// Display value and actual value only apply to attributes tied to a record
				if (isExistingEntity)
				{
					foreach (KeyValuePair<string, object> property in existingRecord.Attributes)
					{
						if (property.Key.Equals(attribute, StringComparison.OrdinalIgnoreCase))
						{
							data.DisplayValue = (string)(existingRecord.FormattedValues.ContainsKey(property.Key) ? existingRecord.FormattedValues[property.Key] : property.Value);
							data.ActualValue = property.Value;
							break;
						}
					}
				}
				attributeData.Add(data);
			}
			return attributeData;
		}

		/// <summary>
		/// Return a single record as a dynamic entity based on a given Guid
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="entityId"></param>
		/// <param name="attributes"></param>
		/// <returns></returns>
		internal Entity RetrieveByIdAsDynamicEntity(string entityName, Guid entityId, params string[] attributes)
		{
			Entity retrieveTarget = new Entity("entityName");
			retrieveTarget.Id = entityId;

			RetrieveRequest retrieveRequest = new RetrieveRequest();
			retrieveRequest.Target = new EntityReference(entityName, entityId);
			ColumnSet cols = new ColumnSet();
			cols.Columns.AddRange(attributes);
			retrieveRequest.ColumnSet = cols;

			RetrieveResponse resp = (RetrieveResponse)svcAct.CdsCommand_Execute(retrieveRequest, "RetrieveByIdAsDynamicEntity");
			if (resp != null && resp.Entity != null)
				return resp.Entity;
			else return null;
		}
	}
}