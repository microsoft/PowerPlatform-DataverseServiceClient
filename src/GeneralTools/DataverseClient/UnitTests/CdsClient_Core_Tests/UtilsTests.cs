#region using
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
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

        [Fact]
        public void ParseAltKeyCollection_WithNulls_Test()
        {
            var keyValuePairs = new KeyAttributeCollection
            {
                { "NotNull", "TestValue" },
                { "NullValue", null }
            };

            var result = Utilities.ParseAltKeyCollection(keyValuePairs);

            result.Should().NotBeNullOrWhiteSpace();
            result.Should().Contain("NotNull='TestValue'");
            result.Should().Contain("NullValue=''");
        }
    }
}
