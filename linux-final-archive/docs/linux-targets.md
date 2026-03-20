# Linux / Debian Target Environment for HASS.Agent

Date: 2025-12-25

Target:
- Debian 12 (Bookworm) and Debian 11 (Bullseye) compatible
- Ubuntu LTS equivalents (optional)

Runtime requirements
- .NET 10 runtime and SDK (or .NET 6 where appropriate). We used `net10.0` in headless projects; ensure `dotnet` 10 is installed.
- System packages: `libnotify-bin`, `dbus`, `dbus-user-session`, `pulseaudio` or `pipewire` (for audio/MPRIS), `bluez`, `cups`, `mosquitto` (for test broker)

Install commands (Debian):

```bash
# install dotnet 10 (Microsoft package feed recommended)
# follow Microsoft docs: https://learn.microsoft.com/dotnet/core/install/linux-debian

# install system packages
sudo apt update
sudo apt install -y libnotify-bin dbus dbus-user-session bluez cups mosquitto curl

# audio backends may be pipewire or pulseaudio depending on system
sudo apt install -y pipewire pipewire-audio-client-libraries

# optional: install build essentials for native tools
sudo apt install -y build-essential pkg-config
```

Development environment
- Install `dotnet` SDK 10
- For debugging GUI wrapper (Avalonia), install GTK dependencies (libgtk-3-dev)

Recommended dev workflow
- Work with `src/HASS.Agent.Headless` for core feature development and testing on Linux
- Build with: `dotnet build src/HASS.Agent.Headless -c Release`
- Run with: `dotnet run --project src/HASS.Agent.Headless -c Release`

Config paths & conventions
- System-wide config: `/etc/hass-agent/` (appsettings.json, mqtt credentials)
- Per-user config: `$HOME/.config/hass-agent/`
- Runtime state: `/var/run/hass-agent/` (RPC sockets), logs `/var/log/hass-agent/`

Systemd unit (suggested)
- Unit should run as `hassagent` user (created by packaging scripts) and drop privileges
- Example unit path: `/etc/systemd/system/hass-agent.service`

Logging
- Use Serilog file sink to log to `/var/log/hass-agent/hass-agent.log`
- Add systemd journald sink for when run as service

Testing with Mosquitto
- Start broker: `mosquitto -v` or `sudo systemctl start mosquitto`
- Use `mosquitto_sub` to monitor discovery topics: `mosquitto_sub -v -t 'homeassistant/#'`

Packaging notes
- Create a .deb with `fpm` or `dpkg-deb`; ensure `dotnet` runtime dependency noted.

Next: I'll harden `src/HASS.Agent.Headless` for Debian: ensure config fallback paths, make RPC socket default `/var/run/hass-agent.sock`, ensure logging to `/var/log/hass-agent`, and run smoke tests against Mosquitto. I'll proceed to update the headless defaults and add a systemd unit template.
