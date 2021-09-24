using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;
using System.Collections.Concurrent;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	/// <summary>
	/// Summary description for MetadataUtility
	/// </summary>
	internal class MetadataUtility
	{
		#region metadata vars.
		/// <summary>
		/// MetadataCache object.
		/// </summary>
		private ConcurrentDictionary<String, EntityMetadata> _entityMetadataCache = new ConcurrentDictionary<String, EntityMetadata>();
		/// <summary>
		/// Attribute metadata cache object
		/// </summary>
		private ConcurrentDictionary<String, AttributeMetadata> _attributeMetadataCache = new ConcurrentDictionary<String, AttributeMetadata>();
		/// <summary>
		/// Global option metadata cache object.
		/// </summary>
		private ConcurrentDictionary<String, OptionSetMetadata> _globalOptionMetadataCache = new ConcurrentDictionary<String, OptionSetMetadata>();
		/// <summary>
		/// Entity Name catch object
		/// </summary>
		private ConcurrentDictionary<int, string> _entityNameCache = new ConcurrentDictionary<int, string>();
		/// <summary>
		/// Lock object
		/// </summary>
		private static Object _lockObject = new Object();
		/// <summary>
		/// Last time Entity data was validated.
		/// </summary>
		private DateTime _metadataLastValidatedAt;

		private ServiceClient svcAct = null;
		#endregion

		public MetadataUtility(ServiceClient svcActions)
		{
			svcAct = svcActions;
		}


		/// <summary>
		/// Clear a specific meta data entity
		/// </summary>
		/// <param name="entityName"></param>
		public void ClearCachedEntityMetadata(string entityName)
		{
			TouchMetadataDate();
			// Not clearing the ETC ID's as they do not change...
			if (_entityMetadataCache.ContainsKey(entityName))
			{
				EntityMetadata removedEntData;
				_entityMetadataCache.TryRemove(entityName, out removedEntData);
			}
			if (_attributeMetadataCache.ContainsKey(entityName))
			{
				AttributeMetadata removedAttribData;
				_attributeMetadataCache.TryRemove(entityName, out removedAttribData);
			}
		}

		/// <summary>
		/// Retrieves all metadata from the solution.. this is a time consuming task
		/// </summary>
		/// <param name="onlyPublished">only return "published" or "published state" of entities</param>
		/// <param name="filter">the depth if detail on the entity to retrieve</param>
		/// <returns></returns>
		public List<EntityMetadata> GetAllEntityMetadata(bool onlyPublished, EntityFilters filter = EntityFilters.Default)
		{
			// this will force a retrieve of all metatdata from entities
			List<EntityMetadata> results = new List<EntityMetadata>();

			RetrieveAllEntitiesRequest request = new RetrieveAllEntitiesRequest();
			request.EntityFilters = filter;
			request.RetrieveAsIfPublished = !onlyPublished;

			RetrieveAllEntitiesResponse response = (RetrieveAllEntitiesResponse)svcAct.Command_Execute(request, "GetAllEntityMetadata");
			if (response != null)
			{
				foreach (var entity in response.EntityMetadata)
				{
					if (_entityMetadataCache.ContainsKey(entity.LogicalName))
						_entityMetadataCache[entity.LogicalName] = entity;  // Update local copy of the entity...
					else
						_entityMetadataCache.TryAdd(entity.LogicalName, entity);

					results.Add(entity);
					// Preload the entity data catch as this has been called already
					if (_entityNameCache.ContainsKey(entity.ObjectTypeCode.Value))
						continue;
					else
						_entityNameCache.TryAdd(entity.ObjectTypeCode.Value, entity.LogicalName);
				}
				TouchMetadataDate();
			}
			else
			{
				if (svcAct.LastException != null)
					throw new DataverseOperationException($"Failed to get metadata from Dataverse.", svcAct.LastException);
				else
					throw new DataverseOperationException($"Failed to get metadata from Dataverse", null);
			}

			return results;
		}

		/// <summary>
		/// Returns Entity Metadata for requested entity.
		/// Applies returns all data available based on version type
		/// </summary>
		/// <param name="entityName">Name of the Entity, data is being requested on</param>
		/// <returns>Entity data</returns>
		public EntityMetadata GetEntityMetadata(string entityName)
		{
			// Filter the EntityFitlers based on the version of Dataverse being connected too.
			if (svcAct.ConnectedOrgVersion < Version.Parse("7.1.0.0"))
				return GetEntityMetadata(EntityFilters.Attributes | EntityFilters.Entity | EntityFilters.Privileges | EntityFilters.Relationships , entityName);
			else
				return GetEntityMetadata(EntityFilters.All, entityName);
		}

		/// <summary>
		/// returns entity data for a given entity
		/// </summary>
		/// <param name="requestType">What type of entity data do you want</param>
		/// <param name="entityName">name of the entity to query</param>
		/// <returns></returns>
		public EntityMetadata GetEntityMetadata(EntityFilters requestType, String entityName)
		{
			EntityMetadata entityMetadata = null;
			ValidateMetadata();

			// if the update is for an existing item, and its for sub components, update just the item, do not reset the overall cache update
			bool bSelectiveUpdate = false;
			if (!_entityMetadataCache.TryGetValue(entityName, out entityMetadata))
				entityMetadata = null;
			if (entityMetadata != null && (int)requestType > (int)EntityFilters.Default)
			{
				switch (requestType)
				{
					case EntityFilters.All:
						if (entityMetadata.Attributes == null ||
							entityMetadata.Privileges == null ||
							(entityMetadata.ManyToOneRelationships == null || entityMetadata.OneToManyRelationships == null || entityMetadata.ManyToManyRelationships == null))
							bSelectiveUpdate = true;

						break;
					case EntityFilters.Attributes:
						if (entityMetadata.Attributes == null)
							bSelectiveUpdate = true;
						break;
					case EntityFilters.Privileges:
						if (entityMetadata.Privileges == null)
							bSelectiveUpdate = true;
						break;
					case EntityFilters.Relationships:
						if (entityMetadata.ManyToOneRelationships == null || entityMetadata.OneToManyRelationships == null || entityMetadata.ManyToManyRelationships == null)
							bSelectiveUpdate = true;
						break;
					default:
						break;
				}
			}

			if (entityMetadata == null || bSelectiveUpdate)
			{
				RetrieveEntityRequest request = new RetrieveEntityRequest();
				request.LogicalName = entityName;
				request.EntityFilters = requestType;
				RetrieveEntityResponse response = (RetrieveEntityResponse)svcAct.Command_Execute(request, "GetEntityMetadata");
				if (response != null)
				{
					entityMetadata = response.EntityMetadata;
					if (!_entityMetadataCache.ContainsKey(entityName))
						_entityMetadataCache.TryAdd(entityName, entityMetadata);
					else
						_entityMetadataCache[entityName] = entityMetadata;

					if (!bSelectiveUpdate)
						TouchMetadataDate();
				}
				else
				{
					if (svcAct.LastException != null)
						throw new DataverseOperationException($"Failed to resolve entity metadata for {entityName}.", svcAct.LastException);
					else
						throw new DataverseOperationException($"Failed to resolve entity metadata for {entityName}.", null);
				}
			}
			return entityMetadata;
		}


		/// <summary>
		/// Get the entity schema name based on the entity type code.
		/// </summary>
		/// <param name="entityTypeCode"></param>
		/// <returns></returns>
		public string GetEntityLogicalName(int entityTypeCode)
		{
			string name = string.Empty;

			if (_entityNameCache.Count == 0)
			{
				if (_entityNameCache.Count == 0)
				{
					RetrieveAllEntitiesRequest request = new RetrieveAllEntitiesRequest();
					request.EntityFilters = EntityFilters.Entity;
					request.RetrieveAsIfPublished = true;
					RetrieveAllEntitiesResponse response = (RetrieveAllEntitiesResponse)svcAct.Command_Execute(request, "GetEntityLogicalName");
					if (response != null)
					{
						foreach (EntityMetadata metadata in response.EntityMetadata)
						{
							_entityNameCache.TryAdd(metadata.ObjectTypeCode.Value, metadata.LogicalName);

							// reload metadata cache.
							if (_entityMetadataCache.ContainsKey(metadata.LogicalName))
								continue;
							else
								_entityMetadataCache.TryAdd(metadata.LogicalName, metadata);
						}
						TouchMetadataDate();
					}
					else
					{
						if (svcAct.LastException != null)
							throw new DataverseOperationException($"Failed to resolve entity name from typecode {entityTypeCode}.", svcAct.LastException);
						else
							throw new DataverseOperationException($"Failed to resolve entity name from typecode {entityTypeCode}.", null);
					}
				}
			}
			_entityNameCache.TryGetValue(entityTypeCode, out name);
			return name;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="attributeName"></param>
		/// <returns></returns>
		public AttributeMetadata GetAttributeMetadata(string entityName, string attributeName)
		{
			string entityAndAttribute = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", entityName, attributeName);
			AttributeMetadata attributeMetadata = null;
			ValidateMetadata();

			if (!_attributeMetadataCache.TryGetValue(entityAndAttribute, out attributeMetadata))
			{
				if (!_attributeMetadataCache.TryGetValue(entityAndAttribute, out attributeMetadata))
				{
					RetrieveAttributeRequest request = new RetrieveAttributeRequest();
					request.EntityLogicalName = entityName;
					request.LogicalName = attributeName;

					RetrieveAttributeResponse response = (RetrieveAttributeResponse)svcAct.Command_Execute(request, "GetAttributeMetadata");
					if (response != null)
					{
						attributeMetadata = response.AttributeMetadata;
						_attributeMetadataCache.TryAdd(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", entityName, attributeName), attributeMetadata);
						_metadataLastValidatedAt = DateTime.UtcNow;
					}
					else
					{
						if (svcAct.LastException != null)
							throw new DataverseOperationException($"Failed to resolve attribute metadata for {attributeName} in entity {entityName}.", svcAct.LastException);
						else
							throw new DataverseOperationException($"Failed to resolve attribute metadata for {attributeName} in entity {entityName}.", null);
					}
				}
			}
			return attributeMetadata;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="entityName"></param>
		/// <returns></returns>
		public List<AttributeMetadata> GetAllAttributesMetadataByEntity(string entityName)
		{
			EntityMetadata entityMetadata = GetEntityMetadata(entityName);
			if (entityMetadata != null)
			{
				// Added to deal with failed call to Dataverse.
				if (entityMetadata.Attributes != null)
				{
					List<AttributeMetadata> results = new List<AttributeMetadata>();
					foreach (AttributeMetadata attribute in entityMetadata.Attributes)
					{
						results.Add(attribute);
					}
					return results;
				}
				else
					return null;
			}
			return null;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="entityName"></param>
		/// <returns></returns>
		public List<String> GetRequiredAttributesByEntity(string entityName)
		{
			List<String> results = new List<String>();
			List<AttributeMetadata> attributes = GetAllAttributesMetadataByEntity(entityName);
			if (attributes != null)
			{
				foreach (AttributeMetadata attribute in attributes)
				{
					if (attribute.RequiredLevel.Value == AttributeRequiredLevel.ApplicationRequired || attribute.RequiredLevel.Value == AttributeRequiredLevel.SystemRequired)
					{
						results.Add(attribute.SchemaName.ToUpperInvariant());
					}
				}
				return results;
			}
			return null;
		}

		/// <summary>
		/// Retrieve Global OptionSet Information.
		/// </summary>
		/// <param name="optionSetName"></param>
		/// <returns></returns>
		public OptionSetMetadata GetGlobalOptionSetMetadata(string optionSetName)
		{
			if (string.IsNullOrEmpty(optionSetName))
				return null;

			ValidateMetadata(); // Check to see if Metadata has expired.

			if (_globalOptionMetadataCache.ContainsKey(optionSetName))
				return _globalOptionMetadataCache[optionSetName];

			// Create the RetrieveOption Set
			RetrieveOptionSetRequest optReq = new RetrieveOptionSetRequest { Name = optionSetName };

			// query Dataverse
			RetrieveOptionSetResponse response = (RetrieveOptionSetResponse)svcAct.Command_Execute(optReq, "GetGlobalOptionSetMetadata");
			if (response != null)
			{
				if (response.OptionSetMetadata is OptionSetMetadata && (OptionSetMetadata)response.OptionSetMetadata != null)
				{
					_globalOptionMetadataCache.TryAdd(optionSetName, (OptionSetMetadata)response.OptionSetMetadata);
					TouchMetadataDate();
					return _globalOptionMetadataCache[optionSetName];
				}
			}
			else
            {
				if (svcAct.LastException != null)
					throw new DataverseOperationException($"Failed to resolve global optionset metadata for {optionSetName}.", svcAct.LastException);
				else
					throw new DataverseOperationException($"Failed to resolve global optionset metadata for {optionSetName}.", null);
			}
			return null;
		}

		/// <summary>
		///
		/// </summary>
		private void ValidateMetadata()
		{
			if (DateTime.UtcNow.Subtract(_metadataLastValidatedAt).TotalHours > 1)
            {
                TouchMetadataDate();
                _attributeMetadataCache.Clear();
                _entityMetadataCache.Clear();
                _entityNameCache.Clear();
                _globalOptionMetadataCache.Clear();
            }
        }

        private void TouchMetadataDate()
        {
            lock (_lockObject)
            {
                _metadataLastValidatedAt = DateTime.UtcNow;
            }
        }
    }
}
