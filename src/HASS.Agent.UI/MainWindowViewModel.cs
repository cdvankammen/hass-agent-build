using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HASS.Agent.UI
{
    public class MainWindowViewModel
    {
        public ObservableCollection<HASS.Agent.Core.CommandModel> Commands { get; } = new();
        public HASS.Agent.Core.CommandModel SelectedCommand { get; set; }
        public ObservableCollection<HASS.Agent.Core.SensorModel> Sensors { get; } = new();

        public ICommand ExecuteCommandCmd { get; }
        public ICommand ServiceStatusCmd { get; }
        public ICommand ServiceStartCmd { get; }
        public ICommand ServiceStopCmd { get; }
        public string ServiceStatusText { get; set; } = "";

        private readonly HttpClient _http = new();

        public MainWindowViewModel()
        {
            ExecuteCommandCmd = new RelayCommand(async p => await ExecuteCommand(p?.ToString()));
            ServiceStatusCmd = new RelayCommand(async _ => await GetServiceStatus());
            ServiceStartCmd = new RelayCommand(async _ => await StartService());
            ServiceStopCmd = new RelayCommand(async _ => await StopService());
            _ = Task.Run(ListCommands);
            _ = Task.Run(ListSensors);
        }

        public async Task RefreshCommands() => await ListCommands();

        private async Task ListCommands()
        {
            try
            {
                var res = await _http.GetAsync("http://127.0.0.1:11111/commands");
                res.EnsureSuccessStatusCode();
                var txt = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<HASS.Agent.Core.CommandModel[]>(txt);
                Commands.Clear();
                if (items != null)
                {
                    foreach (var i in items) Commands.Add(i);
                }
            }
            catch
            {
                // ignore
            }
        }

        private async Task ExecuteCommand(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return;
            var payload = new { id = idOrName };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            try
            {
                var res = await _http.PostAsync("http://127.0.0.1:11111/command", content);
                res.EnsureSuccessStatusCode();

                // try to read updated commands payload and refresh local list without extra GET
                try
                {
                    var txt = await res.Content.ReadAsStringAsync();
                    var items = JsonSerializer.Deserialize<HASS.Agent.Core.CommandModel[]>(txt);
                    if (items != null)
                    {
                        Commands.Clear();
                        foreach (var i in items) Commands.Add(i);
                    }
                }
                catch
                {
                    // fallback to list fetch on failure
                    _ = Task.Run(ListCommands);
                }
            }
            catch
            {
            }
        }

        private async Task ListSensors()
        {
            try
            {
                var res = await _http.GetAsync("http://127.0.0.1:11111/sensors");
                res.EnsureSuccessStatusCode();
                var txt = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<HASS.Agent.Core.SensorModel[]>(txt);
                Sensors.Clear();
                if (items != null)
                {
                    foreach (var i in items) Sensors.Add(i);
                }
            }
            catch
            {
                // ignore
            }
        }

        private async Task GetServiceStatus()
        {
            try
            {
                var res = await _http.GetAsync("http://127.0.0.1:11111/service/status");
                var txt = await res.Content.ReadAsStringAsync();
                ServiceStatusText = txt;
            }
            catch (Exception ex)
            {
                ServiceStatusText = ex.Message;
            }
        }

        private async Task StartService()
        {
            try
            {
                var res = await _http.PostAsync("http://127.0.0.1:11111/service/start", new StringContent(""));
                ServiceStatusText = await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                ServiceStatusText = ex.Message;
            }
        }

        private async Task StopService()
        {
            try
            {
                var res = await _http.PostAsync("http://127.0.0.1:11111/service/stop", new StringContent(""));
                ServiceStatusText = await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                ServiceStatusText = ex.Message;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        public RelayCommand(Func<object, Task> execute) => _execute = execute;
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => true;
        public async void Execute(object parameter) => await _execute(parameter);
    }
}
