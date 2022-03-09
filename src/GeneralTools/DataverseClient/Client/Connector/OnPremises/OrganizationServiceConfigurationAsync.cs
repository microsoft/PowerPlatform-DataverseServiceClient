using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Common;
using System;
using System.IdentityModel.Tokens;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    internal sealed class OrganizationServiceConfigurationAsync : IServiceConfiguration<IOrganizationServiceAsync>,
            IWebAuthentication<IOrganizationServiceAsync>,
            IServiceManagement<IOrganizationServiceAsync>,
            IEndpointSwitch
    {
        private const string XrmServicesRoot = "xrmservices/";
        private ServiceConfiguration<IOrganizationServiceAsync> service;

        private OrganizationServiceConfigurationAsync()
        {
        }

        internal OrganizationServiceConfigurationAsync(Uri serviceUri)
            : this(serviceUri, false, null)
        {
        }

        internal OrganizationServiceConfigurationAsync(Uri serviceUri, bool enableProxyTypes, Assembly assembly)
        {
            try
            {
                service = new ServiceConfiguration<IOrganizationServiceAsync>(serviceUri, false);
                if (enableProxyTypes && assembly != null)
                {
                    EnableProxyTypes(assembly);
                }
                else if (enableProxyTypes)
                {
                    EnableProxyTypes();
                }
            }
            catch (InvalidOperationException ioexp)
            {
                var rethrow = true;
                var wexp = ioexp.InnerException as WebException;
                if (wexp != null)
                {
                    var response = wexp.Response as HttpWebResponse;
                    if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        rethrow = !AdjustServiceEndpoint(serviceUri);
                    }
                }

                if (rethrow)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// This method will enable support for the default strong proxy types. 
        /// 
        /// If you are using a shared Service Configuration instance, you must be careful if using 
        /// </summary>
        public void EnableProxyTypes()
        {
            ClientExceptionHelper.ThrowIfNull(this.CurrentServiceEndpoint, "CurrentServiceEndpoint");

            lock (_lockObject)
            {
                ProxyTypesBehavior behavior = CurrentServiceEndpoint.FindBehavior<ProxyTypesBehavior>();
                if (behavior != null)
                {
                    // Since we have no way of know if the assembly is different, always remove the old one.
                    CurrentServiceEndpoint.RemoveBehavior(behavior);
                }

                CurrentServiceEndpoint.AddBehavior(new ProxyTypesBehavior());
            }
        }

        /// <summary>
        /// This method will enable support for the strong proxy types exposed in the passed assembly.
        /// <param name="assembly">The assembly that will provide support for the desired strong types in the proxy.</param>
        /// </summary>
        public void EnableProxyTypes(Assembly assembly)
        {
            ClientExceptionHelper.ThrowIfNull(assembly, "assembly");

            ClientExceptionHelper.ThrowIfNull(this.CurrentServiceEndpoint, "CurrentServiceEndpoint");

            lock (_lockObject)
            {
                ProxyTypesBehavior behavior = CurrentServiceEndpoint.FindBehavior<ProxyTypesBehavior>();
                if (behavior != null)
                {
                    // Since we have no way of know if the assembly is different, always remove the old one.
                    CurrentServiceEndpoint.RemoveBehavior(behavior);
                }

                CurrentServiceEndpoint.AddBehavior(new ProxyTypesBehavior(assembly));
            }
        }

        private object _lockObject = new object();
        #region IServiceConfiguration<IOrganizationService> Members

        public ServiceEndpoint CurrentServiceEndpoint
        {
            get { return service.CurrentServiceEndpoint; }
            set { service.CurrentServiceEndpoint = value; }
        }

        public IssuerEndpoint CurrentIssuer
        {
            get { return service.CurrentIssuer; }
            set { service.CurrentIssuer = value; }
        }

        public AuthenticationProviderType AuthenticationType
        {
            get { return service.AuthenticationType; }
        }

        public ServiceEndpointDictionary ServiceEndpoints
        {
            get { return service.ServiceEndpoints; }
        }

        public IssuerEndpointDictionary IssuerEndpoints
        {
            get { return service.IssuerEndpoints; }
        }

        public CrossRealmIssuerEndpointCollection CrossRealmIssuerEndpoints
        {
            get { return service.CrossRealmIssuerEndpoints; }
        }

        public ChannelFactory<IOrganizationServiceAsync> CreateChannelFactory()
        {
            return service.CreateChannelFactory(ClientAuthenticationType.Kerberos);
        }

        public ChannelFactory<IOrganizationServiceAsync> CreateChannelFactory(ClientAuthenticationType clientAuthenticationType)
        {
            return service.CreateChannelFactory(clientAuthenticationType);
        }

        public ChannelFactory<IOrganizationServiceAsync> CreateChannelFactory(TokenServiceCredentialType endpointType)
        {
            return service.CreateChannelFactory(endpointType);
        }

        public ChannelFactory<IOrganizationServiceAsync> CreateChannelFactory(ClientCredentials clientCredentials)
        {
            return service.CreateChannelFactory(clientCredentials);
        }

        public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials)
        {
            return service.Authenticate(clientCredentials);
        }

        public SecurityTokenResponse Authenticate(SecurityToken securityToken)
        {
            return service.Authenticate(securityToken);
        }

        public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials, SecurityTokenResponse deviceSecurityTokenResponse)
        {
            throw new InvalidOperationException("Authentication to MSA services is not supported.");
        }

        public SecurityTokenResponse AuthenticateDevice(ClientCredentials clientCredentials)
        {
            throw new InvalidOperationException("Authentication to MSA services is not supported.");
        }

        public SecurityTokenResponse AuthenticateCrossRealm(ClientCredentials clientCredentials, string appliesTo, Uri crossRealmSts)
        {
            return service.AuthenticateCrossRealm(clientCredentials, appliesTo, crossRealmSts);
        }

        public SecurityTokenResponse AuthenticateCrossRealm(SecurityToken securityToken, string appliesTo, Uri crossRealmSts)
        {
            return service.AuthenticateCrossRealm(securityToken, appliesTo, crossRealmSts);
        }

        public PolicyConfiguration PolicyConfiguration
        {
            get { return service.PolicyConfiguration; }
        }

        #endregion

        public IdentityProvider GetIdentityProvider(string userPrincipalName)
        {
            return service.GetIdentityProvider(userPrincipalName);
        }

        #region Implementation of IWebAuthentication<IDiscoveryService>

        public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials, Uri uri, string keyType)
        {
            return service.Authenticate(clientCredentials, uri, keyType);
        }

        public SecurityTokenResponse Authenticate(SecurityToken securityToken, Uri uri, string keyType)
        {
            return service.Authenticate(securityToken, uri, keyType);
        }

        #endregion

        private bool AdjustServiceEndpoint(Uri serviceUri)
        {
            // Try to get the non org-based service info and adjust it.
            // This is most likely because we are requesting the org-based url in AD mode, in which case, the server won't let us access it.
            var newServiceUri = RemoveOrgName(serviceUri);
            if (newServiceUri != null)
            {
                // Don't try to catch the exception this time.  Just let it go.
                service = new ServiceConfiguration<IOrganizationServiceAsync>(newServiceUri);
                if (service != null && service.ServiceEndpoints != null)
                {
                    foreach (var endpointKey in service.ServiceEndpoints)
                    {
                        ServiceMetadataUtility.ReplaceEndpointAddress(endpointKey.Value, serviceUri);
                    }

                    return true;
                }
            }

            return false;
        }

        private static Uri RemoveOrgName(Uri serviceUri)
        {
            if (!serviceUri.AbsolutePath.StartsWith("/" + XrmServicesRoot, StringComparison.OrdinalIgnoreCase))
            {
                // We're accessing the org url.
                var pathBuilder = new StringBuilder();

                for (int i = 2; i < serviceUri.Segments.Length; i++)
                {
                    pathBuilder.Append(serviceUri.Segments[i]);
                }

                if (pathBuilder.Length > 0)
                {
                    var builder = new UriBuilder(serviceUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped));
                    builder.Path = pathBuilder.ToString();
                    serviceUri = builder.Uri;
                    return serviceUri;
                }
            }

            return null;
        }

        public AuthenticationCredentials Authenticate(AuthenticationCredentials authenticationCredentials)
        {
            return service.Authenticate(authenticationCredentials);
        }

        public bool EndpointAutoSwitchEnabled
        {
            get { return service.EndpointAutoSwitchEnabled; }
            set { service.EndpointAutoSwitchEnabled = value; }
        }

        public Uri AlternateEndpoint
        {
            get { return service.AlternateEndpoint; }
        }

        public Uri PrimaryEndpoint
        {
            get { return service.PrimaryEndpoint; }
        }

        public void SwitchEndpoint()
        {
            service.SwitchEndpoint();
        }

        public event EventHandler<EndpointSwitchEventArgs> EndpointSwitched
        {
            add { service.EndpointSwitched += value; }
            remove { service.EndpointSwitched -= value; }
        }

        public event EventHandler<EndpointSwitchEventArgs> EndpointSwitchRequired
        {
            add { service.EndpointSwitchRequired += value; }
            remove { service.EndpointSwitchRequired -= value; }
        }

        public bool HandleEndpointSwitch()
        {
            return service.HandleEndpointSwitch();
        }

        public bool IsPrimaryEndpoint
        {
            get { return service.IsPrimaryEndpoint; }
        }

        public bool CanSwitch(Uri currentUri)
        {
            return service.CanSwitch(currentUri);
        }
    }
}
