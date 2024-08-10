using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;

namespace AzDevOps_ServiceConnection_Test.Operations
{
    internal class ConnectionTest
    {
        internal static async Task Run(Uri dvUrlToUse, Guid tenantId, Guid clientId, Guid serviceConnectionId, string systemAccessTokenEnvironmentId)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                                builder.AddConsole(options =>
                                {
                                    options.IncludeScopes = true;
                                    options.TimestampFormat = "hh:mm:ss ";
                                })
                                .AddConfiguration(config.GetSection("Logging")));

            var logger = loggerFactory.CreateLogger<ServiceClient>();


            ServiceClient client = AzPipelineFederatedIdentityAuth.CreateServiceClient(
                tenantId.ToString(),
                clientId.ToString(),
                serviceConnectionId.ToString(),
                new ConnectionOptions()
                    {
                        ServiceUri = dvUrlToUse,
                        Logger = logger
                    }
                );

            var response = (WhoAmIResponse)await client.ExecuteAsync(new WhoAmIRequest()).ConfigureAwait(false);
            logger.LogInformation($"Response: {response.UserId} - {response.BusinessUnitId} - {response.OrganizationId}");
        }
    }
}
