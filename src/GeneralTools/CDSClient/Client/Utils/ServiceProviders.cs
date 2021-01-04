using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
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

        private static void BindServiceProviders ()
        {
            if (_instance == null)
            {
                var services = new ServiceCollection();
                services.AddHttpClient("CdsHttpClientFactory");

                _instance = services.BuildServiceProvider();
            }
        }
    }
}
