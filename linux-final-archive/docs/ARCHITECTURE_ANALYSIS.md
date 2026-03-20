# HASS.Agent Cross-Platform Migration - Architecture Analysis

## Executive Summary

The current codebase has been significantly refactored for cross-platform support. The architecture follows a clean separation between:
- **Core** - Platform-agnostic business logic
- **Platform** - Platform-specific implementations (Linux, macOS, Windows)
- **Headless** - Background service layer
- **Avalonia** - Cross-platform GUI

## Architecture Comparison

### Original Windows App (LAB02-Research/HASS.Agent)
```
HASS.Agent/
├── Commands/           # Command execution (Windows-specific)
├── Sensors/            # Sensor reading (Windows APIs)
├── MQTT/              # MQTT communication
├── HomeAssistant/     # HA integration
├── Forms/             # WinForms GUI
├── Settings/          # Configuration
└── Platform/          # Minimal abstraction
```

### Current Cross-Platform Implementation
```
src/
├── HASS.Agent.Core/           # ✅ Platform-agnostic core
│   ├── CommandsManager.cs     # Command orchestration
│   ├── SensorsManager.cs      # Sensor orchestration
│   ├── MqttNetManager.cs      # MQTT communication
│   └── DiscoveryPublisher.cs  # HA auto-discovery
│
├── HASS.Agent.Platform/       # ✅ Platform abstraction layer
│   ├── Abstractions/          # Interfaces
│   ├── Linux/                 # Linux implementations
│   │   ├── Sensors/          # System sensors
│   │   ├── Commands/         # Command execution
│   │   └── Audio/            # Audio control
│   └── PlatformFactory.cs    # Factory for platform services
│
├── HASS.Agent.Headless/       # ✅ HTTP API service
│   └── ASP.NET Core minimal API
│
├── HASS.Agent.SimpleHeadless/ # ✅ Minimal headless variant
│
└── HASS.Agent.Avalonia/       # ✅ Cross-platform GUI
    └── Avalonia 11.x UI
```

## Communication Patterns

### MQTT Discovery (HA Integration)
```
Device → MQTT Broker → Home Assistant Integration
        ↓
homeassistant/sensor/hassagent_{deviceid}/config    [auto-discovery]
homeassistant/button/hassagent_{deviceid}/config    [commands]
hassagent/{deviceid}/availability                    [online/offline]
hassagent/sensor/{sensor_id}/state                   [sensor values]
hassagent/command/{command_id}                       [command triggers]
```

### Local API (Headless)
```
HTTP GET  /health                 → Service health
HTTP GET  /sensors                → All sensor states
HTTP GET  /sensors/{id}           → Specific sensor
HTTP POST /command                → Execute command
HTTP POST /import/legacy          → Import Windows config
```

## Feature Parity Matrix

| Feature | Windows | Linux | macOS | Notes |
|---------|---------|-------|-------|-------|
| **Sensors** |||||
| CPU Usage | ✅ WMI | ✅ /proc | ⚠️ Partial | Need sysctl |
| Memory | ✅ WMI | ✅ /proc/meminfo | ⚠️ Partial | |
| Disk Usage | ✅ WMI | ✅ DriveInfo | ✅ DriveInfo | |
| Network | ✅ WMI | ✅ NetworkInterface | ✅ NetworkInterface | |
| Battery | ✅ WMI | ✅ /sys/class/power_supply | ⚠️ Partial | IOKit |
| Audio | ✅ CoreAudio | ✅ PulseAudio | ⚠️ Partial | CoreAudio |
| Display | ✅ user32 | ✅ xrandr | ⚠️ Partial | NSScreen |
| Bluetooth | ✅ Windows.Devices | ⚠️ bluetoothctl | ⚠️ Partial | IOBluetooth |
| **Commands** |||||
| Custom Script | ✅ cmd/ps | ✅ /bin/sh | ✅ /bin/sh | |
| Key Command | ✅ SendKeys | ✅ xdotool | ❌ Missing | CGEvent |
| Multiple Keys | ✅ SendInput | ⚠️ xdotool | ❌ Missing | CGEvent |
| Media Control | ✅ Windows | ✅ playerctl | ⚠️ Partial | AppleScript |
| Volume Control | ✅ CoreAudio | ✅ pactl | ⚠️ Partial | AppleScript |
| Monitor Sleep | ✅ PowerMgmt | ⚠️ xset | ⚠️ Partial | pmset |
| **GUI** |||||
| System Tray | ✅ WinForms | ✅ Avalonia | ✅ Avalonia | |
| Notifications | ✅ Toast | ✅ libnotify | ✅ NSNotification | |
| Settings UI | ✅ WinForms | ✅ Avalonia | ✅ Avalonia | |

## Gap Analysis - Priority Items

### 1. Keyboard Commands (HIGH PRIORITY)
**Linux:**
- Current: xdotool wrapper exists but not integrated into command execution pipeline
- Need: Integration with CommandsManager and MQTT command handler
- Implementation: Link XdotoolInputSimulator to command execution

**macOS:**
- Current: Missing entirely
- Need: CGEvent-based keyboard simulation or AppleScript fallback
- Implementation: Create CGEventInputSimulator class

### 2. Audio/Volume Control (MEDIUM)
**Linux:**
- Current: PulseAudio support partial
- Need: Complete pactl/pamixer integration

**macOS:**
- Current: Missing
- Need: AppleScript or CoreAudio integration

### 3. Display/Monitor Control (MEDIUM)
**Linux:**
- Current: Basic xrandr support
- Need: DPMS control via xset

**macOS:**
- Current: Missing
- Need: pmset or IOKit integration

### 4. Bluetooth Sensors (LOW)
**Linux:**
- Current: Basic bluetoothctl
- Need: Better device enumeration

**macOS:**
- Current: Missing
- Need: IOBluetooth framework

## Decision: Starting Point

**Decision: Continue refactoring current code**

**Justification:**
1. Core architecture already cross-platform capable
2. 59 tests passing, 0 warnings
3. Platform abstraction layer exists
4. MQTT and HA discovery working
5. Major work remaining is platform-specific implementations, not architecture

**Alternative considered:** Starting fresh from new hass-agent repo
**Rejected because:** Current implementation more complete for Linux, would lose existing work

## Implementation Plan

### Phase 1: Complete Linux (Current Sprint)
1. ✅ Archive original code
2. 🔄 Integrate keyboard commands
3. 🔄 Complete audio/media commands
4. 🔄 Add monitor control
5. 🔄 Full integration testing
6. ☐ Archive Linux build

### Phase 2: macOS Support
1. ☐ Create macOS input simulator (CGEvent)
2. ☐ Implement macOS sensors (IOKit, sysctl)
3. ☐ Add AppleScript command support
4. ☐ Test Avalonia on macOS
5. ☐ Notarization and signing

### Phase 3: Final Polish
1. ☐ Security audit
2. ☐ Performance optimization
3. ☐ Documentation
4. ☐ CI/CD pipelines
