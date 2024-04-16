using System;
using System.Reflection;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector
{
    internal abstract class WebProxyClientAsync<TService> : ClientBase<TService>, IDisposable
        where TService : class
    {
        #region Fields

        private string _xrmSdkAssemblyFileVersion;

        #endregion

        protected WebProxyClientAsync(Uri serviceUrl, bool useStrongTypes)
            : base(CreateServiceEndpoint(serviceUrl, useStrongTypes, Utilites.DefaultTimeout, null))
        {
        }

        protected WebProxyClientAsync(Uri serviceUrl, Assembly strongTypeAssembly)
            : base(CreateServiceEndpoint(serviceUrl, true, Utilites.DefaultTimeout, strongTypeAssembly))
        {
        }

        protected WebProxyClientAsync(Uri serviceUrl, TimeSpan timeout, bool useStrongTypes)
            : base(CreateServiceEndpoint(serviceUrl, useStrongTypes, timeout, null))
        {
        }

        protected WebProxyClientAsync(Uri serviceUrl, TimeSpan timeout, Assembly strongTypeAssembly)
            : base(CreateServiceEndpoint(serviceUrl, true, timeout, strongTypeAssembly))
        {
        }

        #region Properties

        public string HeaderToken { get; set; }

        public string SdkClientVersion { get; set; }

        internal string ClientAppName { get; set; }

        internal string ClientAppVersion { get; set; }

        #endregion

        #region Protected Methods

        protected abstract WebProxyClientContextAsyncInitializer<TService> CreateNewInitializer();

        #endregion

        internal void ExecuteAction(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            using (CreateNewInitializer())
            {
                action();
            }
        }

        internal TResult ExecuteAction<TResult>(Func<TResult> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            using (CreateNewInitializer())
            {
                return action();
            }
        }

#if NETCOREAPP
        protected internal Task<T> ExecuteOperation<T>(Func<Task<T>> asyncAction)
        {
            if (asyncAction == null)
            {
                throw new ArgumentNullException(nameof(asyncAction));
            }

            using (CreateNewInitializer())
            {
                return asyncAction();
            }
        }
#else
        protected internal Task<T> ExecuteOperation<T>(Func<Task<T>> asyncAction)
        {
            if (asyncAction == null)
            {
                throw new ArgumentNullException(nameof(asyncAction));
            }

            using (CreateNewInitializer())
            {
                return asyncAction();
            }
        }
#endif
        protected static ServiceEndpoint CreateServiceEndpoint(Uri serviceUrl, bool useStrongTypes, TimeSpan timeout,
            Assembly strongTypeAssembly)
        {
            ServiceEndpoint serviceEndpoint = CreateBaseServiceEndpoint(serviceUrl, timeout);

            // Since we have no way of know if the assembly is different, always remove the old one.

            // Reafactored to support both .net full framework and .net core. 
            if (serviceEndpoint.EndpointBehaviors.Contains(typeof(ProxyTypesBehavior)))
            {
                var behavior = serviceEndpoint.EndpointBehaviors[typeof(ProxyTypesBehavior)];
                if (behavior != null)
                {
                    serviceEndpoint.EndpointBehaviors.Remove(behavior);
                }
            }

            if (useStrongTypes)
            {
                serviceEndpoint.EndpointBehaviors.Add(strongTypeAssembly != null
                    ? new ProxyTypesBehavior(strongTypeAssembly)
                    : new ProxyTypesBehavior());
            }

            return serviceEndpoint;
        }

        private static ServiceEndpoint CreateBaseServiceEndpoint(Uri serviceUrl, TimeSpan timeout)
        {
            Binding binding = GetBinding(serviceUrl, timeout);

            var endpointAddress = new EndpointAddress(serviceUrl);

            ContractDescription contract = ContractDescription.GetContract(typeof(TService));

            var endpoint = new ServiceEndpoint(contract, binding, endpointAddress);

            // Loop through the behaviors for the endpoint and increase the maximum number of objects in the graph
            foreach (OperationDescription operation in endpoint.Contract.Operations)
            {
                // Retrieve the behavior for the operator
                var serializerBehavior = operation.Behaviors.Find<DataContractSerializerOperationBehavior>();
                if (serializerBehavior != null)
                {
                    serializerBehavior.MaxItemsInObjectGraph = int.MaxValue;
                }
            }

            return endpoint;
        }

        protected static Binding GetBinding(Uri serviceUrl, TimeSpan timeout)
        {
            var binding = new BasicHttpBinding(serviceUrl.Scheme == "https"
                ? BasicHttpSecurityMode.Transport
                : BasicHttpSecurityMode.TransportCredentialOnly);

            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferSize = int.MaxValue;

            binding.SendTimeout = timeout;
            binding.ReceiveTimeout = timeout;
            binding.OpenTimeout = timeout;

            // Set the properties on the reader quotas
            binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            binding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;

            return binding;
        }

        /// <summary>
        ///     Get's the file version of the Xrm Sdk assembly that is loaded in the current client domain.
        ///     For Sdk clients called via the OrganizationServiceProxy this is the version of the local Microsoft.Xrm.Sdk dll used
        ///     by the Client App.
        /// </summary>
        /// <returns></returns>
        internal string GetXrmSdkAssemblyFileVersion()
        {
            if (string.IsNullOrEmpty(_xrmSdkAssemblyFileVersion))
            {
                _xrmSdkAssemblyFileVersion = Environs.XrmSdkFileVersion; 
            }

            // If the assembly is embedded as resource and loaded from memory, there is no physical file on disk to check for file version
            if (string.IsNullOrEmpty(_xrmSdkAssemblyFileVersion))
            {
                _xrmSdkAssemblyFileVersion = "9.1.2.3";
            }

            return _xrmSdkAssemblyFileVersion;
        }

#region IDisposable implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
        }

#endregion

        ~WebProxyClientAsync()
        {
            Dispose(false);
        }

#endregion
    }
}
