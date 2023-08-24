using Microsoft.Crm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Client_Core_UnitTests
{
    public class SkippableConnectionTestAttribute : FactAttribute
    {
        private static bool IsConnectionInfoAvailable() => Environment.GetEnvironmentVariable("XUNITCONNTESTURI") != null;

        public SkippableConnectionTestAttribute()
        {
            if (!IsConnectionInfoAvailable())
            {
                Skip = "Ignored test as connection info is not present";
            }
        }

        public SkippableConnectionTestAttribute(bool skip, string skipMessage)
        {
            Skip = skipMessage;
        }
    }
}
