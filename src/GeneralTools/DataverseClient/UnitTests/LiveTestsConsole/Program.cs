using System;

namespace LiveTestsConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Tests");

            var tokenRefresh = new TokenRefresh();

            tokenRefresh.Run();
        }
    }
}
