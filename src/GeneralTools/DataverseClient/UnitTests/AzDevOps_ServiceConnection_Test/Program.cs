using System;
using System.CommandLine;
using System.Threading.Tasks;
using AzDevOps_ServiceConnection_Test.Operations;

namespace AzDevOps_ServiceConnection_Test
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("TEST Creating Service Connection to Dataverse");

            var rootCommand = new RootCommand("Test harness for connecting to Dataverse from DevOps");

            var DvUrlToUse = GenerateOptionItem<Uri>(
                "--DataverseUrl", 
                "URL of the Dataverse Server that you want to connect too.\nMake sure to remove any trailing / from the URL! You will see an auth error if present", 
                "-url", 
                true);

            var TenantId = GenerateOptionItem<Guid>(
                "--tenantid",
                "tenantID (Guid)",
                "-t",
                true);

            var ClientId = GenerateOptionItem<Guid>(
                "--ClientId",
                "ClientId (Guid)",
                "-c",
                true);

            var ServiceConnectionId = GenerateOptionItem<Guid>(
                "--ServiceConnectionId",
                "ServiceConnectionId (Guid)",
                "-s",
                true);


            var SystemAccessTokenEnvironmentId = GenerateOptionItem<string>(
                "--SystemAccessTokenName",
                "Environment Variable name for the System Access Token",
                "-en",
                false);

            var testCommand = new Command("test", "Get Connection to Dataverse and Test it")
            {
                DvUrlToUse,
                TenantId,
                ClientId,
                ServiceConnectionId,
                SystemAccessTokenEnvironmentId
            };

            testCommand.SetHandler(async (dvUrlToUse, tenantId, clientId, serviceConnectionId, systemAccessTokenEnvironmentId) =>
            {
                await ConnectionTest.Run(dvUrlToUse, tenantId, clientId, serviceConnectionId, systemAccessTokenEnvironmentId).ConfigureAwait(false);
            }, DvUrlToUse, TenantId, ClientId, ServiceConnectionId, SystemAccessTokenEnvironmentId);

            rootCommand.Add(testCommand);

            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);

        }


        private static Option<T> GenerateOptionItem<T>(string name, string description, string alias, bool isRequired = false)
        {
            var option = new Option<T>(name: $"--{name}", description: description);
            option.AddAlias($"-{alias}");
            option.IsRequired = isRequired;
            return option;
        }


    }
}
