# First-release story coverage

This matrix maps every first-release story in `SPEC.md` to implementation evidence and an acceptance route. Behavior evidence is automated where the specification confirms the session seam; WPF, OS, display, and visual checks remain manual. The candidate checklist is [manual-acceptance.md](manual-acceptance.md), with recorded outcomes in [acceptance-results.md](acceptance-results.md).

| Story | Coverage route | Evidence |
| ---: | --- | --- |
| 1 | Windows desktop launch | Ticket 01; candidate launch checklist. |
| 2 | Transparent artwork and click-through | `tools/Test-HoodSpriteTransparency.ps1`; packaged-candidate manual checklist. |
| 3 | Initial work-area placement | `HoodSessionTests.Start_UsesThePrimaryWorkAreaAndControlledIdleFrame`; Ticket 01 checklist. |
| 4 | Roaming remains within bounds and leaves time for idle | `HoodSessionTests.Advance_RoamsWithinBoundsAndRelievesBoredomOnlyForMovementTime`; `HoodSessionTests.Advance_RoamingLeavesTimeForTheIdleAnimation`; Ticket 09 checklist. |
| 5 | Reveal edit mode | Ticket 08; area editor checklist. |
| 6 | Move area on both axes | `HoodPlacementTests`; Ticket 08 checklist. |
| 7 | Resize the area | `HoodPlacementTests`; Ticket 08 checklist. |
| 8 | Preserve pet and motion footprint | `HoodPlacementTests.ResizeRoamingArea_EnforcesTheMinimumPetFootprintAndContainsHood`; Ticket 08 checklist. |
| 9 | Hide the outline after editing | Ticket 08; area editor checklist. |
| 10 | Drag pet and area together | `HoodPlacementTests`; Ticket 07 checklist. |
| 11 | Distinguish drag from click | Ticket 07 drag-threshold behavior and manual checklist. |
| 12 | Move between monitors | Supplied-display placement scenarios in `HoodPlacementTests`; Ticket 07 manual checklist. |
| 13 | Clamp placement to usable monitor space | `HoodPlacementTests`; Ticket 07 manual checklist. |
| 14 | Recover after monitor disconnect | Supplied-display relocation scenarios in `HoodPlacementTests`; candidate manual checklist. |
| 15 | Adapt saved placement to display changes | `HoodPlacementTests` restore/update-display scenarios; Ticket 07 checklist. |
| 16 | Disable automatic roaming | `HoodConfigurationTests.SaveAndApply_PersistsBehaviorTogglesIndependently`; Ticket 09 checklist. |
| 17 | Drag while roaming is disabled | `HoodPlacementTests.MoveTo_RemainsAvailableWhenRoamingIsOffAndDoesNotWakeSleepingHood`; Ticket 09 checklist. |
| 18 | Re-enable roaming and retain area | `HoodPlacementTests`; Ticket 09 checklist. |
| 19 | Keep idle animation subtle and visible between roaming bouts | Animation timings in [hood-animations.md](hood-animations.md); roaming-rest regression test; candidate visual checklist. |
| 20 | Disable idle independently | `HoodSessionTests.IdleAnimationToggle_FreezesRestingFramesWithoutStoppingNeedsOrDirectReactions`; Ticket 09 checklist. |
| 21 | Keep other actions available with idle off | Session tests for Feed, sleep, movement, and random reactions; Tickets 09–10. |
| 22 | Awake left-click reaction | `HoodSessionTests.Advance_CompletesTheJumpAndReturnsToAnIdleFrame`; candidate input checklist. |
| 23 | Open the nearby pet menu | Pet menu in Ticket 12; candidate menu checklist. |
| 24 | Feed from the menu | `HoodSessionTests.Feed_ReducesHungerToZeroFloorPersistsNeedsAndFinishesItsDedicatedAction`; candidate menu checklist. |
| 25 | Dedicated Feed animation | Validated Feed mapping and preview in [hood-animations.md](hood-animations.md). |
| 26 | Gradual hunger progression | `HoodSessionTests.Advance_ProgressesConfiguredNeedsWhileRoamingAndIdleAnimationsAreDisabled`. |
| 27 | Inspect needs from the menu | Ticket 05 menu behavior; candidate menu checklist. |
| 28 | Avoid permanent need bars | Ticket 05 UI shape; candidate visual checklist. |
| 29 | Avoid punitive neglect | Bounded/saturating need scenarios in `HoodSessionTests`. |
| 30 | Reduce boredom only while moving | `HoodSessionTests.Advance_RoamsWithinBoundsAndRelievesBoredomOnlyForMovementTime`. |
| 31 | Relieve boredom for stationary clicks | `HoodSessionTests.React_ReducesBoredomForEachStationaryClickAndReplacesTheCurrentAction`. |
| 32 | Automatic naps at the sleep threshold | `HoodSleepTests.Advance_AutomaticNapBeginsAtTheConfiguredThresholdWhileResting`. |
| 33 | Stop roaming during naps | `HoodSessionTests.Advance_RoamingPausesForDraggingEditingSleepAndDirectActionsThenResumes`. |
| 34 | Dedicated sleep and wake animations | Asset manifest validation and [hood-animations.md](hood-animations.md). |
| 35 | Click to wake without an extra jump | `HoodSleepTests.React_WhileSleepingWakesAndConsumesTheClickWithoutJumping`. |
| 36 | Manual Sleep command | `HoodSleepTests.Sleep_ManualNapRecoversSleepProportionallyWithoutChangingOtherNeeds`; candidate menu checklist. |
| 37 | Early-wake grace | `HoodSleepTests.Advance_EarlyWakeGraceSuppressesThenAllowsAutomaticSleep`. |
| 38 | Wave/look/hop reactions | `HoodSessionTests.Advance_FiresFittingRandomReactionAndDirectInputTakesPrecedence` and `Advance_CanChooseTheFittingRandomHopWhenIdleAnimationsAreDisabled`. |
| 39 | Several active minutes between events | Interval endpoint and configured-range scenarios in `HoodSessionTests`; [configuration.md](configuration.md). |
| 40 | Disable random reactions independently | `HoodConfigurationTests.SaveAndApply_RandomEventsToggleChangesAmbientSchedulingIndependently`. |
| 41 | Suspend reactions during interaction/sleep | `HoodSessionTests.Advance_SuspendsRandomEventsDuringDragEditSleepAndForegroundActions`. |
| 42 | Prevent queued repeated actions | Direct-action replacement scenarios in `HoodSessionTests` and `HoodSleepTests`. |
| 43 | Retain suitable source animations | Source/runtime mapping in [hood-animations.md](hood-animations.md); user-owned art preview accepted. |
| 44 | Match missing animations to Hood | Local derived art generator, [hood-animations.md](hood-animations.md), and user-approved action preview. |
| 45 | Map each first-release action | `PetDefinition` required-action/frame validation and packaged animation manifest. |
| 46 | Provide a system-tray icon | Ticket 01 implementation; candidate tray checklist. |
| 47 | Tray Settings and Quit | Ticket 12 tray/menu implementation; candidate tray checklist. |
| 48 | Control always-on-top | `HoodConfigurationTests.SaveAndApply_PreservesAllAmbientToggleCombinationsAndIndependentAppOptions`; Ticket 11 checklist. |
| 49 | Opt in to Windows startup | Startup registration transaction tests; Ticket 11 checklist. |
| 50 | Keep startup disabled by default | Config defaults test and first-run YAML assertion in `HoodConfigurationTests`. |
| 51 | Report startup/display integration failures | Simulated startup registration/rollback tests; display failure path and Ticket 07 checklist. |
| 52 | Compact, modern settings | Ticket 12 settings layout; candidate visual checklist. |
| 53 | Readable contrast and visible state | Settings styles and focus indicator; candidate scaling checklist. |
| 54 | Keyboard navigation | WPF tab order, Space/Enter/Cancel controls; candidate keyboard checklist. |
| 55 | Usable at display scaling | Scrollable settings and per-monitor-aware app manifest; candidate scaling checklist. |
| 56 | Editable YAML | Real-file read/write tests in `HoodConfigurationTests`; [configuration.md](configuration.md). |
| 57 | Document defaults and units | Complete default YAML and units table in [configuration.md](configuration.md). |
| 58 | UI and startup share one configuration | Controller write/reload scenarios in `HoodConfigurationTests`. |
| 59 | Apply valid manual edits on next launch | `HoodConfigurationTests.LoadOrCreate_WritesDocumentedDefaultsAndReloadsManualEdits`. |
| 60 | Preserve independent preferences | All eight ambient combinations and independent app-option scenarios in `HoodConfigurationTests`. |
| 61 | Explain invalid YAML/values | Strict parser and validation scenarios in `HoodConfigurationTests`; [recovery.md](recovery.md). |
| 62 | Preserve invalid source configuration | `HoodConfigurationTests.LoadOrCreate_RejectsInvalidYamlWithoutChangingTheFile`. |
| 63 | Report failed configuration saves | Storage and integration rollback tests in `HoodConfigurationTests`; [recovery.md](recovery.md). |
| 64 | Generate sensible first-run defaults | `HoodConfigurationTests.LoadOrCreate_WritesDocumentedDefaultsAndReloadsManualEdits`. |
| 65 | Restore needs, placement, and area | `HoodSessionPersistenceTests` JSON round-trip/restore scenarios. |
| 66 | Pause needs while closed | Active-time model and quit/relaunch session scenarios; [sleep-and-recovery.md](sleep-and-recovery.md). |
| 67 | Resume remaining nap duration | `HoodSleepTests.QuitAndRestore_PreservesPartialNapAndItsRecoveryAfterDurationSettingsChange`. |
| 68 | Keep data local, without an account | App uses per-user config/state paths and no network/account services; temporary-file persistence tests. |
| 69 | Explain damaged-state recovery | Acknowledged archive/reset tests in `HoodSessionPersistenceTests`; [recovery.md](recovery.md). |
| 70 | Explain missing/unusable assets | `HoodSessionTests.Start_RejectsAMissingBaselineAnimationWithAnActionableError` and manifest validation tests. |
| 71 | Save and stop on Quit | `HoodSessionTests.Quit_SavesTheCurrentSnapshotThroughTheStorageBoundary`; candidate Quit checklist. |
| 72 | Repeatably test observable behavior | Release `DesktopPet.Tests` suite with controlled time, random source, displays, and isolated files; [release-evidence.md](release-evidence.md). |
| 73 | Keep Windows integration checks manual | [manual-acceptance.md](manual-acceptance.md); user results recorded for the accepted build in [acceptance-results.md](acceptance-results.md). |

