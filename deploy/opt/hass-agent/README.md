HASS.Agent Headless

Default config files are located in `/opt/hass-agent/config` after installation.

Default files included:
- `appsettings.json` - basic runtime settings (device name, MQTT defaults)
- `commands.json` - configured commands (empty by default)
- `sensors.json` - configured sensors (empty by default)

Post-install behaviour:
- A system user `hassagent` will be created by the package `postinst` script.
- The service `hass-agent` will be enabled and started by the maintainer script.

To override defaults, edit `/opt/hass-agent/config/appsettings.json` and restart the service:

```
sudo systemctl restart hass-agent
sudo journalctl -u hass-agent -f
```
