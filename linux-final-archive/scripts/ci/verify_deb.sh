#!/usr/bin/env bash
set -euo pipefail

PKG_DIR="dist"
PKG_NAME_PATTERN="hass-agent_*.deb"

echo "Looking for deb packages in $PKG_DIR"
cd "$PKG_DIR"

PKG=$(ls $PKG_NAME_PATTERN 2>/dev/null | head -n1 || true)
if [ -z "$PKG" ]; then
  echo "No .deb found matching $PKG_NAME_PATTERN"
  exit 2
fi

echo "Verifying package: $PKG"

if command -v dpkg-deb >/dev/null 2>&1; then
  echo "dpkg-deb available: showing control info"
  dpkg-deb -I "$PKG"
  echo "Package contents (first 50 lines):"
  dpkg-deb -c "$PKG" | sed -n '1,50p'
else
  echo "dpkg-deb not available; attempting to inspect using ar/tar"
  tmpd=$(mktemp -d)
  ar x "$PKG" -C "$tmpd" || true
  ls -la "$tmpd" || true
fi

if [ -f checksums.sha256 ]; then
  echo "Verifying checksums"
  sha256sum -c checksums.sha256 --quiet && echo "Checksums OK" || (echo "Checksum verification failed" && exit 1)
else
  echo "No checksums.sha256 found in dist/; creating one"
  sha256sum * > checksums.sha256
  echo "Created checksums.sha256"
fi

echo "Verification finished"
