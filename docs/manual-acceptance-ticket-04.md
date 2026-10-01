# Ticket 04: session recovery manual acceptance

Run the Windows app with a malformed or unsupported session file at the path shown in the recovery window.

- Confirm the message identifies the state file and offers **Keep file and exit**.
- Confirm **Reset saved session** remains disabled until the acknowledgement box is checked.
- Choose **Keep file and exit** and verify Hood exits without changing the original file.
- Relaunch, acknowledge the reset, and verify the original file is archived beside the new state file before Hood starts with fresh state.
- Confirm a valid saved session resumes with its saved placement and behavior, without advancing needs or timers for the time Hood was closed.
