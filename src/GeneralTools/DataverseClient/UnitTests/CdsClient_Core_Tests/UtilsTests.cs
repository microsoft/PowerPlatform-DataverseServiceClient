#region using
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
#endregion

namespace DataverseClient_Core_UnitTests
{
    public class UtilsTests
    {
        [Fact]
        public void SerializationTest()
        {
            var request = new ExportSolutionRequest() { SolutionName = "SerializationTest" };
            var expandoObject = request.ToExpandoObject();
            var json = System.Text.Json.JsonSerializer.Serialize(expandoObject);

            json.Should().Contain("\"SolutionName\":\"SerializationTest\"");
            json.Should().Contain("\"Managed\":false");
        }
    }
}
