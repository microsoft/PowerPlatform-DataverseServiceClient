using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.ServiceClientConverter
{
    /// <summary>
    /// Provides Conversion utilities to convert between CrmServiceClient and Dataverse ServiceClient
    /// Works only for JWT based scenarios.
    /// </summary>
    public static class ServiceClientConverter
    {
        #region Crm ServiceClient to Dataverse

        /// <summary>
        /// Create CrmServiceClient based on Dataverse ServiceClient
        /// </summary>
        /// <param name="crmServiceClient"></param>
        /// <returns>a new Dataverse ServiceClient from a CrmServiceClient</returns>
        public static ServiceClient ToServiceClient(this CrmServiceClient crmServiceClient)
        {
            _ = crmServiceClient ?? throw new ArgumentNullException(nameof(crmServiceClient));

            if (
                crmServiceClient.ActiveAuthenticationType == Xrm.Tooling.Connector.AuthenticationType.IFD ||
                crmServiceClient.ActiveAuthenticationType == Xrm.Tooling.Connector.AuthenticationType.Claims ||
                crmServiceClient.ActiveAuthenticationType == Xrm.Tooling.Connector.AuthenticationType.InvalidConnection ||
                crmServiceClient.ActiveAuthenticationType == Xrm.Tooling.Connector.AuthenticationType.Office365 ||
                crmServiceClient.ActiveAuthenticationType == Xrm.Tooling.Connector.AuthenticationType.AD
                )
            {
                throw new ArgumentException($"Only JWT based authentication types are supported for this conversion - {crmServiceClient.ActiveAuthenticationType} is not supported", nameof(crmServiceClient));
            }

            ServiceClient serviceClient = new ServiceClient(crmServiceClient.CrmConnectOrgUriActual, tokenProviderFunction: (uri) =>
            {
                if (crmServiceClient.CrmConnectOrgUriActual.ToString().Contains(uri))
                {
                    return Task.FromResult(crmServiceClient.CurrentAccessToken);
                }
                else
                    return null;
            })
            {
                UseWebApi = false
            };

            return serviceClient;
        }

        #endregion

        #region Dataverse to Crm ServiceClient

        /// <summary>
        /// Create Dataverse ServiceClient based on CrmServiceClient
        /// </summary>
        /// <param name="serviceClient"></param>
        /// <returns>a new CrmServiceClient from a Dataverse ServiceClient</returns>
        public static CrmServiceClient ToCrmServiceClient(this ServiceClient serviceClient)
        {
            _ = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));

            if (
                serviceClient.ActiveAuthenticationType == Client.AuthenticationType.AD ||
                serviceClient.ActiveAuthenticationType == Client.AuthenticationType.InvalidConnection
                )
            {
                throw new ArgumentException($"Only JWT based authentication types are supported for this conversion - {serviceClient.ActiveAuthenticationType} is not supported", nameof(serviceClient));
            }

            CrmServiceClient.AuthOverrideHook = new AuthHandler(serviceClient);
            CrmServiceClient crmServiceClient = new CrmServiceClient(serviceClient.ConnectedOrgUriActual, true);

            return crmServiceClient;
        }

        /// <summary>
        /// Authentication token acquisition handler
        /// </summary>
        internal class AuthHandler : IOverrideAuthHookWrapper
        {
            private ServiceClient _localServiceClient;
            public AuthHandler(ServiceClient serviceClient)
            {
                _localServiceClient = serviceClient;
            }

            public string GetAuthToken(Uri connectedUri)
            {
                if (_localServiceClient.ConnectedOrgUriActual.DnsSafeHost.ToString().Contains(connectedUri.DnsSafeHost.ToString()))
                {
                    return _localServiceClient.CurrentAccessToken;
                }
                else
                    return string.Empty;
            }
        }
        #endregion

    }
}
