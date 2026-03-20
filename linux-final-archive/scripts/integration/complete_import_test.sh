#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)/.."
TMPDIR=$(mktemp -d)

cat > "$TMPDIR/commands.json" <<'JSON'
[
  { "Id": "22222222-2222-2222-2222-222222222222", "Name": "LegacyCmd2", "Type": "CustomCommand", "Command": "echo hi", "EntityType": "Switch" }
]
JSON

cat > "$TMPDIR/sensors.json" <<'JSON'
[
  { "Id": "mem", "Name": "Memory", "Type": "MemorySensor", "UpdateInterval": 15 }
]
JSON

export ASPNETCORE_URLS=http://127.0.0.1:11111
DOTNET_PRINT_TELEMETRY_MESSAGE=0 dotnet run -c Release --project "$ROOT/src/HASS.Agent.SimpleHeadless/HASS.Agent.SimpleHeadless.csproj" > /tmp/simpleheadless_complete_import.log 2>&1 &
PID=$!
echo started $PID
trap 'kill $PID 2>/dev/null || true; rm -rf "$TMPDIR"' EXIT

for i in {1..15}; do
  if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:11111/commands | grep -q "200"; then
    break
  fi
  sleep 1
done

# ensure no leftover config is present
rm -f "$ROOT/src/HASS.Agent.SimpleHeadless/config/commands.json" || true

resp=$(curl -s -X POST http://127.0.0.1:11111/import/legacy -H "Content-Type: application/json" --data-binary @"$TMPDIR/commands.json")
echo "import response: $resp"

cmds=$(curl -s http://127.0.0.1:11111/commands)
echo "commands: $cmds"

imported=$(echo "$resp" | jq -r '.commands // 0')
if [ "$imported" -ge 1 ]; then
  echo "import ok"
else
  echo "import failed (imported=$imported)"; exit 2
fi

echo done
