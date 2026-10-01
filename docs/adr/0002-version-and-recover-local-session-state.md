# ADR 0002: Version and recover local session state

**Status:** Accepted

## Context

Hood needs durable per-user state for its needs, behavior mode, timers, placement, and roaming bounds. The YAML file holds user settings and must remain independent. A damaged state file must not silently erase the user's progress, and closing Hood must pause active-time timers.

## Decision

Store the session as a size-limited JSON document beside the per-user configuration, with an explicit schema version. Version 2 stores the current continuation data and is validated before use. Version 1 snapshots migrate to version 2 with initial needs and awake mode. Remaining durations are persisted directly; no wall-clock timestamp is used to advance them while Hood is closed.

If state cannot be read, parsed, validated, or restored, show a recovery window with a keep-and-exit choice and an explicit acknowledgement before reset. Reset archives the original state file before starting fresh. Replacement saves use a temporary file and atomic replacement so a failed save retains the previous document.

## Consequences

- Settings and session progress can be restored independently.
- New state fields require a schema migration or a deliberate rejection of older versions.
- Recovery preserves damaged input for inspection and requires a clear user action to discard it.
- Only active time in a running session advances durations.
