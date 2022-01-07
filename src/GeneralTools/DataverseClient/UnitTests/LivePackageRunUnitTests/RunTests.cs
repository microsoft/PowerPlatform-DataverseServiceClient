using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace LivePackageRunUnitTests
{
    public class RunTests
    {
        public RunTests(ITestOutputHelper output)
        {
            var converter = new Converter(output);
            Console.SetOut(converter);
        }

        [Fact]
        public void InvokeBasicTest()
        {
            LivePackageTestsConsole.Program.SkipStop = true; 
            LivePackageTestsConsole.Program.Main(new string[] { "BasicFlow" });
        }

        [Fact]
        public void InvokeReadSolutionsTest()
        {
            LivePackageTestsConsole.Program.SkipStop = true;
            LivePackageTestsConsole.Program.Main(new string[] { "listsolutions" });
        }


        [Fact]
        public void InvokeCUDTestTest()
        {
            LivePackageTestsConsole.Program.SkipStop = true;
            LivePackageTestsConsole.Program.Main(new string[] { "CUDTest" });
        }

        #region Utility
        private class Converter : TextWriter
        {
            ITestOutputHelper _output;
            public Converter(ITestOutputHelper output)
            {
                _output = output;
            }
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
            public override void WriteLine(string message)
            {
                _output.WriteLine(message);
            }
            public override void WriteLine(string format, params object[] args)
            {
                _output.WriteLine(format, args);
            }

            public override void Write(char value)
            {
                throw new NotSupportedException("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
            }
        }

        #endregion

    }
}
