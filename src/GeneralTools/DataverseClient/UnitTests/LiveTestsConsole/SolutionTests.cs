using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTestsConsole
{
    public class SolutionTests
    {
        private readonly string _testSolutionPath;

        public SolutionTests()
        {
            _testSolutionPath = Path.GetFullPath(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TestData", "TestSolution_1_0_0_1.zip"));
            if (!File.Exists(_testSolutionPath))
            {
                throw new FileNotFoundException($"Could not fine test zip file located at: {_testSolutionPath}");
            }
        }

        public void ImportSolution()
        {
            Console.WriteLine("Starting ImportSolution");

            var client = Auth.CreateClient();

            client.ImportSolution(_testSolutionPath, out var importId);
            if (importId == Guid.Empty)
            {
                throw new InvalidOperationException($"Import of solution was unsuccessful. See logs or debug.");
            }
            Console.WriteLine($"ImportSolution id:{importId}");
        }

        public void StageSolution()
        {
            Console.WriteLine("Starting StageSolution");

            var client = Auth.CreateClient();

            // BUG: The StageSolutionRequest/StageSolutionResponse message types are currently not generated. This will be fixed shortly.
//#if false
            // Using strong-typed request/response message classes
            var request = new StageSolutionRequest
            {
                CustomizationFile = File.ReadAllBytes(_testSolutionPath)
            };
            var response = (StageSolutionResponse)client.Execute(request);
            var results = response.StageSolutionResults;
//#else
//            // For now, we'll use an OrganizationRequest/Response
//            var request = new OrganizationRequest("StageSolution")
//            {
//                ["CustomizationFile"] = File.ReadAllBytes(_testSolutionPath)
//            };
//            var response = client.Execute(request);
//            // BUG: Throws exception "The given key was not present in the dictionary." - Because the message types are missing for the 'StageSolution' operation.
//            var results = (StageSolutionResults)response["StageSolutionResults"];
//#endif

            // Need to make sure we get the right data back
            Console.WriteLine("Results:");
            Console.WriteLine($"  StageSolutionUploadId: {results.StageSolutionUploadId}");
        }

        public void ExportSolution()
        {
            Console.WriteLine("Starting ExportSolution");

            var client = Auth.CreateClient();

            var request = new ExportSolutionRequest()
            {
                SolutionName = "TestSolution"
            };

            var response = (ExportSolutionResponse)client.Execute(request);
            Console.WriteLine($"ExportSolutionFile length:{response.ExportSolutionFile.Length}");
        }

        public void ListSolutions()
        {
            Console.WriteLine("Starting ListSolutions");

            var client = Auth.CreateClient();

            var request = new RetrieveOrganizationInfoRequest();
            var response = (RetrieveOrganizationInfoResponse)client.Execute(request);
            Console.WriteLine($"Solutions.Count:{response.organizationInfo.Solutions.Count}");

            Console.WriteLine($"Listing non-1st party solutions:");
            var excludePublishers = new[] { "microsoftfirstparty", "microsoftdynamics", "MicrosoftCorporation", "dynamics365customerengagement" };
            var non1stPartySolutions = response.organizationInfo.Solutions.Where(s => !excludePublishers.Contains(s.PublisherUniqueName, StringComparer.OrdinalIgnoreCase));
            foreach (var solution in non1stPartySolutions)
            {
                Console.WriteLine($"  Id: {solution.Id}, Publisher: {solution.PublisherUniqueName,-10}, Name: {solution.SolutionUniqueName}");
            }
        }

        public void DeleteSolution()
        {
            Console.WriteLine("Starting DeleteSolution");

            var client = Auth.CreateClient();

            // First, get the id of our TestSolution we install via the 'ImportSolution'
            var installedSolution = ((RetrieveOrganizationInfoResponse)client.Execute(new RetrieveOrganizationInfoRequest())).organizationInfo.Solutions
                .SingleOrDefault(s => s.SolutionUniqueName == "TestSolution");
            if (installedSolution == null)
            {
                throw new InvalidOperationException($"The org is missing the solution with unique name 'TestSolution'. Be sure to install it by running the 'ImportSolution' test first.");
            }

            var request = new DeleteRequest()
            {
                Target = new EntityReference("solution", installedSolution.Id)
            };
            var response = (DeleteResponse)client.Execute(request);
            Console.WriteLine("Done");
        }
    }
}
