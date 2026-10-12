# Hood Performance — RAM and Notes Latency Specification

## Problem Statement

Hood consumes approximately 200 MB of RAM on the user's machine, and Notes can feel sluggish while typing or formatting. Users want a lighter desktop companion and responsive Notes without losing visual quality, editing features, saved data, or familiar controls.

The investigation reproduced a pet workload around 214–226 MiB total working set. The decoded sprite atlas alone occupies 97.5 MiB. Repeated bitmap crops and duplicated Notes style resources add allocation or retained-memory costs. Hidden sprites and Settings previews retain large decoded images.

Notes performs whole-document processing during edits and rebuilds the entire editor for small formatting changes. With real saving enabled, bold-formatting a small selection took median 40.70 ms in a 100-paragraph fixture and 174.72 ms in a 500-paragraph fixture. Whole-note Markdown recognition, toolbar scans, repeated history accounting, saving, and list refresh also increase typing cost.

These are measured workloads, not a diagnosis of every byte in the user's exact session. Edit latency, resident memory, committed memory, and managed allocation are distinct measurements. The reported RAM usage and a 60 Hz frame interval do not establish universal product budgets.

## Solution

Improve the current Windows/WPF app in small, verified changes. Decode sprites at the resolution needed by the current display, avoid repeating identical image work, and share common Notes appearance resources while keeping each note's content and mutable state independent.

Make ordinary inline formatting local to the selection. Limit Markdown recognition and toolbar updates to relevant content, coalesce content-driven Notes-list refreshes, and avoid reconstructing historical text merely to enforce existing undo limits. Reduce unnecessary decoded-image retention during hidden operation and appearance previews while preserving reveal and Cancel behavior.

Preserve all current editing, autosave, recovery, appearance, sprite replacement, pet behavior, and visibility contracts. Each change must show an improvement in its targeted workload and pass behavior regressions before the next change is accepted. Larger changes to saving policy, asset packaging, or hidden-editor lifetime remain follow-ups rather than being silently bundled into this delivery.

## User Stories

1. As a desktop user, I want Hood to consume less RAM during ordinary use, so that more memory remains available for my other applications.
2. As a desktop user, I want the pet to retain its current appearance after optimization, so that a lighter app does not make the artwork look worse.
3. As a desktop user, I want crisp sprites at 100% display scaling, so that reducing memory does not introduce blur or neighbouring-frame artifacts.
4. As a desktop user, I want crisp sprites at 125%, 150%, and 200% scaling, so that the app remains readable on my display.
5. As a desktop user, I want Hood to adjust correctly when moved between displays with different scaling, so that the pet remains sharp and correctly sized.
6. As a desktop user, I want the pet's logical size to remain unchanged, so that optimization does not alter its desktop footprint.
7. As a desktop user, I want hit testing and dragging to remain aligned with the pet, so that my existing mouse interactions still work.
8. As a desktop user, I want every animation action to retain its timing and sequence, so that the companion behaves as before.
9. As a desktop user, I want the pet's movement to continue during visually unchanged animation frames, so that reducing repeated drawing does not interrupt roaming.
10. As a desktop user, I want still poses to use fewer resources, so that an idle pet remains lightweight.
11. As a desktop user, I want switching among Hood, Pip, Crumb, and Puddle to remain immediate and coherent, so that appearance choices remain convenient.
12. As a desktop user, I want appearance changes to preserve current action, needs, timers, and placement, so that changing the artwork does not restart the pet.
13. As a desktop user, I want a new pet's artwork to appear even when it uses the same frame position as the previous pet, so that switching never leaves a stale image.
14. As a desktop user, I want custom sprites with a valid manifest to keep working, so that optimization does not invalidate my artwork.
15. As a desktop user, I want incompatible or malformed assets to produce the existing actionable error, so that I can restore a valid pet without guessing.
16. As a desktop user, I want an unsuccessful preview to preserve the last valid appearance, so that a loading failure does not blank or corrupt the pet.
17. As a desktop user, I want the existing sprite replacement format and workflow to remain supported, so that I do not need to regenerate assets for this improvement.
18. As a desktop user, I want hidden-sprite startup to avoid retaining unnecessary sprite pixels, so that Notes-only use is lighter.
19. As a desktop user, I want Notes, Settings, and other owner-dependent controls to work while sprites are hidden, so that hiding the pet does not remove access to the app.
20. As a desktop user, I want revealing a hidden pet to restore its correct current appearance, so that the pet does not restart or display an obsolete preview.
21. As a desktop user, I want hidden operation to preserve paused pet timing, so that elapsed hidden time does not change existing behavior.
22. As a desktop user, I want a failed reveal to report its loading error without losing my notes or session, so that unrelated work remains safe.
23. As a desktop user, I want rapidly hiding and revealing sprites to produce the requested final state, so that repeated commands do not strand the pet or duplicate its windows.
24. As a desktop user, I want Settings previews to use less memory, so that trying another pet does not unnecessarily increase the app's footprint.
25. As a desktop user, I want Cancel to restore the appearance active when Settings opened, so that previews remain reversible.
26. As a desktop user, I want closing Settings without saving to restore the original appearance, so that closing retains its existing meaning.
27. As a desktop user, I want Save to retain the selected appearance across restart, so that successful settings changes remain durable.
28. As a desktop user, I want repeated preview changes to release obsolete appearances, so that browsing pets does not retain every preview.
29. As a desktop user, I want Cancel to remain coherent if an asset file changes during a preview, so that rollback does not depend on rereading a changed source.
30. As a desktop user, I want creating several Notes to add less memory overhead, so that multiple small notes remain practical.
31. As a desktop user, I want every note to retain its own content, so that sharing appearance resources never shares my text.
32. As a desktop user, I want every note to retain its own color, so that changing one note does not recolor another.
33. As a desktop user, I want every note to retain independent selection, scroll position, and geometry, so that resource sharing does not couple my editors.
34. As a desktop user, I want all supported UI styles to remain available, so that performance work preserves my chosen appearance.
35. As a desktop user, I want light, dark, and system Notes themes to remain correct, so that shared resources do not use another window's theme.
36. As a desktop user, I want system-theme changes to refresh active surfaces consistently, so that the app remains readable when Windows changes appearance.
37. As a desktop user, I want valid text-size and line-spacing edits to preview immediately, so that I can judge the settings before saving.
38. As a desktop user, I want Cancel or closing Notes settings to restore the previous effective appearance, so that previews remain transactional.
39. As a desktop user, I want invalid settings input to retain the last valid preview, so that an invalid value does not damage the editor layout.
40. As a desktop user, I want menus, toolbars, checklist markers, links, and equations to remain readable in each theme, so that lower memory use does not reduce contrast.
41. As a desktop user, I want closing a note to release its editor resources after cleanup, so that closed windows are not retained indefinitely.
42. As a desktop user, I want hiding a group of Notes to preserve its current editors and positions, so that showing the group resumes my work as before.
43. As a desktop user, I want ordinary typing in a small note to remain responsive, so that optimization does not make the common case slower.
44. As a desktop user, I want typing in a long note to involve less work, so that existing content does not unnecessarily delay new input.
45. As a desktop user, I want literal punctuation elsewhere in a note to remain unchanged when I type, so that an unrelated edit does not reinterpret older content.
46. As a desktop user, I want incomplete Markdown to remain editable text, so that I can finish the expression without losing characters.
47. As a desktop user, I want completed supported Markdown to convert as before, so that faster recognition preserves my writing workflow.
48. As a desktop user, I want escaped markers to remain literal, so that I can write about Markdown syntax.
49. As a desktop user, I want marker deletion and edits across paragraph boundaries to behave correctly, so that limited recognition does not leave stale formatting.
50. As a desktop user, I want supported Markdown paste to handle every affected paragraph, so that pasted content is not partially formatted.
51. As a desktop user, I want code-block paste to preserve multiline text, blank lines, indentation, and Markdown-looking content in one code box, so that code stays literal.
52. As a desktop user, I want editing math source to preserve that source literally, so that unrelated syntax recognition does not consume an expression.
53. As a desktop user, I want plain URLs and labeled links to complete and remain editable as before, so that reducing parsing does not break web references.
54. As a desktop user, I want moving the caret away from a pending URL to complete it correctly, so that link creation retains its existing interaction.
55. As a desktop user, I want formatting a small selection to avoid the delay of recreating the whole note, so that emphasis feels immediate.
56. As a desktop user, I want bold, italic, underline, and strikethrough to preserve other styles, so that changing one property does not erase another.
57. As a desktop user, I want removing a style to affect only the selected property and text, so that surrounding formatting remains intact.
58. As a desktop user, I want formatting selections across multiple runs and paragraphs to work, so that local changes still support real documents.
59. As a desktop user, I want empty-selection formatting to retain normal typing behavior, so that I can choose the style for subsequent text.
60. As a desktop user, I want the toolbar to reflect caret and mixed-selection state accurately, so that faster updates remain trustworthy.
61. As a desktop user, I want selection and focus preserved after formatting, so that I can continue editing the intended text.
62. As a desktop user, I want my scrolled viewport to remain stable when an edit does not require movement, so that formatting or paste does not jump to another part of the note.
63. As a desktop user, I want unrelated tables, lists, equations, and links to retain their content and interaction during nearby edits, so that a local change does not disturb the rest of my note.
64. As a desktop user, I want table-cell edits to preserve cell identity and neighbouring contents, so that incremental changes remain within their intended location.
65. As a desktop user, I want numbered lists and checklists to retain correct markers and checked state, so that their presentation and saved content remain consistent.
66. As a desktop user, I want normal Enter to retain the current element's behavior, so that performance work does not change editing semantics.
67. As a desktop user, I want Ctrl+Enter to create a new line outside the complete current element, so that the existing block-exit shortcut keeps working.
68. As a desktop user, I want a focused rendered equation to remain the target of block-exit commands, so that a stale editor caret does not change the wrong paragraph.
69. As a desktop user, I want undo to recover the previous text and formatting, so that optimized edits remain reversible.
70. As a desktop user, I want redo to restore the undone change accurately, so that the editing history remains useful.
71. As a desktop user, I want undo-restored syntax to remain literal until I deliberately change or complete it, so that automatic recognition does not immediately cancel undo.
72. As a desktop user, I want edits made after undo to retain the existing redo rules, so that history does not apply obsolete changes.
73. As a desktop user, I want each note's history to remain independent, so that editing one note does not alter another's undo state.
74. As a desktop user, I want the existing history limits preserved, so that reduced accounting cost does not remove additional history or allow uncontrolled growth.
75. As a desktop user, I want Unicode text, tabs, and line endings to keep correct selection and formatting boundaries, so that faster offset calculations do not select the wrong characters.
76. As a desktop user, I want editing one note to leave other saved notes unchanged, so that reducing snapshot work does not introduce shared mutable content.
77. As a desktop user, I want a blank note to remain inexpensive and fully functional, so that creating an empty editor is still useful.
78. As a desktop user, I want the visible Notes list to show my latest names and previews promptly, so that reduced refresh work does not leave outdated information.
79. As a desktop user, I want active and Trash searches to use current text, so that delayed visual refresh does not hide matching notes.
80. As a desktop user, I want my list selection to survive content refreshes where the selected note still matches, so that typing does not interrupt note management.
81. As a desktop user, I want opening or returning to the Notes list to show current content, so that work skipped while it was invisible is caught up.
82. As a desktop user, I want creation, closing, deletion, restoration, and permanent deletion to update membership correctly, so that coalesced content refreshes do not delay management actions.
83. As a desktop user, I want rapid edits to settle on the latest preview, so that an older queued refresh cannot overwrite newer information.
84. As a desktop user, I want accepted edits to retain the current immediate autosave behavior in this delivery, so that faster editing does not silently weaken persistence.
85. As a desktop user, I want saved notes to restore with the same semantic content and appearance, so that existing documents are not reparsed or migrated unnecessarily.
86. As a desktop user, I want a failed save to preserve the previous valid file and my editable in-memory content, so that I can retry without losing work.
87. As a desktop user, I want Retry save to persist the latest accepted content, so that recovery does not write an older snapshot.
88. As a desktop user, I want close, hide, destructive actions, and clean exit to retain their save/flush behavior and failure feedback, so that optimized workflows remain safe.
89. As a desktop user, I want invalid or oversized edits rejected without silent truncation, so that existing document and store limits remain meaningful.
90. As a desktop user, I want damaged saved data preserved for acknowledged recovery, so that performance work does not silently reset notes, configuration, or the pet session.
91. As a desktop user, I want keyboard shortcuts, focus, and existing menu actions to remain available, so that responsiveness improvements do not change how I operate Hood.
92. As a desktop user, I want Notes-group toggling to retain my other application's keyboard focus, so that lighter hidden operation does not redirect my input.
93. As a maintainer, I want benchmark fixtures and counter definitions recorded with results, so that I can reproduce and assess the improvements.
94. As a maintainer, I want memory, edit latency, allocation, and native visual acceptance reported separately, so that a gain in one metric is not presented as proof of another.
95. As a maintainer, I want before/after results tied to the tested source and executable, so that performance claims remain traceable.
96. As a desktop user, I want optimizations introduced in verified steps, so that a lower footprint does not come at the expense of correctness.

## Implementation Decisions

- **Architecture:** retain .NET 10/WPF for the Windows frontend and the portable behavior/persistence core, following the accepted architecture decisions. Reuse existing asset, appearance, editor, Notes application, and storage boundaries. No new UI framework, general cache framework, or dependency is required.
- **Delivery order:** first address display-sized sprite decoding and local inline formatting; then unchanged-rectangle reuse and shared Notes resources; then changed-region recognition, toolbar/list work, and history accounting. Apply hidden/preview image-retention improvements after the asset ownership and rollback behavior are verified. Complete each slice's relevant checks before accepting the next.
- **Display-sized decoding:** validate source assets against their original manifest and retain that authoritative metadata. Derive decoded pixel dimensions and crop coordinates from the target display scale without changing logical size or the session definition's behavior contract. Reload or replace the decoded representation on an effective DPI change. Preserve full available source quality when the display needs it; avoid sampling across cell boundaries.
- **Asset compatibility:** preserve all four packaged pets, supported custom PNG/manifest combinations, animation timing/frame counts, motion bounds, asset errors, and the sprite replacement workflow. A cosmetic switch must not reset needs, timers, current action/frame, or placement. A failed load keeps the last valid usable appearance where the existing operation supports rollback.
- **Unchanged-frame reuse:** compare the displayed source rectangle and decoded asset identity. Skip crop creation/source assignment for unchanged pixels while continuing simulation and positioning. A pet or decoded-asset change must invalidate this comparison, even if rectangle coordinates match. Keep this as a small rendering guard rather than caching every decoded frame.
- **Appearance resource sharing:** share stable style/template/theme dictionaries by their effective variant. Keep note palette overrides, mutable appearance previews, documents, controls, and window state local. This uses WPF resource sharing in the sense of a flyweight; a separate pattern class hierarchy is unnecessary. Theme/style transitions must not mutate a shared variant into another window's appearance.
- **Hidden assets:** permit metadata/session/owner initialization without decoding a full sprite sheet when it is not needed for display. Hidden operation must still support Notes and owner-dependent actions. On reveal, resolve the correct current asset and DPI; restore existing timing semantics and use existing loading-error behavior. Releasing decoded pixels also requires dropping any displayed crop's source ownership.
- **Preview ownership:** retain a coherent original appearance until Save or successful rollback. Release obsolete preview references after replacement, while allowing normal deferred WPF cleanup. Display-sized decoding applies to previews too. A compressed-source rollback snapshot may replace a decoded original if it demonstrably reduces retention and preserves the original bytes/metadata; a file path alone is insufficient. This representation choice must not weaken Cancel or unsaved-close behavior.
- **Local inline formatting:** update only the selected inline properties when structure does not change. Preserve combined styles, mixed selections, embedded content, accepted semantic state, and existing history granularity. Validate the intended semantic change and commit it once. Ordinary local formatting must avoid whole-document replacement; use a conservative existing path where a selection genuinely requires structural handling rather than losing semantics.
- **Structural editing and recovery:** limit structural changes to affected blocks where practical. Keep remaining document replacement/recovery deferred past the active WPF edit transaction. Retain caret, selection, focus, and scroll restoration where geometry permits. Storage failure keeps accepted editable content for retry; validation/editor failure uses the existing last-accepted-state recovery behavior.
- **Changed-region recognition:** use changed ranges, neighbouring block context, syntax completion/deletion, and pending-link state to select applicable recognizers. Preserve the necessary wider path for paste and structure changes. Older unrelated punctuation must not invoke an entire whole-note recognizer chain for an ordinary edit. Reuse a pass's text and offset projections where ownership/version permits. Alphabetic input cannot be excluded categorically because it can complete a URL or other supported input.
- **Toolbar and offsets:** derive formatting/list state from relevant selection/blocks and coalesce internal selection updates. Preserve normalized stored-text offsets, Unicode/line-ending behavior, and mixed-state indicators. Do not substitute native text extraction that includes synthetic list markers or embedded-control spaces.
- **Notes-list refresh:** coalesce pending content-driven refreshes and preferably update the changed preview. Model queries and membership actions use current state immediately. Preserve selected identities and active/Trash filtering. Defer content-only visual work for an invisible list; opening, searching, or returning to it must show current results. An older deferred refresh must not overwrite a newer state.
- **History accounting:** store character counts for owned history entries and maintain totals as entries move, are discarded, or are cleared. Preserve the existing per-stack 100-entry and 2,000,000-character limits, undo/redo behavior, and mutable-data ownership. Do not replace these limits with a new byte or aggregate budget in this delivery.
- **Persistence:** preserve current immediate accepted-edit persistence, safe replacement writes, retry, failure feedback, lifecycle flushes, and acknowledged recovery. Reduce redundant copying/projection only when snapshot isolation remains demonstrable. Save failures must not be reported as successful or discard the latest accepted in-memory document. No independently concurrent writes or mutable live documents passed to a worker are introduced by this scope.
- **Contracts:** preserve Notes schema version 7 and its supported migrations, the existing configuration and pet-session schemas, document/store limits, list/Trash lifecycle, and public data-boundary semantics. This feature requires no persistence migration or new user setting. Existing Notes, Markdown/math/link, and UI-style specifications remain authoritative for user interactions.
- **Acceptance metrics:** require a demonstrated reduction in the targeted cost, measured against the unmodified source with the same fixture, counter, collection boundary, elapsed time, runtime, and DPI. Keep evidence of meaningful regressions in other affected workflows and resolve them before acceptance. Use the prototype values as reference evidence, not universal hardware-independent caps.

| Workstream | Required observable acceptance | Reference evidence |
| --- | --- | --- |
| Sprite decoding | At 100% DPI, the packaged 32-bit atlas uses the required half-resolution representation, reducing its pixel storage to one quarter while preserving 192 × 208 logical frame size; higher DPI and valid custom assets remain correct | 97.5 → 24.375 MiB atlas; isolated pet roughly 214–226 → 142–144 MiB working set |
| Rectangle reuse | No redundant crop/source change when the decoded asset and displayed rectangle are unchanged; movement and action timing remain correct | 797 → 229 assignments, 569 → 0 consecutive duplicates in the 60-second probes |
| Shared appearance | Ten blank Notes retain less managed/private resident memory than the matched baseline, with no cross-note appearance or state leakage | Managed heap 47.04 → 27.24 MiB; private working set 193.39 → 177.86 MiB |
| Local formatting | Ordinary plain-text inline formatting changes no unrelated content, avoids whole-document replacement, and reduces edit median/p95 and UI-thread allocation with real saving | 100 paragraphs: 40.70 → 7.79 ms median; 500: 174.72 → 22.38 ms |
| Typing recognition | An unrelated existing marker does not cause unnecessary whole-note recognition; completion, paste, deletion, and URL behavior stay correct; affected fixtures show lower latency/allocation | A no-conversion 500-paragraph pass cost 12.21 ms and 11.24 MiB allocation |
| Toolbar/list/history | Observable state stays current and history limits remain exact; targeted caret, visible-list, or long-history workloads show reduced work/allocation | Visible list raised 25-note typing from 15.15 to 32.69 ms; high-history model updates allocated 2.62 MiB versus 0.68 MiB in the lower-history fixture |
| Hidden/preview assets | Hidden startup avoids an unnecessary decoded atlas; obsolete preview images are not kept by app ownership; reveal and complete rollback remain correct | Hidden probe 182.85 MiB working set; preview transient 412.93 MiB |

## Testing Decisions

- **Confirmed plan:** on 2026-10-08 the user confirmed regressions through real pet/Settings/Notes workflows with temporary storage, controlled Release comparisons of RAM/edit latency/allocation, and separate native Windows acceptance for DPI, keyboard, clipboard, and visual smoothness.
- **Main seam:** exercise the real Windows workflows with the actual portable session/Notes application and owned synthetic assets/data. Drive user-level editing, formatting, appearance, and visibility operations; inspect resulting semantic documents, current appearance, session state, list/search results, history, save errors, and real stored/reloaded data. Reuse the existing STA window fixtures, controlled time/randomness/displays, and Dispatcher-drain helpers. Avoid creating a parallel implementation solely for tests.
- **Independent evidence boundary:** use isolated process workloads for performance counters and native presentation for visual acceptance. These cannot be replaced by semantic unit assertions. Additional lower-level contract regressions are justified for asset coordinate validation, snapshot ownership, history budgets, and injected storage failure, rather than duplicating every workflow at several layers.
- **Good tests:** assert externally observable behavior and state preservation, not private field layouts, class names, resource dictionary counts, timer implementation, or a particular parser algorithm. A regression must be capable of failing on the previous behavior. Resource/source-change instrumentation may support the performance assertions without becoming the sole proof of editing correctness.
- **Prior art:** extend the existing Settings/Stats interaction tests for preview/Cancel, hidden sprites, ownership, theme refresh and rollback; Notes interaction/emphasis/block/code/math/link/table/clipboard tests for real editor behavior; Notes-list tests for selection/search/Trash; and Notes application/session persistence tests for actual JSON round trips, failed replacement writes, retry, history and lifecycle. Existing DPI-awareness tests verify the executable manifest but do not replace monitor/DPI visual acceptance.
- **Pet coverage:** test every packaged pet and representative valid custom assets, source-manifest validation, scaled crop boundaries, unchanged logical bounds, action/frame continuation, repeated rectangles, same-coordinate asset switching, effective DPI changes, missing/corrupt assets, hidden startup/reveal, and rapid toggles. Supply controlled display descriptions where possible rather than depending on the test machine's layout.
- **Preview/resource coverage:** change pet/theme/style repeatedly, Save, Cancel, and close without saving; include failed loading/rollback and changed source files. Open multiple notes with differing colors and verify local state isolation during theme changes and transactional settings previews. Verify closed editors and obsolete preview objects become eligible for cleanup after deferred work, without requiring immediate operating-system memory reclamation.
- **Editor coverage:** include ordinary and marker-containing text, small/long documents, incomplete/escaped/completed syntax, paste across blocks, deletion and paragraph boundaries, plain URL completion/caret departure, and undo-restored literal syntax. Check saved text/kinds/styles rather than synthetic native list-marker text.
- **Formatting coverage:** test selection-free typing styles, partial/mixed/multiline selections, combining/removing each inline style, code, tables, lists/checklists, links, valid/invalid math source and rendered equations, toolbar mixed-state values, undo/redo independence, and selection/caret/viewport restoration. Include the existing normal Enter and Ctrl+Enter contract, including a focused embedded equation with a stale editor caret.
- **Storage/list coverage:** use actual temporary JSON storage for successful immediate-save/restart scenarios and injected failure for deterministic error paths. Verify isolation across notes, original-file preservation, latest-content retry, no silent truncation or reset, close/hide/Trash/permanent-delete/exit behavior, existing history limits, and schema compatibility. Assert active/Trash searches and selected identities after rapid edits, invisible-list updates, and membership changes.
- **Clipboard:** use the established in-memory clipboard event helper for deterministic automated transfer regressions. Preserve literal multiline code and mixed semantic content. Check real Windows clipboard interactions separately; shared-system-clipboard contention must not make the automated suite nondeterministic.
- **Performance protocol:** compare baseline and candidate in at least three paired fresh-process repetitions. Use the pinned SDK/runtime, Release architecture, same tiered-compilation setting, appearance, asset/data fixtures, DPI, warmup, sample count, persistence mode, and collection boundaries. Record run-to-run variation, median, p95, maximum, allocations, store calls, and relevant source-change events. Performance runs are separate from deterministic behavior tests, so machine-speed assertions do not become brittle CI unit tests.
- **RAM workloads:** retain the 5-second and 60-second pet comparisons, original/required-size decode, repeated-frame rendering, one/ten blank Notes, shared-resource candidates, hidden startup, preview switching/Cancel, and open/hide/close cleanup. Record total/private working set, private commit, managed heap, collection boundaries and elapsed times separately. Add a longer matched soak to assess remaining native growth; a one-minute commit rise is not sufficient to claim an indefinite leak.
- **Latency workloads:** retain 10/100/500-paragraph fixtures with approximately 918/9,198/45,998 characters; ordinary typing, an unrelated literal underscore, 1/25 saved notes with and without the visible list, selected formatting with real JSON saving, supported Markdown completion, and lower/full history. Use WPF text composition and verify accepted content after every edit; repeated replacement of the same selected character is not a typing benchmark. Component timings and complete Dispatcher/layout-cycle timings must be labelled separately.
- **Performance assessment:** require the targeted improvement to exceed observed run-to-run variation. Check median/p95 and allocations together where applicable; do not hide increased tail latency behind a faster median. A deterministic change such as reduced decoded pixel dimensions or zero redundant unchanged-rectangle assignments complements process measurements. Record unresolved regressions rather than declaring success from a single favourable sample.
- **Native acceptance:** validate an identified package/executable, retaining its path/hash, at 100/125/150/200% scaling and across monitors. Check sprite quality/size/hit testing, focus-preserving Notes visibility, physical keyboard shortcuts and typing, native clipboard, mixed rich content, menus/themes, preview rollback, scrolling, and frame presentation under representative use. Edit-cycle duration alone cannot establish actual FPS.
- **Completion evidence:** each implementation slice needs its relevant regressions plus matched benchmark evidence before the next slice is accepted. The complete change needs the project's explicit validation workflow, sprite validators and Release tests, followed by the required native checks. Packaging alone is not test execution. Neither the findings nor this specification constitutes an implementation, passing regression suite, or native release acceptance.

## Out of Scope

- Replacing WPF, moving to another UI framework, adding a mobile/cloud platform, synchronization, or a new persistence service.
- New editor features, changed Markdown/math/link syntax, shortcuts, menus, animation behavior, appearance choices, data limits, or persistence schemas.
- A global promise of less than 200 MB RAM, a universal 60 FPS target, or applying a probe's speedup factor to frame rate.
- Forced production GC, reduced text quality, unconditional downscaling on high-DPI displays, a blanket bitmap cache, or increasing timer frequency as substitutes for reducing work.
- Prepacked state strips, a demand-loaded frame-cache/asset-format redesign, or automatic destruction of long-hidden Notes editors. These lack a measured implementation and introduce additional compatibility/lifetime work.
- Lazy creation of optional menus as a required deliverable; its isolated benefit has not been measured.
- Background/debounced persistence or changes to the current immediate accepted-edit save contract in this delivery. An ordered writer is a follow-up that needs a defined pending-save interval and failure/flush semantics before changing that contract.
- A new aggregate/byte history budget, immutable-model redesign, or speculative general structural-sharing layer.
- Tickets, application implementation, deployment, or external issue publication as part of creating this specification.

## Further Notes

- **Status and provenance:** specification created on 2026-10-08 from the conversation, the [performance findings](../research/performance-findings.md), current source snapshot `067a6074c4924c5c716d382d3e5693f3b0472b5d`, and the confirmed testing plan. The findings and their linked local evidence distinguish measured results, prototypes, estimates, and incomplete acceptance. No production optimization has been implemented by this document.
- **Existing specifications:** the [Notes specification](Notes-SPEC.md), [Markdown/math/links specification](Notes-Markdown-Math-Links-SPEC.md), [UI Styles specification](UI-Styles-SPEC.md), and [base specification](SPEC.md) continue to govern existing behavior. The accepted [Windows frontend decision](../adr/0001-wpf-windows-frontend.md) and [session recovery decision](../adr/0002-version-and-recover-local-session-state.md) remain in force.
- **Saving follow-up:** the older Notes specification permits a small bounded editing debounce, while current implementation and regressions guarantee immediate accepted-edit persistence. The conversation has not selected a maximum pending duration or changed this current contract. Retain it for this delivery. A future ordered writer should own immutable snapshots, prevent stale completion/write ordering, expose pending/failure state, flush latest content at lifecycle/destructive boundaries, and explicitly document the interval vulnerable to abrupt process termination.
- **Estimates:** a full-resolution 16-frame strip is roughly 9.75 MiB, but no demand-loaded asset implementation has established its CPU, transition, memory, or compatibility behaviour. Likewise, reclaiming hidden editors trades memory for rebuilding and state restoration. Do not turn these estimates into acceptance claims for the initial optimizations.
- **Resource risks:** decode/crop rounding, neighbouring-cell interpolation, high-DPI monitor movement, mutable shared theme state, retained crop references, and original-preview ownership need explicit verification. WPF/native resource release is deferred; lower app ownership or committed memory is not automatically an equal resident-memory reduction.
- **Baseline limits:** the experiments used isolated synthetic workloads and selected diagnostic collections. Probe builds reported NU1900 when vulnerability metadata could not be retrieved; this was an environment limitation, not permission to bypass validation. RAM markers used while developing probes are not product limits. Native input-to-pixel/FPS acceptance remains unperformed for these prototypes.
- **Rollout:** retain original source artwork and sprite replacement compatibility. Record before/after evidence per slice and refresh the performance findings after verified implementation. Keep internal specifications and diagnostic bundles separate from the existing user-facing package documentation allowlist.
