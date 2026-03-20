# HASS.Agent Installation Guide

## Overview

HASS.Agent is a cross-platform Home Assistant companion application that provides:
- **System sensors**: CPU, memory, disk, network, battery, temperature monitoring
- **Custom commands**: Execute shell commands from Home Assistant
- **Media control**: Control media playback on your computer
- **Notifications**: Receive Home Assistant notifications on your desktop
- **MQTT integration**: Real-time communication with Home Assistant

## Platform Support

| Platform | GUI | Headless | Package |
|----------|-----|----------|---------|
| Linux (Debian/Ubuntu) | ✅ | ✅ | .deb |
| Linux (Other) | ✅ | ✅ | .tar.gz |
| macOS | ✅ | ✅ | .dmg / .app |
| Windows | ✅ | ✅ | .msi / .exe |

---

## Linux Installation

### Option 1: Debian/Ubuntu (.deb package)

```bash
# Download the latest release
wget https://github.com/hass-agent/hass-agent/releases/latest/download/hass-agent_1.0.0_amd64.deb

# Install
sudo dpkg -i hass-agent_1.0.0_amd64.deb

# Fix any missing dependencies
sudo apt-get install -f
```

### Option 2: Quick Install Script

```bash
# One-line installation
curl -fsSL https://raw.githubusercontent.com/hass-agent/hass-agent/main/scripts/install-linux.sh | bash
```

### Option 3: Manual Installation

```bash
# Download and extract
wget https://github.com/hass-agent/hass-agent/releases/latest/download/hass-agent-linux-x64.tar.gz
tar -xzf hass-agent-linux-x64.tar.gz

# Move to installation directory
sudo mkdir -p /opt/hass-agent
sudo mv hass-agent/* /opt/hass-agent/
sudo chmod +x /opt/hass-agent/hass-agent

# Create symlink
sudo ln -sf /opt/hass-agent/hass-agent /usr/local/bin/hass-agent
```

### Running as a Service (systemd)

```bash
# Create service file
sudo tee /etc/systemd/system/hass-agent.service << 'EOF'
[Unit]
Description=HASS.Agent for Home Assistant
After=network.target

[Service]
Type=simple
User=YOUR_USERNAME
ExecStart=/opt/hass-agent/hass-agent-headless
Restart=always
RestartSec=10
Environment=DISPLAY=:0

[Install]
WantedBy=multi-user.target
EOF

# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable hass-agent
sudo systemctl start hass-agent

# Check status
sudo systemctl status hass-agent
```

---

## macOS Installation

### Option 1: DMG Installer (Recommended)

1. Download `HASS.Agent-1.0.0.dmg` from the releases page
2. Double-click to mount the DMG
3. Drag HASS.Agent to the Applications folder
4. Launch from Applications or Spotlight

### Option 2: Quick Install Script

```bash
# One-line installation
curl -fsSL https://raw.githubusercontent.com/hass-agent/hass-agent/main/scripts/install-macos.sh | bash
```

### Option 3: Homebrew (Coming Soon)

```bash
brew install --cask hass-agent
```

### Running at Login

The app can automatically start at login. Enable this in:
**Preferences → General → Start at Login**

Or manually add a launch agent:

```bash
# Create launch agent
mkdir -p ~/Library/LaunchAgents
cat > ~/Library/LaunchAgents/com.hass-agent.plist << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.hass-agent</string>
    <key>ProgramArguments</key>
    <array>
        <string>/Applications/HASS.Agent.app/Contents/MacOS/HASS.Agent</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
EOF

# Load the agent
launchctl load ~/Library/LaunchAgents/com.hass-agent.plist
```

---

## Windows Installation

### Option 1: MSI Installer (Recommended)

1. Download `HASS.Agent-1.0.0.msi` from the releases page
2. Double-click to run the installer
3. Follow the installation wizard

### Option 2: Portable Version

1. Download `hass-agent-windows-x64.zip`
2. Extract to desired location
3. Run `HASS.Agent.exe`

---

## Initial Configuration

### 1. Home Assistant Connection

When you first run HASS.Agent, you'll need to configure the connection to Home Assistant:

1. Open HASS.Agent
2. Go to **Settings** → **Home Assistant**
3. Enter your Home Assistant URL (e.g., `http://homeassistant.local:8123`)
4. Generate a Long-Lived Access Token in Home Assistant:
   - Profile → Long-Lived Access Tokens → Create Token
5. Paste the token in HASS.Agent

### 2. MQTT Configuration (Recommended)

For real-time updates, configure MQTT:

1. Enable MQTT in Home Assistant (Settings → Add-ons → Mosquitto broker)
2. In HASS.Agent, go to **Settings** → **MQTT**
3. Enter:
   - **Server**: Your MQTT broker address
   - **Port**: 1883 (or 8883 for TLS)
   - **Username/Password**: Your MQTT credentials
4. Click **Test Connection** to verify

### 3. Device Registration

After connecting, HASS.Agent will automatically:
- Register your device in Home Assistant
- Create entities for all configured sensors
- Set up commands as buttons/switches

---

## Configuration Files

### Linux/macOS
```
~/.config/hass-agent/
├── config.json          # Main configuration
├── sensors.json         # Sensor definitions
├── commands.json        # Command definitions
└── logs/
    └── hass-agent.log   # Application logs
```

### Windows
```
%APPDATA%\HASS.Agent\
├── config.json
├── sensors.json
├── commands.json
└── logs\
    └── hass-agent.log
```

---

## Troubleshooting

### Check Logs

**Linux/macOS:**
```bash
tail -f ~/.config/hass-agent/logs/hass-agent.log
```

**Windows (PowerShell):**
```powershell
Get-Content "$env:APPDATA\HASS.Agent\logs\hass-agent.log" -Wait
```

### Common Issues

#### "Connection refused" to MQTT
- Verify MQTT broker is running
- Check firewall allows port 1883/8883
- Verify credentials are correct

#### Sensors not appearing in Home Assistant
- Check MQTT discovery is enabled in HA
- Verify device name doesn't contain special characters
- Restart Home Assistant after initial connection

#### High CPU usage
- Reduce sensor update interval
- Disable unused sensors
- Check for infinite loops in custom commands

### Service Management

**Linux (systemd):**
```bash
sudo systemctl restart hass-agent
sudo journalctl -u hass-agent -f
```

**macOS:**
```bash
launchctl stop com.hass-agent
launchctl start com.hass-agent
```

---

## Uninstallation

### Linux (Debian/Ubuntu)
```bash
sudo apt remove hass-agent
sudo rm -rf ~/.config/hass-agent
```

### Linux (Manual)
```bash
sudo rm -rf /opt/hass-agent
sudo rm /usr/local/bin/hass-agent
rm -rf ~/.config/hass-agent
```

### macOS
```bash
# Stop the service
launchctl unload ~/Library/LaunchAgents/com.hass-agent.plist
rm ~/Library/LaunchAgents/com.hass-agent.plist

# Remove application
rm -rf /Applications/HASS.Agent.app
rm -rf ~/.config/hass-agent
```

### Windows
Use "Add or Remove Programs" or run the uninstaller from the installation directory.

---

## Getting Help

- **Documentation**: https://github.com/hass-agent/hass-agent/docs
- **Issues**: https://github.com/hass-agent/hass-agent/issues
- **Discussions**: https://github.com/hass-agent/hass-agent/discussions
- **Discord**: https://discord.gg/hass-agent
