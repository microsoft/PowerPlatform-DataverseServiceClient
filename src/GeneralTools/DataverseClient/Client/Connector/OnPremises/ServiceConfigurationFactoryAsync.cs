using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    internal static class ServiceConfigurationFactoryAsync
    {
        public static IServiceConfiguration<TService> CreateConfiguration<TService>(Uri serviceUri)
        {
            return CreateConfiguration<TService>(serviceUri, false, null);
        }

        public static IServiceConfiguration<TService> CreateConfiguration<TService>(Uri serviceUri, bool enableProxyTypes, Assembly assembly)
        {
            if (serviceUri != null)
            {
                if (typeof(TService) == typeof(Xrm.Sdk.Discovery.IDiscoveryService))
                {
                    return new DiscoveryServiceConfiguration(serviceUri) as IServiceConfiguration<TService>;
                }
                else if (typeof(TService) == typeof(Xrm.Sdk.IOrganizationService))
                {
                    return new OrganizationServiceConfiguration(serviceUri, enableProxyTypes, assembly) as IServiceConfiguration<TService>;
                }
                else if (typeof(TService) == typeof(IOrganizationServiceAsync))
                {
                    return new OrganizationServiceConfigurationAsync(serviceUri, enableProxyTypes, assembly) as IServiceConfiguration<TService>;
                }
            }

            return null;
        }

        public static IServiceManagement<TService> CreateManagement<TService>(Uri serviceUri)
        {
            return CreateManagement<TService>(serviceUri, false, null);
        }

        public static IServiceManagement<TService> CreateManagement<TService>(Uri serviceUri, bool enableProxyTypes, Assembly assembly)
        {
            if (serviceUri != null)
            {
                if (typeof(TService) == typeof(IDiscoveryService))
                {
                    return new DiscoveryServiceConfiguration(serviceUri) as IServiceManagement<TService>;
                }
                else if (typeof(TService) == typeof(IOrganizationService))
                {
                    return new OrganizationServiceConfiguration(serviceUri, enableProxyTypes, assembly) as IServiceManagement<TService>;
                }
                else if (typeof(TService) == typeof(IOrganizationServiceAsync))
                {
                    return new OrganizationServiceConfigurationAsync(serviceUri, enableProxyTypes, assembly) as IServiceManagement<TService>;
                }
            }

            return null;
        }
    }
}
