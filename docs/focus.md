# Focus and breaks

Open **Focus** from the pet's right-click menu or the tray menu. Both restore the same timer, including when pet sprites are hidden.

The compact window keeps the countdown and one main action visible. That button changes between **Start focus**, **Pause**, **Resume**, and **Take break**. **Stop** appears during a cycle; **Skip break** appears only during a running or paused break. Expand **Options** for durations, always-on-top and Windows notification settings.

1. Under **Options**, choose focus and break lengths in whole minutes (1–1440). Defaults are **25/5**. Click **Start focus**; Hood keeps behaving normally.
2. At zero, a soft Little Ripple chime plays once and Hood waves continuously. Click him to stop the wave and bring Focus forward without toggling Notes. Click **Take break** to begin your full break; you can also use that button directly.
3. Hood displays his sleep animation throughout the break. Clicking him brings Focus forward and shows/hides Notes without waking him, including while the break is paused or finished. This appearance does not grant sleep recovery; ordinary pet care still works underneath it.
4. At zero, the chime plays once and Focus appears with **Break finished**. Hood stays asleep until you manually **Start focus** again. Repeat for as many rounds as you like. Neither timer starts automatically.

**Pause/Resume** freezes/continues either countdown. **Skip break** finishes the break early and waits for Start focus, keeping Hood visually asleep. **Stop** returns to idle and removes timer animations. Closing Focus also stops the cycle; minimizing keeps it running. Only natural countdown completion sounds; Pause, Stop, Skip break and closing stay silent. The built-in chime follows your Windows volume and Hood's app volume in the volume mixer.

**Always on top** affects only Focus. Durations, this choice and the notification option remain available when you close/reopen Focus during the app run. Editing durations affects future intervals, never the current countdown. Restarting Hood restores 25/5, always-on-top off and automatic Do not disturb off. Computer sleep discards the cycle; it does not resume on wake. No Focus history or preferences are saved to disk.

## Notifications

Under **Options**, enable **Do not disturb during focus**, then click **Start focus**. Hood automatically enables Windows Do not disturb, including while the timer is minimized. **Pause**, focus completion, **Stop**, closing Focus, computer sleep and orderly app exit restore the previous setting. **Resume** enables Do not disturb again. Breaks do not enable it. Unchecking the option restores the previous setting immediately.

If Do not disturb was already enabled, Hood leaves it enabled, including after focus ends. A different notification setting selected outside Hood is preserved on cleanup. Windows exceptions for priority apps, calls, reminders and alarms still apply; Hood does not edit them, change Teams/Discord presence, mute audio or block applications.

The option starts unchecked and is remembered until Hood exits. Automatic control uses an undocumented Windows interface, so compatibility can change after Windows updates. Any enable/restore failure is displayed in Focus even with Options collapsed; the timer remains usable. A failed restoration is retried on cleanup and reported when closing. For manual recovery, open Windows **Settings > System > Notifications**. Abrupt termination or a crash can leave Do not disturb enabled; this option has no crash recovery.
