#!/usr/bin/env bash
set -euo pipefail
TMP=/tmp/legacy_conv_test
mkdir -p "$TMP"
python3 - <<PY > "$TMP/commands.json"
import json
data = [
  {"Id":"33333333-3333-3333-3333-333333333333","Name":"SampleLegacy","Type":"CustomCommand","Command":"echo sample","EntityType":"Switch"}
]
print(json.dumps(data))
PY
python3 tools/legacy_to_new.py "$TMP/commands.json" "$TMP/out.json"
echo converted file:
sed -n '1,200p' "$TMP/out.json"
