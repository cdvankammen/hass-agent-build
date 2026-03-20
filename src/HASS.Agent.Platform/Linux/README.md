Linux platform adapters

This folder contains platform-specific adapters for Linux (Debian-target):

- Notification: uses `notify-send` as a simple fallback when a desktop notifier is available.
- Media: MPRIS play/pause support via `Tmds.DBus`.
- Bluetooth: paired device listing, connect/disconnect, and scan start/stop via BlueZ/bluetoothctl.

Next steps:
- Expand MPRIS support beyond play/pause as more media actions are needed.
- Expose richer BlueZ adapter data if the UI needs adapter or discovery details.
- Keep the platform adapters behind the shared core interfaces so Linux-specific code stays isolated.
