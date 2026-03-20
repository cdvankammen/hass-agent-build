using System;
using HASS.Agent.Platform;
using HASS.Agent.Platform.Input;

namespace HASS.Agent.TestHarness
{
    internal static class KeyCommandTester
    {
        public static void Run()
        {
            Console.WriteLine("Testing LinuxKeyCommand - will send text 'hello' via xdotool if available.");
            var sim = PlatformFactory.GetInputSimulator();
            sim.SendText("hello from hass-agent");
            Console.WriteLine("Done");
        }
    }
}
