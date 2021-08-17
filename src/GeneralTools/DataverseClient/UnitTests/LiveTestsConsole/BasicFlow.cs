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
    public class BasicFlow
    {

        internal void Run()
        {
            Console.WriteLine("Starting Basic Flow");

            var client = Auth.CreateClient();
            client.IsReady.Should().BeTrue();

            Console.WriteLine("\nCalling WhoAmI");
            var whoAmIResponse = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            whoAmIResponse.Should().NotBeNull();
            Console.WriteLine($"OrganizationId:{whoAmIResponse.OrganizationId} UserId:{whoAmIResponse.UserId}");

            Console.WriteLine("\nCalling RetrieveCurrentOrganizationRequest");
            var retrieveCurrentOrganizationRequest = new RetrieveCurrentOrganizationRequest();
            var retrieveCurrentOrganizationResponse = client.Execute(retrieveCurrentOrganizationRequest) as RetrieveCurrentOrganizationResponse;
            retrieveCurrentOrganizationResponse.Should().NotBeNull();
            Console.WriteLine($"FriendlyName:{retrieveCurrentOrganizationResponse.Detail.FriendlyName} GEO:{retrieveCurrentOrganizationResponse.Detail.Geo}");

            Console.WriteLine("\nCalling RetrieveVersionRequest");
            var retrieveVersionRequest = new RetrieveVersionRequest();
            var retrieveVersionResponse = client.Execute(retrieveVersionRequest) as RetrieveVersionResponse;
            retrieveVersionResponse.Should().NotBeNull();
            Console.WriteLine($"Version:{retrieveVersionResponse.Version}");
        }
    }
}
