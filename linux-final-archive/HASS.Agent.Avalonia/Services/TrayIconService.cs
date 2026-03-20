using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using HASS.Agent.Core.Logging;

namespace HASS.Agent.Avalonia.Services;

/// <summary>
/// Cross-platform system tray service using Avalonia's TrayIcon support
/// </summary>
public class TrayIconService : IDisposable
{
    private TrayIcon? _trayIcon;
    private NativeMenu? _trayMenu;
    private readonly Window _mainWindow;
    private bool _disposed;
    
    public event EventHandler? ShowWindowRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<string>? QuickActionRequested;
    
    public bool IsConnected { get; set; }
    public string StatusText { get; set; } = "Disconnected";
    
    public TrayIconService(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }
    
    public void Initialize()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                AgentLogger.Warning("TrayIcon only supported in desktop applications");
                return;
            }
            
            _trayMenu = CreateTrayMenu();
            
            _trayIcon = new TrayIcon
            {
                ToolTipText = "HASS.Agent - Click to open",
                Menu = _trayMenu,
                IsVisible = true
            };
            
            // Set icon based on platform
            SetTrayIcon();
            
            // Handle clicks
            _trayIcon.Clicked += OnTrayIconClicked;
            
            AgentLogger.Info("System tray icon initialized");
        }
        catch (Exception ex)
        {
            AgentLogger.Error($"Failed to initialize tray icon: {ex.Message}");
        }
    }
    
    private void SetTrayIcon()
    {
        try
        {
            // Use app icon as tray icon
            _trayIcon!.Icon = _mainWindow.Icon;
        }
        catch (Exception ex)
        {
            AgentLogger.Debug($"Could not set tray icon: {ex.Message}");
        }
    }
    
    private NativeMenu CreateTrayMenu()
    {
        var menu = new NativeMenu();
        
        // Status item (non-clickable header)
        var statusItem = new NativeMenuItem($"Status: {StatusText}");
        statusItem.IsEnabled = false;
        menu.Add(statusItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        // Show/Hide window
        var showItem = new NativeMenuItem("Show Window");
        showItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Add(showItem);
        
        // Quick Actions submenu
        var quickActionsMenu = new NativeMenu();
        AddQuickAction(quickActionsMenu, "Lock Screen", "lock_screen");
        AddQuickAction(quickActionsMenu, "Mute Volume", "volume_mute");
        AddQuickAction(quickActionsMenu, "Sleep", "sleep");
        
        var quickActionsItem = new NativeMenuItem("Quick Actions") { Menu = quickActionsMenu };
        menu.Add(quickActionsItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        // Sensors Status submenu
        var sensorsMenu = new NativeMenu();
        var sensorsStatusItem = new NativeMenuItem("View All Sensors...");
        sensorsStatusItem.Click += (s, e) => 
        {
            ShowWindowRequested?.Invoke(this, EventArgs.Empty);
            // Navigate to sensors tab - handled by main window
        };
        sensorsMenu.Add(sensorsStatusItem);
        
        var sensorsItem = new NativeMenuItem("Sensors") { Menu = sensorsMenu };
        menu.Add(sensorsItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        // Settings
        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Add(settingsItem);
        
        // Reconnect
        var reconnectItem = new NativeMenuItem("Reconnect");
        reconnectItem.Click += async (s, e) => await ReconnectAsync();
        menu.Add(reconnectItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        // Exit
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Add(exitItem);
        
        return menu;
    }
    
    private void AddQuickAction(NativeMenu menu, string name, string commandId)
    {
        var item = new NativeMenuItem(name);
        item.Click += (s, e) => QuickActionRequested?.Invoke(this, commandId);
        menu.Add(item);
    }
    
    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void UpdateStatus(bool connected, string statusText)
    {
        IsConnected = connected;
        StatusText = statusText;
        
        // Update icon
        SetTrayIcon();
        
        // Update tooltip
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = $"HASS.Agent - {statusText}";
        }
        
        // Rebuild menu to update status
        if (_trayMenu != null)
        {
            _trayMenu = CreateTrayMenu();
            if (_trayIcon != null)
            {
                _trayIcon.Menu = _trayMenu;
            }
        }
    }
    
    public void ShowBalloonTip(string title, string message, int timeoutMs = 5000)
    {
        // Avalonia doesn't have native balloon tips, but we can use notifications
        // This would integrate with the platform's notification system
        AgentLogger.Info($"Balloon: {title} - {message}");
        
        // On Linux, we could use libnotify
        // On macOS, we could use NSUserNotification
        // On Windows, we could use Toast notifications
        ShowPlatformNotification(title, message);
    }
    
    private void ShowPlatformNotification(string title, string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Use notify-send on Linux
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"{title}\" \"{message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use osascript on macOS
                var script = $"display notification \"{message}\" with title \"{title}\"";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            // Windows would use Toast notifications - handled separately
        }
        catch (Exception ex)
        {
            AgentLogger.Debug($"Could not show notification: {ex.Message}");
        }
    }
    
    private async Task ReconnectAsync()
    {
        AgentLogger.Info("Reconnect requested from tray menu");
        UpdateStatus(false, "Reconnecting...");
        
        // Signal main app to reconnect
        // This would be handled by the main view model
        await Task.Delay(100);
    }
    
    public void Hide()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
        }
    }
    
    public void Show()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = true;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_trayIcon != null)
        {
            _trayIcon.Clicked -= OnTrayIconClicked;
            _trayIcon.IsVisible = false;
            _trayIcon = null;
        }
        
        _trayMenu = null;
        
        GC.SuppressFinalize(this);
    }
}
