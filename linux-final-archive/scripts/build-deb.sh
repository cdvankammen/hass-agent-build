#!/bin/bash
# Build Debian Package for HASS.Agent

set -e

# Configuration
PACKAGE_NAME="hass-agent"
VERSION="2.0.0"
ARCH="amd64"
MAINTAINER="HASS.Agent Contributors <support@hassagent.io>"
DESCRIPTION="Home Assistant Companion Agent for Linux"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/build/deb"
PACKAGE_DIR="$BUILD_DIR/${PACKAGE_NAME}_${VERSION}_${ARCH}"

echo "Building HASS.Agent Debian Package v${VERSION}"
echo "=============================================="

# Clean previous build
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Build the application
echo "Building application..."
cd "$PROJECT_ROOT/src"

dotnet publish HASS.Agent.Headless/HASS.Agent.Headless.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o "$BUILD_DIR/publish/headless"

dotnet publish HASS.Agent.Avalonia/HASS.Agent.Avalonia.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o "$BUILD_DIR/publish/gui"

# Create package directory structure
echo "Creating package structure..."
mkdir -p "$PACKAGE_DIR/DEBIAN"
mkdir -p "$PACKAGE_DIR/opt/hass-agent"
mkdir -p "$PACKAGE_DIR/usr/local/bin"
mkdir -p "$PACKAGE_DIR/usr/share/applications"
mkdir -p "$PACKAGE_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$PACKAGE_DIR/lib/systemd/system"
mkdir -p "$PACKAGE_DIR/etc/hass-agent"

# Copy application files
echo "Copying application files..."
cp "$BUILD_DIR/publish/headless/HASS.Agent.Headless" "$PACKAGE_DIR/opt/hass-agent/"
cp "$BUILD_DIR/publish/gui/HASS.Agent.Avalonia" "$PACKAGE_DIR/opt/hass-agent/"

# Copy any additional resources
[ -f "$PROJECT_ROOT/images/hass-agent-icon.png" ] && \
    cp "$PROJECT_ROOT/images/hass-agent-icon.png" "$PACKAGE_DIR/usr/share/icons/hicolor/256x256/apps/"

# Create symlinks script (will be created during postinst)
# Create DEBIAN/control
cat > "$PACKAGE_DIR/DEBIAN/control" << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Depends: libnotify-bin, playerctl
Recommends: bluez
Maintainer: ${MAINTAINER}
Description: ${DESCRIPTION}
 HASS.Agent is a companion application for Home Assistant that allows
 you to control your Linux computer and send sensor data to Home Assistant.
 .
 Features:
  - Send system sensors to Home Assistant (CPU, Memory, Disk, Network)
  - Execute commands from Home Assistant
  - Control media playback
  - Receive notifications from Home Assistant
  - System tray integration
Homepage: https://github.com/LAB02-Research/HASS.Agent
EOF

# Create DEBIAN/postinst
cat > "$PACKAGE_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# Create symlinks
ln -sf /opt/hass-agent/HASS.Agent.Headless /usr/local/bin/hass-agent
ln -sf /opt/hass-agent/HASS.Agent.Avalonia /usr/local/bin/hass-agent-gui

# Set permissions
chmod +x /opt/hass-agent/HASS.Agent.Headless
chmod +x /opt/hass-agent/HASS.Agent.Avalonia

# Create log directory
mkdir -p /var/log/hass-agent
chmod 777 /var/log/hass-agent

# Update desktop database
if command -v update-desktop-database &> /dev/null; then
    update-desktop-database -q /usr/share/applications 2>/dev/null || true
fi

# Reload systemd
systemctl daemon-reload 2>/dev/null || true

echo ""
echo "HASS.Agent installed successfully!"
echo ""
echo "To start the GUI:     hass-agent-gui"
echo "To start headless:    hass-agent"
echo "To enable as service: sudo systemctl enable --now hass-agent@\$USER"
echo ""

exit 0
EOF
chmod 755 "$PACKAGE_DIR/DEBIAN/postinst"

# Create DEBIAN/prerm
cat > "$PACKAGE_DIR/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Stop service if running
systemctl stop 'hass-agent@*' 2>/dev/null || true

exit 0
EOF
chmod 755 "$PACKAGE_DIR/DEBIAN/prerm"

# Create DEBIAN/postrm
cat > "$PACKAGE_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e

# Remove symlinks
rm -f /usr/local/bin/hass-agent
rm -f /usr/local/bin/hass-agent-gui

# Don't remove logs on upgrade
if [ "$1" = "purge" ]; then
    rm -rf /var/log/hass-agent
fi

exit 0
EOF
chmod 755 "$PACKAGE_DIR/DEBIAN/postrm"

# Create systemd service template
cat > "$PACKAGE_DIR/lib/systemd/system/hass-agent@.service" << EOF
[Unit]
Description=HASS.Agent - Home Assistant Companion for %i
Documentation=https://hassagent.readthedocs.io/
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=%i
ExecStart=/opt/hass-agent/HASS.Agent.Headless
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal
Environment=HOME=/home/%i
WorkingDirectory=/opt/hass-agent

# Security
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=read-only
PrivateTmp=true
ReadWritePaths=/home/%i/.config/hass-agent /var/log/hass-agent

[Install]
WantedBy=multi-user.target
EOF

# Create desktop entry
cat > "$PACKAGE_DIR/usr/share/applications/hass-agent.desktop" << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=HASS.Agent
GenericName=Home Assistant Companion
Comment=Control and monitor your system from Home Assistant
Icon=hass-agent-icon
Exec=/opt/hass-agent/HASS.Agent.Avalonia
Terminal=false
Categories=Utility;System;
Keywords=home;assistant;smart;home;automation;mqtt;
StartupNotify=true
StartupWMClass=HASS.Agent.Avalonia
EOF

# Create default config
cat > "$PACKAGE_DIR/etc/hass-agent/appsettings.json.default" << EOF
{
  "DeviceName": "",
  "MqttEnabled": false,
  "MqttAddress": "",
  "MqttPort": 1883,
  "MqttUsername": "",
  "MqttPassword": "",
  "MqttDiscoveryPrefix": "homeassistant",
  "LocalApiEnabled": true,
  "LocalApiPort": 11111,
  "SensorUpdateInterval": 30,
  "NotificationsEnabled": true
}
EOF

# Build the package
echo "Building .deb package..."
cd "$BUILD_DIR"
dpkg-deb --build --root-owner-group "$PACKAGE_DIR"

# Rename to standard naming
mv "${PACKAGE_DIR}.deb" "${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"

echo ""
echo "Package built successfully!"
echo "Output: $BUILD_DIR/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
echo ""
echo "To install: sudo dpkg -i ${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
echo "To install with dependencies: sudo apt install ./${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
