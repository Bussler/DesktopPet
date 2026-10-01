# Ticket 10: random reaction manual acceptance

Run Hood on Windows with the Release build. For a quicker check, temporarily set both `random_interval` bounds in `config.yaml` to the same short active duration, such as `0.1` minutes, then restore the original values afterward.

- Enable random reactions, turn idle animations off, and leave roaming off. Confirm Hood still plays occasional wave/look/hop reactions while his resting pose remains still.
- Disable random reactions in Settings. Confirm a reaction already playing can finish, then no new one starts while the toggle remains off.
- Re-enable random reactions and confirm they resume without an immediate burst or a backlog of missed events.
- During a countdown, drag Hood, open the roaming-area editor, start a nap, and use Feed or click to wake. Confirm those actions pause ambient reactions and the countdown resumes afterward.
- Reduce the roaming area to its minimum supported size and confirm each reaction stays within Hood's area.
- Restart after a partial countdown. Confirm closed time does not consume the remaining wait.
- Restore the random interval bounds to the documented defaults when finished.
