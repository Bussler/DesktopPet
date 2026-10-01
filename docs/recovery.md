# Configuration and session recovery

Hood stores `config.yaml` and `state.json` in `%LOCALAPPDATA%\HoodDesktopPet`, separately from the installation and runtime files.

## Configuration errors

If configuration is missing, Hood writes the documented defaults. If YAML is malformed, contains unsupported keys or values, or cannot be read, Hood leaves the file untouched and shows its path and correction guidance. Close Hood before editing the file, then relaunch. A Settings save is applied only after the YAML replacement succeeds; if startup registration or saving fails, the previous configuration remains active where integration rollback succeeds, and any possible startup mismatch is named in the error.

## Damaged session state

If Hood cannot restore `state.json`, it keeps the file and waits for a choice. **Keep file and exit** preserves it without starting a fresh session. To recover, check the acknowledgement box and choose **Reset saved session**; Hood archives the damaged file before starting with fresh needs and placement. If archiving fails, Hood reports the failure and leaves the original file in place.

Session needs and remaining timers advance only while Hood is open and active. Closed time is not added on relaunch. Clean Quit saves the current session through replacement writes.
