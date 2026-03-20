#!/bin/bash
# Build macOS DMG for HASS.Agent

set -e

# Configuration
APP_NAME="HASS.Agent"
VERSION="2.0.0"
DMG_NAME="HASS.Agent-${VERSION}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/build/macos"
APP_DIR="$BUILD_DIR/${APP_NAME}.app"

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RUNTIME="osx-arm64"
    DMG_SUFFIX="arm64"
else
    RUNTIME="osx-x64"
    DMG_SUFFIX="x64"
fi

echo "Building HASS.Agent macOS DMG v${VERSION} ($RUNTIME)"
echo "=================================================="

# Clean previous build
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Build the application
echo "Building application..."
cd "$PROJECT_ROOT/src"

# Try HASS.Agent.Desktop first, fall back to HASS.Agent.Avalonia
if [ -d "HASS.Agent.Desktop" ]; then
    GUI_PROJECT="HASS.Agent.Desktop/HASS.Agent.Desktop.csproj"
    GUI_EXECUTABLE="HASS.Agent.Desktop"
elif [ -d "HASS.Agent.Avalonia" ]; then
    GUI_PROJECT="HASS.Agent.Avalonia/HASS.Agent.Avalonia.csproj"
    GUI_EXECUTABLE="HASS.Agent.Avalonia"
else
    echo "Error: No GUI project found!"
    exit 1
fi

dotnet publish "$GUI_PROJECT" \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o "$BUILD_DIR/publish/gui"

dotnet publish HASS.Agent.Headless/HASS.Agent.Headless.csproj \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o "$BUILD_DIR/publish/headless"

# Create app bundle structure
echo "Creating app bundle..."
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

# Copy application files
cp "$BUILD_DIR/publish/gui/$GUI_EXECUTABLE" "$APP_DIR/Contents/MacOS/"
cp "$BUILD_DIR/publish/headless/HASS.Agent.Headless" "$APP_DIR/Contents/MacOS/"

# Make executables
chmod +x "$APP_DIR/Contents/MacOS/$GUI_EXECUTABLE"
chmod +x "$APP_DIR/Contents/MacOS/HASS.Agent.Headless"

# Create Info.plist
cat > "$APP_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>$GUI_EXECUTABLE</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>io.hassagent.app</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>HASS.Agent</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2024 HASS.Agent Contributors</string>
    <key>LSUIElement</key>
    <false/>
    <key>NSAppleEventsUsageDescription</key>
    <string>HASS.Agent needs to control other applications for media control features.</string>
    <key>NSBluetoothAlwaysUsageDescription</key>
    <string>HASS.Agent needs Bluetooth access to detect connected devices.</string>
</dict>
</plist>
EOF

# Copy icon if available
if [ -f "$PROJECT_ROOT/images/AppIcon.icns" ]; then
    cp "$PROJECT_ROOT/images/AppIcon.icns" "$APP_DIR/Contents/Resources/"
fi

# Create PkgInfo
echo -n "APPL????" > "$APP_DIR/Contents/PkgInfo"

# Create DMG
echo "Creating DMG..."
DMG_PATH="$BUILD_DIR/${DMG_NAME}-${DMG_SUFFIX}.dmg"
DMG_TEMP="$BUILD_DIR/dmg_temp"

mkdir -p "$DMG_TEMP"
cp -r "$APP_DIR" "$DMG_TEMP/"

# Create Applications symlink
ln -s /Applications "$DMG_TEMP/Applications"

# Create README
cat > "$DMG_TEMP/README.txt" << EOF
HASS.Agent for macOS
====================

Installation:
1. Drag HASS.Agent.app to Applications folder
2. Open HASS.Agent from Applications

First Run:
- macOS may show a security warning
- Right-click the app and select "Open" to bypass Gatekeeper

Run as Background Service:
1. Open Terminal
2. Run: launchctl load ~/Library/LaunchAgents/io.hassagent.agent.plist

For more information: https://hassagent.readthedocs.io/
EOF

# Create DMG
hdiutil create -volname "$APP_NAME" \
    -srcfolder "$DMG_TEMP" \
    -ov -format UDZO \
    "$DMG_PATH"

# Clean up
rm -rf "$DMG_TEMP"

echo ""
echo "DMG built successfully!"
echo "Output: $DMG_PATH"
echo ""
echo "To install: Open the DMG and drag HASS.Agent to Applications"
