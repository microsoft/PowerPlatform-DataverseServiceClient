using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    internal static class ClientServiceProviders
    {
        /// <summary>
        /// Private property accessor for service provider
        /// </summary>
        private static IServiceProvider _instance = null;

        /// <summary>
        /// Instance of Service providers.
        /// </summary>
        internal static IServiceProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    BindServiceProviders();
                }
                return _instance;
            }
        }

        private static void BindServiceProviders()
        {
            if (_instance == null)
            {
                var services = new ServiceCollection();

                services.AddTransient<MSALHttpRetryHandlerHelper>();
                services.AddOptions<ConfigurationOptions>();
                services.AddHttpClient("DataverseHttpClientFactory")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var hander = new HttpClientHandler
                        {
                            UseCookies = false,
                            //SslProtocols = System.Security.Authentication.SslProtocols.Tls12
                        };
                        return hander;
                    });
                services.AddHttpClient("MSALClientFactory", (sp, client) =>
                   {
                       client.Timeout = sp.GetService<IOptions<ConfigurationOptions>>().Value.MSALRequestTimeout;
                   })
                    .AddHttpMessageHandler<MSALHttpRetryHandlerHelper>(); // Adding on board retry hander for MSAL.
                _instance = services.BuildServiceProvider();
            }
        }
    }
}
