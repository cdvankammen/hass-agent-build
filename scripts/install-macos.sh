#!/bin/bash
# HASS.Agent macOS Installation Script

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
APP_NAME="HASS.Agent"
APP_VERSION="2.0.0"
INSTALL_DIR="/Applications/HASS.Agent.app"
SUPPORT_DIR="$HOME/Library/Application Support/HASS.Agent"
LOG_DIR="$HOME/Library/Logs/HASS.Agent"
LAUNCH_AGENT="$HOME/Library/LaunchAgents/io.hassagent.agent.plist"

echo -e "${BLUE}"
echo "╔══════════════════════════════════════════════════════════╗"
echo "║           HASS.Agent macOS Installer v${APP_VERSION}             ║"
echo "║        Home Assistant Companion for macOS                ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# Check macOS version
check_macos_version() {
    echo -e "${BLUE}Checking macOS version...${NC}"
    
    MACOS_VERSION=$(sw_vers -productVersion)
    MAJOR_VERSION=$(echo $MACOS_VERSION | cut -d. -f1)
    
    if [ "$MAJOR_VERSION" -lt 11 ]; then
        echo -e "${RED}✗ macOS 11 (Big Sur) or later is required${NC}"
        echo "Your version: $MACOS_VERSION"
        exit 1
    fi
    
    echo -e "${GREEN}✓ macOS $MACOS_VERSION${NC}"
}

# Check architecture
check_architecture() {
    ARCH=$(uname -m)
    echo -e "${GREEN}✓ Architecture: $ARCH${NC}"
    
    if [ "$ARCH" = "arm64" ]; then
        RUNTIME="osx-arm64"
    else
        RUNTIME="osx-x64"
    fi
}

# Check and install .NET runtime
install_dotnet() {
    echo -e "\n${BLUE}Checking .NET Runtime...${NC}"
    
    if command -v dotnet &> /dev/null; then
        DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "0.0.0")
        MAJOR_VERSION=$(echo $DOTNET_VERSION | cut -d. -f1)
        
        if [ "$MAJOR_VERSION" -ge 8 ]; then
            echo -e "${GREEN}✓ .NET $DOTNET_VERSION is installed${NC}"
            return 0
        fi
    fi
    
    echo -e "${YELLOW}Installing .NET Runtime via Homebrew...${NC}"
    
    # Check if Homebrew is installed
    if ! command -v brew &> /dev/null; then
        echo -e "${YELLOW}Installing Homebrew first...${NC}"
        /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    fi
    
    brew install --cask dotnet-sdk
    
    echo -e "${GREEN}✓ .NET Runtime installed${NC}"
}

# Create directories
create_directories() {
    echo -e "\n${BLUE}Creating directories...${NC}"
    
    mkdir -p "$SUPPORT_DIR"
    mkdir -p "$LOG_DIR"
    mkdir -p "$HOME/Library/LaunchAgents"
    
    echo -e "${GREEN}✓ Directories created${NC}"
}

# Create app bundle structure
create_app_bundle() {
    echo -e "\n${BLUE}Creating application bundle...${NC}"
    
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    # Check for local build
    if [ -d "$SCRIPT_DIR/../publish/$RUNTIME" ]; then
        SOURCE_DIR="$SCRIPT_DIR/../publish/$RUNTIME"
    elif [ -d "$SCRIPT_DIR/../bin/Release/net10.0/$RUNTIME/publish" ]; then
        SOURCE_DIR="$SCRIPT_DIR/../bin/Release/net10.0/$RUNTIME/publish"
    else
        echo -e "${YELLOW}Downloading latest release...${NC}"
        
        DOWNLOAD_URL="https://github.com/LAB02-Research/HASS.Agent/releases/latest/download/hass-agent-$RUNTIME.tar.gz"
        
        mkdir -p /tmp/hass-agent-install
        curl -L -o /tmp/hass-agent.tar.gz "$DOWNLOAD_URL" || {
            echo -e "${RED}✗ Failed to download. Please check your internet connection.${NC}"
            exit 1
        }
        
        tar -xzf /tmp/hass-agent.tar.gz -C /tmp/hass-agent-install
        SOURCE_DIR="/tmp/hass-agent-install"
    fi
    
    # Remove existing app if present
    [ -d "$INSTALL_DIR" ] && rm -rf "$INSTALL_DIR"
    
    # Create app bundle structure
    mkdir -p "$INSTALL_DIR/Contents/MacOS"
    mkdir -p "$INSTALL_DIR/Contents/Resources"
    
    # Copy application files
    cp -r "$SOURCE_DIR/"* "$INSTALL_DIR/Contents/MacOS/"
    
    # Create Info.plist
    cat > "$INSTALL_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>HASS.Agent.Avalonia</string>
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
    <string>${APP_VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${APP_VERSION}</string>
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
</dict>
</plist>
EOF

    # Make executable
    chmod +x "$INSTALL_DIR/Contents/MacOS/HASS.Agent.Avalonia"
    [ -f "$INSTALL_DIR/Contents/MacOS/HASS.Agent.Headless" ] && chmod +x "$INSTALL_DIR/Contents/MacOS/HASS.Agent.Headless"
    
    # Create wrapper script for headless
    cat > "$INSTALL_DIR/Contents/MacOS/hass-agent-headless" << 'EOF'
#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/HASS.Agent.Headless" "$@"
EOF
    chmod +x "$INSTALL_DIR/Contents/MacOS/hass-agent-headless"
    
    echo -e "${GREEN}✓ Application bundle created at $INSTALL_DIR${NC}"
    
    # Clean up temp files
    rm -rf /tmp/hass-agent-install /tmp/hass-agent.tar.gz 2>/dev/null || true
}

# Create Launch Agent for headless mode
create_launch_agent() {
    echo -e "\n${BLUE}Creating Launch Agent for background service...${NC}"
    
    cat > "$LAUNCH_AGENT" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>io.hassagent.agent</string>
    <key>ProgramArguments</key>
    <array>
        <string>$INSTALL_DIR/Contents/MacOS/HASS.Agent.Headless</string>
    </array>
    <key>RunAtLoad</key>
    <false/>
    <key>KeepAlive</key>
    <dict>
        <key>SuccessfulExit</key>
        <false/>
    </dict>
    <key>StandardOutPath</key>
    <string>$LOG_DIR/hass-agent.log</string>
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/hass-agent-error.log</string>
    <key>WorkingDirectory</key>
    <string>$SUPPORT_DIR</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>HOME</key>
        <string>$HOME</string>
    </dict>
</dict>
</plist>
EOF

    echo -e "${GREEN}✓ Launch Agent created${NC}"
    echo -e "  To start:  ${YELLOW}launchctl load $LAUNCH_AGENT${NC}"
    echo -e "  To enable: ${YELLOW}launchctl load -w $LAUNCH_AGENT${NC}"
}

# Create CLI wrapper
create_cli_wrapper() {
    echo -e "\n${BLUE}Creating CLI commands...${NC}"
    
    # Create /usr/local/bin if it doesn't exist
    [ -d /usr/local/bin ] || sudo mkdir -p /usr/local/bin
    
    # Create hass-agent command
    sudo tee /usr/local/bin/hass-agent > /dev/null << EOF
#!/bin/bash
exec "$INSTALL_DIR/Contents/MacOS/HASS.Agent.Headless" "\$@"
EOF
    sudo chmod +x /usr/local/bin/hass-agent
    
    # Create hass-agent-gui command
    sudo tee /usr/local/bin/hass-agent-gui > /dev/null << EOF
#!/bin/bash
open -a "HASS.Agent" "\$@"
EOF
    sudo chmod +x /usr/local/bin/hass-agent-gui
    
    echo -e "${GREEN}✓ CLI commands created${NC}"
}

# Create default configuration
create_default_config() {
    echo -e "\n${BLUE}Creating default configuration...${NC}"
    
    if [ ! -f "$SUPPORT_DIR/appsettings.json" ]; then
        cat > "$SUPPORT_DIR/appsettings.json" << EOF
{
  "DeviceName": "$(hostname -s)",
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
        echo -e "${GREEN}✓ Default configuration created${NC}"
    else
        echo -e "${YELLOW}⚠ Configuration already exists, skipping${NC}"
    fi
}

# Print completion message
print_completion() {
    echo -e "\n${GREEN}"
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║          Installation Complete! 🎉                       ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
    
    echo -e "Installation Summary:"
    echo -e "  ${BLUE}Application:${NC}   $INSTALL_DIR"
    echo -e "  ${BLUE}Configuration:${NC} $SUPPORT_DIR"
    echo -e "  ${BLUE}Logs:${NC}          $LOG_DIR"
    echo ""
    echo -e "Quick Start:"
    echo -e "  ${YELLOW}1.${NC} Open GUI:           ${GREEN}open -a 'HASS.Agent'${NC}"
    echo -e "  ${YELLOW}2.${NC} Run headless:       ${GREEN}hass-agent${NC}"
    echo -e "  ${YELLOW}3.${NC} Start on login:     ${GREEN}launchctl load -w $LAUNCH_AGENT${NC}"
    echo ""
    echo -e "Web Interface: ${BLUE}http://localhost:11111${NC}"
    echo ""
    echo -e "For more information: ${BLUE}https://hassagent.readthedocs.io/${NC}"
}

# Uninstall function
uninstall() {
    echo -e "${YELLOW}Uninstalling HASS.Agent...${NC}"
    
    # Stop and remove launch agent
    launchctl unload "$LAUNCH_AGENT" 2>/dev/null || true
    rm -f "$LAUNCH_AGENT"
    
    # Remove CLI commands
    sudo rm -f /usr/local/bin/hass-agent
    sudo rm -f /usr/local/bin/hass-agent-gui
    
    # Remove application
    rm -rf "$INSTALL_DIR"
    
    echo -e "${GREEN}✓ HASS.Agent uninstalled${NC}"
    echo -e "${YELLOW}Note: Configuration at $SUPPORT_DIR was preserved${NC}"
    echo -e "To remove config: ${RED}rm -rf '$SUPPORT_DIR'${NC}"
}

# Main installation flow
main() {
    case "${1:-install}" in
        install)
            check_macos_version
            check_architecture
            install_dotnet
            create_directories
            create_app_bundle
            create_launch_agent
            create_cli_wrapper
            create_default_config
            print_completion
            ;;
        uninstall)
            uninstall
            ;;
        *)
            echo "Usage: $0 {install|uninstall}"
            exit 1
            ;;
    esac
}

main "$@"
