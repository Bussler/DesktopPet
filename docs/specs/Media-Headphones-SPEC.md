# Media Headphones — Feature Specification

## Problem Statement

The desktop pet currently looks the same while the user listens to music or other computer audio. The user wants their selected pet to share that activity by wearing headphones whenever Spotify or a supported browser produces audible sound, while keeping the pet's existing personality, animations, and behavior.

## Solution

Automatically show over-ear headphones on Hood, Pip, and Puddle, and in-ear earbuds on Crumb, when audible audio is observed from Spotify desktop, Chrome, Edge, or Firefox. Music, videos, podcasts, calls, browser games, and other browser output qualify when sampled. Poll every 500 ms; brief sounds between polls may be missed and are not guaranteed to activate headphones. Detection works in the background. The user's later corrections replace Crumb's over-ear style with in-ear buds and remove the brief-sound activation requirement.

Headphones appear within approximately one second of qualifying audible audio. They remain through short quiet passages and disappear after three continuous seconds without qualifying sound. Explicit mute or zero volume removes them promptly, unless another qualifying source still warrants headphones. Headphone styles fit each pet; Hood's headband arches visibly over his detective hat, following the user's marked reference (this supersedes the earlier under-hat requirement). Every existing animation continues, and changing headphone visibility preserves the current action, frame, placement, and session state.

Add an enabled-by-default on/off switch in Hood Settings, using the existing behavior-settings Save/Cancel workflow. Save applies and persists the choice; Cancel or closing Settings discards the pending change. Saving the disabled choice removes headphones promptly. The feature changes artwork only and does not control media playback.

## User Stories

1. As a desktop user, I want the pet to wear headphones when Spotify desktop produces audible audio, so that the pet accompanies my listening.
2. As a desktop user, I want audible Chrome audio to trigger headphones, so that listening in Chrome is recognized.
3. As a desktop user, I want audible Edge audio to trigger headphones, so that listening in Edge is recognized.
4. As a desktop user, I want audible Firefox audio to trigger headphones, so that listening in Firefox is recognized.
5. As a desktop user, I want browser music to count, so that web music players work with the feature.
6. As a desktop user, I want audible videos to count, so that the pet accompanies video playback.
7. As a desktop user, I want audible podcasts to count, so that spoken media receives the same treatment as music.
8. As a desktop user, I want audible browser calls to count, so that the pet reflects conversations I can hear.
9. As a desktop user, I want audible browser games to count, so that game audio also gives the pet headphones.
10. As a desktop user, I want browser output observed during a polling interval to count, while accepting that brief sounds between polls can be missed.
11. As a desktop user, I want supported apps to trigger headphones while in the background, so that I can work in another window while listening.
12. As a desktop user, I want an open but silent supported app to leave the pet's ordinary appearance intact, so that opening a player is not mistaken for listening.
13. As a desktop user, I want unrelated application sounds to leave headphone visibility unaffected, so that detection stays within the supported sources.
14. As a desktop user, I want any qualifying source to keep headphones on, so that overlapping playback is handled consistently.
15. As a desktop user, I want pausing one source to leave headphones on when another is audible, so that the pet reflects the audio I still hear.
16. As a desktop user, I want switching between supported sources to preserve headphones through a short gap, so that the appearance does not flicker.
17. As a desktop user, I want headphones to appear within approximately one second of audible playback, so that the response feels timely.
18. As a desktop user, I want headphones to survive quiet passages shorter than three seconds, so that normal pauses do not repeatedly change the artwork.
19. As a desktop user, I want headphones to disappear after three continuous seconds of silence, so that the pet returns to its ordinary appearance when listening ends.
20. As a desktop user, I want observed sound to expire through the silence delay, without requiring brief unsampled notifications to activate headphones.
21. As a desktop user, I want muted browser tabs to stop qualifying, so that silent playback does not count as audible activity.
22. As a desktop user, I want app mute and app volume zero to stop that source from qualifying, so that Windows volume choices are respected.
23. As a desktop user, I want output-device mute and output-device volume zero to stop audio on that device from qualifying, so that muted Windows output does not give the pet headphones.
24. As a desktop user, I want observable Windows app/device mute to remove headphones promptly; browser tab or player mute may use the normal three-second silence delay.
25. As a desktop user, I want muting one source to preserve headphones if another source qualifies, so that one app's mute does not override other listening.
26. As a desktop user, I want unmuted playback to trigger headphones again when sound resumes, so that detection recovers without restarting the pet.
27. As a desktop user, I want audio routed to a different Windows output device to remain recognizable, so that selecting another output does not break the feature.
28. As a desktop user, I want detection to recover after reconnecting an audio device, so that temporary device changes do not permanently disable headphones.
29. As a desktop user, I want Hood to have over-ear headphones that fit his design, so that the added artwork looks like part of the character.
30. As a desktop user, I want Hood's headphone headband to arch visibly over his detective hat, so that the connection follows my reference image throughout head movement.
31. As a desktop user, I want Pip to have headphones suited to his design, so that his existing appearance remains recognizable.
32. As a desktop user, I want Crumb to have headphones suited to his design, so that his existing appearance remains recognizable.
33. As a desktop user, I want Puddle to have headphones suited to his design, so that his existing appearance remains recognizable.
34. As a desktop user, I want the idle animation to continue with headphones, so that listening does not freeze the pet.
35. As a desktop user, I want both running directions to retain their animations with headphones, so that walking remains natural.
36. As a desktop user, I want waving to retain its animation with headphones, so that greetings remain available.
37. As a desktop user, I want looking up to retain its animation with headphones, so that the pet's reactions remain available.
38. As a desktop user, I want looking around to retain its animation with headphones, so that head movements remain natural.
39. As a desktop user, I want jumping to retain its animation with headphones, so that headphones follow the moving pet.
40. As a desktop user, I want feeding to retain its animation with headphones, so that the existing feeding interaction remains available.
41. As a desktop user, I want sleeping to retain its animation with headphones while audio qualifies, so that listening does not change sleep behavior.
42. As a desktop user, I want waking to retain its animation with headphones, so that the transition out of sleep remains intact.
43. As a desktop user, I want headphones to follow every animated pose, so that they do not float, clip, or detach during movement.
44. As a desktop user, I want headphone visibility changes to preserve the ongoing animation, so that the pet does not restart an action when audio changes.
45. As a desktop user, I want headphone visibility changes to preserve needs, timers, and sleep state, so that artwork changes do not alter the simulation.
46. As a desktop user, I want headphone visibility changes to preserve size, placement, and roaming bounds, so that the pet does not move or resize when audio changes.
47. As a desktop user, I want changing the selected pet during playback to show the new pet's own headphones, so that the listening appearance follows my selection.
48. As a desktop user, I want Cancel after previewing another pet to restore the original pet with the current listening appearance, so that rollback does not restore stale audio status.
49. As a desktop user, I want hidden sprites to remain hidden during playback, so that audio does not override my visibility choice.
50. As a desktop user, I want revealing the pet to show its current listening appearance, so that it does not briefly display stale artwork.
51. As a desktop user, I want headphones to remain clear at supported pet sizes and display scales, so that the feature works with my existing size and monitor settings.
52. As a desktop user, I want automatic headphones enabled by default, so that the feature works without initial setup.
53. As a desktop user, I want an accessible on/off switch in Hood Settings, so that I can choose whether audio changes the pet's appearance.
54. As a desktop user, I want Save to apply and retain the switch value, so that the choice survives restart.
55. As a desktop user, I want Cancel or closing Settings to discard a pending switch change, so that browsing settings does not change my preference.
56. As a desktop user, I want saving the disabled choice to remove headphones promptly, so that the switch has an immediate visible effect once committed.
57. As a desktop user, I want saving the enabled choice during audible playback to activate headphones within approximately one second, so that I do not need to restart playback.
58. As a desktop user, I want older configuration files to receive the enabled default while retaining existing values, so that upgrading preserves my settings.
59. As a desktop user, I want an invalid headphone setting to produce the existing configuration error behavior, so that malformed data is not silently accepted.
60. As a desktop user, I want a failed Save to retain the previous effective preference and report the error, so that I know whether the change took effect.
61. As a desktop user, I want startup to determine headphone visibility from current audio, so that an appearance from a previous run is not restored incorrectly.
62. As a desktop user, I want unavailable audio detection to leave the pet usable in its ordinary appearance, so that an optional feature cannot prevent normal operation.
63. As a desktop user, I want detection to recover when supported playback becomes observable again, so that a temporary failure does not require restarting the app.
64. As a desktop user, I want unavailable headphone artwork to leave valid ordinary pet artwork usable, so that an optional appearance cannot break the pet.
65. As a desktop user, I want audio detection to work without taking focus or interrupting playback, so that it does not disturb my other applications.
66. As a desktop user, I want audio detection to avoid recording or transmitting media content, so that the feature remains local and limited to recognizing audible activity.

## Implementation Decisions

- **Boundaries:** Keep Windows audio discovery, source identification, output-device observation, and mute/volume handling in the Windows frontend. Keep the portable core free of Windows APIs, following the accepted WPF frontend ADR. Reuse the existing configuration controller, session, pet asset loader, and overlay presentation boundaries. Introduce only the smallest replaceable audio-observation boundary needed for deterministic presentation tests; avoid a general accessory framework.
- **Eligible sources:** Spotify desktop and the Chrome, Edge, and Firefox browser families qualify, including their audio-producing subprocesses. Eligibility is application-level audio output, without classifying content or requiring a browser to expose a media-playback session. Foreground focus, player-open state, or a reported playing status alone is insufficient.
- **Audibility:** Require actual non-silent output from an eligible source, with its effective Windows/app mute and volume state permitting sound. Observe active Windows playback outputs, including app-specific routing. Browser tab or in-player mute must produce no qualifying sound. Microphone activity alone does not qualify. Choose a small, documented sound-level threshold during implementation and verify that ordinary quiet playback is recognized without treating silence as sound.
- **Timing contract:** Poll every 500 ms, as clarified by the user on 2026-10-13. With the setting enabled, activate within approximately one second of observing eligible audible output. Brief sounds between observations may be missed. Each audible observation renews a three-second quiet grace period; removal may take one additional observation/render interval. Observable Windows app/device mute or zero volume invalidates that source's grace immediately. Tab/player mute without Windows mute metadata uses the ordinary three-second delay, explicitly accepted by the user. Another qualifying source retains its own grace. Feature disable remains prompt. Use monotonic elapsed time; do not add timing controls.
- **Artwork:** Provide complete headphone coverage for all existing poses of Hood, Pip, Crumb, and Puddle: Idle, RunRight, RunLeft, Wave, LookUp, LookAround, Jump, Feed, Sleep, and Wake. Style/color choices are delegated to the implementer. Retain each pet's established character artwork; Hood's visible curved headband goes over his hat and Crumb uses small in-ear buds. Use frame-aware artwork that follows head movement. A single stationary accessory over every frame is insufficient.
- **Presentation contract:** Headphone changes are a presentation variant of the current pet, not a new pet action or session transition. Preserve frame sequence, frame duration, looping, current frame, motion bounds, logical dimensions, needs, action timers, nap state, and placement. Preserve transparency and source-frame isolation at supported pet sizes and DPI. Asset representation may be a synchronized accessory layer or complete compatible variant frames; select the smallest cohesive approach that meets these visual constraints and preserves existing manifests and ordinary custom-sprite loading.
- **Settings contract:** Add a boolean `MediaHeadphones` value to `BehaviorSettings`, serialized as `behavior.media_headphones`, defaulting to true. Add a checkbox labeled “Show headphones during audio playback” to Hood Settings. Follow the existing behavior-checkbox workflow: changes remain pending until successful Save; Cancel or window close discards them. Save uses existing validation, error reporting, and atomic storage behavior and preserves unrelated settings.
- **Compatibility:** Missing `behavior.media_headphones` in a valid older configuration means true. Validate it as a boolean using existing configuration conventions. This additive setting does not change session or Notes schemas. Headphone visibility, audio sources, and quiet grace are transient and are recomputed on startup; do not persist media state in session snapshots. Preserve ordinary custom artwork if it has no compatible headphone assets; supplying headphone variants for third-party artwork is outside this feature.
- **Existing appearance workflows:** Pet selection and size previews keep their current Save/Cancel behavior. Use the effective selected pet and current audio state during preview and rollback. Audio never overrides ShowSprites. On reveal, determine the current appearance; do not replay stale audio observations. Detection must remain independent of whether sleeping, idle-animation settings, or sprite visibility pause the normal animation timer.
- **Lifecycle and failure:** Start observation when the saved feature preference is enabled and stop/release it when disabled or the app exits. Re-evaluate live audio after startup, resume, device changes, and source changes; do not count suspended time as fresh sound. If detection becomes unavailable, discard stale headphone state and retain ordinary pet operation. Use the existing local diagnostic mechanism for failures and recover on later valid observations. An optional headphone asset failure must retain usable ordinary artwork and provide an actionable local diagnostic rather than corrupting configuration or session data.
- **Privacy and responsiveness:** Read only the audio activity and mute/volume metadata needed to decide appearance. Do not record samples for storage, inspect media titles or conversations, transmit audio, take focus, or control players. Keep observation and asset handling bounded so Notes editing, dragging, and animation remain responsive; do not decode full assets on each audio observation or animation tick.

## Testing Decisions

The user confirmed these testing boundaries on 2026-10-12: pet presentation with simulated audio and elapsed time; Settings and persistence; native playback and visual acceptance. Tests should observe the resulting artwork, configuration, and preserved pet state rather than mirror private helpers or depend on live audio services for deterministic coverage.

- **Primary presentation seam:** Drive the real overlay presentation path with controlled audio observations and elapsed time. Observe headphone presence for every pet while checking the session's current action/frame, needs, nap state, placement, and bounds. Cover eligible and unrelated sources; background output; source overlap; silence; pause/stop; sub-three-second gaps; the three-second boundary; explicit mute/zero volume; reactivation; feature disable; startup; loss/recovery of observation; output-device changes; hidden/revealed sprites; and pet preview/rollback. Test source-specific mute with another audible or grace-period source, rather than a single global boolean alone. Avoid wall-clock sleeps.
- **Settings and persistence seam:** Exercise Hood Settings through the existing configuration controller and store. Verify enabled defaults, checkbox accessibility, successful Save, Cancel/window close, restart round-trip, legacy missing-key defaults, invalid values, failed saves, and preservation of unrelated settings/session data. Reuse the established patterns in SettingsAndStatsTests and HoodConfigurationTests, including failed-save and legacy-config coverage.
- **Asset and rendering checks:** Extend the existing HoodAssetsTests, HoodOverlayRenderingTests, and sprite-validation workflow where the selected representation needs coverage. Validate every required action/frame for all four pets, matching animation contracts, transparency, source-crop isolation, size/DPI behavior, and ordinary-artwork fallback. Existing asset reload/state-preservation and pet-switch rendering regressions provide prior art. Structural checks cannot certify that headphones look correct or fit over Hood's hat.
- **Native acceptance:** Run the packaged executable with real Spotify, Chrome, Edge, and Firefox; sustained music/video/podcast/call/game output, background operation, pause, mute/zero volume, overlap, output routing/reconnection, and restart. Measure activation for observed audio, the three-second quiet/tab-player-mute delay with up to one 500 ms polling interval, and prompt observable Windows mute/disable response. Short notifications are allowed to be missed. Inspect all actions/pets, Hood's over-hat band, Crumb's buds, size extremes and mixed DPI. Use identifiable controls; hearing alone is not an automated oracle.
- **Completion evidence:** Run the standard Test workflow for implementation changes and the separate Build-AndPublish workflow for the candidate used in native acceptance. Report automated results and native acceptance separately, tied to that candidate. A passing build or sprite contract check does not establish real browser detection or visual correctness.

## Out of Scope

- Dancing, beat synchronization, new animation actions, or changes to needs, roaming, sleeping, and reactions.
- Playback controls, media titles, cover art, player integrations requiring account login, or browser extensions.
- An always-wear mode, a manual headphone style selector, or new tray/context-menu switches.
- Sources beyond Spotify desktop, Chrome, Edge, and Firefox; arbitrary system-wide sound detection; microphone-only activity.
- User-adjustable audio thresholds/timing, non-Windows frontends, and a general accessory/modding system.
- Creating headphone artwork for custom or third-party pets or detecting physical speaker/headphone mute switches that Windows cannot observe.

## Further Notes

- Scope and testing boundaries originated in the 2026-10-12 interview. Later user corrections require Hood's band over the hat and Crumb's in-ear buds. On 2026-10-13 the user accepted missed brief sounds, 500 ms polling, and the three-second tab/player-mute delay. This document does not claim native acceptance is complete.
- “Audible” means eligible non-silent output permitted by software mute and volume settings observable through Windows. Physical speaker power, analog volume knobs, disconnected hardware not reported by Windows, and what a person can actually hear are not observable guarantees.
- Windows media-session APIs expose playback information only for integrated applications, so playback status alone cannot satisfy the browser-game/call/notification scope. Audio metering also requires explicit mute/volume consideration: output peak levels may be measured before endpoint-volume attenuation. Evaluate the concrete audio-observation approach against these requirements before implementation. References: [Microsoft media-control API](https://learn.microsoft.com/en-us/uwp/api/windows.media.control) and [Microsoft audio-meter API](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudiometerinformation).
- Browser subprocess ownership, app-specific routing, device reconnection, and audio paths that bypass normal software metering require native validation. In particular, unsupported exclusive-output metering is a feasibility risk, not grounds to silently weaken the audible-output contract. Record verified limitations explicitly in implementation evidence.
- New artwork must retain appropriate sources and fit the existing asset regeneration/validation workflow. Include headphone assets in the published application; retain this specification as source-project documentation rather than a shipped user guide. Update the configuration and sprite guides when implementation makes the behavior available.
