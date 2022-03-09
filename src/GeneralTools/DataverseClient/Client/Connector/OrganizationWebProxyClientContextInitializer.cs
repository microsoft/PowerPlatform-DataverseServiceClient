namespace Microsoft.PowerPlatform.Dataverse.Client.Connector
{
    using Microsoft.Xrm.Sdk.Client;
    using Microsoft.PowerPlatform.Dataverse.Client;
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using Microsoft.Xrm.Sdk.XmlNamespaces;

    /// <summary>
    ///     Manages context for sdk calls
    /// </summary>
    internal sealed class OrganizationWebProxyClientAsyncContextInitializer :
        WebProxyClientContextAsyncInitializer<IOrganizationServiceAsync>
    {
        public OrganizationWebProxyClientAsyncContextInitializer(OrganizationWebProxyClientAsync proxy)
            : base(proxy)
        {
            Initialize();
        }

        #region Properties

        private OrganizationWebProxyClientAsync OrganizationWebProxyClient
        {
            get { return ServiceProxy as OrganizationWebProxyClientAsync; }
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            if (ServiceProxy == null)
            {
                return;
            }

            AddTokenToHeaders();

            if (ServiceProxy != null)
            {
                if (OrganizationWebProxyClient.OfflinePlayback)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.IsOfflinePlayback,
                        V5.Contracts, true));
                }

                if (OrganizationWebProxyClient.CallerId != Guid.Empty)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.CallerId,
                        V5.Contracts,
                        OrganizationWebProxyClient.CallerId));
                }

                if (OrganizationWebProxyClient.CallerRegardingObjectId != Guid.Empty)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.CallerRegardingObjectId,
                        V5.Contracts,
                        OrganizationWebProxyClient.CallerRegardingObjectId));
                }

                if (OrganizationWebProxyClient.LanguageCodeOverride != 0)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.LanguageCodeOverride,
                        V5.Contracts,
                        OrganizationWebProxyClient.LanguageCodeOverride));
                }

                if (OrganizationWebProxyClient.SyncOperationType != null)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.OutlookSyncOperationType,
                        V5.Contracts,
                        OrganizationWebProxyClient.SyncOperationType));
                }

                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.UserType,
                                                                                                V5.Contracts,
                                                                                                OrganizationWebProxyClient.userType));

                AddCommonHeaders();
            }
        }

        #endregion
    }
}
