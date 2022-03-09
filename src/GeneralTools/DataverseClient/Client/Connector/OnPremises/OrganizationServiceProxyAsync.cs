using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    /// <summary>
    /// helper class that manages a ChannelFactory and serves up channels for sdk client use
    /// <remarks>For internal use only</remarks>
    /// </summary>
    internal class OrganizationServiceProxyAsync : ServiceProxy<IOrganizationServiceAsync>, IOrganizationServiceAsync
    {
        internal bool OfflinePlayback { get; set; }

        public Guid CallerId { get; set; }

        public UserType UserType { get; set; }

        public Guid CallerRegardingObjectId { get; set; }

        internal int LanguageCodeOverride { get; set; }

        public string SyncOperationType { get; set; }

        internal string ClientAppName { get; set; }

        internal string ClientAppVersion { get; set; }

        public string SdkClientVersion { get; set; }

        private static string _xrmSdkAssemblyFileVersion;

        internal OrganizationServiceProxyAsync()
        {
        }

        public OrganizationServiceProxyAsync(Uri uri, Uri homeRealmUri, ClientCredentials clientCredentials, ClientCredentials deviceCredentials)
            : base(uri, homeRealmUri, clientCredentials, deviceCredentials)
        {
        }

        public OrganizationServiceProxyAsync(IServiceConfiguration<IOrganizationServiceAsync> serviceConfiguration, SecurityTokenResponse securityTokenResponse)
            : base(serviceConfiguration, securityTokenResponse)
        {
        }

        public OrganizationServiceProxyAsync(IServiceConfiguration<IOrganizationServiceAsync> serviceConfiguration, ClientCredentials clientCredentials)
            : base(serviceConfiguration, clientCredentials)
        {
        }

        public OrganizationServiceProxyAsync(IServiceManagement<IOrganizationServiceAsync> serviceManagement, SecurityTokenResponse securityTokenResponse)
            : this(serviceManagement as IServiceConfiguration<IOrganizationServiceAsync>, securityTokenResponse)
        {
        }

        public OrganizationServiceProxyAsync(IServiceManagement<IOrganizationServiceAsync> serviceManagement, ClientCredentials clientCredentials)
            : this(serviceManagement as IServiceConfiguration<IOrganizationServiceAsync>, clientCredentials)
        {
        }

        #region Public Members

        /// <summary>
        /// This method will enable support for the default strong proxy types. 
        /// 
        /// If you are using a shared Service Configuration instance, you must be careful if using 
        /// </summary>
        public void EnableProxyTypes()
        {
            ClientExceptionHelper.ThrowIfNull(this.ServiceConfiguration, "ServiceConfiguration");
            OrganizationServiceConfigurationAsync orgConfig = ServiceConfiguration as OrganizationServiceConfigurationAsync;
            ClientExceptionHelper.ThrowIfNull(orgConfig, "orgConfig");

            orgConfig.EnableProxyTypes();
        }

        /// <summary>
        /// This method will enable support for the strong proxy types exposed in the passed assembly.
        /// <param name="assembly">The assembly that will provide support for the desired strong types in the proxy.</param>
        /// </summary>
        public void EnableProxyTypes(Assembly assembly)
        {
            ClientExceptionHelper.ThrowIfNull(assembly, "assembly");

            ClientExceptionHelper.ThrowIfNull(this.ServiceConfiguration, "ServiceConfiguration");
            OrganizationServiceConfigurationAsync orgConfig = ServiceConfiguration as OrganizationServiceConfigurationAsync;
            ClientExceptionHelper.ThrowIfNull(orgConfig, "orgConfig");

            orgConfig.EnableProxyTypes(assembly);
        }

        /// <summary>
        /// Get's the file version of the Xrm Sdk assembly that is loaded in the current client domain.
        /// For Sdk clients called via the OrganizationServiceProxy this is the version of the local Microsoft.Xrm.Sdk dll used by the Client App.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2143:TransparentMethodsShouldNotDemandFxCopRule")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2141:TransparentMethodsMustNotSatisfyLinkDemandsFxCopRule")]
        [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
        internal static string GetXrmSdkAssemblyFileVersion()
        {
            if (string.IsNullOrEmpty(_xrmSdkAssemblyFileVersion))
            {
                string[] assembliesToCheck = new string[] { "Microsoft.Xrm.Sdk.dll" };
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assemblyToCheck in assembliesToCheck)
                {
                    foreach (Assembly assembly in assemblies)
                    {
                        if (assembly.ManifestModule.Name.Equals(assemblyToCheck, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(assembly.Location) &&
                                System.IO.File.Exists(assembly.Location))
                        {
                            _xrmSdkAssemblyFileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
                            break;
                        }
                    }
                }
            }

            // If the assembly is embedded as resource and loaded from memory, there is no physical file on disk to check for file version
            if (string.IsNullOrEmpty(_xrmSdkAssemblyFileVersion))
            {
                _xrmSdkAssemblyFileVersion = "9.1.2.3";
            }

            return _xrmSdkAssemblyFileVersion;
        }

        #endregion Public Members

        #region Protected Members
        protected internal virtual Guid CreateCore(Entity entity)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        return ServiceChannel.Channel.Create(entity);
                    }
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
            return Guid.Empty;
        }

        protected internal virtual async Task<Guid> CreateAsyncCore(Entity entity)
        {
            return await ExecuteOperation<Guid>(async () => { await ServiceChannel.Channel.CreateAsync(entity).ConfigureAwait(false); });
        }

        protected internal virtual Entity RetrieveCore(string entityName, Guid id, ColumnSet columnSet)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        return ServiceChannel.Channel.Retrieve(entityName, id, columnSet);
                    }
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
            return null;
        }

        protected internal virtual async Task<Entity> RetrieveAsyncCore(string entityName, Guid id, ColumnSet columnSet)
        {
            return await ExecuteOperation<Entity>(async () => { await ServiceChannel.Channel.RetrieveAsync(entityName, id, columnSet).ConfigureAwait(false); });
        }

        protected internal virtual void UpdateCore(Entity entity)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        ServiceChannel.Channel.Update(entity);
                    }

                    return; // CRM SE 33359: Return so retry being true won't cause an infinite loop
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
        }

        protected internal virtual async Task UpdateAsyncCore(Entity entity)
        {
            _ = await ExecuteOperation<bool?>(async () => { await ServiceChannel.Channel.UpdateAsync(entity).ConfigureAwait(false); });
        }

        protected internal virtual void DeleteCore(string entityName, Guid id)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        ServiceChannel.Channel.Delete(entityName, id);
                    }

                    return; // CRM SE 33359: Return so retry being true won't cause an infinite loop
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
        }

        protected internal virtual async Task DeleteAsyncCore(string entityName, Guid id)
        {
            _ = await ExecuteOperation<bool?>(async () => { await ServiceChannel.Channel.DeleteAsync(entityName, id).ConfigureAwait(false); });
        }

        protected internal virtual OrganizationResponse ExecuteCore(OrganizationRequest request)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        return ServiceChannel.Channel.Execute(request);
                    }
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
            return null;
        }

        protected internal virtual async Task<OrganizationResponse> ExecuteAsyncCore(OrganizationRequest request)
        {
            return await ExecuteOperation<OrganizationResponse>(async () => { await ServiceChannel.Channel.ExecuteAsync(request).ConfigureAwait(false); });
        }

        protected internal virtual void AssociateCore(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        ServiceChannel.Channel.Associate(entityName, entityId, relationship, relatedEntities);
                    }

                    return; // CRM SE 33359: Return so retry being true won't cause an infinite loop
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
        }

        protected internal virtual async Task AssociateAsyncCore(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await ExecuteOperation<OrganizationResponse>(async () => { await ServiceChannel.Channel.AssociateAsync(entityName, entityId, relationship, relatedEntities).ConfigureAwait(false); });
        }

        protected internal virtual void DisassociateCore(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        ServiceChannel.Channel.Disassociate(entityName, entityId, relationship, relatedEntities);
                    }

                    return; // CRM SE 33359: Return so retry being true won't cause an infinite loop
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
        }

        protected internal virtual async Task DisassociateAsyncCore(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await ExecuteOperation<OrganizationResponse>(async () => { await ServiceChannel.Channel.DisassociateAsync(entityName, entityId, relationship, relatedEntities).ConfigureAwait(false); });
        }

        protected internal virtual EntityCollection RetrieveMultipleCore(QueryBase query)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        return ServiceChannel.Channel.RetrieveMultiple(query);
                    }
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
            return null;
        }

        protected internal virtual async Task<EntityCollection> RetrieveMultipleAsyncCore(QueryBase query)
        {
            return await ExecuteOperation<EntityCollection>(async () => { await ServiceChannel.Channel.RetrieveMultipleAsync(query).ConfigureAwait(false); });
        }

        protected async internal Task<T> ExecuteOperation<T>(Func<Task> asyncAction)
        {
            bool? retry = null;
            do
            {
                bool forceCloseChannel = false;
                try
                {
                    using (new OrganizationServiceProxyContextAsyncInitializer(this))
                    {
                        await asyncAction().ConfigureAwait(continueOnCapturedContext: false); ;
                    }
                }
                catch (MessageSecurityException messageSecurityException)
                {
                    forceCloseChannel = true;

                    retry = ShouldRetry(messageSecurityException, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (EndpointNotFoundException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (TimeoutException)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    forceCloseChannel = true;
                    retry = HandleFailover(fault.Detail, retry);
                    if (!retry.GetValueOrDefault())
                    {
                        throw;
                    }
                }
                catch
                {
                    forceCloseChannel = true;
                    throw;
                }
                finally
                {
                    CloseChannel(forceCloseChannel);
                }
            }
            while (retry.HasValue && retry.Value);
            return default;
        }

        #endregion Protected Members

        #region IOrganizationService implementation

        public Guid Create(Entity entity)
        {
            return CreateCore(entity);
        }

        public async Task<Guid> CreateAsync(Entity entity)
        {
            return await CreateAsyncCore(entity);
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return RetrieveCore(entityName, id, columnSet);
        }
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return await RetrieveAsyncCore(entityName, id, columnSet);
        }

        public void Update(Entity entity)
        {
            UpdateCore(entity);
        }
        public async Task UpdateAsync(Entity entity)
        {
            await UpdateAsyncCore(entity);
        }

        public void Delete(string entityName, Guid id)
        {
            DeleteCore(entityName, id);
        }

        public async Task DeleteAsync(string entityName, Guid id)
        {
            await DeleteAsyncCore(entityName, id);
        }


        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return ExecuteCore(request);
        }

        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return await ExecuteAsyncCore(request);
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            AssociateCore(entityName, entityId, relationship, relatedEntities);
        }

        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await AssociateAsyncCore(entityName, entityId, relationship, relatedEntities);
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            DisassociateCore(entityName, entityId, relationship, relatedEntities);
        }

        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            await DisassociateAsyncCore(entityName, entityId, relationship, relatedEntities);
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return RetrieveMultipleCore(query);
        }

        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return await RetrieveMultipleAsyncCore(query);
        }

        #endregion

    }
}
