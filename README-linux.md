HASS.Agent - Linux headless build and install

Prerequisites
- .NET 10 runtime (or SDK) installed
- Optional: dpkg-deb to build a .deb package
- Optional: xdotool, libnotify-bin, dbus-user-session for certain features


Build and install (quick)

1. Build the Linux-focused solution (from repo root):

```bash
cd src
dotnet build HASS.Agent.Linux.sln -c Release
cd ..
```

2. Build the package (.deb) locally:

```bash
chmod +x deploy/build_deb.sh
./deploy/build_deb.sh
# output will be in dist/ (hass-agent_0.1.0_amd64.deb or tarball)
```

Alternative (one-script build/test/package):

```bash
chmod +x scripts/ci/linux_build_and_package.sh
./scripts/ci/linux_build_and_package.sh
```

3. Install (Debian/Ubuntu):

```bash
sudo dpkg -i dist/hass-agent_0.1.0_amd64.deb
sudo systemctl daemon-reload
sudo systemctl enable --now hass-agent

# Check status:
sudo systemctl status hass-agent
```

4. Run integration test (local):

```bash
chmod +x scripts/integration/complete_import_test.sh
./scripts/integration/complete_import_test.sh
```

Troubleshooting
- If `dpkg -i` fails due to missing dependencies, run `sudo apt-get install -f` to fix.
- If the service fails to start, check journal logs:

```bash
sudo journalctl -u hass-agent -b --no-pager | tail -n 200
```

- If you built a tarball rather than a .deb, extract and install manually:

```bash
tar -xzf dist/hass-agent_0.1.0_amd64.tar.gz -C /tmp
sudo cp -r /tmp/hass-agent_0.1.0_amd64/opt/hass-agent /opt/
sudo cp /tmp/hass-agent_0.1.0_amd64/etc/systemd/system/hass-agent.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now hass-agent
```

HASS.Agent Linux Headless

This document describes how to build and run the headless HASS.Agent on Linux (minimal features): MQTT integration, Local API, and command execution (keyboard simulation via xdotool).

Requirements:
- .NET 6 SDK
- xdotool (for keyboard/window control)
- notify-send (libnotify) for notifications (optional)

Build & publish (linux-x64):

```bash
# from repo root
cd src/HASS.Agent
# create a linux console publish (use --self-contained if you want standalone)
dotnet publish -c Release -r linux-x64 -o ./publish
```

Run headless:

```bash
# set RPC address if needed
export HASS_AGENT_RPC_ADDR=http://127.0.0.1:50051
# run
./src/HASS.Agent/publish/HASS.Agent
```

Systemd unit example (place in /etc/systemd/system/hass-agent.service):

```ini
[Unit]
Description=HASS.Agent Headless
After=network.target

[Service]
Type=simple
User=youruser
WorkingDirectory=/opt/hass-agent
ExecStart=/opt/hass-agent/HASS.Agent
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

Note: This is an incremental port. GUI and many Windows-only features are not included.
