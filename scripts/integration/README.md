Integration scripts for headless import testing

Scripts:
- import_filepath_test.sh: quick test that posts legacy files by file path.
- import_filepath_debug.sh: debug variant that leaves temp files and prints logs.
- complete_import_test.sh: end-to-end test creating temp legacy files and verifying import.

Run:

```bash
chmod +x scripts/integration/*.sh
scripts/integration/complete_import_test.sh
```

The scripts start the SimpleHeadless server, call `/import/legacy`, then verify `/commands`.
