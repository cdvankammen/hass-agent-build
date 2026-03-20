# HASS.Agent Archive - Pre-Linux/macOS Migration

**Archive Date:** December 26, 2025  
**Purpose:** Preserve the working Windows/cross-platform code before major Linux/macOS adaptation work.

## Contents

This archive contains the complete source code at the point before Linux/macOS keyboard commands, platform-specific sensors, and advanced cross-platform adaptations were implemented.

### Projects Included

| Project | Description |
|---------|-------------|
| `HASS.Agent/` | Original Windows WinForms application (reference) |
| `HASS.Agent.Core/` | Cross-platform core library with sensors, commands, MQTT |
| `HASS.Agent.Platform/` | Platform abstraction layer |
| `HASS.Agent.Headless/` | ASP.NET Core headless service |
| `HASS.Agent.SimpleHeadless/` | Minimal headless implementation |
| `HASS.Agent.Avalonia/` | Cross-platform GUI using Avalonia |
| `tests/` | Unit and integration tests |

## Working Features at Archive Time

- ✅ Core sensor/command infrastructure
- ✅ MQTT communication with Home Assistant
- ✅ Basic platform abstraction (Linux/macOS/Windows)
- ✅ Headless HTTP API service
- ✅ Avalonia GUI framework (basic)
- ✅ systemd/launchd service integration
- ✅ Debian/macOS packaging scripts
- ⚠️ Keyboard commands (Windows only - not yet ported)
- ⚠️ Full platform-specific sensors (partial)

## How to Restore

1. Copy this entire directory to a new location
2. Copy the `src/` structure from here to replace the main project
3. Run `dotnet restore` and `dotnet build`

```bash
# Example restoration
cp -R old-hass-agent-archive/HASS.Agent.* /path/to/new/project/src/
cp -R old-hass-agent-archive/tests /path/to/new/project/
cd /path/to/new/project/src
dotnet restore HASS.Agent.Linux.sln
dotnet build -c Release
```

## Build Commands

```bash
# Build all Linux projects
dotnet build HASS.Agent.Linux.sln -c Release

# Build Avalonia GUI
dotnet build HASS.Agent.Avalonia/HASS.Agent.Avalonia.csproj -c Release

# Run tests
dotnet test ../tests/HASS.Agent.Core.Tests -c Release
dotnet test ../tests/HASS.Agent.Platform.Tests -c Release
```

## Important Notes

- This archive represents a **stable, buildable state**
- All 59 tests pass (43 Core + 16 Platform)
- 0 warnings, 0 errors in Release build
- Use this as fallback if Linux/macOS adaptation causes issues
