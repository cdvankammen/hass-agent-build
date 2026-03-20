# HASS.Agent Cross-Platform Migration - Complete TODO List

## Migration Status Overview

| Phase | Status | Completion |
|-------|--------|------------|
| Archive Original Code | ✅ Done | 100% |
| Architecture Analysis | ✅ Done | 100% |
| Linux Adaptation | 🔄 In Progress | 85% |
| macOS Adaptation | 🔄 In Progress | 60% |
| Final QA | ⏳ Pending | 0% |

---

## 1. Architecture (✅ Complete)

- [x] Create platform abstraction layer
- [x] Define `IInputSimulator` interface
- [x] Define `INotifier` interface
- [x] Define `ISensor` interface
- [x] Create `PlatformFactory` for service location
- [x] Separate Core from Platform-specific code
- [x] Design CommandModel for cross-platform execution
- [x] Design SensorModel for cross-platform sensors
- [x] MQTT communication layer (MQTTnet)
- [x] HTTP API layer (ASP.NET Core Minimal)

---

## 2. Linux Platform (🔄 85% Complete)

### 2.1 Input Simulation (✅ Done)
- [x] `LinuxInputSimulator` using xdotool
- [x] Support for ydotool (Wayland)
- [x] SendKeys syntax parser
- [x] Key combination support (Ctrl+Alt+T)
- [x] Text typing support
- [x] Multiple keys support
- [x] Mouse move/click support
- [ ] Global hotkey registration (requires X11/DBus)

### 2.2 Commands (✅ Done)
- [x] Custom command execution
- [x] Shell/Bash command support
- [x] KeyCommand type
- [x] MultipleKeysCommand type
- [x] LaunchUrl command
- [x] SetVolume command (pactl)
- [x] Monitor sleep/wake (xset dpms)
- [x] Lock screen (loginctl/xdg-screensaver)
- [x] System suspend/hibernate/shutdown/restart

### 2.3 Sensors (⚠️ Partial)
- [x] CPU usage (from /proc/stat)
- [x] Memory usage (from /proc/meminfo)
- [x] Disk usage (DriveInfo)
- [x] Network interfaces
- [x] Battery status (/sys/class/power_supply)
- [x] Display info (xrandr)
- [x] Current user
- [x] System uptime
- [ ] GPU sensors (nvidia-smi, amd-smi)
- [ ] USB devices
- [ ] Webcam detection
- [ ] Microphone activity

### 2.4 Audio (✅ Done)
- [x] Volume control (pactl)
- [x] Mute/unmute
- [ ] Audio input devices list
- [ ] Audio output devices list
- [ ] Active audio source detection

### 2.5 Bluetooth (⚠️ Partial)
- [x] Device discovery (bluetoothctl)
- [ ] Connection status
- [ ] Paired devices list
- [ ] Connect/disconnect commands

### 2.6 Notifications (✅ Done)
- [x] libnotify integration
- [x] DBus notification support

### 2.7 GUI (✅ Done)
- [x] Avalonia 11.x integration
- [x] Main window with navigation
- [x] Sensors view
- [x] Commands view  
- [x] Settings view
- [ ] System tray icon
- [ ] Quick actions panel

### 2.8 Service (✅ Done)
- [x] systemd service file
- [x] Auto-restart configuration
- [x] Logging to /var/log/hass-agent
- [x] Config file support

### 2.9 Packaging (✅ Done)
- [x] Debian .deb package scripts
- [x] Build scripts for multiple architectures

---

## 3. macOS Platform (🔄 60% Complete)

### 3.1 Input Simulation (✅ Done)
- [x] `MacOSInputSimulator` using AppleScript
- [x] Key codes mapping
- [x] Key combination support
- [x] Text typing support
- [ ] CGEvent native implementation (for non-script use)
- [ ] Accessibility permission checks

### 3.2 Commands (✅ Done)
- [x] Shell/Bash command support
- [x] KeyCommand type
- [x] LaunchUrl command (open)
- [x] SetVolume command (osascript)
- [x] Monitor sleep (pmset)
- [x] Lock screen
- [x] System shutdown/restart (osascript)
- [ ] iTunes/Music control
- [ ] Finder control

### 3.3 Sensors (⚠️ Partial)
- [x] Disk usage (DriveInfo)
- [x] Network interfaces
- [ ] CPU usage (sysctl/host_statistics)
- [ ] Memory usage (mach vm_statistics)
- [ ] Battery status (IOPowerSources)
- [ ] Display info (CGDisplay)
- [ ] System uptime (sysctl)
- [ ] Active applications
- [ ] Frontmost window
- [ ] Screen brightness

### 3.4 Audio (⚠️ Partial)
- [x] Volume control (osascript)
- [ ] CoreAudio integration
- [ ] Input/output device control

### 3.5 Bluetooth (❌ Not Started)
- [ ] IOBluetooth framework
- [ ] Device enumeration
- [ ] Connection status

### 3.6 Notifications (⚠️ Partial)
- [ ] NSUserNotificationCenter
- [ ] UNUserNotificationCenter (modern)

### 3.7 GUI (✅ Done)
- [x] Avalonia on macOS
- [ ] Menu bar app integration
- [ ] Native macOS look and feel

### 3.8 Service (⚠️ Partial)
- [x] launchd service file
- [ ] Login item configuration
- [ ] Sandbox configuration

### 3.9 Packaging (⚠️ Partial)
- [x] .app bundle creation script
- [x] DMG creation script
- [ ] Code signing automation
- [ ] Notarization automation

---

## 4. Security (⚠️ Partial)

- [x] Command sanitization
- [x] Input validation
- [ ] API authentication (token-based)
- [ ] TLS for API connections
- [ ] Secure MQTT connections
- [ ] Config file encryption
- [ ] Secrets management

---

## 5. Testing (🔄 In Progress)

### 5.1 Unit Tests
- [x] Core library tests (43 passing)
- [x] Platform tests (24 passing + 7 skipped)
- [ ] Command executor tests
- [ ] API endpoint tests

### 5.2 Integration Tests
- [ ] MQTT connection tests
- [ ] Home Assistant discovery tests
- [ ] Command execution end-to-end
- [ ] Sensor publishing end-to-end

### 5.3 Platform Tests
- [ ] Linux VM testing
- [ ] macOS hardware testing
- [ ] Cross-platform consistency

---

## 6. Documentation (⚠️ Partial)

- [x] Architecture analysis document
- [x] Archive README
- [ ] User installation guide
- [ ] Configuration reference
- [ ] API documentation
- [ ] Developer guide
- [ ] Troubleshooting guide

---

## 7. CI/CD (❌ Not Started)

- [ ] GitHub Actions workflow
- [ ] Automated builds (Linux, macOS)
- [ ] Automated tests
- [ ] Release automation
- [ ] Package publishing

---

## Priority Items for Next Sprint

1. **macOS sensors** - Implement CPU, memory, battery via sysctl/IOKit
2. **API authentication** - Add token-based security to HTTP API
3. **Integration tests** - End-to-end MQTT tests with Home Assistant
4. **Linux archive** - Create `linux-final-archive` before macOS deep work
5. **Documentation** - User installation and configuration guides

---

## Technical Debt

- [ ] Reduce code duplication between Linux/macOS input simulators
- [ ] Add cancellation token support to all async operations
- [ ] Improve error messages and user-facing diagnostics
- [ ] Add telemetry/metrics for debugging
- [ ] Review and fix all nullable warnings
