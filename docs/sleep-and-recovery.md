# Sleep and recovery

Hood's sleep need increases only during an open, awake session. Reaching the configured threshold starts a nap after any direct action finishes. **Sleep** in Hood's right-click menu starts a manual nap at any time.

The nap duration comes from `sleep.duration_minutes` when the nap starts. Sleep need recovers evenly over that duration; hunger and boredom do not change during the nap. Clean Quit saves the remaining nap and its recovery rate, so reopening resumes the same nap even if the configured duration has since changed. Time while Hood is closed or Windows is suspended does not advance needs or timers.

A left-click during a nap plays the wake animation and uses that click only to wake Hood. **Feed** wakes and feeds in one action. Both preserve recovery already earned and start the configured early-wake grace period, during which automatic naps are suppressed. Manual Sleep remains available during grace.

An awake session's needs resume at the saved values. If a state file cannot be restored, follow the [session recovery guidance](recovery.md); Hood preserves the original file when archiving it.

[Focus breaks](focus.md) use a separate visual sleep animation. They do not start a care nap or award sleep recovery. During a break, its pause, or the wait after it finishes, clicking Hood restores Focus and shows/hides Notes without waking him. Normal care still applies underneath the appearance. Starting focus, Stop, or closing Focus clears the override. Unlike care timers, the Focus cycle is discarded on computer sleep and never saved across app restarts.
