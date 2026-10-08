# Hood Notes — Feature Specification

## Problem Statement

The user wants to capture and manage small notes directly from Hood without launching a separate notes application. Notes must be easy to bring into view, edit, and put away while preserving the current desktop workflow. Multiple notes need independent windows, useful text formatting, reliable local storage, and recovery from accidental deletion.

## Solution

Add Hood's own locally stored notes app with compact, colored Sticky Notes-style windows. A completed left-click on Hood retains the existing pet reaction and toggles the previously open group of notes using a short slide from or toward Hood. Showing notes preserves the current application's keyboard focus. The first notes activation creates one blank note; later, if all notes have been individually closed, left-click opens the Notes list.

Preserve Hood's right-click menu and add a Notes submenu. Each note also has a header menu for note management. Provide a searchable Notes list, rich text editing, shared notes settings, individual note colors, and recoverable Trash. Automatically save content and window state locally, and restore the previous visible or hidden state when Hood restarts.

## User Stories

1. As a desktop user, I want to access my notes through Hood, so that capturing information fits into my existing desktop workflow.
2. As a desktop user, I want a left-click on Hood to show my previously open notes, so that I can bring my working notes into view together.
3. As a desktop user, I want another left-click to hide that group, so that I can clear my desktop quickly.
4. As a desktop user, I want Hood's existing click reaction to remain available when toggling notes, so that the companion still responds as before.
5. As a desktop user, I want clicking sleeping Hood to preserve his existing wake behavior while toggling notes, so that notes do not change how sleep works.
6. As a desktop user, I want dragging Hood to avoid toggling notes, so that repositioning the pet does not interrupt my notes workflow.
7. As a desktop user, I want spontaneous Hood reactions to leave note visibility alone, so that notes only appear when I request them.
8. As a desktop user, I want notes to slide briefly from or toward Hood when shown or hidden, so that their connection to Hood is visually understandable.
9. As a desktop user, I want the slide to retain my chosen resting positions, so that toggling notes does not rearrange my desktop.
10. As a desktop user, I want rapid repeated clicks to leave notes in a predictable visible or hidden state, so that animations do not accumulate or strand windows.
11. As a desktop user, I want showing notes to preserve my current application's keyboard focus, so that I do not accidentally type into a note.
12. As a desktop user, I want to click inside a visible note to begin editing, so that entering text is deliberate.
13. As a desktop user, I want my first notes activation to create one blank note, so that I can start without navigating management screens.
14. As a desktop user, I want later activations to reuse existing notes, so that toggling does not create duplicate blank notes.
15. As a desktop user, I want left-click to open the Notes list when all notes are individually closed, so that I can choose a saved note to reopen.
16. As a desktop user, I want Hood's right-click menu to retain its existing controls, so that adding notes does not remove care actions or settings.
17. As a desktop user, I want a Notes submenu in Hood's right-click menu, so that note management is available from the pet.
18. As a desktop user, I want every note to have a header menu, so that I can manage notes without returning to Hood.
19. As a desktop user, I want to create another note from either menu, so that I can capture separate topics quickly.
20. As a desktop user, I want multiple notes open simultaneously, so that I can consult and edit several topics together.
21. As a desktop user, I want each note to be an independently movable window, so that I can place it near the work it supports.
22. As a desktop user, I want to resize each note independently, so that its size suits its content.
23. As a desktop user, I want long note content to remain accessible through scrolling, so that a compact window does not limit what I can write.
24. As a desktop user, I want notes to sit behind other applications normally, so that they do not cover my work unnecessarily.
25. As a desktop user, I want optional always-on-top for notes, so that I can keep reference text visible when needed.
26. As a desktop user, I want notes always-on-top independent of Hood's setting, so that changing one does not change the other.
27. As a desktop user, I want note windows omitted from the taskbar and Alt+Tab, so that multiple notes do not clutter application switching.
28. As a desktop user, I want to reach those windows through Hood and the Notes list, so that omitting taskbar entries does not make notes inaccessible.
29. As a desktop user, I want closing a note window to keep its saved contents, so that closing does not mean deleting.
30. As a desktop user, I want individually closed notes excluded from group show/hide, so that notes I put away stay closed.
31. As a desktop user, I want to reopen a closed note through the Notes list, so that I can resume work on it later.
32. As a desktop user, I want opening an already open note to reuse its window, so that I do not edit conflicting copies.
33. As a desktop user, I want notes to remain reachable after monitor or display-layout changes, so that saved placement cannot strand my work off-screen.
34. As a desktop user, I want note controls and text readable at common display scaling levels, so that the app remains comfortable on my monitor.
35. As a desktop user, I want to type and edit multiline text, so that notes can contain more than a short label.
36. As a desktop user, I want to select, copy, cut, and paste text, so that note editing supports ordinary desktop workflows.
37. As a desktop user, I want bold formatting, so that I can emphasize important text.
38. As a desktop user, I want italic formatting, so that I can distinguish selected phrases.
39. As a desktop user, I want underline formatting, so that I have another way to mark important text.
40. As a desktop user, I want strikethrough formatting, so that I can mark text that is no longer current.
41. As a desktop user, I want supported text styles to combine on selected text, so that emphasis does not remove other formatting.
42. As a desktop user, I want to remove a selected style, so that I can return text to its normal appearance.
43. As a desktop user, I want bulleted lists, so that I can organize related points.
44. As a desktop user, I want numbered lists, so that I can express an ordered sequence.
45. As a desktop user, I want clickable checklist items, so that I can track completed tasks within a note.
46. As a desktop user, I want to check and uncheck an item without losing its text, so that task completion is reversible.
47. As a desktop user, I want to continue or end a list while typing, so that list editing feels natural.
48. As a desktop user, I want formatting toolbar buttons, so that supported editing features are discoverable.
49. As a desktop user, I want familiar keyboard shortcuts, so that common editing actions are quick.
50. As a desktop user, I want toolbar states to reflect the selected formatting, so that I can understand the text I am editing.
51. As a desktop user, I want undo and redo, so that I can correct editing mistakes.
52. As a desktop user, I want undo and redo to apply to the note I am editing, so that editing one note does not change another.
53. As a desktop user, I want text styles, list types, and checklist completion retained after reopening, so that saved notes preserve their meaning.
54. As a desktop user, I want a compact colored note design, so that each window feels like a sticky note.
55. As a desktop user, I want to choose a color for an individual note, so that I can distinguish it visually.
56. As a desktop user, I want individual color choices retained, so that global defaults do not erase my organization.
57. As a desktop user, I want a default color for new notes, so that creating notes uses my preferred appearance.
58. As a desktop user, I want a shared text-size setting, so that notes are comfortable to read.
59. As a desktop user, I want light, dark, and system theme choices, so that notes suit my desktop appearance.
60. As a desktop user, I want to disable show/hide animation, so that visibility changes can be immediate.
61. As a desktop user, I want shared notes settings available from Hood and note menus, so that configuration is easy to find.
62. As a desktop user, I want notes settings preserved across restart, so that I do not repeat configuration.
63. As a desktop user, I want a clear error when a setting cannot be saved or is invalid, so that I know whether the change took effect.
64. As a desktop user, I want a Notes list containing my active notes, including closed ones, so that all saved work is discoverable.
65. As a desktop user, I want recognizable text previews in the Notes list, so that I can identify notes without opening every window.
66. As a desktop user, I want to search note text, so that I can find a saved topic quickly.
67. As a desktop user, I want an understandable empty Notes list with a New note action, so that I can continue when no active notes remain.
68. As a desktop user, I want a clear no-results state when searching, so that I can distinguish a search mismatch from missing notes.
69. As a desktop user, I want deleting a note to move it to Trash, so that an accidental deletion is recoverable.
70. As a desktop user, I want deleted notes removed from active windows and the Notes list, so that deleted work does not return through group show/hide.
71. As a desktop user, I want to inspect Trash through note management, so that recovery is easy to discover.
72. As a desktop user, I want to restore a trashed note with its content and formatting, so that recovery preserves my work.
73. As a desktop user, I want Trash retained until I explicitly permanently delete notes, so that recovery does not expire unexpectedly.
74. As a desktop user, I want confirmation before permanent deletion, so that an irreversible action is deliberate.
75. As a desktop user, I want canceling permanent deletion to retain the note, so that reconsidering the action is safe.
76. As a desktop user, I want an understandable empty Trash state, so that I know when there are no deleted notes to recover.
77. As a desktop user, I want edits automatically saved locally, so that ordinary editing does not require a Save command.
78. As a desktop user, I want blank notes retained when I close them, so that deliberately created notes are not silently discarded.
79. As a desktop user, I want each note's content, formatting, color, position, and size restored, so that reopening continues my work.
80. As a desktop user, I want hiding notes to remember the open group, so that showing them restores the same working set.
81. As a desktop user, I want quitting Hood to save and close note windows, so that the app exits as one application.
82. As a desktop user, I want restarting Hood to restore visible notes when I quit with them visible, so that my desktop arrangement returns.
83. As a desktop user, I want restarting Hood to keep notes hidden when I quit with them hidden, so that reopening does not unexpectedly fill my desktop.
84. As a desktop user, I want my closed notes and Trash restored after restart, so that window visibility does not determine whether data survives.
85. As a desktop user, I want to use notes without an account or network connection, so that my work is available locally.
86. As a desktop user, I want failed saves reported while my previous saved data remains intact, so that I can respond to storage problems without silent data loss.
87. As a desktop user, I want damaged or unsupported saved notes explained and preserved before any reset, so that recovery does not silently erase my work.
88. As a desktop user, I want adding notes to preserve my existing Hood settings and session, so that the new feature does not reset the companion.
89. As a desktop user, I want menus, lists, settings, and editing controls usable through keyboard navigation with readable labels and contrast, so that precise pointer input is not required for every action.
90. As the project owner, I want deterministic tests of notes behavior and saved data, so that changes can be verified repeatably.
91. As the project owner, I want to manually assess Windows focus, editing, window integration, and visual quality, so that automated coverage stays focused on application behavior.

## Implementation Decisions

- **Existing architecture:** Follow the accepted WPF frontend and portable .NET core boundary. Keep notes lifecycle, persisted contracts, validation, and management behavior portable. Keep native windows, rich-text control adaptation, keyboard focus, taskbar/Alt+Tab policy, theme rendering, and slide presentation in the Windows frontend. Reuse existing configuration and replacement-write mechanisms where suitable; introduce only the smallest cohesive notes boundary. Do not introduce a plugin framework or synchronization infrastructure.
- **Application contract:** Provide one conceptual notes application contract used by both Hood controls and note management. Commands cover activation/toggle, create, edit, open, close, move/resize, color selection, search, settings, move to Trash, restore, confirmed permanent deletion, and quit/restore. Observable output covers note documents, open membership, group visibility, list/search results, Trash, settings, and actionable errors. Supply local storage and display/work-area information explicitly. Any deferred autosave or animation scheduling must be controllable in tests. This defines observable responsibilities rather than prescribing a class hierarchy.
- **Click integration:** A completed non-drag left-click invokes Hood's current reaction and notes activation once. Awake Hood retains his jump; sleeping Hood retains wake-only behavior without an added jump. Notes visibility does not depend on whether another pet action can start. Dragging, right-click, random events, and automatic pet actions do not toggle notes. Preserve existing care, sleep, roaming, and configuration behavior.
- **Open versus visible:** Distinguish an active note's membership in the open group from whether that group is currently visible. Group hide preserves membership; individual Close removes membership and retains the document. Show restores only the open group. Opening a closed note makes it part of the visible group; opening an already open note reuses its window. Create adds one blank note to the visible group. Showing or creating a note through Hood preserves keyboard focus until the user deliberately enters a note. Closing management or settings windows does not delete notes or quit Hood.
- **First activation and empty group:** If notes have never been activated and no active note has already been created through management, create exactly one blank note on first activation. Persist completion of this first-use behavior so deleting, closing, or trashing notes cannot repeatedly seed blank notes. After initial use, activation with an empty open group opens or reuses the Notes list instead of creating a note. The empty list provides New note. Restoring startup state is not itself the first user activation.
- **Windows and animation:** Each note is an independently movable, resizable, scrollable window, with no individual taskbar or Alt+Tab entry. Normal notes are not topmost; the shared notes always-on-top setting affects notes independently of Hood. Group transitions briefly slide from or toward Hood's current location, then settle at each note's saved bounds. Animation must not overwrite those bounds or capture keyboard focus. Repeated toggles converge to the latest requested state without a transition backlog or duplicate windows. With animation disabled, apply the same visibility semantics immediately. Use monitor work areas and logical coordinates; adapt inaccessible saved bounds to a usable display. Exact motion duration, travel distance, initial placement, minimum usable size, and styling are low-impact implementation choices subject to manual acceptance.
- **Editing contract:** Support multiline text and the four combinable inline styles: bold, italic, underline, and strikethrough. Support bulleted, numbered, and clickable checklist items, including persisted checked state. Applying toolbar formatting must preserve the intended selection. Enter continues a list at the same indentation. Enter on an empty nested item moves it out one level into its parent list; Enter on an empty outer item allows normal text to resume. Provide ordinary selection/copy/cut/paste, note-local undo/redo, and visible formatting states. Undo/redo covers text, formatting, list changes, and checklist toggles within an editing session; persistence of undo history across restart is not required. At minimum provide familiar copy/cut/paste, select-all, bold, italic, underline, undo, and redo shortcuts. Document additional shortcuts chosen for the remaining actions and avoid overriding standard editing keys. Unsupported pasted formatting must not discard readable text or introduce unsupported document content.
- **Document storage:** Give each note a stable identity and store enough versioned document structure to round-trip all supported text styles, list kinds, and checked states. Keep note content and per-note runtime data separate from Hood's pet continuation data and shared configuration. Use per-user local application storage, not the installation directory. Preserve active and trashed notes, per-note bounds/color, open-group membership, group visibility, and the first-use marker. Validate saved documents before applying them. Do not reuse the pet-state size limit as an implicit notes-content limit; choose and document practical bounds without silently truncating notes. Exact serialization and document representation are implementation choices within the portable-core contract.
- **Menus and discovery:** Hood's existing right-click menu gains a Notes submenu offering New note, Notes list, and Notes settings. Note headers offer a New note button; their menus offer Notes list, Note color, Delete note, and Notes settings. Access Trash only through the Notes list's separate Trash tab. Color and Delete target the originating note; shared management routes to the same app state. Each note has an optional customizable name editable in its header, saved with the note and retained through Trash/restore; unnamed notes display "Note". The Notes list includes active notes whether open or closed, displays names above text-derived previews, and searches names and note text without treating formatting as text. Reuse existing management windows rather than opening duplicate lists or settings windows.
- **Shared settings and appearance:** Provide a compact Sticky Notes-style design with colored backgrounds, a header menu, and a formatting toolbar. Shared settings cover default note color, text size, line spacing, light/dark/system theme, notes always-on-top, and animation on/off. They apply across notes. Line spacing has a numeric field and slider beside text size, accepts a value from 1 through 3, and defaults to normal spacing (2); lower values bring lines and paragraphs closer together. Valid Notes settings changes preview immediately without persisting configuration. Save keeps the changes; Cancel or closing the window restores the settings active when it opened. Each note retains its own color, with an individual override; changing the default color affects subsequently created notes. Text-size and line-spacing changes preserve inline styles, list kinds, and checked state. Default notes always-on-top is off; default animation is on. Initial palette, text size, and theme default are implementation choices that must be documented and remain readable with all supported backgrounds.
- **Configuration compatibility:** Extend the existing authoritative YAML configuration with notes settings rather than introducing a competing settings store. Existing configuration without notes settings loads with notes defaults and retains every existing Hood option. Validate new values and apply UI setting changes only after a successful save, following current configuration behavior. Preserve supported manual configuration edits and document any schema change or migration. Adding notes must not reset or reinterpret the existing pet session.
- **Autosave and lifecycle:** Automatically save edits, note names, checklist changes, color, placement/size, open-group changes, Trash operations, and visibility at appropriate boundaries. A small bounded editing debounce is acceptable; flush pending edits before close, hide, destructive operations, and clean exit. Use replacement writes that retain previous saved data on failure. Report unsaved changes clearly and retain editable in-memory content for retry where the session remains open; do not claim a failed save succeeded. On explicit quit, attempt to flush notes as well as save Hood, report failures, and close app-owned windows. Startup restores the last successfully saved documents and open/hidden state without playing a user-triggered show animation or taking keyboard focus.
- **Trash:** Delete moves a note into recoverable Trash and removes it from active windows, open membership, and active search. Retain its document, formatting, color, and bounds until explicit permanent deletion. Restore returns the note to the active Notes list as closed; the user can open it deliberately. Permanent deletion requires confirmation; cancel preserves the note. Trash does not expire automatically. Removing the last active/open note follows the empty-group behavior and never deletes other notes.
- **Failure and recovery:** Preserve the last valid saved data after a failed write. Follow the existing acknowledged recovery pattern for unreadable, malformed, or unsupported notes state: explain the error, preserve the source, and require an explicit user choice before resetting, with the original archived before a fresh start. A notes recovery action must not reset Hood's pet state or configuration. Reject invalid settings, document structures, and geometry with actionable errors. Silent resets, silent truncation, and destructive fallback to blank notes are unacceptable.

## Testing Decisions

- **Confirmed seam:** The user confirmed automated testing of application behavior and saved data, with Windows visuals, focus, and display handling checked manually. Exercise the single conceptual notes application contract used by the UI, including its rich document representation, using controlled display descriptions, controllable deferred work where needed, and temporary local storage. Test actual configuration parsing and notes serialization at this seam. Native rich-text control operations and presentation remain manual acceptance boundaries.
- **Good tests:** Send user-level commands and inspect documents, open membership, visibility, list/search results, Trash, settings, saved/restored state, and errors. Avoid private methods, class layout, exact animation frames, timer internals, real delays, and dependence on the tester's monitor arrangement. A fake storage boundary is useful for deterministic errors; it must not replace successful round-trip tests through the real local store.
- **Prior art:** Existing HoodSession behavior tests exercise supplied time, randomness, and displays. HoodSessionPersistenceTests cover real JSON round trips, earlier-schema migration, separate configuration/state restoration, damaged-data preservation, and failed replacement writes. HoodConfigurationTests cover YAML defaults, independent settings, validation, preservation of other values, and failed saves retaining effective settings. Follow these patterns for notes rather than introducing Windows automation.
- **Activation and lifecycle acceptance:** Verify one blank note on first activation, no repeated seeding after close/delete/restart, open-group show/hide, individually closed notes staying closed, list fallback when the open group is empty, and no duplicate note or management instances. Verify each completed click toggles once and retains the existing awake/sleep reaction contract; drag and spontaneous reactions do not toggle. Repeated toggles and disabled animation must produce the same final visibility and preserve saved bounds. Quit/restart must retain the visible or hidden group and all closed notes.
- **Editing and management acceptance:** Exercise multiline content, combined and removed inline styles, bullets, numbering, checklist toggles, note-local undo/redo, and document round trips. Verify active-list search and empty/no-results states, opening closed notes, per-note color retention, shared settings effects, and independence from Hood's always-on-top preference. Verify Trash excludes notes from the active group/search; restore retains content/formatting/color/bounds; retention survives restart; confirmed permanent deletion removes only the chosen note; cancel changes nothing.
- **Data and failure acceptance:** Verify autosave scheduling and flush boundaries through controlled deferred work, missing storage, blank-note persistence, configuration upgrades/defaults, supported schema migrations, malformed/unsupported documents, invalid settings/bounds, save failures, retry, and previous-data preservation. Test recovery acknowledgement and archiving without touching pet state. Supply changed display layouts to test reachable placement policy without testing native monitor APIs. Verify stable note identities and absence of cross-note content changes.
- **Manual Windows acceptance, performed by the user:** Launch the built app and verify both menus, actual rich-text selection and formatting, typing/list continuation, checklist clicking, toolbar states, shortcuts, undo/redo, clipboard behavior, scrolling, keyboard navigation, and theme contrast. Check independent drag/resize, absence from taskbar/Alt+Tab, independent always-on-top, real monitor/DPI recovery, and focus preservation while showing notes, creating the initial blank note, and restoring startup windows. Inspect the slide from/toward Hood, stable resting positions, rapid toggles, and animation-off behavior. Actual editor adaptation must preserve formatting across close/reopen and restart. Record failures and the tested build before declaring Windows/visual acceptance complete.

## Out of Scope

- Synchronization, accounts, remote storage, synchronization conflict handling, and mobile clients.
- Integration with or importing data from Microsoft Sticky Notes; resemblance defines this agreed workflow and appearance rather than complete feature parity.
- Attachments, images, drawing, reminders, notifications, and additional document-editing features beyond the specified text and list support.
- Independent notes processes, separate startup registration for notes, or notes remaining open after Hood quits.
- Individual note taskbar/Alt+Tab entries and per-note overrides of shared settings other than color.
- Automatic Trash expiration and persistence of undo/redo history across restart.
- General plugin systems, speculative synchronization abstractions, and a replacement for the existing WPF frontend.
- Automated native Windows UI or operating-system integration tests; manual acceptance remains required.
- Application implementation, ticket creation, or issue-tracker publication as part of creating this specification.

## Further Notes

- **Authority and confirmation:** This document records the confirmed notes interview on 2026-10-02. The user approved the final scope and all three proposed defaults: shared settings with individual color overrides, retained Trash with confirmation for permanent deletion, and Notes list fallback when all notes are closed. The same confirmation covered behavior/data automation and manual Windows acceptance; no new seam checkpoint is needed for this synthesis.
- **Relationship to Hood:** This specification extends the existing Hood specification. Existing pet behavior remains authoritative except for the explicitly added notes actions. The current implementation has a WPF frontend, a portable core, YAML configuration, versioned local pet state, and behavior-level tests. The earlier first-release specification's statements that no code or prior test art exists describe its original creation context; they do not describe the current repository.
- **Architecture references:** Respect the accepted decisions on the WPF Windows frontend and versioned, acknowledged local-state recovery. Keep any significant new storage/editor decision documented during implementation, including alternatives, trade-offs, and compatibility. No new ADR or implementation is required merely to publish this feature spec.
- **Implementation discretion:** The exact palette, initial note placement and dimensions, slide timing/distance, serialization format, documented editing debounce, text-size range, and additional shortcut assignments remain low-impact implementation choices within these contracts. Undo history is session-local, and restored Trash notes return closed. These refinements do not extend the confirmed feature scope.
- **Rollout and future work:** Deliver notes behavior, local persistence, configuration guidance, and automated behavior tests together, then perform user-owned Windows/visual acceptance. Synchronization can be scoped later; local storage does not imply an approved future cloud architecture or conflict policy.
