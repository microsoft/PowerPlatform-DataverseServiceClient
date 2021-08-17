using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
        public void ImportSolution()
        {
            Console.WriteLine("Starting ImportSolution");

            var client = Auth.CreateClient();

            client.ImportSolution(Path.Combine("TestData", "TestSolution_1_0_0_1.zip"), out var importId);
            Console.WriteLine($"ImportSolution id:{importId}");
        }

        public void ExportSolution()
        {
            Console.WriteLine("Starting ExportSolution");

            var client = Auth.CreateClient();

            var request = new ExportSolutionRequest()
            {
                SolutionName = "TestSolution"
            };

            var response = client.Execute(request) as ExportSolutionResponse;
            Console.WriteLine($"ExportSolutionFile length:{response.ExportSolutionFile.Length}");
        }

        public void ListSolutions()
        {
            Console.WriteLine("Starting ListSolutions");

            var client = Auth.CreateClient();

            var request = new RetrieveOrganizationInfoRequest();
            var response = client.Execute(request) as RetrieveOrganizationInfoResponse;
            Console.WriteLine($"Solutions.Count:{response.organizationInfo.Solutions.Count}");
        }

        public void DeleteSolution()
        {
            Console.WriteLine("Starting DeleteSolution");

            var client = Auth.CreateClient();

            var request = new DeleteRequest() { Target = new EntityReference("solution", new Guid("a50ac31a-b3f3-4fd3-b691-20ddc4d494d7")) };
            //var request = new DeleteRequest() { Target = new EntityReference("solutions", "UniqueName", "TestSolution") };
            var response = client.Execute(request) as DeleteResponse;
            Console.WriteLine("Done");
        }
    }
}
