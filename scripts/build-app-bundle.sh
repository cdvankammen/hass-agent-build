#!/usr/bin/env bash
# macOS .app Bundle Builder for HASS Agent Desktop
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION="${1:-1.0.0}"
BUNDLE_NAME="HASS Agent"
BUNDLE_ID="com.hass-agent.desktop"
APP_NAME="${BUNDLE_NAME}.app"
OUTPUT_DIR="$PROJECT_ROOT/dist/macos"

echo "Building macOS .app bundle for HASS Agent Desktop v${VERSION}..."

# Create bundle structure
rm -rf "$OUTPUT_DIR/$APP_NAME"
mkdir -p "$OUTPUT_DIR/$APP_NAME/Contents/MacOS"
mkdir -p "$OUTPUT_DIR/$APP_NAME/Contents/Resources"

# Build the application for macOS
echo "Building application for macOS..."

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
  RID="osx-arm64"
else
  RID="osx-x64"
fi

# Publish the desktop app
dotnet publish "$PROJECT_ROOT/src/HASS.Agent.Desktop" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -o "$OUTPUT_DIR/publish" \
  /p:PublishSingleFile=true \
  /p:EnableCompressionInSingleFile=true

# Copy executable and resources
if [ -f "$OUTPUT_DIR/publish/HASS.Agent.Desktop" ]; then
  cp "$OUTPUT_DIR/publish/HASS.Agent.Desktop" "$OUTPUT_DIR/$APP_NAME/Contents/MacOS/"
  chmod +x "$OUTPUT_DIR/$APP_NAME/Contents/MacOS/HASS.Agent.Desktop"
fi

# Copy any additional files
for f in "$OUTPUT_DIR/publish/"*.dll "$OUTPUT_DIR/publish/"*.json "$OUTPUT_DIR/publish/"*.dylib 2>/dev/null; do
  [ -f "$f" ] && cp "$f" "$OUTPUT_DIR/$APP_NAME/Contents/MacOS/" || true
done

# Create Info.plist
cat > "$OUTPUT_DIR/$APP_NAME/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>HASS.Agent.Desktop</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${BUNDLE_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2024 HASS Agent. MIT License.</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>CFBundleDocumentTypes</key>
    <array/>
    <key>LSUIElement</key>
    <false/>
</dict>
</plist>
EOF

# Create PkgInfo
echo "APPL????" > "$OUTPUT_DIR/$APP_NAME/Contents/PkgInfo"

# Create icns icon (if iconutil is available)
if command -v iconutil &> /dev/null; then
  ICONSET_DIR="$OUTPUT_DIR/AppIcon.iconset"
  mkdir -p "$ICONSET_DIR"
  
  # Check if we have a source icon
  if [ -f "$PROJECT_ROOT/images/icon.png" ]; then
    echo "Creating .icns from icon.png..."
    # Use sips to create different sizes
    for SIZE in 16 32 64 128 256 512 1024; do
      sips -z $SIZE $SIZE "$PROJECT_ROOT/images/icon.png" --out "$ICONSET_DIR/icon_${SIZE}x${SIZE}.png" 2>/dev/null || true
    done
    # Create @2x versions
    for SIZE in 16 32 128 256 512; do
      DOUBLE=$((SIZE * 2))
      sips -z $DOUBLE $DOUBLE "$PROJECT_ROOT/images/icon.png" --out "$ICONSET_DIR/icon_${SIZE}x${SIZE}@2x.png" 2>/dev/null || true
    done
    iconutil -c icns "$ICONSET_DIR" -o "$OUTPUT_DIR/$APP_NAME/Contents/Resources/AppIcon.icns" 2>/dev/null || true
  else
    echo "Warning: No source icon found at images/icon.png"
  fi
  rm -rf "$ICONSET_DIR"
fi

# Sign the app if codesign is available and we have an identity
if command -v codesign &> /dev/null; then
  # Check for signing identity
  if [ -n "${MACOS_SIGNING_IDENTITY:-}" ]; then
    echo "Signing application with identity: $MACOS_SIGNING_IDENTITY"
    codesign --force --deep --sign "$MACOS_SIGNING_IDENTITY" \
      --options runtime \
      --entitlements "$SCRIPT_DIR/entitlements.plist" \
      "$OUTPUT_DIR/$APP_NAME" 2>/dev/null || echo "Warning: Code signing failed"
  else
    # Ad-hoc sign for local development
    echo "Ad-hoc signing application..."
    codesign --force --deep --sign - "$OUTPUT_DIR/$APP_NAME" 2>/dev/null || echo "Warning: Ad-hoc signing failed"
  fi
fi

# Verify the bundle
echo ""
echo "Verifying bundle structure..."
if [ -f "$OUTPUT_DIR/$APP_NAME/Contents/MacOS/HASS.Agent.Desktop" ]; then
  echo "✓ Executable exists"
else
  echo "✗ Executable missing!"
fi

if [ -f "$OUTPUT_DIR/$APP_NAME/Contents/Info.plist" ]; then
  echo "✓ Info.plist exists"
else
  echo "✗ Info.plist missing!"
fi

echo ""
echo "Bundle created at: $OUTPUT_DIR/$APP_NAME"
echo ""
echo "To create a DMG, run:"
echo "  ./scripts/build-dmg.sh"
