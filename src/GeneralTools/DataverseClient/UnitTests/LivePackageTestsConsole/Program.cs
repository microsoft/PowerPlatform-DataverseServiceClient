using LiveTestsConsole;
using System;

namespace LivePackageTestsConsole
{
    /// <summary>
    /// This program will test run against live tests on a known Nuget Package version. 
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting Tests");

            if (0 < args.Length)
            {
                if (string.Compare(args[0], "BasicFlow", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new BasicFlow();
                    tests.Run();
                }
                else if (string.Compare(args[0], "ListSolutions", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new SolutionTests();

                    tests.ListSolutions();
                }
                else if (string.Compare(args[0], "ExportSolution", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new SolutionTests();

                    tests.ExportSolution();
                }
                else if (string.Compare(args[0], "ImportSolution", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new SolutionTests();

                    tests.ImportSolution();
                }
                else if (string.Compare(args[0], "StageSolution", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new SolutionTests();

                    tests.StageSolution();
                }
                else if (string.Compare(args[0], "DeleteSolution", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new SolutionTests();

                    tests.DeleteSolution();
                }
                else if (string.Compare(args[0], "TokenRefresh", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new TokenRefresh();

                    tests.Run();
                }
                else if ( string.Compare(args[0], "CUDTest", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var tests = new CUDTest();
                    tests.RunTest();
                    tests.RunTest2();
                }
            }
            else
            {
                var tests = new BasicFlow();
                tests.Run();
            }
        }
    }
}
