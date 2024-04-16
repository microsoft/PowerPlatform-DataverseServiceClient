// Ignore Spelling: Dataverse Crm

using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.PowerPlatform.Dataverse.Client.Utilities;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;

namespace Microsoft.PowerPlatform.Dataverse.Client.Builder
{
    /// <summary>
    /// Internal use only. Request Builder base class.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractClientRequestBuilder<T> : IOrganizationServiceAsync2
            where T : AbstractClientRequestBuilder<T>
    {
        private IOrganizationServiceAsync2 _client;
        private Guid? _correlationId; // this is the correlation id of the request
        private Guid? _requestId; // this is the request id of the request
        private Dictionary<string, string> _headers = new Dictionary<string, string>();
        private Guid? _aadOidId; // this ObjectID to use for the requesting user.
        private Guid? _crmUserId; // this is the CRM user id to use for the requesting user.

        /// <summary>
        /// Internal use only,  used to build a base class for request builders.
        /// </summary>
        /// <param name="client"></param>
        internal AbstractClientRequestBuilder(IOrganizationServiceAsync2 client)
        {
            _client = client;
        }

        /// <summary>
        /// Adds a request id of your choosing to this request. This is used for tracing purposes.
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public T WithRequestId(Guid requestId)
        {
            _requestId = requestId;
            return (T)this;
        }

        /// <summary>
        /// Adds a correlation id of your choosing to this request. This is used for tracing purposes.
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public T WithCorrelationId(Guid correlationId)
        {
            _correlationId = correlationId;
            return (T)this;
        }

        /// <summary>
        /// Adds an individual header to the request. This works in conjunction with the custom headers request behavior.
        /// </summary>
        /// <param name="key">Header Key</param>
        /// <param name="value">Header Value</param>
        /// <returns></returns>
        public T WithHeader(string key, string value)
        {
            _headers.Add(key, value);
            return (T)this;
        }

        /// <summary>
        /// Adds an array of headers to the request. This works in conjunction with the custom headers request behavior.
        /// </summary>
        /// <param name="headers">Dictionary of Headers to add to there request.</param>
        /// <returns></returns>
        public T WithHeaders(IDictionary<string, string> headers)
        {
            foreach (var itm in headers)
                _headers.Add(itm.Key, itm.Value);
            return (T)this;
        }

        /// <summary>
        /// Adds the AAD object ID to the request
        /// </summary>
        /// <param name="userObjectId"></param>
        /// <returns></returns>
        public T WithUserObjectId(Guid userObjectId)
        {
            _aadOidId = userObjectId;
            return (T)this;
        }

        /// <summary>
        /// Adds the CrmUserId to the request.
        /// </summary>
        /// <param name="crmUserId"></param>
        /// <returns></returns>
        public T WithCrmUserId(Guid crmUserId)
        {
            _crmUserId = crmUserId;
            return (T)this;
        }

        /// <summary>
        /// This configured the request to send to Dataverse. 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal OrganizationRequest BuildRequest(OrganizationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("Request is not set");
            }

            ParameterCollection parameters = new ParameterCollection();
            Guid requestTracker = _requestId ?? Guid.NewGuid();
            request.RequestId = requestTracker;
            

            if (_correlationId != null)
            {
                parameters.Add(RequestHeaders.X_MS_CORRELATION_REQUEST_ID, _correlationId.Value);
            }

            if (_headers.Any())
            {
                parameters.Add(RequestBinderUtil.HEADERLIST, new Dictionary<string,string>(_headers));
            }

            request.Parameters.AddRange(parameters);

            // Clear in case this is reused.             
            ClearRequest();

            return request;
        }

        /// <summary>
        /// Clear request parameters when the request is executed. 
        /// </summary>
        private void ClearRequest()
        {
            _requestId = null;
            _correlationId = null;
            _headers.Clear();
        }

        #region IOrganization Services Interface Implementations
        /// <summary>
        /// Associate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            AssociateRequest request = new AssociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            request = (AssociateRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken);
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>The ID of the created record</returns>
        public Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken)
        {
            CreateRequest request = new CreateRequest()
            {
                Target = entity
            };
            request = (CreateRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken).ContinueWith((t) =>
            {
                return ((CreateResponse)t.Result).id;
            });
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Returns the newly created record</returns>
        public Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delete instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public Task DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken)
        {
            DeleteRequest request = new DeleteRequest()
            {
                Target = new EntityReference(entityName, id)
            };
            request = (DeleteRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken);
        }

        /// <summary>
        /// Disassociate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            DisassociateRequest request = new DisassociateRequest()
            {
                Target = new EntityReference(entityName, entityId),
                Relationship = relationship,
                RelatedEntities = relatedEntities
            };
            request = (DisassociateRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform an action in an organization specified by the request.
        /// </summary>
        /// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Results from processing the request</returns>
        public Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, CancellationToken cancellationToken)
        {
            request = BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken);
        }

        /// <summary>
        /// Retrieves instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="columnSet">Column Set collection to return with the request</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Selected Entity</returns>

        public Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, CancellationToken cancellationToken)
        {
            RetrieveRequest request = new RetrieveRequest()
            {
                ColumnSet = columnSet,
                Target = new EntityReference(entityName, id)
            };
            request = (RetrieveRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken).ContinueWith((t) =>
            {
                return ((RetrieveResponse)t.Result).Entity;
            });
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <param name="query"></param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Returns an EntityCollection Object containing the results of the query</returns>
        public Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken)
        {
            RetrieveMultipleRequest request = new RetrieveMultipleRequest()
            {
                Query = query
            };
            request = (RetrieveMultipleRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken).ContinueWith((t) =>
            {
                return ((RetrieveMultipleResponse)t.Result).EntityCollection;
            });

        }

        /// <summary>
        /// Updates an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to update</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        public Task UpdateAsync(Entity entity, CancellationToken cancellationToken)
        {
            UpdateRequest request = new UpdateRequest()
            {
                Target = entity
            };
            request = (UpdateRequest)BuildRequest(request);
            return _client.ExecuteAsync(request, cancellationToken);
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <returns>Returns the newly created record</returns>
        public Task<Guid> CreateAsync(Entity entity)
        {
            return CreateAsync(entity, CancellationToken.None);
        }

        /// <summary>
        /// Retrieves instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="columnSet">Column Set collection to return with the request</param>
        /// <returns>Selected Entity</returns>
        public Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return RetrieveAsync(entityName, id, columnSet, CancellationToken.None);
        }

        /// <summary>
        /// Updates an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to update</param>
        public Task UpdateAsync(Entity entity)
        {
            return UpdateAsync(entity, CancellationToken.None);
        }

        /// <summary>
        /// Delete instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        public Task DeleteAsync(string entityName, Guid id)
        {
            return DeleteAsync(entityName, id, CancellationToken.None);
        }

        /// <summary>
        /// Perform an action in an organization specified by the request.
        /// </summary>
        /// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
        /// <returns>Results from processing the request</returns>
        public Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return ExecuteAsync(request, CancellationToken.None);
        }

        /// <summary>
        /// Associate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return AssociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None);
        }

        /// <summary>
        /// Disassociate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return DisassociateAsync(entityName, entityId, relationship, relatedEntities, CancellationToken.None);
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <param name="query"></param>
        /// <returns>Returns an EntityCollection Object containing the results of the query</returns>
        public Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return RetrieveMultipleAsync(query, CancellationToken.None);
        }

        /// <summary>
        /// Create an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to create</param>
        /// <returns>The ID of the created record</returns>
        public Guid Create(Entity entity)
        {
            return CreateAsync(entity).Result;
        }

        /// <summary>
        /// Retrieves instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        /// <param name="columnSet">Column Set collection to return with the request</param>
        /// <returns>Selected Entity</returns>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return RetrieveAsync(entityName, id, columnSet).Result;
        }

        /// <summary>
        /// Updates an entity and process any related entities
        /// </summary>
        /// <param name="entity">entity to update</param>
        public void Update(Entity entity)
        {
            UpdateAsync(entity).Wait();
        }

        /// <summary>
        /// Delete instance of an entity
        /// </summary>
        /// <param name="entityName">Logical name of entity</param>
        /// <param name="id">Id of entity</param>
        public void Delete(string entityName, Guid id)
        {
            DeleteAsync(entityName, id).Wait();
        }

        /// <summary>
        /// Perform an action in an organization specified by the request.
        /// </summary>
        /// <param name="request">Refer to SDK documentation for list of messages that can be used.</param>
        /// <returns>Results from processing the request</returns>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return ExecuteAsync(request).Result;
        }

        /// <summary>
        /// Associate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            AssociateAsync(entityName, entityId, relationship, relatedEntities).Wait();
        }

        /// <summary>
        /// Disassociate an entity with a set of entities
        /// </summary>
        /// <param name="entityName"></param>
        /// <param name="entityId"></param>
        /// <param name="relationship"></param>
        /// <param name="relatedEntities"></param>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            DisassociateAsync(entityName, entityId, relationship, relatedEntities).Wait();
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <param name="query"></param>
        /// <returns>Returns an EntityCollection Object containing the results of the query</returns>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return RetrieveMultipleAsync(query).Result;
        }
        #endregion
    }
}
