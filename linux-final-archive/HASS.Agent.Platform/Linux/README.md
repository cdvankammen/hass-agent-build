Linux platform adapters

This folder contains platform-specific adapters for Linux (Debian-target). Current placeholders:

- Notification: uses `notify-send` as a simple fallback (requires libnotify-bin)
- Media: MPRIS placeholder (to be implemented using Tmds.DBus or dbus-sharp)
- Bluetooth: BlueZ placeholder (to be implemented using BlueZ D-Bus APIs)

Next steps:
- Implement MPRIS interactions using `Tmds.DBus` or `Tmds.DBus.Client`
- Implement BlueZ scanning using DBus to query `org.bluez`
- Provide dependency injection hooks in core to use these adapters when running on Linux
