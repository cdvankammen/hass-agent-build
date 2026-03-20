# HASS.Agent Troubleshooting Guide

## Quick Diagnostics

### Check Application Status

**Linux:**
```bash
# Check if service is running
systemctl status hass-agent

# View recent logs
journalctl -u hass-agent -n 100 --no-pager

# Check process
ps aux | grep hass-agent
```

**macOS:**
```bash
# Check launch agent
launchctl list | grep hass-agent

# View logs
tail -100 ~/.config/hass-agent/logs/hass-agent.log

# Check process
pgrep -l hass-agent
```

**Windows (PowerShell):**
```powershell
# Check service
Get-Service hass-agent

# View recent logs
Get-Content "$env:APPDATA\HASS.Agent\logs\hass-agent.log" -Tail 100

# Check process
Get-Process hass-agent
```

---

## Common Issues

### 1. Application Won't Start

#### Symptoms
- Application crashes immediately
- No window appears
- Service fails to start

#### Solutions

**Check for port conflicts:**
```bash
# Check if API port is in use
lsof -i :11111  # Linux/macOS
netstat -an | findstr 11111  # Windows
```

**Verify configuration:**
```bash
# Validate JSON syntax
cat ~/.config/hass-agent/config.json | jq .
```

**Check permissions:**
```bash
# Linux/macOS
ls -la ~/.config/hass-agent/
chmod 755 ~/.config/hass-agent/
chmod 644 ~/.config/hass-agent/*.json
```

**Clear corrupted config:**
```bash
# Backup and reset
mv ~/.config/hass-agent/config.json ~/.config/hass-agent/config.json.bak
```

---

### 2. MQTT Connection Failed

#### Symptoms
- "Connection refused" error
- "Authentication failed"
- Sensors not appearing in Home Assistant

#### Diagnostic Steps

**1. Test MQTT connectivity:**
```bash
# Install mosquitto-clients
sudo apt install mosquitto-clients  # Debian/Ubuntu
brew install mosquitto               # macOS

# Test connection
mosquitto_pub -h YOUR_MQTT_SERVER -p 1883 \
  -u YOUR_USERNAME -P YOUR_PASSWORD \
  -t "test/topic" -m "hello"
```

**2. Check MQTT broker logs:**
```bash
# Mosquitto on Home Assistant
docker logs addon_core_mosquitto
# or check HA logs in Settings → System → Logs
```

**3. Verify credentials:**
- Check username/password match exactly
- Ensure user has publish permissions
- Check for special characters that need escaping

**4. Network issues:**
```bash
# Test port connectivity
nc -zv YOUR_MQTT_SERVER 1883
telnet YOUR_MQTT_SERVER 1883
```

#### Common Fixes

| Issue | Solution |
|-------|----------|
| "Connection refused" | Broker not running, wrong port, firewall |
| "Authentication failed" | Wrong credentials, user not created |
| "Client ID already in use" | Another instance running, change clientId |
| "TLS handshake failed" | Certificate issues, try useTls: false |

---

### 3. Sensors Not Appearing in Home Assistant

#### Symptoms
- Device not visible
- Entities missing
- Sensors showing "unavailable"

#### Diagnostic Steps

**1. Check MQTT discovery:**
```bash
# Subscribe to discovery topic
mosquitto_sub -h YOUR_MQTT_SERVER -p 1883 \
  -u YOUR_USERNAME -P YOUR_PASSWORD \
  -t "homeassistant/#" -v
```

**2. Verify discovery prefix:**
Make sure `discoveryPrefix` in HASS.Agent matches Home Assistant:
```yaml
# Home Assistant configuration.yaml
mqtt:
  discovery_prefix: homeassistant  # default
```

**3. Check entity registry:**
- Go to Settings → Devices & Services → MQTT
- Look for your device
- Check if entities are disabled

**4. Restart MQTT integration:**
- Settings → Devices & Services → MQTT → ... → Reload

#### Common Fixes

| Issue | Solution |
|-------|----------|
| Device not discovered | Check discovery_prefix matches |
| Entities disabled | Enable in entity settings |
| Duplicate entities | Delete from entity registry, restart |
| Wrong device name | Change in HASS.Agent settings, restart |

---

### 4. High CPU Usage

#### Symptoms
- HASS.Agent using 50%+ CPU
- System slowdown
- Fans running constantly

#### Diagnostic Steps

**1. Check sensor update intervals:**
```bash
# View current configuration
cat ~/.config/hass-agent/sensors.json | jq '.[].updateInterval'
```

**2. Enable debug logging:**
```json
{
  "logging": {
    "level": "Debug"
  }
}
```

**3. Monitor which sensors are heavy:**
```bash
tail -f ~/.config/hass-agent/logs/hass-agent.log | grep "Sensor update"
```

#### Solutions

1. **Increase update intervals:**
   - Set non-critical sensors to 60+ seconds
   - Active window sensor is CPU intensive

2. **Disable unused sensors:**
```json
{
  "sensors": [
    { "id": "heavy_sensor", "enabled": false }
  ]
}
```

3. **Check for infinite loops:**
   - Custom shell command sensors might hang
   - Set reasonable timeouts

---

### 5. Notifications Not Working

#### Symptoms
- No notification appears
- Notification times out immediately
- Images don't load

#### Diagnostic Steps

**1. Test local notification:**
```bash
# Linux (notify-send)
notify-send "Test" "This is a test notification"

# macOS (osascript)
osascript -e 'display notification "Test" with title "Test"'
```

**2. Check API endpoint:**
```bash
curl -X POST http://localhost:11111/notification \
  -H "Content-Type: application/json" \
  -d '{"title": "Test", "message": "Hello"}'
```

**3. Verify Home Assistant service:**
- Developer Tools → Services
- Search for `notify.hass_agent_*`
- Test calling the service

#### Common Fixes

| Issue | Solution |
|-------|----------|
| No notification service | MQTT not connected, device not registered |
| Permission denied | Grant notification permissions to app |
| Image not loading | Check URL is accessible, increase timeout |
| Notification daemon not running | Start notification service |

---

### 6. Media Control Not Working

#### Symptoms
- No media player in Home Assistant
- Play/pause doesn't work
- Wrong player detected

#### Diagnostic Steps

**1. Check media players detected:**
```bash
# Linux - check MPRIS
dbus-send --print-reply --dest=org.freedesktop.DBus /org/freedesktop/DBus \
  org.freedesktop.DBus.ListNames | grep mpris
```

**2. Verify media integration:**
```bash
curl http://localhost:11111/media
```

**3. Test local control:**
```bash
# Linux MPRIS
dbus-send --print-reply --dest=org.mpris.MediaPlayer2.spotify \
  /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.PlayPause
```

#### Solutions

1. **Enable media sources:**
```json
{
  "media": {
    "sources": [
      { "name": "spotify", "enabled": true }
    ]
  }
}
```

2. **Set correct player priority:**
   - First enabled source is primary
   - Reorder in configuration

---

## Log Analysis

### Understanding Log Format

```
[2024-01-15 10:23:45 INF] [SensorManager] ▶ Starting operation: Update Sensors [abc123]
                                          │            │                          │
                                          │            │                          └─ Operation ID
                                          │            └─ Operation description
                                          └─ Source class
```

### Log Levels

| Level | When Used |
|-------|-----------|
| `VRB` | Verbose - very detailed debugging |
| `DBG` | Debug - debugging information |
| `INF` | Information - normal operation |
| `WRN` | Warning - potential issues |
| `ERR` | Error - operation failed |
| `FTL` | Fatal - application crash |

### Finding Errors

```bash
# Show only errors and warnings
grep -E '\[(ERR|WRN|FTL)\]' ~/.config/hass-agent/logs/hass-agent.log

# Show errors with context
grep -B 5 -A 10 '\[ERR\]' ~/.config/hass-agent/logs/hass-agent.log

# Find specific operation
grep "Operation ID" ~/.config/hass-agent/logs/hass-agent.log
```

### Error Format

```
[2024-01-15 10:23:45 ERR] [MqttClient.Connect] Failed to connect to MQTT broker
  {"StackTrace": "...", "ExceptionType": "SocketException", "LineNumber": 156, 
   "MemberName": "ConnectAsync", "ThreadId": 12}
System.Net.Sockets.SocketException: Connection refused
   at System.Net.Sockets.Socket.ConnectAsync(...)
   at HASS.Agent.Core.Mqtt.MqttClient.ConnectAsync() in MqttClient.cs:line 156
```

Key information:
- **SourceContext**: Class where error occurred (`MqttClient`)
- **MemberName**: Method name (`ConnectAsync`)
- **LineNumber**: Exact line number (156)
- **ExceptionType**: Type of error (`SocketException`)
- **Stack trace**: Full call stack

---

## Diagnostic Export

### Generate Diagnostic Report

```bash
hass-agent --export-diagnostics
```

This creates a ZIP file containing:
- Configuration files (sensitive data redacted)
- Recent log files
- System information
- Network configuration
- Sensor status
- MQTT connection status

### Manual Information Gathering

```bash
# System info
uname -a
cat /etc/os-release

# .NET runtime
dotnet --info

# Network
ip addr
cat /etc/resolv.conf

# HASS.Agent version
hass-agent --version

# Configuration
cat ~/.config/hass-agent/config.json | jq 'del(.mqtt.password, .homeAssistant.token)'
```

---

## Getting Help

### Before Asking for Help

1. ✅ Check this troubleshooting guide
2. ✅ Search existing issues on GitHub
3. ✅ Enable debug logging and reproduce the issue
4. ✅ Gather relevant logs
5. ✅ Note your OS version and HASS.Agent version

### Reporting an Issue

Include:
- OS and version
- HASS.Agent version
- Home Assistant version
- Steps to reproduce
- Expected vs actual behavior
- Relevant log excerpts
- Configuration (with secrets redacted)

### Support Channels

- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: Questions and general help
- **Discord**: Real-time community support
- **Home Assistant Community**: Integration questions

---

## FAQ

**Q: Can I run multiple instances of HASS.Agent?**
A: Yes, but each needs a unique device name and MQTT client ID.

**Q: How do I migrate configuration to a new machine?**
A: Copy the entire `~/.config/hass-agent/` directory.

**Q: Why do sensors show "unavailable" after restart?**
A: Normal during startup. Should resolve within 30 seconds.

**Q: Can I use HASS.Agent without MQTT?**
A: Yes, but you lose real-time updates and some features.

**Q: How do I reset to factory defaults?**
A: Delete the config directory and restart the application.
