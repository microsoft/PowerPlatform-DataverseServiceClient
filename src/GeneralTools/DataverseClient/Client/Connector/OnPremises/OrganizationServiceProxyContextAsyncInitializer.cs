using System;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.XmlNamespaces;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    /// <summary>
    /// Manages context for sdk calls
    /// </summary>
    internal sealed class OrganizationServiceProxyContextAsyncInitializer : ServiceContextInitializer<IOrganizationServiceAsync>
    {
        public OrganizationServiceProxyContextAsyncInitializer(OrganizationServiceProxyAsync proxy)
            : base(proxy)
        {
            Initialize();
        }

        private OrganizationServiceProxyAsync OrganizationServiceProxyAsync
        {
            get { return ServiceProxy as OrganizationServiceProxyAsync; }
        }

        private void Initialize()
        {
            if (OrganizationServiceProxyAsync != null)
            {
                if (OrganizationServiceProxyAsync.OfflinePlayback)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.IsOfflinePlayback,
                                                                                                   V5.Contracts, true));
                }

                if (OrganizationServiceProxyAsync.CallerId != Guid.Empty)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.CallerId,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.CallerId));
                }

                if (OrganizationServiceProxyAsync.CallerRegardingObjectId != Guid.Empty)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.CallerRegardingObjectId,
                                                                                                    V5.Contracts,
                                                                                                    OrganizationServiceProxyAsync.CallerRegardingObjectId));
                }

                if (OrganizationServiceProxyAsync.LanguageCodeOverride != 0)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.LanguageCodeOverride,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.LanguageCodeOverride));
                }

                if (OrganizationServiceProxyAsync.SyncOperationType != null)
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.OutlookSyncOperationType,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.SyncOperationType));
                }

                if (!string.IsNullOrEmpty(OrganizationServiceProxyAsync.ClientAppName))
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.ClientAppName,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.ClientAppName));
                }

                if (!string.IsNullOrEmpty(OrganizationServiceProxyAsync.ClientAppVersion))
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.ClientAppVersion,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.ClientAppVersion));
                }

                if (!string.IsNullOrEmpty(OrganizationServiceProxyAsync.SdkClientVersion))
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.SdkClientVersion,
                                                                                                   V5.Contracts,
                                                                                                   OrganizationServiceProxyAsync.SdkClientVersion));
                }
                else
                {
                    string fileVersion = Utilities.GetXrmSdkAssemblyFileVersion();
                    if (!string.IsNullOrEmpty(fileVersion))
                    {
                        OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.SdkClientVersion,
                                                                                                       V5.Contracts,
                                                                                                       fileVersion));
                    }
                }

                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(SdkHeaders.UserType,
                                                                                                V5.Contracts,
                                                                                                OrganizationServiceProxyAsync.UserType));
            }
        }
    }
}
