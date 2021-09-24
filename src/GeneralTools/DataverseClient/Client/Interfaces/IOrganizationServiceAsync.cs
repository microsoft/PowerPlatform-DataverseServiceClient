using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	/// <summary>
	/// Interface containing extension methods provided by the DataverseServiceClient for the IOrganizationService Interface.
	/// These extensions will only operate from within the client and are not supported server side.
	/// </summary>
	[ServiceContract(Name = "IOrganizationService", Namespace = Xrm.Sdk.XmlNamespaces.V5.Services)]
	[KnownAssembly]
	public interface IOrganizationServiceAsync: IOrganizationService
	{
		/// <summary>
		/// Create an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to create</param>
		/// <returns>The ID of the created record</returns>
		[OperationContract]

		Task<Guid> CreateAsync(Entity entity);

		/// <summary>
		/// Retrieves instance of an entity
		/// </summary>
		/// <param name="entityName">Logical name of entity</param>
		/// <param name="id">Id of entity</param>
		/// <param name="columnSet">Column Set collection to return with the request</param>
		/// <returns>Selected Entity</returns>
		[OperationContract]

		Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet);

		/// <summary>
		/// Updates an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to update</param>
		[OperationContract]

		Task UpdateAsync(Entity entity);

		/// <summary>
		/// Delete instance of an entity
		/// </summary>
		/// <param name="entityName">Logical name of entity</param>
		/// <param name="id">Id of entity</param>
		[OperationContract]

		Task DeleteAsync(string entityName, Guid id);

		/// <summary>
		/// Perform an action in an organization specified by the request.
		/// </summary>
		/// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
		/// <returns>Results from processing the request</returns>
		[OperationContract]

		Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request );

		/// <summary>
		/// Associate an entity with a set of entities
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="entityId"></param>
		/// <param name="relationship"></param>
		/// <param name="relatedEntities"></param>
		[OperationContract]

		Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities);

		/// <summary>
		/// Disassociate an entity with a set of entities
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="entityId"></param>
		/// <param name="relationship"></param>
		/// <param name="relatedEntities"></param>
		[OperationContract]

		Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities);

		/// <summary>
		/// Retrieves a collection of entities
		/// </summary>
		/// <param name="query"></param>
		/// <returns>Returns an EntityCollection Object containing the results of the query</returns>
		[OperationContract]

		Task<EntityCollection> RetrieveMultipleAsync(QueryBase query);
	}
}
