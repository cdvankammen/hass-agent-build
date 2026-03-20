# HASS.Agent Headless UI Guide

This document explains how to run the headless UI, edit settings, and use the form.

Prerequisites
- .NET 10 SDK or runtime

Run headless UI

```bash
export HASS_AGENT_CONFIG_PATH=/path/to/config
export HASS_AGENT_AUTO_START_PLATFORM=true
dotnet run --project src/HASS.Agent.Headless/HASS.Agent.Headless.csproj -c Release
```

Open the UI at: http://127.0.0.1:11111/

Features
- Settings editor: edit JSON config files directly.
- Settings form: a generated form for common `appsettings` keys. Open `/settings-form.html`.
- Auto-save: default ON (controlled by checkbox), optional auto-apply.
- Platform status: shows DBus availability and fallback tools.

Environment variables
- `HASS_AGENT_APPLY_TO_GUI` (true/false): when true (default), headless will attempt to update GUI in-memory `AppSettings` via reflection and call `SettingsManager.StoreAppSettings()` if present. Set to `false` to only write files.
- `HASS_AGENT_AUTO_START_PLATFORM` (true/false): whether to auto-start platform adapters on startup.
 - `HASS_AGENT_LOG_PATH` (optional): set a full path for the headless log file (defaults to `/var/log/hass-agent/hass-headless.log` on Linux or `./hass-headless.log` in the working dir).

Testing
- `scripts/test-gui.sh` runs a simple integration test that saves a sample `appsettings.json` and calls `/settings/apply`.

Notes
- Form and schema are generated from a static schema; if you need nested or complex types, we can expand the schema extraction to inspect actual `AppSettings` types at runtime.

Validation rules (server-side)
- `DeviceName` is required.
- `LocalApiPort` must be between 1 and 65535.
- `HassUri` must be a valid URL.
- When `MqttEnabled` is true, `MqttAddress` is required and `MqttPort` must be a valid port.
- Certificate file paths (`HassClientCertificate`, `MqttRootCertificate`, `MqttClientCertificate`) must exist on disk if provided.
- Binaries provided in `BrowserBinary` and `CustomExecutorBinary` must exist on disk or be found in `PATH`.
