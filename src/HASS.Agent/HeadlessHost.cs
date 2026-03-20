using System;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using HASS.Agent.API;
using HASS.Agent.HomeAssistant;
using Serilog;

namespace HASS.Agent
{
    internal static class HeadlessHost
    {
        private static readonly CancellationTokenSource Cts = new();

        public static async Task<int> RunAsync(string[] args)
        {
            try
            {
                // prepare logging
                Managers.LoggingManager.PrepareLogging(args);

                // load settings
                var settingsLoaded = Settings.SettingsManager.LoadAsync(true).GetAwaiter().GetResult();
                if (!settingsLoaded)
                {
                    Log.Error("[HEADLESS] Unable to load settings");
                    return 1;
                }

                // initialize shared base
                AgentSharedBase.Initialize(Variables.AppSettings.DeviceName, Variables.MqttManager, Variables.AppSettings.CustomExecutorBinary);

                // handle termination
                AssemblyLoadContext.Default.Unloading += _ => Cts.Cancel();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; Cts.Cancel(); };

                // start managers
                _ = Task.Run(ApiManager.Initialize);
                _ = Task.Run(async () => await HassApiManager.InitializeAsync());
                _ = Task.Run(Variables.MqttManager.Initialize);
                _ = Task.Run(Managers.SensorsManager.Initialize);
                _ = Task.Run(Managers.CommandsManager.Initialize);

                Log.Information("[HEADLESS] Started. Press Ctrl+C to exit.");

                // wait for cancellation
                while (!Cts.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }

                Log.Information("[HEADLESS] Shutting down");
                Variables.ShuttingDown = true;
                await Task.Delay(500);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[HEADLESS] Fatal error: {err}", ex.Message);
                return 2;
            }
        }
    }
}
