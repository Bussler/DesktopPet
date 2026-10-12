# Project overview

Hood is a local Windows desktop app built with C# and .NET 10. The WPF frontend owns desktop integration; the portable core owns behavior and persisted contracts. There is no account, synchronization service, or mobile frontend in the current project.

## Repository structure

| Location | Responsibility |
| --- | --- |
| [DesktopPet.Core](../DesktopPet.Core) | Pet/session behavior, configuration, placement policies, Notes models and editing operations, and file stores. Targets `net10.0` without WPF. |
| [DesktopPet.Windows](../DesktopPet.Windows) | WPF overlay and Notes windows, appearance, rich-text rendering, assets, tray, startup, and monitor integration. Targets `net10.0-windows`. |
| [DesktopPet.Tests](../DesktopPet.Tests) | Portable behavior and persistence tests. |
| [DesktopPet.Windows.Tests](../DesktopPet.Windows.Tests) | Frontend interaction, rendering, and script tests on Windows. |
| [Assets](../Assets) | Runtime sprite sheets/manifests, retained artwork sources, and the embedded Hood icon. |
| [tools](../tools) | Build, test, packaging, artwork helpers, and manual acceptance checklists. |
| [docs](README.md) | User guides, developer references, specifications, ADRs, and historical research. |

`DesktopPet.sln` brings the four projects together. `global.json` pins the SDK, `Directory.Packages.props` pins package versions, and `Directory.Build.props` enables nullable references, implicit usings, and restore lock files. See [build and development](build.md) for the supported workflows.

## Runtime responsibilities

[App.xaml.cs](../DesktopPet.Windows/App.xaml.cs) loads configuration, Notes, session state, pet assets, and displays, then coordinates app-owned windows and the tray. It presents recovery choices before proceeding with invalid data and handles final Notes/session saves on Quit.

[HoodSession](../DesktopPet.Core/HoodSession.cs) advances needs, naps, reactions, movement, and animation using active elapsed time. Closed or suspended time does not advance the simulation. [HoodOverlayWindow](../DesktopPet.Windows/HoodOverlayWindow.xaml.cs) renders frames and translates desktop input into session commands. Placement policies keep the pet and notes within usable display areas; Windows-specific display discovery stays in the frontend.

[NotesApplication](../DesktopPet.Core/NotesApplication.cs) owns active/trashed notes, visibility, document edits, saving, search, and session-local undo/redo. [NoteWindow](../DesktopPet.Windows/NoteWindow.xaml.cs) adapts WPF rich text and embedded editors to the semantic document; its partial files cover tables, Markdown, math, links, and list interactions. Notes persistence stores supported document structure rather than arbitrary WPF markup. [NotesListWindow](../DesktopPet.Windows/NotesListWindow.xaml.cs) presents search and lifecycle actions.

Appearance previews update open UI immediately. Settings keep the original choices for Cancel/close rollback and persist choices on Save. Pet switching and sizing preserve the ongoing session. See [UI style development](ui-style-development.md) and the [style specification](specs/UI-Styles-SPEC.md).

## Persistence contracts

The default root is `%LOCALAPPDATA%\HoodDesktopPet`, overridden at startup by a nonblank `HOOD_DATA_DIRECTORY`. All three stores share that root. Artwork stays beside the executable. See [data recovery](recovery.md) for operational instructions.

| Store | Contract |
| --- | --- |
| [YamlConfigurationStore](../DesktopPet.Core/HoodConfiguration.cs), `config.yaml` | Authoritative settings, schema 1; strict key/value validation and a 64 KiB read limit. Missing configuration creates defaults; invalid input is preserved. |
| [JsonSessionStore](../DesktopPet.Core/JsonSessionStore.cs), `state.json` | Session schema 2, with schema 1 migration; needs, remaining timers, and placement. Read limit: 64 KiB. Remaining durations preserve active-time behavior across restarts. |
| [JsonNotesStore](../DesktopPet.Core/JsonNotesStore.cs), `notes.json` | Notes schema 8, with versions 1–7 migrated on load; content, formatting, list depth, colors, bounds, names, Trash, and open/hidden state. Size limit: 16 MiB. Undo history is not persisted. |

[AtomicFile](../DesktopPet.Core/AtomicFile.cs) uses temporary files and replacement writes to retain the previous saved document when a replacement fails. Accepted Notes edits save immediately; a failure retains editable in-memory changes and exposes Retry save. Invalid session/Notes input requires an explicit recovery choice and is archived before resetting that store. Configuration errors require correction and relaunch.

Preserve older supported formats when changing contracts. New schema fields need deliberate validation and migration. An upgrade may write a format an older executable cannot read; backups are necessary before rollback. The [session ADR](adr/0002-version-and-recover-local-session-state.md) explains the recovery decision.

## Verification and documentation

Keep behavior tests deterministic by controlling elapsed time, randomness, display descriptions, and storage at existing boundaries. Add regressions at the behavior or frontend seam affected by a change. `tools\Test.ps1` is the full automated workflow; `tools\Build-AndPublish.ps1` packages independently. Native acceptance covers visible rendering, physical input, tray, clipboard, and multiple monitor/DPI setups against the packaged app.

Use [specifications](README.md#specifications-and-history) for agreed requirements and ADRs for significant architecture choices. The [performance investigation](research/performance-findings.md) preserves historical measurements and their limits; its figures are not measurements of the current checkout. Record session evidence in `.codex/` and update durable guides when supported behavior or workflows change.
