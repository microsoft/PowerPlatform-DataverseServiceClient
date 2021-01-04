using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace CdsClient_Core_UnitTests
{
    public class TraceConsoleSupport : TraceListener
    {
        private readonly ITestOutputHelper outWriter;
        public TraceConsoleSupport(ITestOutputHelper output)
        {
            outWriter = output;
        }

        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
            try
            {
                outWriter.WriteLine(message);
            }
            catch (System.InvalidOperationException)
            {
                // Do nothing here.. this can happen if the test does not reset the appdomain and is restarted. 
            }

        }
    }
}
