#!/usr/bin/env bash
set -euo pipefail

# Publish HASS.Agent for linux-x64
cd "$(dirname "$0")/.."
cd src/HASS.Agent

echo "Publishing HASS.Agent for linux-x64"
dotnet publish -c Release -r linux-x64 -o ./publish

echo "Published to src/HASS.Agent/publish"

echo "Publishing HASS.Agent.Headless for linux-x64"
cd ../HASS.Agent.Headless
dotnet publish -c Release -r linux-x64 -o ../HASS.Agent/publish/headless

echo "Publishing HASS.Agent.UI (Avalonia) for linux-x64"
cd ../HASS.Agent.UI
dotnet publish -c Release -r linux-x64 -o ../HASS.Agent/publish/ui

echo "Publishing HASS.Agent.SimpleHeadless for linux-x64"
cd ../HASS.Agent.SimpleHeadless
dotnet publish -c Release -r linux-x64 -o ../HASS.Agent/publish/simple-headless
