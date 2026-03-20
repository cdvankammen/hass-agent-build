using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HASS.Agent.Core.Logging;

namespace HASS.Agent.Avalonia.Services;

/// <summary>
/// Cross-platform notification service for displaying system notifications
/// </summary>
public class NotificationService
{
    private readonly Queue<NotificationRequest> _pendingNotifications = new();
    private bool _isProcessing;
    
    public event EventHandler<NotificationActionEventArgs>? ActionClicked;
    
    /// <summary>
    /// Shows a notification using the platform's native notification system
    /// </summary>
    public async Task ShowNotificationAsync(string title, string message, string? imageUrl = null, int durationMs = 5000, List<NotificationAction>? actions = null)
    {
        var request = new NotificationRequest
        {
            Title = title,
            Message = message,
            ImageUrl = imageUrl,
            DurationMs = durationMs,
            Actions = actions ?? new List<NotificationAction>()
        };
        
        _pendingNotifications.Enqueue(request);
        await ProcessNotificationQueueAsync();
    }
    
    private async Task ProcessNotificationQueueAsync()
    {
        if (_isProcessing) return;
        _isProcessing = true;
        
        try
        {
            while (_pendingNotifications.Count > 0)
            {
                var notification = _pendingNotifications.Dequeue();
                await ShowPlatformNotificationAsync(notification);
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
    
    private async Task ShowPlatformNotificationAsync(NotificationRequest notification)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await ShowLinuxNotificationAsync(notification);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await ShowMacOSNotificationAsync(notification);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ShowWindowsNotificationAsync(notification);
            }
            else
            {
                AgentLogger.Warning($"Notifications not supported on this platform: {RuntimeInformation.OSDescription}");
            }
        }
        catch (Exception ex)
        {
            AgentLogger.Error($"Failed to show notification: {ex.Message}");
        }
    }
    
    private async Task ShowLinuxNotificationAsync(NotificationRequest notification)
    {
        // Use notify-send for Linux notifications
        // Supports: title, message, icon, timeout
        var arguments = new List<string>();
        
        // Add urgency based on context
        arguments.Add("-u");
        arguments.Add("normal");
        
        // Add timeout
        arguments.Add("-t");
        arguments.Add(notification.DurationMs.ToString());
        
        // Add app name
        arguments.Add("-a");
        arguments.Add("HASS.Agent");
        
        // Add icon if available
        if (!string.IsNullOrEmpty(notification.ImageUrl))
        {
            arguments.Add("-i");
            arguments.Add(notification.ImageUrl);
        }
        
        // Add title and message
        arguments.Add($"\"{EscapeShellArg(notification.Title)}\"");
        arguments.Add($"\"{EscapeShellArg(notification.Message)}\"");
        
        await RunProcessAsync("notify-send", string.Join(" ", arguments));
    }
    
    private async Task ShowMacOSNotificationAsync(NotificationRequest notification)
    {
        // Use osascript for macOS notifications
        var escapedTitle = notification.Title.Replace("\"", "\\\"");
        var escapedMessage = notification.Message.Replace("\"", "\\\"");
        
        var script = $"display notification \"{escapedMessage}\" with title \"{escapedTitle}\"";
        
        // Add subtitle if we have additional info
        // script += " subtitle \"HASS.Agent\"";
        
        // Add sound
        script += " sound name \"default\"";
        
        await RunProcessAsync("osascript", $"-e '{script}'");
    }
    
    private async Task ShowWindowsNotificationAsync(NotificationRequest notification)
    {
        // For Windows, we could use PowerShell or integrate with Toast notifications
        // This is a simple implementation using PowerShell
        var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$template = @""
<toast>
    <visual>
        <binding template=""ToastText02"">
            <text id=""1"">{EscapePowerShellString(notification.Title)}</text>
            <text id=""2"">{EscapePowerShellString(notification.Message)}</text>
        </binding>
    </visual>
</toast>
""@

$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier(""HASS.Agent"").Show($toast)
";
        
        await RunProcessAsync("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");
    }
    
    private static string EscapeShellArg(string arg)
    {
        return arg.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("$", "\\$")
                  .Replace("`", "\\`");
    }
    
    private static string EscapePowerShellString(string str)
    {
        return str.Replace("&", "&amp;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("\"", "&quot;");
    }
    
    private async Task RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    AgentLogger.Debug($"Notification process exited with code {process.ExitCode}: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            AgentLogger.Debug($"Failed to run notification process: {ex.Message}");
        }
    }
}

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int DurationMs { get; set; } = 5000;
    public List<NotificationAction> Actions { get; set; } = new();
}

public class NotificationAction
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Uri { get; set; }
}

public class NotificationActionEventArgs : EventArgs
{
    public string ActionId { get; }
    public string? Uri { get; }
    
    public NotificationActionEventArgs(string actionId, string? uri = null)
    {
        ActionId = actionId;
        Uri = uri;
    }
}
