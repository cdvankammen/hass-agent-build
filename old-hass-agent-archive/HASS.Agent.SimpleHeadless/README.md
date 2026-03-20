HASS.Agent.SimpleHeadless

This is a small, cross-platform headless HTTP server that exposes endpoints the GUI expects:
- GET /commands
- POST /command
- GET /sensors
- GET /service/status
- POST /service/start
- POST /service/stop

Configuration files are stored in the working directory under `config/`.

Run:

```bash
cd src/HASS.Agent.SimpleHeadless
dotnet run --urls http://127.0.0.1:11111
```

Set `HASS_AGENT_MQTT_BROKER` environment variable to enable MQTT publishing.
