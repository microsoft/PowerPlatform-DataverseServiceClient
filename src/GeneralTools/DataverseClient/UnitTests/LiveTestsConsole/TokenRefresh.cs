using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
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

        internal void Run()
        {
            Console.WriteLine("Starting TokenRefresh");

            var client = Auth.CreateClient();
            client.IsReady.Should().BeTrue();

            Console.WriteLine("Calling WhoAmI");
            var response1 = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            response1.Should().NotBeNull();

            Console.WriteLine("Going to sleep for 26 hours until token expires");
            Thread.Sleep(1000 * 60 * 60 * 26);

            Console.WriteLine("Calling WhoAmI after long sleep");
            var response2 = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            response2.Should().NotBeNull();
        }
    }
}
