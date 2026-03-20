#!/usr/bin/env bash
set -euo pipefail

OUTDIR="dist"
PKGNAME="hass-agent"
VERSION="0.1.0"
ARCH="amd64"
WORKDIR="$OUTDIR/${PKGNAME}_${VERSION}_${ARCH}"

rm -rf "$WORKDIR"
mkdir -p "$OUTDIR"
mkdir -p "$WORKDIR/opt/hass-agent"
mkdir -p "$WORKDIR/etc/systemd/system"
mkdir -p "$WORKDIR/DEBIAN"
mkdir -p "$WORKDIR/opt/hass-agent/config"

# produce a clean publish output for the headless project and copy that (avoids Windows UI files)
PUBLISH_DIR="artifacts/publish/hass-agent-headless"
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"
if dotnet --version >/dev/null 2>&1; then
  dotnet publish src/HASS.Agent.Headless -c Release -r linux-x64 --no-self-contained -o "$PUBLISH_DIR" || true
  cp -r "$PUBLISH_DIR"/* "$WORKDIR/opt/hass-agent/" || true
else
  # fallback: copy any existing build output
  if [ -d "src/HASS.Agent.Headless/bin/Release/net10.0" ]; then
    cp -r src/HASS.Agent.Headless/bin/Release/net10.0/* "$WORKDIR/opt/hass-agent/" || true
  fi
fi
if [ -f "deploy/hass-agent.service" ]; then
  cp deploy/hass-agent.service "$WORKDIR/etc/systemd/system/" || true
fi
if [ -f "deploy/DEBIAN/control" ]; then
  cp deploy/DEBIAN/control "$WORKDIR/DEBIAN/control" || true
fi
if [ -d "deploy/opt/hass-agent/config" ]; then
  cp -r deploy/opt/hass-agent/config/* "$WORKDIR/opt/hass-agent/config/" || true
fi
if [ -f "deploy/DEBIAN/postinst" ]; then
  cp deploy/DEBIAN/postinst "$WORKDIR/DEBIAN/postinst" || true
  chmod 755 "$WORKDIR/DEBIAN/postinst" || true
fi

if command -v dpkg-deb >/dev/null 2>&1; then
  dpkg-deb --build "$WORKDIR" "$OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.deb"
  echo "Created $OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.deb"
else
  # Manual .deb creation (ar archive) when dpkg-deb is not available
  TMPDIR=$(mktemp -d)
  pushd "$TMPDIR" >/dev/null

  # create data.tar.gz containing etc/ and opt/ (preserve paths)
  tar -C "$WORKDIR" -czf data.tar.gz ./opt ./etc || tar -C "$WORKDIR" -czf data.tar.gz opt etc

  # create control.tar.gz containing control file at ./control
  mkdir -p control
  cp "$WORKDIR/DEBIAN/control" control/control
  tar -czf control.tar.gz -C control control

  printf "2.0\n" > debian-binary

  # create final ar archive
  if command -v ar >/dev/null 2>&1; then
    ar rcs "$OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.deb" debian-binary control.tar.gz data.tar.gz
    echo "Created $OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.deb (manual)"
  else
    # fallback to tarball if 'ar' is also unavailable
    tar -C "$OUTDIR" -czf "$OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.tar.gz" "${PKGNAME}_${VERSION}_${ARCH}"
    echo "Neither dpkg-deb nor ar found; created tarball: $OUTDIR/${PKGNAME}_${VERSION}_${ARCH}.tar.gz"
  fi

  popd >/dev/null
  rm -rf "$TMPDIR"
fi
