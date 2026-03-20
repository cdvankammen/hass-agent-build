#!/usr/bin/env bash
# Test persistence: start simple headless, POST a command that toggles state, then check commands.json
set -e
cd "$(dirname "$0")/.."
cd src/HASS.Agent.SimpleHeadless

export ASPNETCORE_URLS=http://127.0.0.1:11111
DOTNET_PRINT_TELEMETRY_MESSAGE=0 dotnet run -c Debug &
PID=$!
sleep 1

# read original commands
CONFIG_DIR="$(pwd)/config"
COMMANDS_FILE="$CONFIG_DIR/commands.json"

echo "commands file: $COMMANDS_FILE"

curl -s http://127.0.0.1:11111/commands > /tmp/commands_before.json

# post a command
curl -s -X POST http://127.0.0.1:11111/command -H "Content-Type: application/json" -d '{"id":"1","name":"Test","entityType":"Switch","state":"ON"}'

sleep 1

curl -s http://127.0.0.1:11111/commands > /tmp/commands_after.json

kill $PID
wait $PID 2>/dev/null || true

echo "before:"; jq . /tmp/commands_before.json || true
echo "after:"; jq . /tmp/commands_after.json || true
