#!/bin/bash
#
# macOS Build and Run Script for HASS.Agent
# Builds, packages, and runs HASS.Agent on macOS
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC_DIR="$ROOT_DIR/src"
PUBLISH_DIR="$ROOT_DIR/publish/macos"

echo "=== HASS.Agent macOS Build ==="
echo "Root: $ROOT_DIR"
echo ""

# Clean previous builds
echo "[1/7] Cleaning previous builds..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

# Restore dependencies
echo "[2/7] Restoring dependencies..."
cd "$SRC_DIR"
dotnet restore HASS.Agent.Linux.sln

# Build solution
echo "[3/7] Building solution..."
dotnet build HASS.Agent.Linux.sln -c Release

# Run tests
echo "[4/7] Running tests..."
dotnet test HASS.Agent.Linux.sln -c Release --no-build --verbosity quiet

# Publish Headless (CLI)
echo "[5/7] Publishing Headless (CLI)..."
dotnet publish HASS.Agent.Headless/HASS.Agent.Headless.csproj \
    -c Release \
    -r osx-x64 \
    --self-contained false \
    -o "$PUBLISH_DIR/headless"

# Publish Avalonia (GUI)
echo "[6/7] Publishing Avalonia (GUI)..."
dotnet publish HASS.Agent.Avalonia/HASS.Agent.Avalonia.csproj \
    -c Release \
    -r osx-x64 \
    --self-contained false \
    -o "$PUBLISH_DIR/avalonia"

echo "[7/7] Build complete!"
echo ""
echo "=== Published Binaries ==="
echo "Headless: $PUBLISH_DIR/headless/HASS.Agent.Headless"
echo "GUI:      $PUBLISH_DIR/avalonia/HASS.Agent.Avalonia"
echo ""
echo "=== Run Options ==="
echo "Headless: $PUBLISH_DIR/headless/HASS.Agent.Headless"
echo "GUI:      $PUBLISH_DIR/avalonia/HASS.Agent.Avalonia"
echo ""
echo "=== Quick Start (Headless) ==="
echo "export HASS_AGENT_CONFIG_PATH=/tmp/hass-agent-test"
echo "$PUBLISH_DIR/headless/HASS.Agent.Headless"
echo ""
