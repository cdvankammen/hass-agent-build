#!/usr/bin/env bash
set -euo pipefail
WORKDIR="$(cd "$(dirname "$0")/.." && pwd)/.."
cd "$WORKDIR/src/HASS.Agent.SimpleHeadless"

export ASPNETCORE_URLS=http://127.0.0.1:11111
DOTNET_PRINT_TELEMETRY_MESSAGE=0 dotnet run -c Release > /tmp/simpleheadless_e2e.log 2>&1 &
PID=$!
echo "started $PID"
trap 'kill $PID 2>/dev/null || true' EXIT

for i in {1..10}; do
  if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:11111/commands | grep -q "200"; then
    echo "server ready"
    break
  fi
  sleep 1
done

curl -s http://127.0.0.1:11111/commands > /tmp/hass_commands_before.json
printf '{"id":"e2e-test","name":"E2E Test","entityType":"Switch","state":"ON"}' | curl -s -X POST http://127.0.0.1:11111/command -H "Content-Type: application/json" -d @- > /tmp/hass_commands_after.json

echo "before:"; jq . /tmp/hass_commands_before.json || true
echo "after:"; jq . /tmp/hass_commands_after.json || true

kill $PID

echo "log tail:"; tail -n 50 /tmp/simpleheadless_e2e.log || true

echo "done"
