# HASS.Agent Configuration Guide

## Table of Contents
1. [Configuration Overview](#configuration-overview)
2. [Home Assistant Integration](#home-assistant-integration)
3. [MQTT Configuration](#mqtt-configuration)
4. [Sensors](#sensors)
5. [Commands](#commands)
6. [Notifications](#notifications)
7. [Media Integration](#media-integration)
8. [Advanced Configuration](#advanced-configuration)

---

## Configuration Overview

HASS.Agent uses JSON configuration files stored in your config directory:

| File | Purpose |
|------|---------|
| `config.json` | Main application settings |
| `sensors.json` | Sensor definitions |
| `commands.json` | Command definitions |
| `media.json` | Media player settings |

### Config Directory Locations

- **Linux/macOS**: `~/.config/hass-agent/`
- **Windows**: `%APPDATA%\HASS.Agent\`

---

## Home Assistant Integration

### Connection Settings

```json
{
  "homeAssistant": {
    "url": "http://homeassistant.local:8123",
    "token": "YOUR_LONG_LIVED_ACCESS_TOKEN",
    "verifySsl": true
  }
}
```

### Generating an Access Token

1. Open Home Assistant
2. Click your profile (bottom left)
3. Scroll to "Long-Lived Access Tokens"
4. Click "Create Token"
5. Name it "HASS.Agent"
6. Copy the token (you can't see it again!)

### Device Registration

When connected, HASS.Agent creates a device in Home Assistant:

```yaml
device:
  identifiers: ["hass-agent-YOUR_DEVICE_ID"]
  name: "Your Computer Name"
  manufacturer: "HASS.Agent"
  model: "Desktop Agent"
  sw_version: "1.0.0"
```

---

## MQTT Configuration

MQTT provides real-time, bidirectional communication with Home Assistant.

### Basic Configuration

```json
{
  "mqtt": {
    "enabled": true,
    "server": "homeassistant.local",
    "port": 1883,
    "useTls": false,
    "username": "mqtt_user",
    "password": "mqtt_password",
    "discoveryPrefix": "homeassistant",
    "clientId": "hass-agent-{deviceId}"
  }
}
```

### TLS/SSL Configuration

```json
{
  "mqtt": {
    "enabled": true,
    "server": "homeassistant.local",
    "port": 8883,
    "useTls": true,
    "allowUntrustedCertificates": false,
    "clientCertificate": "/path/to/client.crt",
    "clientKey": "/path/to/client.key"
  }
}
```

### Topic Structure

HASS.Agent uses the following MQTT topic structure:

```
homeassistant/sensor/hass-agent-{deviceId}/{sensorId}/state
homeassistant/sensor/hass-agent-{deviceId}/{sensorId}/config
homeassistant/button/hass-agent-{deviceId}/{commandId}/state
homeassistant/button/hass-agent-{deviceId}/{commandId}/config
```

---

## Sensors

### Built-in Sensors

| Sensor | Description | Update Interval |
|--------|-------------|-----------------|
| `cpu_usage` | CPU utilization percentage | 10s |
| `memory_usage` | RAM usage percentage | 30s |
| `disk_usage` | Disk space per volume | 60s |
| `network_interfaces` | Network adapter status | 30s |
| `battery` | Battery level and charging status | 60s |
| `temperature` | CPU/GPU temperature | 30s |
| `active_window` | Currently focused window | 5s |
| `user_sessions` | Logged-in users | 60s |
| `display_state` | Screen on/off status | 10s |
| `bluetooth_devices` | Connected Bluetooth devices | 30s |

### Sensor Configuration

```json
{
  "sensors": [
    {
      "id": "cpu_usage",
      "name": "CPU Usage",
      "type": "system_resource",
      "enabled": true,
      "updateInterval": 10,
      "category": "diagnostic"
    },
    {
      "id": "custom_wmi",
      "name": "Custom WMI Sensor",
      "type": "wmi_query",
      "enabled": true,
      "updateInterval": 30,
      "query": "SELECT LoadPercentage FROM Win32_Processor",
      "scope": "\\\\localhost\\root\\cimv2"
    }
  ]
}
```

### Custom Sensors

#### Shell Command Sensor
```json
{
  "id": "custom_script",
  "name": "Custom Script Output",
  "type": "shell_command",
  "command": "/path/to/script.sh",
  "updateInterval": 60,
  "parseJson": true
}
```

#### File Content Sensor
```json
{
  "id": "file_sensor",
  "name": "File Content",
  "type": "file_content",
  "path": "/tmp/sensor_value.txt",
  "updateInterval": 30
}
```

---

## Commands

### Command Types

| Type | Description | Platform |
|------|-------------|----------|
| `shell` | Execute shell command | All |
| `key_press` | Simulate keyboard input | All |
| `url` | Open URL in browser | All |
| `application` | Launch application | All |
| `power` | System power control | All |
| `custom` | Custom script execution | All |

### Command Configuration

```json
{
  "commands": [
    {
      "id": "lock_screen",
      "name": "Lock Screen",
      "type": "power",
      "action": "lock",
      "enabled": true
    },
    {
      "id": "open_spotify",
      "name": "Open Spotify",
      "type": "application",
      "path": "/usr/bin/spotify",
      "arguments": "",
      "runAsAdmin": false
    },
    {
      "id": "volume_mute",
      "name": "Mute Volume",
      "type": "key_press",
      "key": "VOLUME_MUTE"
    },
    {
      "id": "custom_script",
      "name": "Run Backup",
      "type": "shell",
      "command": "/home/user/scripts/backup.sh",
      "workingDirectory": "/home/user",
      "timeout": 300,
      "showOutput": true
    }
  ]
}
```

### Power Commands

| Action | Description |
|--------|-------------|
| `shutdown` | Shutdown computer |
| `restart` | Restart computer |
| `sleep` | Enter sleep mode |
| `hibernate` | Enter hibernation |
| `lock` | Lock screen |
| `logout` | Log out current user |

### Quick Actions

Configure up to 10 quick action buttons in the system tray:

```json
{
  "quickActions": [
    { "commandId": "lock_screen", "icon": "lock" },
    { "commandId": "volume_mute", "icon": "volume-mute" },
    { "commandId": "open_spotify", "icon": "music" }
  ]
}
```

---

## Notifications

### Notification Settings

```json
{
  "notifications": {
    "enabled": true,
    "defaultDuration": 5000,
    "position": "top-right",
    "maxVisible": 3,
    "sounds": {
      "enabled": true,
      "volume": 0.8
    },
    "images": {
      "enabled": true,
      "maxWidth": 400,
      "maxHeight": 300
    }
  }
}
```

### Home Assistant Notification Service

After setup, you can send notifications from Home Assistant:

```yaml
service: notify.hass_agent_YOUR_DEVICE
data:
  title: "Hello!"
  message: "This is a notification from Home Assistant"
  data:
    image: "/local/images/notification.png"
    duration: 10000
    actions:
      - action: "open_url"
        title: "Open Website"
        uri: "https://home-assistant.io"
```

---

## Media Integration

### Media Player Settings

```json
{
  "media": {
    "enabled": true,
    "publishState": true,
    "updateInterval": 2,
    "sources": [
      { "name": "Spotify", "enabled": true },
      { "name": "VLC", "enabled": true },
      { "name": "Chrome", "enabled": true }
    ]
  }
}
```

### Home Assistant Media Player

HASS.Agent exposes a media player entity in Home Assistant:

```yaml
media_player.hass_agent_YOUR_DEVICE
```

You can control playback:
- Play/Pause
- Next/Previous track
- Volume up/down/mute
- Seek position

---

## Advanced Configuration

### Logging

```json
{
  "logging": {
    "level": "Information",
    "maxFiles": 7,
    "maxFileSize": "10MB",
    "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
  }
}
```

Log levels:
- `Verbose` - Everything (debugging)
- `Debug` - Detailed info
- `Information` - Normal operation
- `Warning` - Potential issues
- `Error` - Errors only
- `Fatal` - Critical errors only

### API Server

HASS.Agent runs a local REST API:

```json
{
  "LocalApiEnabled": true,
  "LocalApiPort": 11111,
  "BindHost": "0.0.0.0",
  "CorsAllowedOrigins": "http://localhost:5173, http://192.168.1.50:3000"
}
```

API Endpoints:
- `GET /health` - Health check
- `GET /sensors` - All sensor states
- `GET /sensors/{id}` - Single sensor
- `GET /network/status` - API bind host and LAN interface status
- `GET /network/interfaces` - Raw LAN interface inventory
- `POST /commands/{id}` - Execute command
- `POST /notification` - Show notification

By default, the headless API listens on `0.0.0.0` so other devices on the LAN can reach it directly. Set `BindHost` to `127.0.0.1` if you want localhost-only access, and use `CorsAllowedOrigins` to allow a separate management GUI to call the API from a browser.

### Performance Tuning

```json
{
  "performance": {
    "sensorThreadPoolSize": 4,
    "mqttReconnectInterval": 30,
    "maxSensorHistory": 100,
    "batchPublishInterval": 1000
  }
}
```

### Proxy Configuration

```json
{
  "proxy": {
    "enabled": true,
    "server": "http://proxy.company.com:8080",
    "username": "user",
    "password": "pass",
    "bypassLocal": true
  }
}
```

---

## Environment Variables

You can override configuration with environment variables:

| Variable | Description |
|----------|-------------|
| `HASS_AGENT_CONFIG_DIR` | Config directory path |
| `HASS_AGENT_LOG_LEVEL` | Override log level |
| `HASS_AGENT_BIND_HOST` | Override the API bind host |
| `HASS_AGENT_CORS_ORIGINS` | Comma-separated allowed CORS origins, or `*` |
| `HASS_AGENT_MQTT_SERVER` | MQTT server address |
| `HASS_AGENT_MQTT_PORT` | MQTT port |
| `HASS_AGENT_MQTT_USER` | MQTT username |
| `HASS_AGENT_MQTT_PASS` | MQTT password |
| `HASS_AGENT_HA_URL` | Home Assistant URL |
| `HASS_AGENT_HA_TOKEN` | Home Assistant token |

Example:
```bash
export HASS_AGENT_LOG_LEVEL=Debug
export HASS_AGENT_MQTT_SERVER=192.168.1.100
hass-agent
```

---

## Configuration Validation

Validate your configuration:

```bash
hass-agent --validate-config
```

This checks:
- JSON syntax
- Required fields
- Valid ranges
- Network connectivity
- MQTT connection
- Home Assistant API access
