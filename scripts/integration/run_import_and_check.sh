#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)/.."
cd "$ROOT/src/HASS.Agent.SimpleHeadless"

export ASPNETCORE_URLS=http://127.0.0.1:11111
DOTNET_PRINT_TELEMETRY_MESSAGE=0 dotnet run -c Release > /tmp/simpleheadless_import.log 2>&1 &
PID=$!
echo "started $PID"
trap 'kill $PID 2>/dev/null || true' EXIT

for i in {1..15}; do
  if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:11111/commands | grep -q "200"; then
    echo "server ready"
    break
  fi
  sleep 1
done

echo "calling import"
curl -s -X POST http://127.0.0.1:11111/import/legacy -d @/dev/null -H "Content-Type: application/json" || true
sleep 1

echo "commands after import:"
curl -s http://127.0.0.1:11111/commands | jq . || true

echo "tail log:" && tail -n 80 /tmp/simpleheadless_import.log || true

kill $PID
wait $PID 2>/dev/null || true
echo "done"
