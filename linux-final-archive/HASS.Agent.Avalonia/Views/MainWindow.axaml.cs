using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HASS.Agent.Avalonia.ViewModels;
using Serilog;

namespace HASS.Agent.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void RunCommand_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CommandDisplayModel command && DataContext is MainWindowViewModel vm)
        {
            _ = ExecuteCommand(vm, command);
        }
    }

    private void EditCommand_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CommandDisplayModel command)
        {
            Log.Information("[GUI] Edit command: {id}", command.Id);
            // TODO: Open edit command dialog
        }
    }

    private void DeleteCommand_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CommandDisplayModel command && DataContext is MainWindowViewModel vm)
        {
            _ = DeleteCommand(vm, command);
        }
    }

    private void EditSensor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SensorDisplayModel sensor)
        {
            Log.Information("[GUI] Edit sensor: {id}", sensor.Id);
            // TODO: Open edit sensor dialog
        }
    }

    private void DeleteSensor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SensorDisplayModel sensor && DataContext is MainWindowViewModel vm)
        {
            _ = DeleteSensor(vm, sensor);
        }
    }

    private void ToggleBluetoothConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BluetoothDeviceModel device && DataContext is MainWindowViewModel vm)
        {
            _ = ToggleBluetoothConnection(vm, device);
        }
    }

    private void OpenWebUI_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            OpenUrl(vm.HeadlessUrl);
        }
    }

    private void OpenGitHub_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/LAB02-Research/HASS.Agent");
    }

    private void OpenDocs_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://hassagent.readthedocs.io/");
    }

    private void OpenIssues_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/LAB02-Research/HASS.Agent/issues");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GUI] Failed to open URL: {url}", url);
        }
    }

    private async System.Threading.Tasks.Task ExecuteCommand(MainWindowViewModel vm, CommandDisplayModel command)
    {
        try
        {
            Log.Information("[GUI] Executing command: {name} ({id})", command.Name, command.Id);
            using var client = new HttpClient();
            var payload = JsonSerializer.Serialize(new
            {
                Id = command.Id,
                Name = command.Name,
                Execute = command.Command
            });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{vm.HeadlessUrl}/commands/execute/{command.Id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                vm.StatusMessage = $"Command '{command.Name}' executed";
                Log.Information("[GUI] Command executed successfully: {name}", command.Name);
            }
            else
            {
                vm.StatusMessage = $"Failed to execute '{command.Name}'";
                Log.Warning("[GUI] Command execution failed: {name}, status: {status}", command.Name, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Error executing command: {ex.Message}";
            Log.Error(ex, "[GUI] Error executing command: {name}", command.Name);
        }
    }

    private async System.Threading.Tasks.Task DeleteCommand(MainWindowViewModel vm, CommandDisplayModel command)
    {
        try
        {
            Log.Information("[GUI] Deleting command: {name} ({id})", command.Name, command.Id);
            using var client = new HttpClient();
            var response = await client.DeleteAsync($"{vm.HeadlessUrl}/commands/{command.Id}");
            
            if (response.IsSuccessStatusCode)
            {
                vm.StatusMessage = $"Command '{command.Name}' deleted";
                vm.RefreshCommandsCommand.Execute(null);
            }
            else
            {
                vm.StatusMessage = $"Failed to delete '{command.Name}'";
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Error deleting command: {ex.Message}";
            Log.Error(ex, "[GUI] Error deleting command: {name}", command.Name);
        }
    }

    private async System.Threading.Tasks.Task DeleteSensor(MainWindowViewModel vm, SensorDisplayModel sensor)
    {
        try
        {
            Log.Information("[GUI] Deleting sensor: {name} ({id})", sensor.Name, sensor.Id);
            using var client = new HttpClient();
            var response = await client.DeleteAsync($"{vm.HeadlessUrl}/sensors/{sensor.Id}");
            
            if (response.IsSuccessStatusCode)
            {
                vm.StatusMessage = $"Sensor '{sensor.Name}' deleted";
                vm.RefreshSensorsCommand.Execute(null);
            }
            else
            {
                vm.StatusMessage = $"Failed to delete '{sensor.Name}'";
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Error deleting sensor: {ex.Message}";
            Log.Error(ex, "[GUI] Error deleting sensor: {name}", sensor.Name);
        }
    }

    private async System.Threading.Tasks.Task ToggleBluetoothConnection(MainWindowViewModel vm, BluetoothDeviceModel device)
    {
        try
        {
            var action = device.IsConnected ? "disconnect" : "connect";
            Log.Information("[GUI] {action} Bluetooth device: {name}", action, device.Name);
            
            using var client = new HttpClient();
            var response = await client.PostAsync($"{vm.HeadlessUrl}/bluetooth/{action}/{device.MacAddress}", null);
            
            if (response.IsSuccessStatusCode)
            {
                vm.StatusMessage = $"Bluetooth {action} successful for '{device.Name}'";
                vm.RefreshBluetoothCommand.Execute(null);
            }
            else
            {
                vm.StatusMessage = $"Failed to {action} '{device.Name}'";
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Error with Bluetooth: {ex.Message}";
            Log.Error(ex, "[GUI] Error with Bluetooth device: {name}", device.Name);
        }
    }
}