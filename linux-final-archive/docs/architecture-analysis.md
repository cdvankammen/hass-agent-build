# HASS.Agent Architecture Analysis: Windows vs Cross-Platform

## Executive Summary

This document compares the original Windows-only HASS.Agent architecture with the new cross-platform refactored architecture, explaining design decisions and migration strategies.

---

## 1. High-Level Architecture Comparison

### Original Windows Architecture
```
┌─────────────────────────────────────────────────────────┐
│                    HASS.Agent (Windows)                  │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │
│  │ Windows Forms│  │    WMI      │  │ Windows APIs    │ │
│  │     GUI     │  │   Sensors   │  │ (Registry, etc) │ │
│  └─────────────┘  └─────────────┘  └─────────────────┘ │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │
│  │    MQTT     │  │  Commands   │  │    Keyboard     │ │
│  │   Client    │  │   Manager   │  │    Shortcuts    │ │
│  └─────────────┘  └─────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
              ┌───────────────────────┐
              │    Home Assistant     │
              │     (MQTT Broker)     │
              └───────────────────────┘
```

### New Cross-Platform Architecture
```
┌─────────────────────────────────────────────────────────────────────┐
│                        Application Layer                             │
├──────────────────┬─────────────────────┬────────────────────────────┤
│ HASS.Agent.      │ HASS.Agent.Headless │ HASS.Agent.SimpleHeadless  │
│ Avalonia (GUI)   │ (ASP.NET API)       │ (Minimal API)              │
└────────┬─────────┴──────────┬──────────┴─────────────┬──────────────┘
         │                    │                        │
         ▼                    ▼                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        HASS.Agent.Core                               │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐│
│  │  Sensors    │  │  Commands   │  │    MQTT     │  │   Logging   ││
│  │  Manager    │  │   Manager   │  │   Manager   │  │  (Serilog)  ││
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘│
└─────────────────────────────┬───────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       HASS.Agent.Platform                            │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    IPlatformProvider                            ││
│  │  GetHostname(), GetMemoryInfo(), GetCpuUsage(), etc.           ││
│  └─────────────────────────────────────────────────────────────────┘│
│                              │                                       │
│    ┌────────────────────────┼────────────────────────┐              │
│    ▼                        ▼                        ▼              │
│ ┌──────────┐         ┌──────────┐            ┌──────────┐          │
│ │  Linux   │         │  macOS   │            │ Windows  │          │
│ │ Provider │         │ Provider │            │ Provider │          │
│ └──────────┘         └──────────┘            └──────────┘          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────┐
              │    Home Assistant     │
              │  (MQTT/WebSocket API) │
              └───────────────────────┘
```

---

## 2. Component Comparison

### 2.1 GUI Framework

| Aspect | Windows (Original) | Cross-Platform (New) |
|--------|-------------------|---------------------|
| Framework | Windows Forms | Avalonia 11.x |
| Rendering | GDI+/DirectX | Skia |
| MVVM Support | Manual | CommunityToolkit.MVVM |
| Tray Icon | NotifyIcon | Platform-specific |
| Themes | Windows-only | Fluent (cross-platform) |

### 2.2 System Information Collection

| Sensor Type | Windows (Original) | Linux (New) | macOS (New) |
|-------------|-------------------|-------------|-------------|
| CPU Usage | WMI/PerformanceCounter | /proc/stat | sysctl |
| Memory | WMI | /proc/meminfo | vm_stat |
| Disk Usage | DriveInfo | statvfs | statvfs |
| Network | NetworkInterface | /sys/class/net | route/ifconfig |
| Battery | WMI | /sys/class/power_supply | IOKit |
| Process List | Process.GetProcesses | /proc | ps |

### 2.3 MQTT Communication

| Aspect | Windows (Original) | Cross-Platform (New) |
|--------|-------------------|---------------------|
| Library | M2MqttDotnetCore | MQTTnet 4.x |
| TLS Support | Yes | Yes |
| Reconnection | Manual | Built-in |
| QoS Support | Yes | Yes |
| Discovery | Home Assistant MQTT | Home Assistant MQTT |

### 2.4 Commands System

| Command Type | Windows (Original) | Cross-Platform (New) |
|--------------|-------------------|---------------------|
| Custom Commands | CMD/PowerShell | sh/bash/zsh |
| Keyboard | SendKeys/InputSimulator | X11/Cocoa/Windows API |
| Media Control | Windows Media API | MPRIS/MediaKey/WinRT |
| URL Launch | Process.Start | xdg-open/open/start |
| Notifications | Windows Toast | libnotify/Cocoa/Toast |

---

## 3. File Structure Comparison

### Original Windows Structure
```
HASS.Agent/
├── API/                    # Local API server
├── Commands/               # Command handlers
├── Controls/               # WinForms custom controls
├── Forms/                  # WinForms dialogs
├── Functions/              # Utility functions
├── HomeAssistant/          # HA communication
├── Libraries/              # Windows-specific libs
├── Managers/               # Business logic managers
├── Media/                  # Media control
├── Models/                 # Data models
├── MQTT/                   # MQTT client
├── Properties/             # Assembly properties
├── Resources/              # Embedded resources
├── Sensors/                # Sensor implementations
├── Service/                # Windows Service
├── Settings/               # Configuration
├── Program.cs              # Entry point
└── Variables.cs            # Global state
```

### New Cross-Platform Structure
```
src/
├── HASS.Agent.Core/           # Shared business logic
│   ├── CommandsManager.cs     # Command orchestration
│   ├── SensorsManager.cs      # Sensor orchestration
│   ├── MqttNetManager.cs      # MQTT communication
│   ├── Logging/               # Structured logging
│   ├── Update/                # Auto-update service
│   └── Models/                # Shared models
│
├── HASS.Agent.Platform/       # Platform abstraction
│   ├── Abstractions/          # Interfaces
│   │   └── IPlatformProvider.cs
│   ├── Linux/                 # Linux implementation
│   │   └── LinuxPlatformProvider.cs
│   └── PlatformFactory.cs     # Factory pattern
│
├── HASS.Agent.Avalonia/       # Cross-platform GUI
│   ├── Views/                 # XAML views
│   ├── ViewModels/            # MVVM view models
│   ├── Services/              # GUI services
│   └── App.axaml              # Application definition
│
├── HASS.Agent.Headless/       # Background service
│   └── Program.cs             # ASP.NET Core host
│
└── HASS.Agent.SimpleHeadless/ # Minimal headless
    └── Program.cs             # Minimal API
```

---

## 4. Key Design Decisions

### 4.1 Why Avalonia over Electron/MAUI?
- **Performance**: Native rendering, lower memory footprint
- **Compatibility**: Mature Linux/macOS support
- **Familiarity**: XAML-based, similar to WPF
- **Active Development**: Strong community support

### 4.2 Why Platform Abstraction Layer?
- **Testability**: Mock platform calls in tests
- **Maintainability**: Isolate platform-specific code
- **Extensibility**: Easy to add new platforms
- **Clean Architecture**: Dependency inversion principle

### 4.3 Why MQTTnet over M2Mqtt?
- **Active Maintenance**: Regular updates
- **Modern API**: Async/await support
- **Performance**: Better throughput
- **Features**: More configuration options

### 4.4 Why Serilog for Logging?
- **Structured Logging**: Better observability
- **Multiple Sinks**: Console, file, external services
- **Enrichers**: Add context automatically
- **Performance**: Async logging support

---

## 5. Migration Path

### Phase 1: Core Functionality (Completed ✅)
- [x] Platform abstraction layer
- [x] Linux platform provider
- [x] Core sensors manager
- [x] MQTT communication
- [x] Structured logging

### Phase 2: GUI Application (Completed ✅)
- [x] Avalonia project setup
- [x] Main window with navigation
- [x] Settings view
- [x] Sensors view
- [x] Commands view

### Phase 3: Packaging (Completed ✅)
- [x] Debian package (.deb)
- [x] macOS application bundle (.app)
- [x] Windows installer (WiX/MSI)
- [x] Auto-update mechanism

### Phase 4: Testing & Polish (In Progress 🔄)
- [ ] Keyboard functionality testing
- [ ] Integration testing with HA
- [ ] Security audit
- [ ] Performance optimization

---

## 6. Communication with Home Assistant Integration

### MQTT Topic Structure
```
homeassistant/
├── sensor/
│   └── {device_id}/
│       ├── {sensor_name}/config    # Discovery
│       └── {sensor_name}/state     # State updates
├── switch/
│   └── {device_id}/
│       ├── {switch_name}/config
│       ├── {switch_name}/state
│       └── {switch_name}/set       # Commands
└── button/
    └── {device_id}/
        └── {button_name}/config
```

### WebSocket API (Optional)
```
ws://{ha_host}:8123/api/websocket
- auth: {"type": "auth", "access_token": "..."}
- subscribe_events: {"type": "subscribe_events", "event_type": "..."}
```

---

## 7. Security Considerations

### Credentials Storage
| Platform | Storage Method |
|----------|---------------|
| Windows | DPAPI / Windows Credential Manager |
| Linux | libsecret / GNOME Keyring |
| macOS | Keychain Services |

### Network Security
- TLS 1.3 for MQTT connections
- Certificate validation
- Token-based authentication

---

## 8. Recommendations

1. **Complete keyboard testing** on both Linux (X11/Wayland) and macOS
2. **Add integration tests** that verify MQTT message format
3. **Implement credential storage** using platform-specific secure storage
4. **Add telemetry opt-in** for crash reporting and diagnostics
5. **Consider Flatpak/Snap** for Linux distribution
6. **Add code signing** for release builds

---

## Appendix: File Mapping

| Original File | New Location | Notes |
|--------------|--------------|-------|
| `Sensors/*.cs` | `HASS.Agent.Platform/Linux/*.cs` | Platform-specific |
| `Commands/*.cs` | `HASS.Agent.Core/CommandsManager.cs` | Unified |
| `MQTT/*.cs` | `HASS.Agent.Core/MqttNetManager.cs` | MQTTnet-based |
| `Forms/*.cs` | `HASS.Agent.Avalonia/Views/*.axaml` | Avalonia XAML |
| `Settings/*.cs` | `HASS.Agent.Core/SettingsHelper.cs` | JSON-based |
| `Variables.cs` | `HASS.Agent.Core/VariablesCore.cs` | Simplified |
