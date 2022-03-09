using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.ServiceModel;


namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    internal abstract class ServiceContextInitializer<TService> : IDisposable
            where TService : class
    {
        private OperationContextScope _operationScope;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceContextInitializer{TService}"/> class.
        /// Constructs a context initializer
        /// </summary>
        /// <param name="proxy">sdk proxy</param>
        protected ServiceContextInitializer(ServiceProxy<TService> proxy)
        {
            ClientExceptionHelper.ThrowIfNull(proxy, "proxy");

            ServiceProxy = proxy;

            Initialize(proxy);
        }

        public ServiceProxy<TService> ServiceProxy { get; private set; }

        protected void Initialize(ServiceProxy<TService> proxy)
        {
            // This call initializes operation context scope for the call using the channel
            _operationScope = new OperationContextScope((IContextChannel)proxy.ServiceChannel.Channel);
        }

        #region IDisposable implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ServiceContextInitializer()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_operationScope != null)
                {
                    _operationScope.Dispose();
                }
            }
        }

        #endregion
    }
}
