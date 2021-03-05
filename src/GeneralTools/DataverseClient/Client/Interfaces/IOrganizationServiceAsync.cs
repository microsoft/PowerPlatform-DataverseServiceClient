using System;
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
	public interface IOrganizationServiceAsync	
	{
		/// <summary>
		/// Create an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to create</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		/// <returns>The ID of the created record</returns>
		Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken);

		/// <summary>
		/// Create an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to create</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		/// <returns>Returns the newly created record</returns>
		Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken);

		/// <summary>
		/// Retrieves instance of an entity
		/// </summary>
		/// <param name="entityName">Logical name of entity</param>
		/// <param name="id">Id of entity</param>
		/// <param name="columnSet">Column Set collection to return with the request</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		/// <returns>Selected Entity</returns>
		Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, CancellationToken cancellationToken);

		/// <summary>
		/// Updates an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to update</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		Task UpdateAsync(Entity entity, CancellationToken cancellationToken);

		/// <summary>
		/// Delete instance of an entity
		/// </summary>
		/// <param name="entityName">Logical name of entity</param>
		/// <param name="id">Id of entity</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		void DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken);

		/// <summary>
		/// Perform an action in an organization specified by the request.
		/// </summary>
		/// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
		/// <returns>Results from processing the request</returns>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request , CancellationToken cancellationToken);

		/// <summary>
		/// Associate an entity with a set of entities
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="entityId"></param>
		/// <param name="relationship"></param>
		/// <param name="relatedEntities"></param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		void AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken);

		/// <summary>
		/// Disassociate an entity with a set of entities
		/// </summary>
		/// <param name="entityName"></param>
		/// <param name="entityId"></param>
		/// <param name="relationship"></param>
		/// <param name="relatedEntities"></param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		void DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken);

		/// <summary>
		/// Retrieves a collection of entities
		/// </summary>
		/// <param name="query"></param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		/// <returns>Returns an EntityCollection Object containing the results of the query</returns>
		Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken);
	}
}
