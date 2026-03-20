#!/usr/bin/env bash
# Simple integration test: starts simple headless, checks endpoints
set -e
cd "$(dirname "$0")/.."
cd src/HASS.Agent.SimpleHeadless

# run in background
dotnet run --urls http://127.0.0.1:11111 &
PID=$!
sleep 1

echo "Checking /commands"
curl -s http://127.0.0.1:11111/commands | jq .

echo "Checking /sensors"
curl -s http://127.0.0.1:11111/sensors | jq .

# post sample command
echo "Posting command"
curl -s -X POST http://127.0.0.1:11111/command -H "Content-Type: application/json" -d '{"id":"1","name":"Test","entityType":"Switch","state":"ON"}'

# cleanup
kill $PID
wait $PID 2>/dev/null || true

echo "Done"
