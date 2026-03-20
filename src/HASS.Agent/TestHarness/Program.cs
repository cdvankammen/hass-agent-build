using System;

namespace HASS.Agent.TestHarness
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                KeyCommandTester.Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Test failed: " + e.Message);
                return 2;
            }
        }
    }
}
