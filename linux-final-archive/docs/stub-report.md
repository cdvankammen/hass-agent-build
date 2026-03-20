# Stub & TODO Report

Date: 2025-12-25

This report lists implemented features, placeholders, TODOs, and remaining work after the headless & platform-focused porting pass.

Summary of what was implemented in this session
- Headless web UI and REST endpoints (`src/HASS.Agent.Headless/Program.cs`, `wwwroot/*`).
- Robust settings apply marker writing to temp paths and `/tmp` for CI detection.
- Server-side validation endpoint (`/settings/validate`) with DNS checks and binary detection.
- MQTT manager resilience and retained publish support (`src/HASS.Agent.Core/MqttNetManager.cs`, `IMqttManager` additions).
- Discovery publish/clear (`src/HASS.Agent.Core/DiscoveryPublisher.cs`).
- Platform adapters for Linux: Notification (`notify-send`), Media (MPRIS via DBus and `playerctl` fallback), Bluetooth (BlueZ DBus + `bluetoothctl` fallback).
- RPC server/client for Unix domain socket (`src/HASS.Agent.Headless/RpcServer.cs`, `src/HASS.Agent.Platform/Linux/PlatformRpcClient.cs`).
- Settings helper for reading flags from `appsettings.json` without GUI types.
- Several small improvements and bug fixes (regex parsing, media request parsing).

High-level areas still not implemented or intentionally left as Windows-only
- Windows GUI (`src/HASS.Agent`) — uses WinForms, Syncfusion, WebView2, UWP/WinRT APIs (notifications, GSMTC media, Windows Bluetooth/Toast). These are intentionally not ported here and remain Windows-only.
- Windows-specific sensors and features that depend on System.Drawing/Printing/Windows APIs (printer sensors, certain hardware sensors).

Files containing TODO comments or placeholders (partial list)
- `src/HASS.Agent/Functions/HelperFunctions.cs` — cosmetic TODO about messagebox styles.
- `src/HASS.Agent/Media/MediaManager.cs` — todo: OS check and Windows GSMTC handling; Linux adapter implemented separately under `src/HASS.Agent.Platform/Linux`.
- `src/HASS.Agent/Extensions/MediaRouteExtensions.cs` — previously used only first query key; improved.
- `src/HASS.Agent/HomeAssistant/HassApiManager.cs` — TODO about data checks; improved settings check.

Windows-only or GUI-heavy files (not ported)
- `src/HASS.Agent/**` (many files) — references to System.Windows.Forms, WebView2, Syncfusion, Windows.* namespaces. Building this project on non-Windows will produce errors — expected.

Platform adapters and stubs introduced (Linux-target)
- `src/HASS.Agent.Platform/Linux/Notification/NotificationManager.cs` — uses `notify-send`.
- `src/HASS.Agent.Platform/Linux/Media/*` — `MprisManager`, `MediaManager` using `Tmds.DBus` and `playerctl`.
- `src/HASS.Agent.Platform/Linux/Bluetooth/*` — `BluezManager`, `BluetoothManager` using `Tmds.DBus` and `bluetoothctl`.
- `src/HASS.Agent.Platform/Linux/PlatformRpcClient.cs` — UDS/TCP RPC client.

Stubs and places to prioritize next
1. Sensors needing Linux implementations (disk, network, CPU, memory, temperature where available).
2. Replace Windows-only notification/GUI flows with web UI interactions or cross-platform desktop wrapper (Avalonia/Electron) if native GUI is desired.
3. Add unit tests for headless endpoints and CI integration steps.

How to read this report
- Use this file as a roadmap for porting the remaining features.
- The headless project is functional and intended to be the primary Linux runtime. The GUI project can remain Windows-only or be reimplemented as a cross-platform wrapper.

Next recommended actions
1. Create prioritized plan (I will generate this next) and pick 2 sensors to implement: disk usage sensor and network interface sensor.
2. Add xUnit tests for the headless endpoints.
3. Create packaging guidance and a systemd unit (already added as template in `scripts/`).

If you want me to start implementing the disk and network sensors now, I'll proceed and wire them into `src/HASS.Agent.Core` so they publish via MQTT and appear in `/sensors` output.
