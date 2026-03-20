# Release Notes - HASS.Agent Linux v0.1.0

This release introduces a headless Linux build of HASS.Agent with minimal dependencies.

## What's New

- **Linux Headless Support**: Run HASS.Agent on Linux without GUI dependencies
- **Debian Packaging**: `.deb` package with systemd service integration
- **Command Import**: Import legacy commands from Windows HASS.Agent configurations
- **MQTT Integration**: Full MQTTnet support for sensor/command publishing
- **Unit Tests**: Core tests for mapping and conversion logic

## Installation

### Debian/Ubuntu (Quick)

```bash
wget https://github.com/YOUR_ORG/HASS.Agent/releases/download/v0.1.0/hass-agent_0.1.0_amd64.deb
sudo dpkg -i hass-agent_0.1.0_amd64.deb
sudo systemctl daemon-reload
sudo systemctl enable --now hass-agent
```

Check status:
```bash
sudo systemctl status hass-agent
```

### Manual Installation

Extract the tarball and install:
```bash
tar -xzf hass-agent_0.1.0_amd64.tar.gz -C /tmp
sudo cp -r /tmp/hass-agent_0.1.0_amd64/opt/hass-agent /opt/
sudo cp /tmp/hass-agent_0.1.0_amd64/etc/systemd/system/hass-agent.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now hass-agent
```

## Configuration

Place your configuration files in `/opt/hass-agent/config/`:
- `commands.json` - Command definitions
- `sensors.json` - Sensor definitions (optional)

## Troubleshooting

View logs:
```bash
sudo journalctl -u hass-agent -f
```

Check dependencies:
```bash
dotnet --version  # Should be 10.0 or later
```

## Building from Source

See `README-linux.md` for detailed build instructions.

## Known Issues

- Service user configuration may need adjustment for your environment
- Edit `/etc/systemd/system/hass-agent.service` to change the `User=` line if needed

## Contributors

Thank you to all contributors who made this release possible!

---

For full changelog, see `CHANGELOG.md`
For documentation, see `README-linux.md`

## Publishing a Release

To create a GitHub release with the built artifacts, tag the commit and push the tag:

```bash
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```

This repository contains a GitHub Actions `release` job that will automatically upload the `dist/` artifacts when a tag is pushed. The action uses `GITHUB_TOKEN` and will create a release named after the tag.
