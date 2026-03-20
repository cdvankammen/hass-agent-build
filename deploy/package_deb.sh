#!/usr/bin/env bash
set -euo pipefail

# Minimal Debian packaging helper (creates a simple directory structure and tarball)
OUTDIR="dist"
PKGNAME="hass-agent-linux"
VERSION="0.1.0"
mkdir -p "$OUTDIR/$PKGNAME-$VERSION/opt/hass-agent"
mkdir -p "$OUTDIR/$PKGNAME-$VERSION/etc/systemd/system"

# Copy headless binary
cp -r src/HASS.Agent.Headless/bin/Release/net10.0/* "$OUTDIR/$PKGNAME-$VERSION/opt/hass-agent/" || true

# Use the provided systemd unit if exists
if [ -f "deploy/hass-agent.service" ]; then
  cp deploy/hass-agent.service "$OUTDIR/$PKGNAME-$VERSION/etc/systemd/system/"
fi

tar -C "$OUTDIR" -czf "$OUTDIR/${PKGNAME}_${VERSION}.tar.gz" "$PKGNAME-$VERSION"
echo "Created $OUTDIR/${PKGNAME}_${VERSION}.tar.gz"
