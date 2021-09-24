using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth
{
    internal class MSALHttpClientFactory : IMsalHttpClientFactory
    {
        /// <summary>
        /// Return the HTTP client for MSAL.
        /// </summary>
        /// <returns></returns>
        public HttpClient GetHttpClient()
        {
            HttpClient msalClient = ClientServiceProviders.Instance.GetService<IHttpClientFactory>().CreateClient("MSALClientFactory");
            return msalClient;
        }
    }
}
