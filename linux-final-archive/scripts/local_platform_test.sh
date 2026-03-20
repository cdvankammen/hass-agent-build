#!/usr/bin/env bash
set -euo pipefail

echo "Checking platform dependencies..."
which mpv >/dev/null 2>&1 && echo "mpv: OK" || echo "mpv: MISSING"
which espeak >/dev/null 2>&1 && echo "espeak: OK" || echo "espeak: MISSING"
which spd-say >/dev/null 2>&1 && echo "spd-say: OK" || echo "spd-say: MISSING"
which notify-send >/dev/null 2>&1 && echo "notify-send: OK" || echo "notify-send: MISSING"

# quick memory read
if [ -f /proc/meminfo ]; then
  echo "/proc/meminfo exists"
  grep -E "MemTotal|MemAvailable" /proc/meminfo || true
fi

# quick cpu usage measurement using our SystemMetricsAdapter via dotnet run (best-effort)
if command -v dotnet >/dev/null 2>&1; then
  echo "Attempting to run a small diagnostic against the built binaries..."
  # find a built net10.0 dll to use the runtime
  # this script assumes the repo was built and HASS.Agent.Core is available
  # we will not run tests here; just inform user how to run
  echo "To test adapters programmatically, run: dotnet run --project src/HASS.Agent.Headless"
fi

echo "Done"
