#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)/.."
TMPDIR=$(mktemp -d)
echo "tmp: $TMPDIR"

cat > "$TMPDIR/commands.json" <<'JSON'
[
  { "Id": "11111111-1111-1111-1111-111111111111", "Name": "LegacyCmd1", "Type": "CustomCommand", "Command": "echo hello", "EntityType": "Switch" }
]
JSON

cat > "$TMPDIR/sensors.json" <<'JSON'
[
  { "Id": "cpu", "Name": "CPU", "Type": "CpuLoadSensor", "UpdateInterval": 10 }
]
JSON

export ASPNETCORE_URLS=http://127.0.0.1:11111
DOTNET_PRINT_TELEMETRY_MESSAGE=0 dotnet run -c Release --project "$ROOT/src/HASS.Agent.SimpleHeadless/HASS.Agent.SimpleHeadless.csproj" > /tmp/simpleheadless_import_fp.log 2>&1 &
PID=$!
echo "started $PID"
trap 'kill $PID 2>/dev/null || true; rm -rf "$TMPDIR"' EXIT

for i in {1..15}; do
  if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:11111/commands | grep -q "200"; then
    echo "server ready"
    break
  fi
  sleep 1
done

echo "calling import with filePath"
curl -s -X POST http://127.0.0.1:11111/import/legacy -H "Content-Type: application/json" -d "{ \"filePath\": \"$TMPDIR\" }" | jq . || true
sleep 1

echo "commands after import:"
curl -s http://127.0.0.1:11111/commands | jq . || true

kill $PID
wait $PID 2>/dev/null || true
rm -rf "$TMPDIR"
echo done
