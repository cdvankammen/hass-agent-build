#!/bin/bash
# HASS.Agent Linux Installation Script
# Supports Debian/Ubuntu and Fedora/RHEL based distributions

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
INSTALL_DIR="/opt/hass-agent"
CONFIG_DIR="$HOME/.config/hass-agent"
LOG_DIR="/var/log/hass-agent"
SYSTEMD_SERVICE="/etc/systemd/system/hass-agent.service"
DESKTOP_FILE="/usr/share/applications/hass-agent.desktop"
APP_VERSION="2.0.0"

echo -e "${BLUE}"
echo "╔══════════════════════════════════════════════════════════╗"
echo "║            HASS.Agent Linux Installer v${APP_VERSION}            ║"
echo "║        Home Assistant Companion for Linux                ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# Check if running as root for system-wide install
check_root() {
    if [ "$EUID" -ne 0 ]; then
        echo -e "${YELLOW}Note: Running without root. Some features may require elevated privileges.${NC}"
        SUDO_CMD="sudo"
    else
        SUDO_CMD=""
    fi
}

# Detect distribution
detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO=$ID
        DISTRO_VERSION=$VERSION_ID
        echo -e "${GREEN}✓ Detected: $NAME $VERSION${NC}"
    else
        echo -e "${RED}✗ Unable to detect distribution${NC}"
        exit 1
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
    
    echo -e "${YELLOW}Installing .NET Runtime...${NC}"
    
    case $DISTRO in
        ubuntu|debian)
            $SUDO_CMD apt-get update
            $SUDO_CMD apt-get install -y wget apt-transport-https
            wget https://packages.microsoft.com/config/$DISTRO/$DISTRO_VERSION/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            $SUDO_CMD dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            $SUDO_CMD apt-get update
            $SUDO_CMD apt-get install -y dotnet-runtime-8.0
            ;;
        fedora|rhel|centos)
            $SUDO_CMD dnf install -y dotnet-runtime-8.0
            ;;
        arch|manjaro)
            $SUDO_CMD pacman -Sy --noconfirm dotnet-runtime-8.0
            ;;
        *)
            echo -e "${RED}✗ Unsupported distribution. Please install .NET 8.0 manually.${NC}"
            echo "Visit: https://dotnet.microsoft.com/download"
            exit 1
            ;;
    esac
    
    echo -e "${GREEN}✓ .NET Runtime installed${NC}"
}

# Install system dependencies
install_dependencies() {
    echo -e "\n${BLUE}Installing dependencies...${NC}"
    
    case $DISTRO in
        ubuntu|debian)
            $SUDO_CMD apt-get install -y \
                libnotify-bin \
                playerctl \
                bluez \
                libx11-6 \
                libice6 \
                libsm6 \
                libfontconfig1
            ;;
        fedora|rhel|centos)
            $SUDO_CMD dnf install -y \
                libnotify \
                playerctl \
                bluez \
                libX11 \
                libICE \
                libSM \
                fontconfig
            ;;
        arch|manjaro)
            $SUDO_CMD pacman -Sy --noconfirm \
                libnotify \
                playerctl \
                bluez \
                libx11 \
                libice \
                libsm \
                fontconfig
            ;;
        *)
            echo -e "${YELLOW}⚠ Please install dependencies manually: libnotify, playerctl, bluez${NC}"
            ;;
    esac
    
    echo -e "${GREEN}✓ Dependencies installed${NC}"
}

# Create directories
create_directories() {
    echo -e "\n${BLUE}Creating directories...${NC}"
    
    $SUDO_CMD mkdir -p "$INSTALL_DIR"
    mkdir -p "$CONFIG_DIR"
    $SUDO_CMD mkdir -p "$LOG_DIR"
    $SUDO_CMD chmod 777 "$LOG_DIR"
    
    echo -e "${GREEN}✓ Directories created${NC}"
}

# Download and install application
install_application() {
    echo -e "\n${BLUE}Installing HASS.Agent...${NC}"
    
    # Check if we're installing from local build or downloading
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    if [ -d "$SCRIPT_DIR/../publish/linux-x64" ]; then
        echo "Installing from local build..."
        $SUDO_CMD cp -r "$SCRIPT_DIR/../publish/linux-x64/"* "$INSTALL_DIR/"
    elif [ -d "$SCRIPT_DIR/../bin/Release/net10.0/linux-x64/publish" ]; then
        echo "Installing from local publish folder..."
        $SUDO_CMD cp -r "$SCRIPT_DIR/../bin/Release/net10.0/linux-x64/publish/"* "$INSTALL_DIR/"
    else
        echo "Downloading latest release..."
        DOWNLOAD_URL="https://github.com/LAB02-Research/HASS.Agent/releases/latest/download/hass-agent-linux-x64.tar.gz"
        
        wget -q --show-progress -O /tmp/hass-agent.tar.gz "$DOWNLOAD_URL" || {
            echo -e "${RED}✗ Failed to download. Please check your internet connection.${NC}"
            exit 1
        }
        
        $SUDO_CMD tar -xzf /tmp/hass-agent.tar.gz -C "$INSTALL_DIR"
        rm /tmp/hass-agent.tar.gz
    fi
    
    # Make executables
    $SUDO_CMD chmod +x "$INSTALL_DIR/HASS.Agent.Headless"
    [ -f "$INSTALL_DIR/HASS.Agent.Avalonia" ] && $SUDO_CMD chmod +x "$INSTALL_DIR/HASS.Agent.Avalonia"
    
    # Create symlinks
    $SUDO_CMD ln -sf "$INSTALL_DIR/HASS.Agent.Headless" /usr/local/bin/hass-agent
    [ -f "$INSTALL_DIR/HASS.Agent.Avalonia" ] && $SUDO_CMD ln -sf "$INSTALL_DIR/HASS.Agent.Avalonia" /usr/local/bin/hass-agent-gui
    
    echo -e "${GREEN}✓ Application installed to $INSTALL_DIR${NC}"
}

# Create systemd service for headless mode
create_systemd_service() {
    echo -e "\n${BLUE}Creating systemd service...${NC}"
    
    $SUDO_CMD tee "$SYSTEMD_SERVICE" > /dev/null << EOF
[Unit]
Description=HASS.Agent - Home Assistant Companion
Documentation=https://hassagent.readthedocs.io/
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$USER
Group=$USER
ExecStart=$INSTALL_DIR/HASS.Agent.Headless
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal
Environment=DOTNET_ROOT=/usr/share/dotnet
Environment=HOME=$HOME
WorkingDirectory=$INSTALL_DIR

# Security hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=read-only
PrivateTmp=true
ReadWritePaths=$CONFIG_DIR $LOG_DIR

[Install]
WantedBy=multi-user.target
EOF

    $SUDO_CMD systemctl daemon-reload
    
    echo -e "${GREEN}✓ Systemd service created${NC}"
    echo -e "  To start:  ${YELLOW}sudo systemctl start hass-agent${NC}"
    echo -e "  To enable: ${YELLOW}sudo systemctl enable hass-agent${NC}"
}

# Create desktop entry for GUI
create_desktop_entry() {
    echo -e "\n${BLUE}Creating desktop entry...${NC}"
    
    if [ -f "$INSTALL_DIR/HASS.Agent.Avalonia" ]; then
        $SUDO_CMD tee "$DESKTOP_FILE" > /dev/null << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=HASS.Agent
GenericName=Home Assistant Companion
Comment=Control and monitor your system from Home Assistant
Icon=$INSTALL_DIR/hass-agent-icon.png
Exec=$INSTALL_DIR/HASS.Agent.Avalonia
Terminal=false
Categories=Utility;System;
Keywords=home;assistant;smart;home;automation;
StartupNotify=true
StartupWMClass=HASS.Agent.Avalonia
EOF
        
        echo -e "${GREEN}✓ Desktop entry created${NC}"
    else
        echo -e "${YELLOW}⚠ GUI not found, skipping desktop entry${NC}"
    fi
}

# Create default configuration
create_default_config() {
    echo -e "\n${BLUE}Creating default configuration...${NC}"
    
    if [ ! -f "$CONFIG_DIR/appsettings.json" ]; then
        cat > "$CONFIG_DIR/appsettings.json" << EOF
{
  "DeviceName": "$(hostname)",
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
        echo -e "${GREEN}✓ Default configuration created at $CONFIG_DIR/appsettings.json${NC}"
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
    echo -e "  ${BLUE}Application:${NC}  $INSTALL_DIR"
    echo -e "  ${BLUE}Configuration:${NC} $CONFIG_DIR"
    echo -e "  ${BLUE}Logs:${NC}         $LOG_DIR"
    echo ""
    echo -e "Quick Start:"
    echo -e "  ${YELLOW}1.${NC} Edit configuration: ${GREEN}nano $CONFIG_DIR/appsettings.json${NC}"
    echo -e "  ${YELLOW}2.${NC} Start headless:     ${GREEN}hass-agent${NC}"
    echo -e "  ${YELLOW}3.${NC} Start GUI:          ${GREEN}hass-agent-gui${NC}"
    echo -e "  ${YELLOW}4.${NC} Enable on boot:     ${GREEN}sudo systemctl enable --now hass-agent${NC}"
    echo ""
    echo -e "Web Interface: ${BLUE}http://localhost:11111${NC}"
    echo ""
    echo -e "For more information: ${BLUE}https://hassagent.readthedocs.io/${NC}"
}

# Uninstall function
uninstall() {
    echo -e "${YELLOW}Uninstalling HASS.Agent...${NC}"
    
    $SUDO_CMD systemctl stop hass-agent 2>/dev/null || true
    $SUDO_CMD systemctl disable hass-agent 2>/dev/null || true
    $SUDO_CMD rm -f "$SYSTEMD_SERVICE"
    $SUDO_CMD rm -f "$DESKTOP_FILE"
    $SUDO_CMD rm -f /usr/local/bin/hass-agent
    $SUDO_CMD rm -f /usr/local/bin/hass-agent-gui
    $SUDO_CMD rm -rf "$INSTALL_DIR"
    
    echo -e "${GREEN}✓ HASS.Agent uninstalled${NC}"
    echo -e "${YELLOW}Note: Configuration at $CONFIG_DIR was preserved${NC}"
    echo -e "To remove config: ${RED}rm -rf $CONFIG_DIR${NC}"
}

# Main installation flow
main() {
    case "${1:-install}" in
        install)
            check_root
            detect_distro
            install_dotnet
            install_dependencies
            create_directories
            install_application
            create_systemd_service
            create_desktop_entry
            create_default_config
            print_completion
            ;;
        uninstall)
            check_root
            uninstall
            ;;
        *)
            echo "Usage: $0 {install|uninstall}"
            exit 1
            ;;
    esac
}

main "$@"
