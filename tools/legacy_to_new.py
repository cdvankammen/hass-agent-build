#!/usr/bin/env python3
import json
import sys
from pathlib import Path

def convert_command(c):
    return {
        "id": str(c.get("Id") or c.get("id") or c.get("Id")),
        "name": c.get("Name") or c.get("name"),
        "entityType": c.get("EntityType") or c.get("entityType") or c.get("Type"),
        "state": "OFF",
        "command": (c.get("Command") or c.get("command") or "") + (" " + c.get("Args") if c.get("Args") else ""),
        "keyCode": c.get("KeyCode") or c.get("keyCode") or "",
        "keys": c.get("Keys") or c.get("keys") or [],
        "runAsLowIntegrity": c.get("RunAsLowIntegrity") or c.get("runAsLowIntegrity") or False
    }

def main():
    if len(sys.argv) < 3:
        print("usage: legacy_to_new.py <in_commands.json> <out_commands.json>")
        sys.exit(2)

    inp = Path(sys.argv[1])
    out = Path(sys.argv[2])
    if not inp.exists():
        print("input not found", inp)
        sys.exit(2)

    data = json.loads(inp.read_text())
    converted = [convert_command(c) for c in data]
    out.write_text(json.dumps(converted, indent=2))
    print(f"wrote {len(converted)} commands to {out}")

if __name__ == '__main__':
    main()
