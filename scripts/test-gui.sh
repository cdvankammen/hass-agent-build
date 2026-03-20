#!/usr/bin/env bash
# Comprehensive integration test: start headless, check /settings, modify appsettings, apply, verify log and validation
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export HASS_AGENT_CONFIG_PATH=/tmp/hass-agent-test
export HASS_AGENT_AUTO_START_PLATFORM=true
export HASS_AGENT_APPLY_TO_GUI=false

# ensure clean
rm -rf /tmp/hass-agent-test
mkdir -p /tmp/hass-agent-test
rm -f /tmp/hass-headless.log /tmp/hass-agent-apply.log

# stop any existing process listening on 11111
EXIST_PID=$(lsof -nP -iTCP:11111 -sTCP:LISTEN -t || true)
if [ -n "${EXIST_PID}" ]; then
  echo "Killing existing listener pid(s): ${EXIST_PID}"
  kill ${EXIST_PID} || true
  sleep 1
fi

# start headless in background
nohup dotnet run --project src/HASS.Agent.Headless/HASS.Agent.Headless.csproj -c Release &>/tmp/hass-headless.log &
PID=$!
echo "Started headless $PID"
# wait for server
for i in {1..10}; do
  sleep 1
  if curl -sS http://127.0.0.1:11111/settings >/dev/null 2>&1; then
    echo "Server is up"
    break
  fi
done

curl -sS http://127.0.0.1:11111/settings | jq '.' || true

# create appsettings and set MediaPlayerEnabled=false
cat > /tmp/hass-agent-test/appsettings.json <<'JSON'
{
  "DeviceName": "test-device",
  "MediaPlayerEnabled": false,
  "NotificationsEnabled": true
}
JSON

# save via API
curl -sS -X POST http://127.0.0.1:11111/settings/appsettings -d @/tmp/hass-agent-test/appsettings.json

# apply
applyResp=$(curl -sS -X POST http://127.0.0.1:11111/settings/apply || true)
echo "apply response: $applyResp"

# wait a moment and show tail of log
sleep 1
sed -n '1,200p' /tmp/hass-headless.log || true

# assert that API returned ok
if [ "$applyResp" = "ok" ]; then
  echo "apply-api-ok"
else
  echo "apply-api-failed: $applyResp"
fi

# verify apply marker file
apply_ok=0
for i in {1..5}; do
  if grep -q "applied:" /tmp/hass-agent-apply.log 2>/dev/null; then echo "apply-marker-ok"; apply_ok=1; break; fi
  sleep 1
done
if [ $apply_ok -eq 0 ]; then echo "apply-marker-missing"; fi

# validation negative test: invalid HassUri
cat > /tmp/hass-agent-test/appsettings.json <<'JSON'
{
  "DeviceName": "test",
  "HassUri": "not-a-url"
}
JSON
vresp=$(curl -sS -X POST -d @/tmp/hass-agent-test/appsettings.json -H "Content-Type: application/json" http://127.0.0.1:11111/settings/validate || true)
echo "validate response: $vresp"
if echo "$vresp" | grep -q "HassUri"; then echo "validate-failed-ok"; else echo "validate-failed-missing"; fi

# cleanup
kill $PID 2>/dev/null || true
