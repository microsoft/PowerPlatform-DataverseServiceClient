using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth
{
    internal class MSALHttpRetryHandlerHelper : DelegatingHandler
    {
        private readonly int MaxRetryCount = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>().Value.MsalRetryCount;

        /// <summary>
        /// Handel Failure and retry
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (int i = 0; i < MaxRetryCount; i++)
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.RequestTimeout)
                    {
                        break;
                    }
                    else
                    {
#if DEBUG
                        System.Diagnostics.Trace.WriteLine($">>> MSAL RETRY ON TIMEOUT >>> {Thread.CurrentThread.ManagedThreadId} - {i}");
#endif
                    }
                }
            }
            return response;
        }
    }
}
