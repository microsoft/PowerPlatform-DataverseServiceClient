namespace Microsoft.Xrm.Sdk.WebServiceClient
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.Reflection;
	using System.Threading.Tasks;
    using Microsoft.PowerPlatform.Dataverse.Client;
    using Query;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

	internal class OrganizationWebProxyClientAsync : WebProxyClient<IOrganizationServiceAsync>, IOrganizationServiceAsync
	{
		public OrganizationWebProxyClientAsync(Uri serviceUrl, bool useStrongTypes)
			: base(serviceUrl, useStrongTypes)
		{
		}

        public OrganizationWebProxyClientAsync(Uri serviceUrl, Assembly strongTypeAssembly)
            : base(serviceUrl, strongTypeAssembly)
		{
		}

		public OrganizationWebProxyClientAsync(Uri serviceUrl, TimeSpan timeout, bool useStrongTypes)
			: base(serviceUrl, timeout, useStrongTypes)
		{
		}

		public OrganizationWebProxyClientAsync(Uri uri, TimeSpan timeout, Assembly strongTypeAssembly)
			: base(uri, timeout, strongTypeAssembly)
		{
		}

		#region Properties

		internal bool OfflinePlayback { get; set; }

		public string SyncOperationType { get; set; }

		public Guid CallerId { get; set; }

		public UserType userType { get; set; }

		public Guid CallerRegardingObjectId { get; set; }

		internal int LanguageCodeOverride { get; set; }

		#endregion

		#region IOrganizationService implementation

		public void Associate(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			AssociateCore(entityName, entityId, relationship, relatedEntities);
		}

		public Task AssociateAsync(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			return AssociateAsyncCore(entityName, entityId, relationship, relatedEntities);
		}

		public Guid Create(Entity entity)
		{
			return CreateCore(entity);
		}

		public Task<Guid> CreateAsync(Entity entity)
		{
			return CreateAsyncCore(entity);
		}

		public void Delete(string entityName, Guid id)
		{
			DeleteCore(entityName, id);
		}

		public Task DeleteAsync(string entityName, Guid id)
		{
			return DeleteAsyncCore(entityName, id);
		}

		public void Disassociate(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			DisassociateCore(entityName, entityId, relationship, relatedEntities);
		}

		public Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			return DisassociateAsyncCore(entityName, entityId, relationship, relatedEntities);
		}

		public OrganizationResponse Execute(OrganizationRequest request)
		{
			return ExecuteCore(request);
		}

		public Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
		{
			return ExecuteAsyncCore(request);
		}

		public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
		{
			return RetrieveCore(entityName, id, columnSet);
		}

		public Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
		{
			return RetrieveAsyncCore(entityName, id, columnSet);
		}

		public EntityCollection RetrieveMultiple(QueryBase query)
		{
			return RetrieveMultipleCore(query);
		}

		public Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
		{
			return RetrieveMultipleAsyncCore(query);
		}

		public void Update(Entity entity)
		{
			UpdateCore(entity);
		}

		public Task UpdateAsync(Entity entity)
		{
			return UpdateAsyncCore(entity);
		}

		#endregion

		#region Protected IOrganizationService CoreMembers

		protected internal virtual Guid CreateCore(Entity entity)
		{
			return ExecuteAction(() => Channel.Create(entity));
		}

		protected Task<Guid> CreateAsyncCore(Entity entity)
		{
			return ExecuteAction(() => Channel.CreateAsync(entity));
		}

		protected internal virtual Entity RetrieveCore(string entityName, Guid id, ColumnSet columnSet)
		{
			return ExecuteAction(() => Channel.Retrieve(entityName, id, columnSet));
		}

		protected internal virtual Task<Entity> RetrieveAsyncCore(string entityName, Guid id, ColumnSet columnSet)
		{
			return ExecuteAction(() => Channel.RetrieveAsync(entityName, id, columnSet));
		}

		protected internal virtual void UpdateCore(Entity entity)
		{
			ExecuteAction(() => Channel.Update(entity));
		}

		protected internal virtual Task UpdateAsyncCore(Entity entity)
		{
			return ExecuteAction(() => Channel.UpdateAsync(entity));
		}

		protected internal virtual void DeleteCore(string entityName, Guid id)
		{
			ExecuteAction(() => Channel.Delete(entityName, id));
		}

		protected internal virtual Task DeleteAsyncCore(string entityName, Guid id)
		{
			return ExecuteAction(() => Channel.DeleteAsync(entityName, id));
		}

		protected internal virtual OrganizationResponse ExecuteCore(OrganizationRequest request)
		{
			return ExecuteAction(() => Channel.Execute(request));
		}

		protected internal virtual Task<OrganizationResponse> ExecuteAsyncCore(OrganizationRequest request)
		{
			return ExecuteAction(() => Channel.ExecuteAsync(request));
		}

		protected internal virtual void AssociateCore(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			ExecuteAction(() => Channel.Associate(entityName, entityId, relationship, relatedEntities));
		}

		protected internal virtual Task AssociateAsyncCore(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			return ExecuteAction(() => Channel.AssociateAsync(entityName, entityId, relationship, relatedEntities));
		}

		protected internal virtual void DisassociateCore(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			ExecuteAction(() => Channel.Disassociate(entityName, entityId, relationship, relatedEntities));
		}

		protected internal virtual Task DisassociateAsyncCore(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			return ExecuteAction(() => Channel.DisassociateAsync(entityName, entityId, relationship, relatedEntities));
		}

		protected internal virtual EntityCollection RetrieveMultipleCore(QueryBase query)
		{
			return ExecuteAction(() => Channel.RetrieveMultiple(query));
		}

		protected internal virtual Task<EntityCollection> RetrieveMultipleAsyncCore(QueryBase query)
		{
			return ExecuteAction(() => Channel.RetrieveMultipleAsync(query));
		}

		#endregion Protected Members

		#region Protected Methods

		protected override WebProxyClientContextInitializer<IOrganizationServiceAsync> CreateNewInitializer()
		{
			return new OrganizationWebProxyClientAsyncContextInitializer(this);
		}

		#endregion
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

}
