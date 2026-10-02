# Hood configuration

Hood creates `%LOCALAPPDATA%\HoodDesktopPet\config.yaml` on first launch. The file is the single source of truth for settings. Edit it while Hood is closed; valid edits load on the next launch. Invalid YAML is left untouched, and Hood shows the file location and correction/relaunch guidance instead of starting with partial settings.

The file uses YAML schema version 1. Unknown or duplicate keys are errors. All numeric values must be finite. Rates and gains accept zero or greater; durations and dimensions must be greater than zero.

The configured initial area is adjusted when necessary to contain Hood's full frame, and oversized saved areas shrink to fit the usable display while preserving that minimum footprint. See [placement behavior](placement.md) for details.

With roaming enabled, Hood moves in 8-second bouts followed by 32-second idle rests so the resting animation remains visible. The cadence uses active behavior time and pauses while roaming is blocked by sleep, dragging, editing, or a foreground action.

```yaml
schema_version: 1
behavior:
  roaming: true
  idle_animations: true
  random_events: true
  always_on_top: true
  start_with_windows: false
needs:
  hunger_points_per_hour: 12.5
  boredom_points_per_hour: 16
  sleep_points_per_hour: 20
  feed_gain: 40
  movement_relief_per_second: 0.5
  stationary_click_relief: 10
sleep:
  automatic_nap_threshold: 80
  duration_minutes: 5
  early_wake_grace_minutes: 15
random_interval:
  minimum_minutes: 1
  maximum_minutes: 5
initial_area:
  width: 480
  height: 180
  bottom_margin: 16
```

| Setting | Units | Valid bounds |
| --- | --- | --- |
| `schema_version` | Version | Must be `1`. |
| `behavior.roaming`, `idle_animations`, `random_events`, `always_on_top`, `start_with_windows` | Boolean | `true` or `false`; each option is independent. |
| `needs.hunger_points_per_hour`, `boredom_points_per_hour`, `sleep_points_per_hour` | Need points per awake active hour | Finite and at least `0`. |
| `needs.feed_gain` | Hunger points per Feed action | Finite and at least `0`. |
| `needs.movement_relief_per_second` | Boredom points per second of movement | Finite and at least `0`. |
| `needs.stationary_click_relief` | Boredom points per completed stationary click | Finite and at least `0`. |
| `sleep.automatic_nap_threshold` | Need points | Greater than `0` and at most `100`. |
| `sleep.duration_minutes`, `sleep.early_wake_grace_minutes` | Active minutes | Finite and greater than `0`. |
| `random_interval.minimum_minutes`, `maximum_minutes` | Active awake minutes | Finite, each greater than `0`, and minimum no greater than maximum. Defaults: `1` and `5`. |
| `initial_area.width`, `height` | Logical units | Finite and greater than `0`. |
| `initial_area.bottom_margin` | Logical units | Finite and at least `0`. |

Need values start at zero in a new session. The settings window exposes roaming, idle animations, random reactions, their minimum and maximum wait in minutes, always-on-top, and the opt-in per-user Windows startup setting; each change saves back to this same file before taking effect. Reaction timing accepts positive fractional minutes, with minimum no greater than maximum; equal values give a fixed wait. Saving a changed range starts a fresh countdown within that range. If a save fails, Hood keeps the prior effective settings and reports the affected setting. If Windows startup registration cannot be rolled back, Hood names the possible mismatch. Windows startup uses the current user's `Run` registry key and is reconciled on launch. Random reactions choose a wave, look around (left/right), look up, or hop every 1–5 active awake minutes by default, at the same rate while idle or roaming. This also applies with idle animations disabled; the independent random-events toggle controls these reactions. The countdown pauses during sleep, drag/edit interactions, and other reactions. Closed time does not consume the saved interval; on relaunch, any saved wait exceeding the configured maximum is shortened to that maximum. Existing configuration files retain their explicit values; set the minimum and maximum in Settings or YAML to adopt the new defaults. Need rates and nap timing remain editable here in YAML. See [recovery guidance](recovery.md) for invalid configuration and saved-session recovery.
