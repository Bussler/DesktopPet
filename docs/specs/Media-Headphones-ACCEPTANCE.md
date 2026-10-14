# Media Headphones acceptance evidence

**Date:** 2026-10-13  
**Status:** Automated validation and offline artwork review passed; the user confirmed Spotify works. Broader native playback and size/DPI acceptance remain pending.  
**Specification:** [Media-Headphones-SPEC.md](Media-Headphones-SPEC.md)

This record distinguishes automated evidence from native acceptance. The seven implementation tickets were reviewed and tested in order. Structural tests and a package do not establish that every live audio path is detected or that the accessory is visually correct in every packaged pose.

## Automated evidence

| Check | Result |
| --- | --- |
| `tools/Test.ps1`, including sprite validators | Passed after review remediation: 422 Core tests and 612 Windows tests (1,034 total); no failures or skips. |
| `MediaHeadphoneArtworkTests` | Eighteen rendered-pixel regressions cover both walking directions for Hood/Pip, animal-ear height, Pip's clear forehead and side cups, Hood's late head-turn direction and visible continuous over-hat band, and Crumb's compact in-ear buds with no band across all animation frames. |
| `WindowsMediaAudioProbeTests.SessionVolumeQueriesPreserveNativeVolumeAndMute` | Both muted/unmuted cases failed before the fix and passed afterward. Native volume/mute getters now return correct metadata and invoke zero setters against an independent native COM vtable. |
| Native enumeration failure | Independent fake COM device/session enumeration rejects a failed session-count query as unavailable, but continues with another usable output. The sole-output case failed before moving the usable-device increment and passes afterward. |
| Custom-art fallback | The actual overlay reproduced the valid `Custom grid` manifest crash before the fix; it now retains ordinary pixels and transparent accessory fallback. |
| All-frame overlay integration | Four per-pet cases drive all 1,060 animation entries through overlap, source/endpoint mute, handoff, unavailability/recovery, silence expiry and disable. Check actual current artwork and preserve session/nap/bounds state; subsequent frame/movement timing matches an unaffected reference session. |
| Accessible diagnostics | Isolated tests verify persistent actionable log entries, the 256 KiB history bound, and continued operation on a log-write failure. Failures/recovery use `media-headphones.log` in the configured data folder. |
| `HoodOverlayRenderingTests.AllAvailablePetHeadphonesCoverEveryAnimationFrameAndPreserveTheSession` | Passed for every available animation frame of Hood, Pip, Crumb, and Puddle; the test also checks action/frame, needs, and placement preservation through headphone transitions. |
| Media policy and lifecycle regressions | Passed for supported process names, unrelated processes, audible threshold, silence grace, app/endpoint mute and zero volume, overlapping sources, endpoint isolation and handoff, suspend/resume, unavailable observations, and hidden/revealed sprites. |
| Settings/configuration regressions | Passed for the enabled default, older missing-key configuration, Settings Save/Cancel, and saved preference behavior. |
| Final package | `Build-AndPublish.ps1` succeeded. The candidate includes all four pet asset folders and the allowlisted user guides; the feature specification and acceptance record are source-only. |

## Painted artwork redesign

The user confirmed Spotify now works, then reported that Pip's cups sat on top of his head and Hood's walking cups were misplaced. The former flat vector shapes and generic phase offsets were replaced with shaded anime/chibi raster parts, calibrated against each actual source pose. Front views place narrow cups at the sides; profiles show the near exterior cup behind the face and hide the far side. Hood's initial under-hat layering was subsequently replaced by the user's over-hat reference. All original pet atlases and animation manifests are unchanged.

The built-in image-generation tool produced [the transparent parts atlas](../../Assets/Headphones/headphones.png); [source notes and the exact prompt](../../Assets/Headphones/README.md) record its provenance. [The preview](../../Assets/Headphones/preview.png) shows idle, running, and sleeping pets. `tools/Generate-HeadphonePoses.ps1` reproducibly regenerates embedded `poses.json` from opaque frame bounds and explicit fitted poses.

Offline WPF contact sheets were inspected for all 40 pet/action combinations, covering 580 distinct source poses; structural coverage includes all 1,060 animation entries. Comparison against the preceding renderer reproduced headphone pixels over forward facial regions while walking (Hood: 1,200 right / 1,185 left; Pip: 508 right / 504 left) and across Pip's upper forehead (687). Those regions contain zero accessory pixels after the redesign, while the intended side cups remain visible. Hood's late head-turn test also failed before its source-direction calibration and passed afterward.

Package verification confirms its embedded PNG/pose hashes match the reviewed source, all four original atlases/manifests match source, all four accessories render, and the corrected audio COM slots remain intact. Offline artwork review does not replace packaged size/DPI checks.

After further user feedback, Hood's cups were raised 27 logical pixels (30 while walking), and Pip's 24 (33 while walking), toward their animal ears. Crumb's cups were reduced to 80% and raised within the ear outlines; his near ear fully occludes the profile band. All 30 affected pet/action contact sheets were reviewed again. New pixel regressions reproduced the former low cups and Crumb's lower-rim overlap before the correction. An isolated multi-STA test run also exposed decoder thread affinity in cached crops; copying pixels to a frozen standalone bitmap resolves it and the complete artwork suite passes independently.

The user accepted Hood/Pip's raised fit and subsequently requested in-ear headphones for Crumb instead. His final artwork is a separate original transparent earbud atlas, `Assets/Headphones/crumb-earbuds.png`, generated with the built-in image tool; the exact prompt is recorded in the asset README. Small mustard-and-rose buds sit near his inner ear bases, with no band in any pose. All ten Crumb contact sheets were reviewed. The all-frame compactness/no-band regression fails against his former over-ear art and passes with the buds. An offline comparison confirms Hood, Pip, and Puddle's part crops and every fitted pose, plus the original over-ear atlas, are unchanged from the preceding candidate. Normal character atlases/manifests remain unchanged.

The user's later marked Hood image supersedes the under-hat requirement: the existing brown/brass band now arches in front of and above the detective hat. Its crown follows each actual source pose, and the full connection remains visible in profiles rather than stopping at the crown. All ten Hood action sheets were reviewed again. Three rendered-layer cases fail against the original under-hat fit and pass after the change; their continuity assertions also reject the initial clipped profile arch. Pip/Crumb/Puddle's fitted data and all original raster assets are unchanged from the preceding earbud candidate.

## Spotify volume regression

The user reported that the candidate set Spotify's Windows app volume to zero. A native-boundary regression reproduced unintended volume writes on two runs: calling the production volume getter invoked a native setter. The `ISimpleAudioVolume` declaration used the wrong COM method order. Correcting it to the [Windows SDK order](https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/Audioclient.h) fixes the getter dispatch and the mute reading. The earlier policy/presentation tests did not exercise that native ABI boundary.

The replacement package below contains the fix and the full suite passes. The previous `candidate-ticket-07-final-20261012` package is superseded and should not be used. Its Windows application DLL has SHA-256 `23B1DEEE450E0EAB2DC03B09A02A8D8E97A26FFA6692A09C3225B0B97E278462`. Close that build before restoring Spotify's chosen volume. No prior volume was recorded, so the fix cannot restore it automatically.

The regression uses a fake native session; it does not change live audio settings. The user subsequently confirmed that Spotify works. That confirmation does not establish measured timing, volume stability under repeated polling, or browser/device coverage. Earlier smoke-probe volume/mute readings used the incorrect interface and cannot establish silence or audibility.

The user also reported browser volume dropping to zero and confirmed this occurred in an earlier build; browser identity remains unspecified. Browser sessions share the same volume-reading interface, so the earlier mismatch could affect them too. A repeatable comparison of the actual published DLLs against the independent fake native session reproduces a setter on the old ticket-07 build's first read. The current painted-headphones package passes 100 reads in each mute state with volume/mute unchanged and zero setters. This verifies the packaged fix; live browser volume stability remains pending. No browser-volume incident in the latest candidate has been reported.

## Story reconciliation

| Stories | Automated evidence | Native evidence/status |
| --- | --- | --- |
| 1–4 | Process eligibility covers Spotify, Chrome, Edge, and Firefox. | The user confirmed Spotify works after the volume fix. Browser playback remains pending; previous smoke metadata is invalidated by the COM bug. |
| 5–11 | Detection is process-output based and independent of foreground focus or media-session metadata. | Music, video, podcasts, calls, games, notifications, and background use have not been exercised with real playback. |
| 12–16 | Tests cover silence, unrelated sources, overlap, source-specific grace, and handoff. | Live silent-app and overlapping-source checks are pending. |
| 17–20 | Deterministic elapsed-time tests cover activation, the three-second grace, and expiry. Polling is 500 ms; brief-sound activation is no longer required. | Packaged timing for sustained/observed audio remains pending. Short sounds may be missed by the explicitly revised scope. |
| 21–26 | Tests cover Windows session/endpoint mute and zero volume, grace invalidation, overlap, and reactivation. | The user explicitly accepted the normal three-second delay for tab/player mute on 2026-10-13. This is no longer a prompt-removal gap; native mute checks remain pending. |
| 27–28 | All active output endpoints are sampled; endpoint-specific grace and route handoff are tested. | Alternate-device routing and disconnect/reconnect are pending native checks. |
| 29–43 | Every available frame for all ten actions and all four pets is structurally checked; all 40 redesigned contact sheets were reviewed. Rendered-pixel regressions cover reported side-cup defects, Crumb's in-ear replacement, and Hood's visible over-hat connection. Hood's band is in front of the hat, following the later explicit requirement. | Complete native packaged visual/size/DPI acceptance remains pending. |
| 44–50 | Presentation tests cover state preservation, pet preview/Cancel with current audio, hidden sprites, and current appearance on reveal. | Packaged UI transitions and every visual edge case remain pending native confirmation. |
| 51 | Accessory layers share the pet's frame canvas and scale with it. | 50%/200% sizes and mixed-DPI/multi-monitor changes have not been visually checked. |
| 52–60 | Configuration and Settings tests cover the enabled default, checkbox, Save/Cancel, persistence, and legacy defaults. | Native keyboard/accessibility and failed-save UI checks remain pending. |
| 61–63 | Startup uses current observations; unavailable observations clear stale state; observer lifecycle and recovery are covered by tests. | Packaged startup, real observer failure/recovery, device reconnection, and shutdown resource behavior remain pending native checks. |
| 64 | Raster parts/poses are embedded; load failure retains ordinary art and writes an actionable local log. Unknown valid custom names use transparent accessories without throwing. Ordinary sprite sheets load independently. | Published missing-resource injection and arbitrary custom-art fit remain outside verified native evidence. |
| 65 | The native regression verifies that observation's volume/mute getters do not call setters. The observation path has no focus or player-control calls. | The reported volume defect is fixed at the COM boundary; live volume stability, focus, and playback continuity remain pending confirmation with the replacement package. |
| 66 | The implementation reads local process audio levels and mute/volume metadata; no audio content or playback control is part of the observation path. | No network/media-recording path was found in the implementation review. This is code evidence, not a live privacy instrumentation test. |

## Native acceptance still required

Run the packaged executable with sustained audible output from Spotify desktop and Chrome, Edge, and Firefox. Include background use, music/video/podcasts/calls/games, pause/quiet gaps, overlap, mute/zero volume, alternate outputs and reconnection. Measure activation and the three-second quiet/tab-player-mute delay with up to one 500 ms check of tolerance; observable Windows mute/disable remains prompt. Brief sounds may be missed and no longer require activation.

Check that observation preserves the chosen Windows per-app volume and mute for each supported source throughout playback and repeated polling.

Visually inspect every action/frame of the four pets in the package, including the two running directions, LookAround, Feed, Sleep, and Wake. Check Hood's curved headband over the hat, Crumb's compact in-ear buds, supported size extremes, and mixed-DPI monitor changes. Verify the Settings checkbox with keyboard and pointer, Save/Cancel/window-close, enabling during playback, disabling, and restart behavior.

Exclusive-mode output, hardware mute switches, and physical speaker audibility are not established by Core Audio process-session sampling. Record whether the native setup exercises those paths; do not infer support from a passing automated suite or package.

A live Edge probe was attempted, but the browser safety policy rejected the local `data:` test page before navigation. No tone played and no Edge session was observed, so this attempt did not exercise or fail product detection. The browser policy also disallows reaching the same result through another browser surface or an indirect route. Use an ordinary trusted media source for the remaining native check.

The superseded candidate was launched with a fresh isolated data directory. After four seconds its process remained alive and exposed a window titled “Windows startup setting mismatch”. Closing that warning did not close the application within three seconds, so the test process was stopped. The pet window itself was not visually inspected, and the startup-registration error was not captured; this does not establish a successful visual launch or a demonstrated product defect. The replacement candidate has not been launched for native visual acceptance.

## Candidate package

Candidate directory: `.scratch/media-headphones/candidate-review-fixes-20261013`  
Executable: `app/DesktopPet.Windows.exe`  
Executable SHA-256: `5A93FB21C2C9505AC40599A2184917C32609376D4EAED07C1AE7D627650BE31A`  
Windows application DLL SHA-256: `34444AFF7E30C4D5A23E17D85632E3B1BB44C75C4EB7ECA3960A91A5603A827A`  
Core DLL SHA-256: `8A9B2A12E6DF76CA8549550B04F72F62588DB5E731EEFA95EFBABEAEC0847BAA`  
Payload hash manifest: `.scratch/media-headphones/candidate-review-fixes-20261013.sha256.txt` (497 files).  
Source commit: `1512b797860d57c8af62fe656f4c4113180a5883` plus the worktree changes in this implementation.  
SDK: .NET `10.0.401`.

The package build succeeded. NuGet could not retrieve its online vulnerability index (`NU1900`) because network access was unavailable; the locked restore and publish completed with the local package cache.

The launcher executable hash is identical to the old build because product code resides in the DLLs. Use the candidate directory and Windows DLL hash to identify this build. The preceding candidates contain older accessory styles and are superseded by this package. The package embeds the exact reviewed original atlas, Crumb earbud atlas, and fitted poses. This candidate also passes 100 native-boundary reads per mute state against fake sessions with zero setters and unchanged volume/mute; no live audio was controlled.

The current published DLL additionally verifies 500 ms polling, transparent unknown-custom-name fallback, fake-native enumeration availability with a failed sole device versus a second usable device, and a persistent diagnostic in isolated workspace data. Review findings about brief sounds and tab/player mute were explicitly superseded by the user's revised scope; the four remaining findings were fixed and regression-tested. Build/test approval-service capacity errors were transient and resolved on normal retries. No native app launch was performed.
