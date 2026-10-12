# Desktop Pet — Hood, First Release

## Problem Statement

The user wants Hood to be a pleasant companion on the Windows desktop: present and responsive, but quiet enough to coexist with everyday work. Existing Hood sprites are available, but there is no desktop application to render the pet, manage relaxed needs, or provide configurable movement and interactions. The app should eventually support other pets and mobile phones without making those future features part of the first release.

## Solution

Build a Windows desktop pet app featuring Hood inside a transparent desktop overlay. Hood roams within a user-editable area, reacts to clicks, occasionally performs small random actions, and has gradually changing sleep, boredom, and hunger needs. Automatic naps and forgiving care avoid demanding frequent attention.

Provide tray and right-click controls, a modern settings UI, and documented YAML configuration. Store all first-release data locally and pause needs while the app is closed. Retain suitable Hood animations and create dedicated missing animations before declaring the first release complete. Automate tests of application behavior; the user will manually verify Windows integration and visual quality.

## User Stories

1. As a desktop user, I want Hood to appear on my Windows desktop, so that I have a familiar companion while working.
2. As a desktop user, I want only Hood's artwork to be visible around the pet, so that the companion fits naturally into my desktop.
3. As a desktop user, I want Hood to start near the bottom of my monitor, so that he initially occupies a predictable, unobtrusive position.
4. As a desktop user, I want Hood to stay within his roaming area, so that he does not wander across the entire desktop.
5. As a desktop user, I want to reveal the roaming area in an edit mode, so that I can see the space Hood is allowed to occupy.
6. As a desktop user, I want to move the roaming area horizontally and vertically, so that I can place it where it suits my work.
7. As a desktop user, I want to resize the roaming area, so that I can control how much desktop space Hood uses.
8. As a desktop user, I want the roaming area to remain large enough to contain Hood, so that resizing does not leave him clipped or trapped outside it.
9. As a desktop user, I want the roaming-area outline to disappear when editing ends, so that the normal desktop remains uncluttered.
10. As a desktop user, I want to drag Hood and his roaming area together, so that repositioning the pet also repositions his movement boundary.
11. As a desktop user, I want dragging to avoid triggering a click reaction, so that moving Hood does not also make him jump or wake up.
12. As a desktop user, I want Hood's area to follow him onto another monitor, so that I can move the companion between screens.
13. As a desktop user, I want Hood and his area to stay within usable monitor space after movement, so that the pet remains accessible.
14. As a desktop user, I want Hood to return to a usable location if a monitor disconnects, so that he does not become stranded off-screen.
15. As a desktop user, I want saved placement to adapt when my monitor layout changes, so that reopening the app does not hide Hood.
16. As a desktop user, I want to disable roaming, so that Hood can remain at the position I choose.
17. As a desktop user, I want to drag Hood while roaming is disabled, so that stopping automatic movement does not prevent manual placement.
18. As a desktop user, I want to re-enable roaming without losing my area, so that I can change behavior without repeating setup.
19. As a desktop user, I want idle animations to be subtle, so that Hood does not distract me from work.
20. As a desktop user, I want to disable idle animations independently of roaming, so that I can choose how quiet Hood should be.
21. As a desktop user, I want other actions to keep working with idle animations disabled, so that reducing idle motion does not disable interaction.
22. As a desktop user, I want a left-click to make awake Hood jump or look up, so that direct interaction feels responsive.
23. As a desktop user, I want a right-click to open Hood's menu, so that I can access care actions and settings nearby.
24. As a desktop user, I want to feed Hood through his menu, so that I can reduce hunger deliberately.
25. As a desktop user, I want feeding to have a dedicated animation, so that the action is visually understandable.
26. As a desktop user, I want hunger to develop gradually over hours, so that Hood does not require frequent feeding.
27. As a desktop user, I want to inspect sleep, boredom, and hunger from the menu, so that I can understand Hood's needs when I choose.
28. As a desktop user, I want needs to stay out of permanent desktop status bars, so that the pet remains visually unobtrusive.
29. As a desktop user, I want neglect to avoid illness, death, or punishment, so that the companion remains relaxing.
30. As a desktop user, I want movement to reduce boredom, so that Hood benefits from roaming.
31. As a desktop user, I want click interactions to reduce boredom when roaming is disabled, so that keeping Hood stationary does not remove his source of stimulation.
32. As a desktop user, I want Hood to become sleepy gradually and nap automatically, so that I do not need to manage a strict sleep schedule.
33. As a desktop user, I want automatic naps to stop roaming, so that Hood visibly rests.
34. As a desktop user, I want dedicated sleeping and waking animations, so that I can distinguish rest from ordinary idle behavior.
35. As a desktop user, I want a left-click to wake sleeping Hood, so that I can interact with him immediately.
36. As a desktop user, I want to send Hood to sleep from his menu, so that I can also initiate a quiet period myself.
37. As a desktop user, I want waking early to allow an awake period, so that Hood does not immediately return to sleep after I click him.
38. As a desktop user, I want occasional waves, looks, and hops, so that Hood feels alive without constant activity.
39. As a desktop user, I want random reactions to occur several minutes apart, so that their frequency remains unobtrusive.
40. As a desktop user, I want to disable random events separately, so that I can keep direct reactions while eliminating spontaneous ones.
41. As a desktop user, I want random reactions to respect sleep and active interactions, so that animations do not interrupt each other confusingly.
42. As a desktop user, I want repeated clicks and care actions to remain controlled, so that input does not create a long queue of disruptive animations.
43. As a desktop user, I want suitable existing Hood animations to be retained, so that the original character is preserved.
44. As a desktop user, I want missing animations to match Hood's appearance and style, so that new actions look like the same pet.
45. As a desktop user, I want every first-release action to have an appropriate animation, so that placeholder reactions do not obscure what Hood is doing.
46. As a desktop user, I want a system-tray icon, so that I can access the app even when Hood is covered by another window.
47. As a desktop user, I want settings and quitting available from the tray, so that I have a reliable way to manage the app.
48. As a desktop user, I want to control whether Hood stays above other windows, so that I can choose how visible he is while working.
49. As a desktop user, I want optional startup with Windows, so that I can choose whether Hood starts automatically.
50. As a desktop user, I want startup with Windows disabled initially, so that the app does not register automatic startup without my choice.
51. As a desktop user, I want a clear message if a requested startup or display setting cannot be applied, so that the UI does not falsely report success.
52. As a desktop user, I want a modern, compact settings UI with clear labels, so that configuring Hood feels polished and straightforward.
53. As a desktop user, I want readable contrast and visible control states, so that I can understand the settings comfortably.
54. As a desktop user, I want settings controls and menus to work with keyboard navigation, so that routine configuration is not dependent on precise pointer input.
55. As a desktop user, I want settings to remain usable at common display scaling levels, so that the UI is readable on my monitor.
56. As a desktop user, I want an editable YAML configuration file, so that I can configure behavior directly without using every UI control.
57. As a desktop user, I want documented default settings and timing units, so that manual configuration is easy to understand.
58. As a desktop user, I want UI changes saved to the same configuration used at startup, so that the UI and YAML do not represent competing settings.
59. As a desktop user, I want valid manual YAML edits applied on the next launch, so that direct file changes predictably affect the app.
60. As a desktop user, I want independent settings retained when I change one option, so that adjusting idle animations does not reset my other preferences.
61. As a desktop user, I want invalid YAML or invalid values identified clearly, so that I can correct the problem without guessing.
62. As a desktop user, I want invalid configuration left intact for correction, so that an error does not destroy my edits.
63. As a desktop user, I want failed settings saves reported, so that I know when my changes have not been stored.
64. As a desktop user, I want sensible defaults on first launch, so that I can use Hood before creating a configuration myself.
65. As a desktop user, I want my needs, placement, area, and settings restored after reopening, so that each session continues from the previous one.
66. As a desktop user, I want needs to pause while the app is closed, so that absence does not create a backlog of care.
67. As a desktop user, I want a nap in progress to resume with its remaining duration after reopening, so that closed time is not treated as active pet time.
68. As a desktop user, I want all first-release data kept locally without an account, so that I can use the companion independently of an online service.
69. As a desktop user, I want recovery from damaged saved state to be explained, so that I understand why a session cannot be restored normally.
70. As a desktop user, I want missing or unusable Hood assets reported clearly, so that the app does not silently replace dedicated animations with unrelated actions.
71. As a desktop user, I want quitting to stop the app and save my current session, so that Hood does not continue running unexpectedly.
72. As the project owner, I want repeatable tests of externally visible application behavior, so that behavior changes can be verified without manually repeating every care scenario.
73. As the project owner, I want to perform Windows integration checks myself, so that automated testing focuses on application behavior rather than operating-system UI automation.

## Implementation Decisions

- **Scope and architecture:** This is a new application with no existing modules, APIs, tests, glossary, or ADRs to preserve. Use the smallest cohesive implementation. Separate portable pet behavior/session coordination from Windows rendering, input, monitor discovery, tray controls, and startup registration. Do not build a plugin framework, cloud service, or mobile shell. Framework/language selection remains an implementation decision; document significant platform choices and their trade-offs before committing to them.
- **Application contract:** UI commands include reacting/waking, feeding, sleeping, editing/moving/resizing the roaming area, updating configuration, and quitting. Application output exposes the current pet presentation, needs, placement, roaming area, and user-facing errors. Accept elapsed active-session time, randomness, display information, and local storage through explicit boundaries so the same behavior can be exercised reproducibly in tests. This contract is conceptual, not a requirement for a particular class hierarchy or public network API.
- **Need semantics:** Represent hunger, boredom, and sleep need on a bounded 0–100 scale, where higher means more need. Feeding reduces hunger; actual movement reduces boredom; click interaction reduces boredom at least while roaming is disabled. Sleeping reduces sleep need. Saturated needs never cause illness, death, forced relocation, or an escalating interruption. Show values with friendly labels in the menu. Needs continue during an open session even when idle animation or roaming is disabled.
- **Boredom-driven roaming and default idle timing (confirmed 2026-10-07):** Keep eight-second roaming bouts and the current travel speed. Scale bout frequency linearly from 10% of the previous once-per-40-seconds cadence at boredom 0 to 120% at boredom 100, adapting the current rest as boredom changes. Retain a positive roaming frequency even at zero boredom and respect existing toggles and pauses. Increase the default idle animation's repetition frequency by about 20% solely by shortening the still pause; retain the moving poses' speed and all spontaneous-gesture intervals and durations. Preserve saved-session compatibility and shared behavior across pet variants.
- **Sleep and action coordination:** Automatic sleep begins at the configured sleep threshold when a foreground interaction has finished. A nap pauses roaming, idle animation, and random reactions and uses dedicated sleep presentation. Manual Sleep is available. Left-click wakes a sleeping Hood and consumes that click without also starting a jump. Feeding during sleep wakes Hood and plays the feeding action. Dragging can reposition a sleeping Hood without waking him. Direct actions take precedence over idle/random activity; discard conflicting random actions rather than queueing them. Repeated input may update needs but must not accumulate a playback backlog. After an action, return to the appropriate resting/roaming state. Disabling roaming stops automatic position changes; disabling idle animation produces a static resting pose but leaves sleep, feeding, movement, and direct/random actions available as otherwise enabled.
- **Random and movement scheduling:** Random events choose among wave, look-around (left/right), look-up, and hop actions within a configured interval range, defaulting to 1–5 active awake minutes equally during idle and roaming, including with idle animations disabled. Expose minimum and maximum minutes in Settings and YAML; require positive finite numbers with minimum no greater than maximum. Saving a changed range starts a fresh countdown; on restore, cap saved remaining time to the configured maximum. Suspend the countdown during sleep, area editing, dragging, or another action; resume without replaying missed events. Roaming consists of gentle, bounded movement inside the area and respects the current toggle. Hood can travel horizontally, vertically, or diagonally when the area has room, using both axes at once for diagonal runs without increasing total speed. Each axis reverses independently at its boundary; shallow areas focus on horizontal roaming. Running has visibly alternating feet and a restrained body bounce, with unchanged travel speed and scheduling. Click and spontaneous jumps go straight up and land in the same spot, targeting about half Hood's visible height over 1–1.2 seconds including crouch and landing. Jumps pause roaming and may rise above the roaming area; reduce peak height when necessary to keep the complete character inside the current monitor's usable space. Preserve the resting position and roaming area during the jump and in saved state. Exact low-impact motion details are delegated to implementation, constrained by the quiet-companion goal. These motion and configurable reaction timing refinements were confirmed on 2026-10-02.
- **Placement:** Use monitor work areas, excluding reserved desktop space such as the taskbar, and consistent logical coordinates rather than assuming one pixel density. On first launch use the primary monitor and place the area near its bottom edge. Area editing reveals movable/resizable bounds; each edge and corner can be dragged to resize while the opposite edges stay fixed. Resizing keeps Hood inside the area and stops at the minimum footprint and monitor boundaries. Normal operation hides the outline. Dragging translates Hood and the area by the same offset, preserving Hood's relative placement. Choose the destination monitor from the dragged pet's position and keep the final area within that monitor's work area. Clamp the entire area and pet together at boundaries; do not silently change their relative offset. Enforce a minimum area that accommodates Hood and supported motions. On a smaller screen shrink oversized saved bounds as needed; on monitor removal move to the primary monitor. Do not force later placement back to the bottom edge.
- **Controls and UI:** Supply a tray icon and a pet right-click menu with needs, Feed, Sleep, Edit roaming area, Settings, and Quit. The tray provides Settings and Quit at minimum. Settings use a compact, modern visual style with consistent spacing, readable contrast, clear toggle states, keyboard access, and sensible scaling. Avoid permanent need bars and decorative motion in settings. Closing settings leaves Hood running; Quit saves and exits. Always-on-top affects the pet overlay. Optional Windows startup must be removable when disabled and must report registration failures accurately. Ordinary pet activity must not steal keyboard focus from the user's active application.
- **Configuration:** A single human-editable YAML document is authoritative for settings. UI saves update this same document; do not maintain a second settings store. Include a schema version and documented units/defaults. Load manual file edits at the next launch; file watching is not required. UI setting changes apply during the current session after validation and a successful save. Preserve other supported values when changing one setting. The UI may expose ordinary options while advanced rates remain editable in YAML. Generate defaults on first launch and make the configuration location discoverable from Settings. Formatting/comment preservation is not a first-release requirement.
- **Configuration validation:** Use safe YAML parsing and reject malformed syntax, duplicate/unknown keys, unsupported schema versions, incorrect value types, non-finite numbers, negative rates/gains, invalid need thresholds, nonpositive durations/sizes, and inverted interval ranges. Report the affected setting and correction when possible. Leave invalid source data intact; do not run from a partially accepted document or silently overwrite it with defaults. Show an actionable startup configuration error so the user can correct the file and relaunch. A failed UI save keeps the previous effective settings and reports the failure.
- **Local state and lifecycle:** Store runtime needs, behavior mode, remaining nap/event time, placement, and roaming bounds separately from YAML settings in versioned local state. Use per-user application storage, not the application's installation folder. Persist changes at meaningful boundaries and on clean exit using replacement writes that avoid leaving partially written documents. On relaunch restore from the last saved state and start a new elapsed-time baseline: wall-clock time spent closed must not change needs or complete timers. Treat system suspension as paused time too. A missing state starts a new session. Invalid state is reported and preserved for recovery before an explicitly acknowledged reset to defaults. Do not require an account, network access, or machine-specific asset locations at runtime.
- **Assets:** Package Hood with the app. Retain suitable existing idle, left/right movement, wave, and jump artwork. Inspect and create any missing look-up/look-around, feeding, sleeping, and waking sequences needed for the defined actions. Match Hood's detective owlet appearance, silhouette, palette, transparency, and scale. Validate asset mappings before running pet behavior and provide actionable errors for incomplete assets. Do not ship static “Zzz” or an unrelated reaction as the final substitute for a dedicated action. Use a small data-driven pet definition and action-to-animation mapping that can later accommodate another pet, without implementing selection/import UI or requiring the desktop app to reproduce Codex's internal sprite format.

The following defaults are specification choices made under the user's explicit delegation, rather than additional requirements supplied by the user. Keep the behavior values configurable in YAML.

| Setting or behavior | Initial default | Contract |
| --- | --- | --- |
| Roaming, idle animations, random events, always-on-top | Enabled independently | Boolean settings; no toggle implicitly changes another |
| Start with Windows | Disabled | Opt-in setting; registration result is surfaced |
| Initial hunger, boredom, sleep need | 0 | Each need remains between 0 and 100 |
| Hunger increase while awake | 12.5 points/hour | Approximately eight active hours from 0 to 100 without feeding |
| Boredom increase while awake | 16 points/hour | Slow progression without negative consequences |
| Sleep need increase while awake | 20 points/hour | Automatic nap threshold defaults to 80 |
| Feeding benefit | 40 hunger points | Floor at 0; no overfeeding penalty |
| Movement benefit | 0.5 boredom points/second of actual movement | Floor at 0; merely enabling roaming does not count as movement |
| Stationary click benefit | 10 boredom points | Applied to completed left-click interactions while roaming is off |
| Nap duration | 5 active minutes | Manual and automatic naps share the duration setting |
| Sleep recovery | Full recovery over a completed nap | Sleep need decreases proportionally; hunger/boredom remain unchanged during a nap |
| Early-wake grace period | 15 active minutes | Retain recovery already earned; suppress automatic sleep during this period |
| Random-reaction interval | Randomly sampled between 1 and 5 active awake minutes | Minimum and maximum configurable in Settings and YAML; each next interval stays within the configured range; tests control the random source |
| Initial roaming area | 480 × 180 logical units, with 16-unit bottom margin | Adapt to the monitor and actual pet/motion footprint |

## Testing Decisions

- **Confirmed seam:** Automated testing focuses on application behavior through the application/session contract used by the UI. The user explicitly removed Windows integration from automated testing and will perform those checks manually. No Windows end-to-end automation, native UI driver, or operating-system integration test suite is required.
- **Good tests:** Issue user-level commands, advance controlled active time, and inspect observable needs/presentation, positions, configuration, saved state, and errors. Avoid assertions about private functions, class structure, timer implementations, or incidental frame counts. Use a controllable random source, supplied monitor/work-area descriptions, temporary local storage, and test pet assets; do not wait real minutes or depend on the tester's monitor layout or account. Exercise real configuration parsing and local persistence at this seam.
- **Prior art:** There is no application code, test suite, or existing test seam in the project. Establish one behavior-level integration harness rather than multiple overlapping layers. The Windows adapter remains outside automated integration coverage; simulated display information tests placement policy, not actual Windows monitor APIs.
- **Needs and time acceptance:** Verify bounded need progression at the documented/configured rates, feeding and movement benefits, stationary click relief, and saturation without punitive effects. Verify automatic sleep at the threshold, manual sleep, paused roaming/random behavior during sleep, nap recovery, early waking/grace, and feeding while asleep. Closing and reopening must preserve needs and remaining timers without adding closed time; different wall-clock dates must not alter the restored session.
- **Interaction and toggle acceptance:** Verify awake click reactions, click-to-wake without an extra reaction, dragging without triggering click effects, interruption rules, and no queued random-event backlog. Test roaming, idle, and random-event settings independently and in combinations. Idle disabled must still allow enabled direct reactions, feeding, sleep, and movement. Sampled random schedules must remain within configured bounds and never fire while suspended.
- **Placement acceptance:** With supplied monitor geometries, verify bottom-edge first placement; editable area movement/resizing; minimum sizes; Hood and area translated together; cross-monitor relocation; bounded motion/hops; changed screen sizes; and primary-monitor recovery after disconnection. Include differing scale factors and coordinate origins. These checks validate application decisions only; the user checks actual monitor/DPI integration.
- **Configuration and persistence acceptance:** Verify first-run defaults; valid YAML overrides after restart; UI changes reflected in the same file; preservation of other settings; and independent state/configuration restoration. Reject malformed, duplicate, unknown, mistyped, out-of-range, and unsupported-version input with actionable errors and without overwriting source data. Simulate save failures and damaged state through the storage boundary and verify visible recovery behavior. Validate required action-to-animation mappings through application loading; visual artistry is reviewed manually.
- **Manual Windows acceptance, performed by the user:** Launch the actual app and inspect transparent Hood rendering, dedicated animations, subtle idle motion, input/drag distinction, roaming-area editing, tray/menu/settings usability, focus behavior, keyboard access, display scaling, always-on-top both on and off, startup enable/disable, real cross-monitor movement, and monitor disconnection recovery. The user also assesses whether the UI is modern and slick. Document manual results and unresolved failures before calling the release complete; do not implement automatic Windows tests for this checklist.

## Out of Scope

- Music detection and dancing.
- Browser foreground detection and search animation.
- Pet-switching, importing, browsing, or downloading UI; first-release selection is Hood only.
- Mobile apps, account creation, remote storage, and cross-device synchronization.
- Illness, death, punitive care, or need deterioration while the app is closed.
- Permanent desktop need bars, sounds, notifications demanding care, or disruptive spontaneous behavior.
- Live reload/file watching for manually edited YAML.
- General plugin systems, speculative extensibility frameworks, and cloud infrastructure.
- Automatic Windows integration testing. Manual integration acceptance is still required.
- Ticket creation, issue-tracker publication, and application implementation as part of creating this document.

## Further Notes

- **Source of truth:** This document synthesizes the confirmed interview and subsequent chat decisions, including YAML configuration, delegated timing/UI choices, and the user's final testing instruction. The earlier interview note questioning manual Sleep is superseded by the confirmed summary that includes it. The roaming area's bottom-edge placement is an initial default only; dragging is free in both directions.
- **Current assets:** The inspected Hood manifest lists idle, run-left, run-right, wave, jump, failure, waiting, active-work, and review motions. No dedicated sleep or feeding sequence is listed. Inspect actual artwork before deciding which look/reaction assets can be retained. Completing missing first-release animations is required deliverable work, not a post-release enhancement.
- **Asset risks:** Confirm the source artwork is suitable for packaging and redistribution before release. Additional animation creation must preserve Hood's identity and fit the application action mapping. Asset-production tools and packaging format can be selected during implementation; this spec does not authorize uploading artwork to an external service.
- **Future desktop cycle:** Add music detection/dance, browser foreground/search behavior, and easy pet switching when compatible sprites are available. Revisit detection semantics, action precedence, and dedicated animations in that cycle rather than assuming them now.
- **Future mobile cycle:** Port pet behavior to phones and introduce account-based persistence/synchronization. Account ownership, synchronization conflict rules, and mobile-specific background behavior remain intentionally unspecified and require a later requirements pass.
- **Rollout:** Deliver the Windows application, packaged Hood assets, documented configuration, and behavior tests together. The user performs manual Windows/visual acceptance. Significant implementation choices should record context, alternatives, trade-offs, and compatibility constraints without turning this small application into a speculative architecture project.
- **References:** local, ignored session records `.codex/sessions/2026-09-30-desktop-pet-interview.md` and `.codex/sessions/2026-09-30-desktop-pet-spec-synthesis.md`, and [project instructions](../../AGENTS.md). The specification is readable without those session records.
