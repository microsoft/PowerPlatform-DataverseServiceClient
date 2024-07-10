using Client_Core_UnitTests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DataverseClient_Core_UnitTests
{
    public class AzAuthExtentionTests
    {
        TestSupport testSupport = new TestSupport();
        public AzAuthExtentionTests(ITestOutputHelper output)
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
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddProvider(new TraceConsoleLoggingProvider(output)));
            testSupport.logger = loggerFactory.CreateLogger<ClientDynamicsExtensionsTests>();
        }

        //[SkippableConnectionTest]
        //[Fact]
        [Trait("Category", "Live Connect Required")]
        public void CreateServiceClient()
        {
            //var client = AzAuth.CreateServiceClient("<orgA>", true);
            //var client = AzAuth.CreateServiceClient("<orgB>", true);
            var client = AzAuth.CreateServiceClient(new ConnectionOptions()
            {
                ServiceUri = new Uri("<orgC>"),
                Logger = testSupport.logger
            });

            Assert.NotNull(client);

            var rslt = client.Execute(new WhoAmIRequest());
            Assert.NotNull(rslt);
            Assert.IsType<WhoAmIResponse>(rslt);

            var rlst1 = client.Execute(new RetrieveCurrentOrganizationRequest() { AccessType = 0});
            Assert.NotNull(rlst1);
            Assert.IsType<RetrieveCurrentOrganizationResponse>(rlst1);

        }
    }
}
