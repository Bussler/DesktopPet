# Hood UI Styles — Feature Specification

## Problem Statement

The user wants to try different modern appearances for Hood without changing how Hood or Notes works. Appearance values are currently spread across shared note resources, individual windows, and a separate tray menu. This makes experiments repetitive and can leave menus, notes, and settings looking inconsistent. The user needs concise, central definitions for the app's visual parts and a simple way to select a complete style while retaining existing data and controls.

## Solution

Provide four app-wide styles: Sticky Notes, Minimal, Soft, and Studio. Sticky Notes retains the present design and remains the default. Minimal uses compact spacing, subtle borders, restrained corners, and blue accents. Soft uses warmer surfaces, rounder corners, more breathing room, and green accents. Studio uses crisp panels, stronger typography and selection emphasis, and purple accents. Each style supplies distinct light and dark fills for the five saved note-color categories. The approved illustrations define their design direction rather than exact browser-rendered pixels.

Add a global style selector to Hood Settings. Every style supports Light, Dark, and System and preserves each note's saved color category while displaying its style-specific fill. Preview the selected style immediately and consistently across note windows and their menus, Hood's context menu and submenus, the Notes list including Trash, Notes Settings, Hood Settings, and the tray menu. Save persists the choice; Cancel or closing Settings restores the appearance from before the menu opened.

Keep central visual definitions concise enough to change shared colors, typography, spacing, shapes, and interaction states without repeatedly editing each screen. Preserve the app's existing editing, management, focus, persistence, and pet behavior. Work in a separate Codex worktree carrying the current uncommitted changes. Complete the relevant automated checks for each ticket and retain its isolated candidate. Per-ticket manual reviews are currently deferred at the user's direction. Finish with a separate integrated manual acceptance pass against the final package.

## User Stories

1. As a desktop user, I want four named app styles, so that I can compare the current appearance with three modern alternatives.
2. As a desktop user, I want Sticky Notes to remain the default, so that installing the feature does not unexpectedly change the app's design.
3. As a desktop user, I want to return to Sticky Notes after trying another style, so that experimentation is reversible.
4. As a desktop user, I want a compact Minimal style with quiet borders and blue accents, so that the interface feels clean and unobtrusive.
5. As a desktop user, I want a warmer Soft style with rounder shapes and gentler spacing, so that the interface feels relaxed.
6. As a desktop user, I want a crisp Studio style with purple accents and stronger visual emphasis, so that controls and selections are easy to distinguish.
7. As a desktop user, I want the three alternatives to differ in shape, spacing, and emphasis as well as color, so that they offer meaningful design choices.
8. As an app maintainer, I want shared colors defined concisely in one central place, so that a palette change can reach the whole app consistently.
9. As an app maintainer, I want corner shapes defined centrally, so that menus, cards, and controls can follow the same style direction.
10. As an app maintainer, I want shared spacing defined centrally, so that compactness can be adjusted without editing every screen.
11. As an app maintainer, I want interface typography defined centrally, so that labels and headings remain consistent across the app.
12. As an app maintainer, I want common hover, selection, and focus appearances defined centrally, so that interactive elements communicate their states consistently.
13. As an app maintainer, I want concise definitions for distinct visual parts, so that note surfaces can retain their color while menus and settings follow the same overall style.
14. As an app maintainer, I want styles to reuse the existing interface structure, so that maintaining several appearances does not multiply the app's behavior implementations.
15. As a desktop user, I want the global style selector in Hood Settings, so that its app-wide effect is easy to discover.
16. As a desktop user, I want the selector to show my saved style, so that I can see which appearance is configured.
17. As a desktop user, I want Save to commit my selected style, so that a selection has the same clear confirmation as other settings.
18. As a desktop user, I want Cancel or closing Settings to restore my previous style, so that browsing live previews does not permanently change the app.
19. As a desktop user, I want my style choice restored after restart, so that I do not need to select it every time.
20. As a desktop user, I want a light version of every style, so that all four choices work on a light desktop.
21. As a desktop user, I want a dark version of every style, so that all four choices work on a dark desktop.
22. As a desktop user, I want System mode available for every style, so that the interface can follow Windows appearance.
23. As a desktop user, I want style and Light/Dark/System mode to be separate choices, so that selecting a visual design does not force its brightness.
24. As a desktop user, I want my existing theme preference retained, so that adding the global selector does not reset it.
25. As a desktop user, I want System mode to respond to Windows appearance changes, so that supported live surfaces continue to match my desktop.
26. As a desktop user, I want newly opened windows to use the effective app style, so that they do not revert to an earlier appearance.
27. As a desktop user, I want reopened menus and submenus to use the effective app style, so that detached menu surfaces remain consistent.
28. As a desktop user, I want supported open surfaces to refresh after saving a style, so that I can compare appearances without restarting unnecessarily.
29. As a desktop user, I want a clear notice when a preview cannot be applied, so that I know which appearance remains visible.
30. As a desktop user, I want appearance changes to avoid a lasting mixture of incompatible styles, so that the app continues to feel coherent.
31. As a desktop user, I want repeated style changes to produce the same predictable result, so that earlier choices do not leave stale colors or shapes behind.
32. As a desktop user, I want a settings save failure to retain the previous saved and effective style, so that an unsuccessful change is not silently applied.
33. As a desktop user, I want a readable save error and a way to retry, so that I can recover without losing the previous configuration.
34. As a desktop user, I want invalid style values in manually edited configuration to produce a clear validation error, so that unsupported input is not silently accepted.
35. As a desktop user, I want older configuration without a style choice to load as Sticky Notes, so that my current installation remains compatible.
36. As a desktop user, I want supported manual configuration edits to take effect on restart, so that the documented configuration remains authoritative.
37. As a desktop user, I want changing appearance to preserve unrelated Hood and Notes settings, so that styling does not reset my preferences.
38. As a desktop user, I want changing appearance to preserve Hood's needs, mode, timers, and position, so that experimentation does not restart the companion's session.
39. As a desktop user, I want every note to retain its saved color category across styles, so that visual organization survives global changes.
40. As a desktop user, I want each style to render the five saved color categories with distinct, style-tuned fills, so that differently colored notes remain distinguishable together.
41. As a desktop user, I want light and dark note fills to remain recognizable and readable in every style, so that the saved color categories stay clear without sacrificing contrast.
42. As a desktop user, I want note text retained exactly when styles change, so that an appearance experiment cannot alter my work.
43. As a desktop user, I want note names retained, so that I can still identify and search my notes.
44. As a desktop user, I want formatting, lists, and checklist completion retained, so that style changes preserve the meaning of my notes.
45. As a desktop user, I want live restyling to retain caret, selection, scroll position, and undo/redo history, so that I can continue editing where I left off.
46. As a desktop user, I want open, hidden, closed, and trashed notes to keep their existing membership and visibility, so that styling does not open or remove notes.
47. As a desktop user, I want note positions and sizes retained, so that style experiments do not rearrange my desktop.
48. As a desktop user, I want Hood and Notes always-on-top preferences to remain independent, so that appearance changes do not alter window placement behavior.
49. As a desktop user, I want show/hide animation preferences retained, so that selecting a style does not change how notes appear and disappear.
50. As a desktop user, I want note headers and formatting controls to retain their existing focus-based visibility, so that a new design does not add unwanted desktop clutter.
51. As a desktop user, I want restyling to preserve keyboard focus, so that an appearance change does not direct typing into another window.
52. As a desktop user, I want note header menus to follow the selected style, so that management controls match their notes.
53. As a desktop user, I want color submenus and their selected-color marks to remain readable, so that choosing a note color works in every style.
54. As a desktop user, I want Hood's needs rows to remain readable and informational, so that a visual redesign does not hide status or turn it into an action.
55. As a desktop user, I want existing Feed, Sleep, roaming, Notes, Settings, and Quit actions preserved, so that a style choice does not remove controls.
56. As a desktop user, I want existing editor context menus and settings dropdowns to follow the selected style, so that popup controls do not fall back to an unrelated appearance.
57. As a desktop user, I want the active Notes and Trash views to use the selected style, so that note management is visually consistent.
58. As a desktop user, I want search, empty-list, and no-results states to stay readable, so that styling does not make saved notes harder to find.
59. As a desktop user, I want selected notes and existing multi-selection behavior preserved, so that list actions continue to target the intended notes.
60. As a desktop user, I want both settings windows to use the selected style, so that application and Notes preferences look related.
61. As a desktop user, I want text-size fields, sliders, checkboxes, and existing validation to remain usable, so that restyling does not change supported settings or their ranges.
62. As a desktop user, I want my chosen note text size retained, so that interface typography does not override my reading preference.
63. As a desktop user, I want the tray menu to match the selected style and theme, so that the app's alternate entry point feels consistent.
64. As a desktop user, I want the tray menu's existing actions and keyboard behavior retained, so that styling does not change how I open Settings or quit.
65. As a desktop user, I want disabled and informational controls visibly distinguishable from enabled actions, so that each control's availability is understandable.
66. As a desktop user, I want readable hover and pressed states, so that pointer interaction is clear in every style.
67. As a desktop user, I want visible keyboard focus and selection states, so that I can navigate without relying on the mouse.
68. As a desktop user, I want readable text, carets, selected text, errors, and checked states, so that light and dark appearances remain practical for editing.
69. As a desktop user, I want controls to remain usable at common Windows scaling levels, so that larger text or high-density displays do not clip the interface.
70. As a desktop user, I want each style to fit the existing minimum window sizes, so that a more spacious design does not hide essential actions.
71. As a desktop user, I want long note names and ordinary long labels handled safely, so that text does not overlap buttons or menus.
72. As a desktop user, I want native Windows message boxes to remain familiar and functional, so that styling does not interfere with operating-system-owned dialogs.
73. As a desktop user, I want configuration replacement to preserve the prior valid file after failure, so that saving a style cannot corrupt my settings.
74. As an app maintainer, I want a shared appearance change to reach all supported surfaces, so that adding a style does not require separate screen-by-screen implementations.
75. As the project owner, I want this feature prepared and implemented in a separate Codex worktree, so that current development remains undisturbed.
76. As the project owner, I want my current uncommitted changes included in that worktree, so that styling builds on the latest behavior fixes.
77. As the project owner, I want the original checkout and its current changes left intact, so that I can continue other work independently.
78. As the project owner, I want relevant automated checks completed after each implementation ticket, so that regressions are caught before proceeding.
79. As the project owner, I want automated ticket checks followed by one final integrated manual acceptance pass, so that visual acceptance applies to the complete final package.
80. As the project owner, I want a concrete final package and executable identity, so that manual acceptance applies to the build I actually tried.
81. As the project owner, I want automated results distinguished from manual acceptance, so that passing tests are not mistaken for verified desktop appearance.
82. As the project owner, I want focused usage and customization documentation, so that I can switch styles and adjust their central definitions later.
83. As the project owner, I want the established user-facing package layout retained, so that internal tickets and evidence do not become shipped application content.
84. As the project owner, I want final visual defects recorded against the tested package, so that a later rebuilt package does not inherit unverified acceptance.

## Implementation Decisions

- Keep the accepted portable Core and Windows frontend architecture. Core owns the persisted style identifier and validation; Windows owns C# visual definitions, WPF resources/templates, theme resolution, and the native tray adapter. Do not add Windows or WPF dependencies to Core or introduce a new UI toolkit.
- Reuse the shared Notes appearance and resource mechanism, existing settings controller, and existing application appearance refresh path. Expand them into the common app appearance path rather than building a parallel styling framework. A small mechanical prefactor may centralize the existing style first while preserving behavior.
- Use concise C# definitions for a style's shared palette and dimensions, with clearly named values for distinct parts such as note chrome, menu surfaces, settings surfaces, list cards, typography, borders, focus, hover, and selection. Common WPF templates consume those values; they are not copied wholesale for every style. Keep genuinely necessary per-part differences explicit and avoid speculative abstractions.
- Treat the four style identifiers as a finite persisted contract: sticky-notes, minimal, soft, and studio. Add the app-level configuration field ui_style with sticky-notes as its default. Accept supported names case-insensitively and save their canonical lowercase form. Reject all other identifiers, including the former existing value. Missing input uses the default; explicit blank, null, numeric, or unknown input is invalid. Use the existing configuration validator and atomic save path. No Notes or Hood session-data migration is needed.
- Keep the existing Notes theme preference as the single authoritative Light/Dark/System choice and apply its meaning to every in-scope surface. Retain the existing Notes Settings theme control and make its app-wide scope clear. Do not add a competing theme preference or reset an older theme choice. The new style selector lives in Hood Settings.
- Sticky Notes retains the current notes/menu design and light appearance as the compatibility baseline. Completing dark/System support for previously unthemed settings and tray surfaces is an intentional extension of that baseline. Keep transparency and interaction semantics where already supported; the new styles follow their approved visual directions without requiring literal browser-pixel matching.
- Minimal uses compact spacing, restrained corners, subtle borders, and blue accents. Soft uses rounder controls, warm light and dark neutral surfaces, gentler spacing, and green accents. Studio uses crisp panels, purple accents, stronger heading/selection emphasis, and restrained edge accents. Avoid decorative animations or visual effects beyond the agreed appearance work.
- The style and pet selectors preview immediately without changing the authoritative configuration. Capture the original appearance when Settings opens. Save persists the preview choices and keeps them active on close; Cancel, the window close button, or another unsaved close restores the original style and sprites. Reopening displays the saved choices. Other settings retain their existing Save behavior. Validation or save failure leaves the previous persisted configuration intact, restores the original appearance, and presents a recoverable error.
- Apply previews and restoration through shared resources and the existing dispatcher/application refresh path. Include open notes, management/settings windows, and future/reopened popup surfaces; synchronize the tray renderer from the same effective definition. Preserve existing windows and editor documents during refresh rather than reconstructing them. If a preview fails, restore the previous coherent appearance and report the error without saving the failed choice.
- Keep the effective preview style separate from the saved configuration while Settings is open. Newly opened surfaces use the same effective style and participate in restoration. Pet previews replace only artwork, preserving current animation progress, needs, modes, timers, and placement. Use retained original assets for restoration. If restoration fails, keep Settings open with a recoverable error so closing can be retried.
- System mode follows the Windows app appearance, using the existing platform-change notification path. Explicit Light or Dark remains fixed. A restart fallback must also explain any corresponding deferred System-mode behavior; otherwise supported live surfaces update together.
- Preserve each note's saved color category and content. Each style supplies distinct light and dark fills for yellow, blue, green, pink, and lavender; switching style changes the displayed fill only and never rewrites note data. Preserve text-size preference, names, formatting, lists, checklist states, undo/redo, caret/selection, scroll position, geometry, visibility, focus, and independent topmost settings.
- Style all named surfaces, including nested WPF popups and dropdowns that have separate resource trees. Adapt the existing Windows Forms tray menu through a small renderer/appearance adapter using the same style values; retain its native actions, placement, keyboard behavior, and resource lifetime handling. Matching native tray styling need not duplicate WPF templates pixel for pixel. Native Windows message boxes retain operating-system rendering.
- The worktree includes the current modified tracked files and untracked Windows settings/stats regression tests. Do not reset, stash, stage, commit, or merge the source checkout's changes as part of this feature without separate instruction. Preserve the original checkout throughout planning and implementation.
- Document only significant or hard-to-reverse deviations from the accepted architecture. Favor the smallest cohesive change and existing platform components over additional dependencies or theme engines.

## Testing Decisions

- The user confirmed these boundaries: automated settings/configuration behavior, automated Windows interaction behavior, and final native Windows/visual acceptance. Test observable effects through existing settings, storage, and window interaction boundaries rather than asserting private class layouts or mirroring the style definitions.
- At the configuration/settings boundary, cover round trips for all four style identifiers, supported casing, rejection of the former existing identifier, Light/Dark/System preservation, a missing ui_style default, invalid explicit style input, unrelated preferences, atomic save failure, and retention of the previous persisted/effective style. Reuse HoodConfigurationTests and the existing controller/store failure tests as prior art.
- At the Windows interaction boundary, exercise immediate selection previews, repeated changes, Save, Cancel, window close, failed preview, failed save, failed restoration and retry, and reopen. Verify unsaved previews leave configuration and note data untouched and that pet restoration keeps current animation progress. Cover Sticky Notes, Minimal, Soft, and Studio in light/dark variants, including every note-color category. System resolves to both effective modes using controlled platform input where practical. Verify open and newly opened windows, detached popup/submenu/dropdown resources, and the tray appearance adapter reflect the effective selection. Reuse SettingsAndStatsTests, NoteInteractionTests, and NotesListInteractionTests rather than inventing a separate test framework.
- Include meaningful regressions for preserved editor color, caret/selection/scroll and undo/redo during live refresh, note-color identity, visible informational stats, selected-color/checklist indicators, existing search/multi-selection, theme changes, and repeated style switching. Observe behavior and displayed state through established fixtures; do not create a broad pixel-golden suite that simply repeats the implementation.
- Retain Core Notes/session regression coverage to establish that appearance changes leave data, visibility membership, bounds, needs, modes, and timers intact. Use isolated temporary storage for persistence tests and deterministic fixtures for error paths. Do not alter the user's live data directory for automated testing.
- Finish the relevant automated checks for each implementation ticket before proceeding. Automated fixture windows and render assertions are allowed throughout implementation and do not count as manual acceptance. Build the Windows frontend explicitly through the project workflow because Core/solution test results alone may not compile every frontend change.
- Run the full established locked-restore, sprite verification, Release tests/build, and self-contained package workflow once all implementation slices are integrated. Preserve the package's established README, app, and docs layout; retain internal planning and verification evidence outside it. Record checks and failures honestly.
- Per-ticket manual reviews are deferred at the user's direction. Run each ticket's automated checks before proceeding. After final integration automation, perform the single native Windows review against the final package and record its exact path and executable hash. If a manual check finds a defect, fix it, rerun relevant automation, rebuild, and repeat affected checks against the new candidate.
- The final checklist compares all four styles and all five style-specific note fills in light and dark modes, checks System switching, and covers all named surfaces including the tray and nested popups. Inspect contrast, disabled/hover/pressed/selected/focus states, text/caret/checkbox visibility, keyboard navigation, menu hit testing, focus preservation, dragging/resizing, minimum window sizes, long labels, and 100%, 150%, and 200% display scaling. Exercise live refresh or the disclosed restart fallback, restart persistence, and representative existing Notes/Hood behavior with isolated acceptance data.
- Record the exact final package path and executable hash alongside the environment and manual results. A passing automated suite, hidden-window render test, illustrative mockup, or successful package build is not native visual acceptance. If final manual testing finds a defect, repair it, rerun appropriate automation, produce a new final candidate, and repeat the affected final acceptance checks; do not transfer acceptance to a changed executable.

## Out of Scope

- Feature implementation or ticket execution during this specification/ticket-planning step.
- Additional styles beyond Sticky Notes, Minimal, Soft, and Studio; downloadable themes, user-imported themes, runtime theme editors, per-note style choices, or per-window independent style/theme preferences.
- Browser CSS support, replacing WPF/Windows Forms, introducing a third-party control suite, or building a general theme/plugin engine.
- Changes to Hood's artwork, animation assets, behavior, needs, timers, or roaming rules.
- New Notes capabilities or changes to existing note content, formatting, deletion/recovery, multi-selection, storage, shortcuts, focus, and visibility semantics.
- Redesigning roaming-area editor or recovery windows beyond the named surfaces, and overriding operating-system-owned dialogs or desktop/taskbar appearance.
- Accounts, network services, synchronization, cloud storage, telemetry, cross-platform frontends, or operating-system integration unrelated to appearance.
- Treating a per-ticket review or mockup as final acceptance of the integrated app, automatic merging into the original checkout, or treating automated evidence as native acceptance.

## Further Notes

The interview and visual direction were confirmed in this conversation. The user explicitly authorized carrying current uncommitted changes into a separate Codex worktree, accepted live updates where practical with restart as a fallback, confirmed preservation of individual note colors and Light/Dark/System modes, and approved the tray scope and native message-box exception. Per-ticket automated checks and candidate packages are complete. The user later deferred manual review for now; final integrated manual acceptance remains at the end.

Approved design illustrations are retained in the worktree's session material for Minimal, Soft, and Studio, each with light/dark examples. They illustrate appearance; their shortened menu content is not a request to remove existing actions, and their sample note content is not application data. Consult the established Hood and Notes specifications for existing behavior to preserve. The accepted WPF frontend and local session-recovery ADRs remain in force; no competing architecture or data store is introduced.

The existing tray uses Windows Forms while most other surfaces use WPF, and WPF submenus have detached resource trees. These are implementation risks to verify at the confirmed automation boundaries and final native acceptance. Live refresh must preserve active editing state; if safely preserving it requires excessive complexity, the approved restart fallback is preferable.

The six-ticket dependency-aware implementation breakdown was approved on 2026-10-04 and published as local issue files in the feature worktree. This specification/ticket-publication step does not execute implementation tickets, merge the worktree, or perform early manual testing.

## Approved refinements (2026-10-04)

The current style is named Sticky Notes, uses the persisted identifier sticky-notes, and is the default when no style is configured. The previous identifier existing is invalid. Each style defines visibly distinct light and dark fills for all five saved note-color categories; style changes preserve the category, note content, and behavior.
