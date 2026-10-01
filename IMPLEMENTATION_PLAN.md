# Desktop Pet implementation plan

Source of truth: [SPEC.md](SPEC.md). This plan covers the first Windows release featuring Hood. It defines implementation work; creating this plan does not begin that work or create tickets.

## Technical approach

Use **C# and .NET 10 LTS**, a **WPF Windows app**, and a **portable application-behavior library**. .NET 10 is an actively supported LTS release. WPF supports transparent borderless windows, and its Fluent styles provide a starting point for the requested polished settings UI; their remaining limitations must be evaluated during the first milestone. This is the recommended implementation choice, to be recorded in a short ADR after the initial desktop feasibility check. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [WPF transparency](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0), [WPF Fluent styling](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100).

Keep the solution to three projects initially:

| Project | Responsibility |
| --- | --- |
| DesktopPet.Core | Application/session commands, needs, action coordination, scheduling, placement policy, configuration, state persistence, and pet definitions; no WPF or Windows API references |
| DesktopPet.Windows | Pet rendering, mouse interaction, roaming-area editor, menus, settings, tray, monitor/DPI discovery, startup registration, and suspension handling |
| DesktopPet.Tests | Deterministic application-behavior scenarios against the same session contract used by the UI |

Use YamlDotNet for YAML parsing/serialization, built-in JSON serialization for saved state and the small pet definition, and MSTest for behavior scenarios. Pin versions when scaffolding. Use a Windows Forms `NotifyIcon` from the Windows project for tray integration rather than adding a tray framework. These capabilities are documented by their maintainers. [YamlDotNet](https://github.com/aaubry/yamldotnet), [MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-mstest), [NotifyIcon](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0).

Compose dependencies explicitly at startup. Introduce only the concrete boundaries needed for controlled time, randomness, local storage, and Windows integration; avoid a generic service framework, event bus, or plugin architecture. Thin UI commands call the session and render its presentation. A later phone app would reuse behavior and data contracts while providing a new frontend and platform integration; this plan does not assume WPF screens can be ported unchanged.

**Prerequisites verified during planning:** the machine has .NET SDKs 5.0.416 and 8.0.101, but no .NET 10 SDK. Install the .NET 10 SDK when implementation starts. The Hood metadata and both recorded sprite sources are present locally. Import and package the required assets so runtime behavior does not depend on the local Codex installation.

## Milestones and dependencies

Each milestone should produce a runnable build or a directly usable asset deliverable, with behavior tests added alongside the relevant functionality. Do not defer all testing or desktop feasibility discovery to the final milestone.

| Milestone | Deliverable | Depends on |
| --- | --- | --- |
| 1 | Hood visible on the desktop, basic reaction, tray, and clean exit | SDK available; existing Hood art |
| 2 | YAML configuration and resumable local session | 1 |
| 3 | Complete first-release Hood animation set and validated mappings | 1; stable action names and asset contract |
| 4 | Working care actions, needs, naps, and waking | 2 and 3 |
| 5 | Editable roaming area, dragging, and monitor placement | 2 and 4 |
| 6 | Gentle roaming, random events, and independent quiet controls | 3, 4, and 5 |
| 7 | Polished settings, full menus, and Windows startup controls | 2, 5, and 6 |
| 8 | Packaged first-release candidate and acceptance handoff | 1–7 |

Animation preparation can proceed after milestone 1 while configuration is developed. It remains a dependency of the first fully functional care build. Early internal builds may expose only the actions for which art is ready; the release must contain every required dedicated animation.

### 1. Establish a working desktop slice

**Work**

- Install the chosen SDK, scaffold the three projects, and make restore/build/behavior-test commands reproducible.
- Inspect Hood's actual sprite layout. Define a small action-to-animation manifest containing frame regions, playback durations, loop behavior, and visible motion bounds. Use packaged PNG frames/sheets for runtime rendering and retain the source artwork.
- Establish the session boundary with commands for reacting and quitting, a static presentation, and a first-run placement policy. Add controlled elapsed-time and random inputs without implementing the full care model yet.
- Render Hood in a transparent borderless window near the primary monitor's bottom work-area edge. Avoid intercepting desktop input across an invisible roaming rectangle.
- Play an existing jump on an awake left-click. Add a tray icon with Quit and ensure the process exits cleanly.
- Provide an actionable error for missing/unusable baseline assets.
- Record a concise ADR with context, alternatives, the selected stack, and the trade-off that the Windows frontend will be replaced for mobile. Compare WPF against WinUI and a shared desktop/mobile frontend only to the extent needed to confirm the requirements can be met.

**Verification and completion**

- Behavior tests cover initial presentation, baseline asset errors, reaction completion, and quit requests at the application boundary.
- Provide the user with an early build and short manual checklist for transparency, click handling, tray exit, and focus behavior. The implementation team does not automate these Windows checks.
- Completion means the desktop rendering approach works and the application/test contract is real, rather than a separate simulation.

### 2. Add configuration and local continuity

**Work**

- Define version-one typed configuration for independent toggles, care rates/gains, sleep threshold and duration, early-wake grace, random interval bounds, and initial area settings. Use all default values already specified in SPEC.md.
- Store configuration as `config.yaml` and runtime state as `state.json` under per-user application data. Keep the location discoverable through Settings when that screen is added.
- Generate a documented default YAML file on first launch. Use safe parsing and explicit validation, including duplicates, unknown keys, schema versions, types, finite numbers, bounds, and interval ordering.
- Define a versioned session snapshot that includes needs, current mode, the context needed to continue a nap, remaining nap/random/grace time, Hood's placement, and roaming bounds.
- Use replacement writes and save at meaningful changes plus clean exit. A failed settings save must retain the previous effective configuration and report an error.
- Restore from saved state without counting closed time. Persist remaining durations rather than deriving them from wall-clock timestamps. Preserve the remaining duration of an existing nap when new configuration is loaded; changed duration settings apply to subsequent naps.
- Keep invalid YAML intact and show a correction/relaunch error instead of starting pet behavior with partially accepted settings. Keep invalid state intact and require an acknowledged reset before starting a fresh session.
- Connect configuration loading and session restore to the runnable Windows app, using a simple error screen before the final UI polish.

**Verification and completion**

- Test first launch, valid manual edits on relaunch, independent field preservation, unsupported versions, malformed/duplicate/unknown values, numeric bounds, failed writes, and damaged-state recovery.
- Use real YAML parsing and temporary files for normal persistence scenarios; supply failing storage only for failure-path scenarios.
- Test exit/relaunch across arbitrary wall-clock changes with unchanged needs and remaining timers.
- Completion means there is one authoritative settings file and one resumable local state contract, with no second UI settings store.

### 3. Complete Hood's first-release animations

**Work**

- Inspect existing idle, run-left, run-right, wave, and jump artwork and retain suitable sequences.
- Create missing look-up, look-around, feeding, sleeping, and waking sequences in Hood's established style. Do not generate assets for deferred dance/search behavior.
- Keep character identity, transparency, alignment, apparent size, and frame placement consistent. Make idle motion restrained.
- Establish action mappings and playback completion rules. Include each action's full visible footprint so roaming-area validation can contain hops and other motion.
- Validate required mappings, image availability, frame regions, and usable durations during application loading.
- Integrate the completed animation set into the desktop build and prepare a concise preview for manual visual review.

**Verification and completion**

- Application loading tests fail clearly on incomplete mappings or unusable assets and accept the complete first-release definition.
- The user reviews style consistency, transparency, action readability, and idle subtlety visually. Do not add screenshot-diff or native UI automation.
- Completion requires dedicated art for all enabled first-release actions; unrelated reaction substitutions and a static Zzz indicator do not count.

### 4. Deliver the care-and-sleep interaction loop

**Work**

- Implement bounded hunger, boredom, and sleep need progression using elapsed active-session time and the configuration values.
- Add Feed and Sleep to the pet menu and display needs there without permanent desktop bars.
- Implement feeding, automatic threshold-triggered naps, manual sleep, gradual nap recovery, waking, and early-wake grace.
- Apply the specified action rules: awake clicks react; sleeping clicks wake without also jumping; feeding while asleep wakes and feeds; dragging need not wake Hood; foreground actions take priority over ambient behavior.
- Bound repeated input handling so animations do not accumulate in a queue. Return to the appropriate presentation when an action completes.
- Store the full continuing state so an interrupted nap or grace period resumes correctly after relaunch.
- Exclude system suspension from active time: the Windows adapter resets the elapsed-time baseline on resume rather than sending suspended time to the core.

**Verification and completion**

- Advance controlled time through the default rates and custom rates; verify bounds and absence of punitive effects.
- Test automatic/manual naps, recovery, wake consumption, grace, feeding during sleep, repeated input, and restart partway through a nap.
- Test equal total active time using different advance increments around thresholds, so behavior is not accidentally dependent on rendering frame rate.
- Completion means the user can care for Hood and observe the entire sleep/wake loop in a runnable build, backed by deterministic behavior tests.

### 5. Implement placement, dragging, and the area editor

**Work**

- Represent work areas and monitor scale information as supplied display descriptions; keep placement calculations independent of Windows APIs.
- Derive minimum area dimensions from the pet/motion footprints. Enforce valid resizing and clamp oversized saved bounds for smaller work areas.
- Add Edit roaming area with a visible outline and move/resize controls. Hide it during normal use.
- Distinguish a click from a drag before dispatching the session command. Translating Hood must translate the area by the same offset, preserving relative placement.
- Allow free horizontal/vertical placement after the initial bottom-edge position. Keep dragging available while roaming is off and while Hood is sleeping.
- Select the destination monitor from Hood's dragged position, constrain the final area to its work area, and preserve the pet/area relationship when clamping.
- Handle changed layouts and disconnected monitors by restoring a valid location on the primary monitor.
- Persist successful placement changes and expose the editor through the menu.

**Verification and completion**

- Exercise area commands through the session with synthetic displays: differing scales, negative coordinates, edges, undersized resize requests, oversized restore, cross-monitor drags, and monitor removal.
- Verify pet/area offsets, drag-versus-click dispatch outcomes, and persistence of freely moved placement.
- Provide the user a build for actual multi-monitor/DPI and area-editor checks. These checks remain manual.
- Completion means the area can be edited and moved with Hood without forcing it back to the bottom edge.

### 6. Add quiet ambient behavior

**Work**

- Implement gentle movement within the area and reduce boredom from actual movement time, not simply from the enabled setting.
- Reduce boredom for completed stationary left-click interactions while roaming is disabled.
- Schedule wave/look-around/hop reactions within the configured random interval range. Select only actions whose footprints fit the current area.
- Suspend ambient actions during sleep, dragging, editing, and foreground reactions. Resume without replaying a backlog.
- Expose roaming, idle animation, and random-event toggles independently in the initial settings view. Retain dedicated action animations when idle is disabled.
- Keep session progression separate from animation playback; static poses should not require unnecessary frame redraws or busy polling.

**Verification and completion**

- Test all combinations of the three behavior toggles and verify no implicit coupling.
- Test known random choices and interval endpoints, suspension/resumption, area-fit fallback, and direct-action precedence.
- Verify motion decreases boredom only when Hood actually moves and clicks provide relief when stationary.
- Completion means Hood can behave quietly or actively according to independent settings, with deterministic tests for the interaction combinations.

### 7. Finish the settings UI and Windows controls

**Work**

- Polish the existing settings view using a compact Fluent-inspired layout, consistent spacing, clear labels, readable contrast, visible focus, and restrained visual effects. Use built-in controls/templates before adding a UI dependency.
- Organize ordinary controls into Behavior, Placement, and App settings. Keep advanced rates documented in YAML; a dedicated editor for every advanced field is unnecessary.
- Provide a way to open the configuration location. Preserve other supported settings whenever the UI changes a field.
- Apply validated UI changes to the same YAML file and current session. Manual YAML edits remain startup-only.
- Complete tray/menu access to settings and quitting, needs/care controls, and roaming-area editing. Closing settings must leave the companion running.
- Apply always-on-top independently of other behavior settings.
- Add opt-in Windows startup, disabled by default, and removal when switched off. Coordinate registration and configuration saving so failures retain or restore the previous setting where possible; report any remaining mismatch rather than falsely showing success.
- Keep ordinary pet activity from stealing keyboard focus; let intentionally opened settings behave like a normal accessible window.

**Verification and completion**

- Behavior scenarios verify settings save/apply outcomes, preservation of advanced values, rejection/errors, and independent desired settings. Simulated integration failures verify user-visible error handling without testing Windows APIs.
- The user manually reviews appearance, keyboard operation, scaling, tray controls, focus, always-on-top, and startup enable/disable on Windows.
- Completion means the configuration system is convenient through both UI and YAML and the UI meets the requested modern, slick presentation.

### 8. Package and hand off the first release

**Work**

- Publish a self-contained Windows build with Hood's assets and dependency notices, initially targeting the user's machine architecture. A self-contained deployment bundles its runtime; release updates must therefore also keep the bundled runtime current. [Microsoft publishing guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
- Prefer a reproducible publish folder/archive for the first handoff. An installer, signing workflow, automatic updater, and wider architecture matrix are separate distribution decisions, not prerequisites invented by this plan.
- Add concise build/run instructions, documented YAML settings and units, recovery steps for invalid configuration/state, and the manual Windows acceptance checklist.
- Verify the packaged app references bundled assets rather than the developer's Codex directory and writes data only to per-user storage.
- Run the complete behavior suite and a clean release build. Reconcile the final implementation against every story in SPEC.md.
- Hand the candidate to the user for the manual Windows and visual checks. Address reported failures and add behavior regression tests when the failure is reproducible at the application seam.

**Verification and completion**

- All application-behavior scenarios pass with controlled time/randomness and isolated storage.
- Configuration, resume behavior, area movement, toggles, and every required animation are included in the release candidate.
- The user records manual Windows/visual acceptance. The release remains a candidate until those checks are complete; there is no automatic Windows integration suite.
- Completion means the runnable package, configuration documentation, required art, and testing evidence are delivered together.

## Coverage map

This maps work to the numbered user stories in SPEC.md. Overlap indicates that a story is completed by more than one milestone, rather than a separate test layer or duplicate implementation.

| Spec stories | Primary milestones |
| --- | --- |
| 1–3: desktop presence and initial placement | 1, 5 |
| 4–18: roaming area, dragging, monitor recovery, movement controls | 5, 6 |
| 19–21: idle subtlety and independent controls | 3, 6, 7 |
| 22–37: click/care behavior, needs, sleep and waking | 3, 4, 6 |
| 38–42: random events and action coordination | 4, 6 |
| 43–45: retain suitable art and complete dedicated actions | 3 |
| 46–51: tray, always-on-top, startup, integration errors | 1, 7 |
| 52–55: polished, accessible settings | 7 |
| 56–64: YAML, validation, shared settings, first-run defaults | 2, 7 |
| 65–71: local continuity, recovery, assets, clean quit | 1, 2, 3, 4, 8 |
| 72–73: repeatable behavior tests and user-owned Windows checks | All milestones; final handoff in 8 |

## Working rules and release risks

- Add meaningful behavior tests with each changed behavior; keep a single session-level automated seam. Do not create tests that mirror private methods, Windows UI drivers, or a parallel simulation of the application.
- Keep SPEC.md authoritative. Record significant architecture choices and implementation findings in project documentation and session notes, rather than quietly changing requirements.
- Resolve transparent-window hit testing, focus, and DPI feasibility early using the user's manual checks. These issues can otherwise invalidate a late frontend choice.
- Complete missing animations before the full care milestone. Asset quality and usable motion bounds affect both presentation and roaming geometry.
- Test invalid YAML, failed saves, and interrupted sessions before UI polish; these are data integrity behavior, not optional visual refinements.
- Report native integration failures truthfully. Passing behavior tests does not establish actual Windows integration or visual quality.

## Later implementation cycles

1. **Desktop extensions:** music detection with dance, browser foreground detection with search animation, and pet switching with compatible sprites. Specify detection rules, precedence, and additional art in that cycle.
2. **Mobile:** phone frontend and platform lifecycle behavior using the portable core where appropriate, followed by account-based persistence/synchronization. Clarify account identity, conflict handling, offline behavior, and background execution before implementing synchronization.

These cycles are recorded for future work and do not block the first Windows release. Do not introduce cloud infrastructure or mobile-specific abstractions during the initial milestones solely to anticipate them.
