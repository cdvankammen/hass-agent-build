#!/usr/bin/env bash
set -euo pipefail

echo "Starting Linux build+test+package script"

# Restore
dotnet restore src/HASS.Agent.Linux.sln

# Try format (install if needed)
if ! command -v dotnet-format >/dev/null 2>&1; then
  echo "dotnet-format not found, attempting to install"
  if [ -f dotnet-tools.json ]; then
    dotnet tool restore || true
  else
    dotnet tool install -g dotnet-format || true
    export PATH="$PATH:$HOME/.dotnet/tools"
  fi
fi

if command -v dotnet-format >/dev/null 2>&1; then
  dotnet format src/HASS.Agent.Linux.sln --verify-no-changes || echo "Formatting differences detected (non-blocking in script)"
fi

# Build & test
dotnet build src/HASS.Agent.Linux.sln -c Release --no-restore
dotnet test src/HASS.Agent.Linux.sln -c Release --no-build --verbosity minimal

# Publish headless targets
dotnet publish src/HASS.Agent.Headless -c Release -r linux-x64 --no-self-contained -o artifacts/publish/hass-agent-headless
dotnet publish src/HASS.Agent.SimpleHeadless -c Release -r linux-x64 --no-self-contained -o artifacts/publish/hass-agent-simpleheadless

# Package
chmod +x deploy/build_deb.sh
./deploy/build_deb.sh

echo "Linux build+package completed. Artifacts in dist/"
