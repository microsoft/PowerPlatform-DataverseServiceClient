using System;

namespace LiveTestsConsole
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Starting Tests");

            try
            {
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
                }
                else
                {
                    var tests = new BasicFlow();
                    tests.Run();
                }
            }
            catch (Exception ex)
            {
                // We catch and write to console here so we don't make requests to umwatson.events.data.microsoft.com due to exe crash
                Console.WriteLine($"Unhandled Exception: {ex}");
                return 1;
            }


            Console.WriteLine("Finished executing tests");
            return 0;
        }
    }
}
