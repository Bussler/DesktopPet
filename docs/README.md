# Documentation

Start with the [app README](../README.md) for installation, daily use, local data, and a first build. This directory contains user guides and source-project references.

## User guides

These files ship with the app, together with the Hood animation preview and third-party notices.

| Guide | Purpose |
| --- | --- |
| [Notes](notes.md) | Create, edit, format, search, close, and restore notes. |
| [Focus](focus.md) | Manual focus/break timers, pet reminders, and Windows notification settings. |
| [Configuration](configuration.md) | Settings, audio-following headphones, YAML defaults and bounds, and Windows startup. |
| [UI styles](ui-styles.md) | Appearance, theme, and preview behavior. |
| [Placement](placement.md) | Dragging, roaming areas, monitors, and scaling. |
| [Sleep](sleep-and-recovery.md) | Needs, naps, waking, and paused timers. |
| [Data recovery](recovery.md) | Storage, backups, isolated profiles, save errors, and recovery. |
| [Changing sprites](changing-sprites.md) | Replace artwork and understand animation manifests. |
| [Hood animations](hood-animations.md) | Actions, frame timing, and the pose preview. |
| [Third-party notices](third-party-notices.md) | Bundled library, font, and runtime notices. |

## Project documentation

These references belong to the source checkout and are excluded from published packages.

| Reference | Purpose |
| --- | --- |
| [Build and development](build.md) | Compile, test, publish, troubleshoot, and maintain assets. |
| [Project overview](project.md) | Repository structure, runtime responsibilities, persistence, and validation boundaries. |
| [UI style development](ui-style-development.md) | Shared style tokens and extending the existing styles. |
| [WPF frontend decision](adr/0001-wpf-windows-frontend.md) | Why the Windows frontend uses WPF with a portable core. |
| [Session recovery decision](adr/0002-version-and-recover-local-session-state.md) | Versioned session data and acknowledged recovery. |
| [Animation assets](previews/pet-animations/README.md) | Alternate-pet viewer, source assets, and regeneration. |
| [Artwork concepts](previews/pet-prototypes/README.md) | Retained concept variants and selected character designs. |

## Specifications and history

Specifications record agreed behavior and refinements. They preserve their original story numbering; the user guides and project overview describe current operation. The first-release plan and dated performance measurements are historical context, rather than current task lists or new performance guarantees.

| Document | Scope |
| --- | --- |
| [Base specification](specs/SPEC.md) | Pet behavior, placement, configuration, and local continuity. |
| [Notes specification](specs/Notes-SPEC.md) | Notes lifecycle, editing, management, and persistence. |
| [Markdown, math, and links](specs/Notes-Markdown-Math-Links-SPEC.md) | Rich Notes input, rendering, and clipboard contracts. |
| [UI styles specification](specs/UI-Styles-SPEC.md) | Shared appearance and preview/rollback behavior. |
| [Media headphones specification](specs/Media-Headphones-SPEC.md) | Audible Spotify/browser detection and headphones across all pet animations. |
| [Media headphones acceptance](specs/Media-Headphones-ACCEPTANCE.md) | Automated evidence, native acceptance gaps, and the candidate package identity. |
| [Focus mode specification](specs/Focus-Mode-SPEC.md) | Manual focus/break cycles, pet reminders, a timer window, and Windows notification-control fallback. |
| [Focus mode acceptance](specs/Focus-Mode-ACCEPTANCE.md) | Delivered mode, automated evidence, package identity, and pending native checks. |
| [Performance specification](specs/Performance-SPEC.md) | Measured optimization requirements and acceptance. |
| [First-release implementation plan](specs/IMPLEMENTATION_PLAN.md) | Original milestones and story coverage. |
| [Performance investigation](research/performance-findings.md) | Dated measurements, methods, limitations, and local evidence pointers. |

Native acceptance checklists live in [tools/manual-acceptance](../tools/manual-acceptance). Session findings and scratch evidence go in the ignored `.codex/` directory; durable behavior, architecture, and workflow documentation belongs here.
