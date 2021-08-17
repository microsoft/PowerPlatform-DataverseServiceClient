using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTestsConsole
{
    public class Auth
    {
        /// <summary>
        /// Sample / stand-in appID used when replacing O365 Auth
        /// </summary>
        internal static string SampleClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
        /// <summary>
        /// Sample / stand-in redirect URI used when replacing o365 Auth
        /// </summary>
        internal static string SampleRedirectUrl = "app://58145B91-0C36-4500-8554-080854F2AC97";

        public static ServiceClient CreateClient()
        {
            var userName = Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            var password = Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            var connectionUrl = Environment.GetEnvironmentVariable("XUNITCONNTESTURI");
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(connectionUrl))
            {
                throw new ArgumentNullException("Make sure to set XUNITCONNTESTUSERID, XUNITCONNTESTPW, XUNITCONNTESTURI environment variables");
            }

            return new ServiceClient(userName, ServiceClient.MakeSecureString(password), new Uri(connectionUrl), true, SampleClientId, new Uri(SampleRedirectUrl), PromptBehavior.Never);
        }
    }
}
