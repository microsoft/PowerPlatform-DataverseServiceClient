using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Cds.Client;
using Microsoft.PowerPlatform.Cds.Client.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveTestsConsole
{
    public class TokenRefresh
    {
        /// <summary>
        /// Sample / stand-in appID used when replacing O365 Auth
        /// </summary>
        internal static string SampleClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
        /// <summary>
        /// Sample / stand-in redirect URI used when replacing o365 Auth
        /// </summary>
        internal static string SampleRedirectUrl = "app://58145B91-0C36-4500-8554-080854F2AC97";

        internal void Run()
        {
            Console.WriteLine("Starting TokenRefresh");

            var userName = Environment.GetEnvironmentVariable("XUNITCONNTESTUSERID");
            var password = Environment.GetEnvironmentVariable("XUNITCONNTESTPW");
            var connectionUrl = Environment.GetEnvironmentVariable("XUNITCONNTESTURI");
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(connectionUrl))
            {
                Console.WriteLine("Make sure to set XUNITCONNTESTUSERID, XUNITCONNTESTPW, XUNITCONNTESTURI environment variables");
                return;
            }

            var client1 = new CdsServiceClient(userName, CdsServiceClient.MakeSecureString(password), new Uri(connectionUrl), true, SampleClientId, new Uri(SampleRedirectUrl), PromptBehavior.Never);
            client1.IsReady.Should().BeTrue();

            Console.WriteLine("Calling WhoAmI");
            var response1 = client1.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            response1.Should().NotBeNull();

            Console.WriteLine("Going to sleep for 26 hours until token expires");
            Thread.Sleep(1000 * 60 * 60 * 26);

            Console.WriteLine("Calling WhoAmI after long sleep");
            var response2 = client1.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            response2.Should().NotBeNull();
        }
    }
}
