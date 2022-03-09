using System;
using System.ServiceModel;
using System.Net;
using System.ServiceModel.Channels;
using Microsoft.Xrm.Sdk.XmlNamespaces;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.WebServiceClient;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector
{
    /// <summary>
    ///     Manages context for sdk calls
    /// </summary>
    internal abstract class WebProxyClientContextAsyncInitializer<TService> : IDisposable
        where TService : class
    {
        #region Fields

        private OperationContextScope _operationScope;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="WebProxyClientContextInitializer{TService}"/> class.
        ///     Constructs a context initializer
        /// </summary>
        /// <param name="proxy">sdk proxy</param>
        protected WebProxyClientContextAsyncInitializer(WebProxyClientAsync<TService> proxy)
        {
            ServiceProxy = proxy;

            Initialize(proxy);
        }

        #region Properties

        public WebProxyClientAsync<TService> ServiceProxy { get; private set; }

        #endregion

        #region Protected Methods

        protected void AddTokenToHeaders()
        {
            var request = new HttpRequestMessageProperty();
            request.Headers[HttpRequestHeader.Authorization.ToString()] = "Bearer " + ServiceProxy.HeaderToken;
            OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = request;
        }

        protected void AddCommonHeaders()
        {
            if (!string.IsNullOrEmpty(ServiceProxy.ClientAppName))
            {
                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.ClientAppName,
                    V5.Contracts,
                    ServiceProxy.ClientAppName));
            }

            if (!string.IsNullOrEmpty(ServiceProxy.ClientAppVersion))
            {
                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.ClientAppVersion,
                    V5.Contracts,
                    ServiceProxy.ClientAppVersion));
            }

            if (!string.IsNullOrEmpty(ServiceProxy.SdkClientVersion))
            {
                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.SdkClientVersion,
                    V5.Contracts,
                    ServiceProxy.SdkClientVersion));
            }
            else
            {
                string fileVersion = ServiceProxy.GetXrmSdkAssemblyFileVersion();
                if (!string.IsNullOrEmpty(fileVersion))
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.SdkClientVersion,
                        V5.Contracts,
                        fileVersion));
                }
            }
        }

        protected void Initialize(ClientBase<TService> proxy)
        {
            // This call initializes operation context scope for the call using the channel
            _operationScope = new OperationContextScope(proxy.InnerChannel);
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region Protected Methods

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

        ~WebProxyClientContextAsyncInitializer()
        {
            Dispose(false);
        }

        #endregion
    }
}
