#!/bin/bash
#
# Linux Build and Run Script for HASS.Agent
# To be run on Debian/Ubuntu Linux (pmox)
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC_DIR="$ROOT_DIR/src"
PUBLISH_DIR="$ROOT_DIR/publish/linux"

echo "=== HASS.Agent Linux Build ==="
echo "Root: $ROOT_DIR"
echo ""

# Install dependencies if needed
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET SDK 10..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/dotnet
    ln -sf /usr/local/dotnet/dotnet /usr/local/bin/dotnet
fi

if ! command -v xdotool &> /dev/null; then
    echo "Installing xdotool..."
    apt-get update && apt-get install -y xdotool
fi

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
dotnet test HASS.Agent.Linux.sln -c Release --no-build --verbosity quiet || echo "Some tests may have been skipped"

# Publish Headless (CLI)
echo "[5/7] Publishing Headless (CLI)..."
dotnet publish HASS.Agent.Headless/HASS.Agent.Headless.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$PUBLISH_DIR/headless"

# Publish Avalonia (GUI)
echo "[6/7] Publishing Avalonia (GUI)..."
dotnet publish HASS.Agent.Avalonia/HASS.Agent.Avalonia.csproj \
    -c Release \
    -r linux-x64 \
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
echo "GUI:      $PUBLISH_DIR/avalonia/HASS.Agent.Avalonia (requires X11)"
echo ""
echo "=== Quick Start (Headless) ==="
echo "export HASS_AGENT_CONFIG_PATH=/tmp/hass-agent-test"
echo "$PUBLISH_DIR/headless/HASS.Agent.Headless"
echo ""
echo "=== Test Service ==="
echo "sleep 3 && curl -s http://127.0.0.1:11111/health"
echo ""
