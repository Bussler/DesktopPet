# Notes writing animations: delivery and acceptance

The user acknowledged the four artwork loops and authorized the remaining implementation tickets. Tickets 2, 3 and 4 were each reviewed and tested before the next began. This record separates implementation/automated evidence from native checks that require the identified package.

## Candidate and validation

Implementation tickets 1-5 are complete. The final candidate is [DesktopPet.Windows.exe](../../artifacts/hood-notes-writing-20261019-final/app/DesktopPet.Windows.exe), built with `tools/Build-AndPublish.ps1 -CandidateDirectory artifacts/hood-notes-writing-20261019-final`. Close older running copies before testing it. This local build is ready for the pending native checklist.

| File | SHA-256 |
| --- | --- |
| DesktopPet.Windows.exe | `16FDBCD1F71D1FA81343D6EDA66554B27F931C906A41AD11654A76C5090A08BF` |
| DesktopPet.Windows.dll | `14ADDDCD28F07ED3B282C41A6C9BB21E7F7F6AA78CD10AEC4D0644A823E17AEB` |
| DesktopPet.Core.dll | `58284CFF49DF677F17EEA99E6EBA3B25E3BED8DF176A7F604C053B510C337AEF` |

Completed review and validation gates:

| Gate | Result |
| --- | --- |
| Artwork | Four custom loops, eight poses at 120 ms; original pixels/action definitions/accessory poses preserved; user artwork acknowledgement recorded. |
| Ticket 2 | 56 configuration cases and 39 Windows writing/settings cases passed. |
| Ticket 3 | 48 writing cases plus 9 existing inline-math interaction cases passed. |
| Ticket 4 | 345 writing/Focus/rendering/assets/accessories/settings cases passed; both strengthened old-size legacy variants passed afterward. |
| Final audit regression | Ctrl+Enter source/body handoff first failed for inline and display source; the corrected ordering and deterministic real-window key handler passed the 17-case input/handoff matrix. |
| Durable-state regression | A saved Write action was incorrectly accepted after adding the optional artwork action. The new save/load/direct-restore case first failed; rejecting Write at the existing snapshot-validation boundary made all 15 persistence cases pass. Invalid input is retained and failed saves preserve the previous file. |
| Rendering fixture follow-up | The saved-action guard exposed four every-pose fixtures that restored Write as durable state. They now drive transient writing through the overlay and controlled time, retaining every writing frame, audio handoff, session comparison and next-frame assertion. All four passed. |
| Final full workflow | `tools/Test.ps1` passed locked restore, all sprite validators, 461 Core tests and 816 Windows tests: **1,277 passed, zero failures or skips**. The final Windows run includes all 84 writing interaction cases. |
| Package | Self-contained win-x64 build passed. All eight packaged atlas/manifest hashes match source; each writing loop has eight 120 ms poses at the existing logical size. Embedded accessory poses match source. Actual packaged configuration defaults, missing-field upgrade and independent true/false round trips pass. All shipped guide hashes match source; 30 package links resolve. Both .NET runtime frameworks and required runtime files are present; only app/docs/LICENSE/README.md appear at root, with no test, prompt or internal specification files. |
| Final document/diff audit | Every story 1-82 is mapped exactly once; local source links and whitespace checks pass. Tracked changes and new source/tests/previews were reviewed against repository standards and the approved contract; no outstanding implementation finding remains. |

The earlier artwork-only full workflow reported 51 failures in unchanged Notes interaction tests. A representative Divider-versus-Paragraph failure reproduced on an isolated original HEAD export. Those failures did not recur in the final full workflow; an individually proven common cause is not claimed.

Local generated evidence is retained in the [final test log](../../artifacts/writing-final-workflow.log), [package-build log](../../artifacts/writing-final-package-build.log) and [package verification record](../../artifacts/writing-package-verification.json). These ignored artifacts belong to this checkout; the test and build commands above regenerate the delivery checks. Full-run TRX results are under DesktopPet.Tests/TestResults (Core 21:12:16 and Windows 21:12:18 on 2026-10-19).

## Specification coverage

The [approved specification](Notes-Writing-Animations-SPEC.md) has 82 stories. The table maps every story to current implementation boundaries and observable checks. Test names refer to the [writing interaction suite](../../DesktopPet.Windows.Tests/NotesWritingInteractionTests.cs), unless otherwise stated. Physical-input and appearance checks remain listed separately below.

| Stories | Current implementation and evidence |
| --- | --- |
| 1-6 | App-owned Notes input event wiring reaches the overlay for each pet. RealNotesTypingReachesTheSelectedPetAndSavesWithoutMovingFocus, title/formula cases, RichContentTypingKeepsItsStructureAndUndoHistory and TableCellTypingReachesThePetAndRetainsCellMembership check actual editor/model/presentation outcomes and save/reload. |
| 7-8 | GetWritingField accepts only a focused eligible editing field. Initial focus and opening a source do not animate; RoutedEditingKeyAndFocusDepartureUseTheRealWindowPath and title/formula tests verify fresh-input requirements. |
| 9-15 | Character composition and the real NoteWindow key handler cover spaces/composed characters, Enter/Tab, Backspace/Delete and keyboard paste. RepeatedKeysRenewTheDeadlineWithoutRestartingTheAnimation verifies renewed activity and continued phase. |
| 16-23 | NavigationModifiersAndOtherCommandsDoNotStartOrRenewWriting and ProgrammaticChangesAndMousePasteDoNotCountAsTyping verify exclusions and nonrenewal. Notes owns these window-local handlers; search/settings/other-app windows do not participate. |
| 24-29 | Monotonic overlay clock/deadline controls rendering and temporary suppression. Tests observe continued phase, 799/800 ms expiry, normal roaming eligibility and a fresh cycle after an ended burst. |
| 30-32 | Keyboard/logical-source ownership and scoped cleanup stop on field departure. Title Enter, formula source departure, Ctrl+Enter handoff and note-switch tests verify immediate stop and fresh-only restart. |
| 33-35 | HoodSession.IsWriting temporarily suppresses movement and ambient random countdown without changing saved roaming. WritingPausesMovementAndRandomActionsWithoutSavingAWriteAction and deadline tests observe position and continuation. |
| 36-41 | SleepCareClickAndDraggingKeepPriority, WritingDoesNotDeferAnAutomaticNap and EndingFeedResumesOnlyStillCurrentTyping cover care/sleep/drag/click precedence and current-versus-expired resumption. |
| 42-44 | FocusOverridesWinAndOnlyCurrentTypingResumesAfterward and ActualChecklistCompletionWinsThenReturnsOnlyToFreshTyping exercise all pets with existing Focus/checklist presentation paths. Existing Focus and overlay suites passed the ticket-4 gate. |
| 45-47 | Four option combinations and disabled roaming/random-event fixtures verify independence. The writing preference is separate from idle and other behavior choices. |
| 48-59 | WritingAnimationsCheckBox uses existing Settings resources/transactions. Configuration tests verify new/missing defaults, true/false round trips and malformed-YAML preservation; Settings tests cover Save/Cancel/close/failure, unrelated values and fresh-only enable behavior. Native readability and physical keyboard use remain below. |
| 60-65 | Distinct built-in notebook/keyboard/ground-writing source sheets, exact prompts, manifests, packers and previews are retained. Asset tests verify distinct nonempty unclipped frames and logical dimensions. The user acknowledged the artwork. |
| 66 | Expiry, continuous phase and fresh-cycle tests verify return to normal presentation; the acknowledged loops and exported previews provide artwork review. Native transition quality remains below. |
| 67 | WritingPosesKeepTheirAudioAccessories and the existing accessory rendering suite pass; review strips use the application's actual accessory renderer. Live audio/DPI appearance remains below. |
| 68 | PetPreviewSaveCancelAndFailureKeepWritingOnTheCorrectArtwork tests every pet for Save/Cancel/failed Save, retained phase/needs/position and correct crop source. Scale/DPI tests verify phase and logical size. |
| 69-71 | SwitchingNotesAndOldWindowCleanupCannotCancelNewerTyping and ClosingHidingAndDeletingClearOnlyThatNotesActivity cover actual App window ownership, old callbacks, close/native/group/animated hide and delete. |
| 72-74 | ActualSpriteToggleKeepsNotesVisibleAndNeverQueuesHiddenTyping, SuspensionDiscardsTypingEvenWhenResumeOccursBeforeItsDeadline, ClosingTheOverlayReleasesItsWritingSuppression and QuitAndActualSessionReloadNeverRestoreWritingActivity cover visibility, suspension, close and actual JSON reload. The Core persistence regression WritingCannotBeSavedOrRestoredAsDurableSessionAction also rejects a hand-edited durable Write action through save, load and direct restore. |
| 75 | Rich-content/undo/table/formula cases and real Notes JSON reload preserve accepted semantic data. Animation notification does not edit or save documents. Source identity is transient WPF metadata; no Notes schema changed. |
| 76 | RealNotesTypingReachesTheSelectedPetAndSavesWithoutMovingFocus asserts the editor retains focus, with the original no-activation overlay/window ownership retained. Physical foreground behavior remains below. |
| 77 | Session presentation flags preserve ordinary needs/nap progression and saved placement. Movement/random/care tests and preview/size tests observe those contracts; no writing care gain was added. |
| 78-79 | LegacyWritingFallbackRespectsIdleAndSwitchesBackToCustomWriting uses an actual old-size atlas without write, tests both idle preferences and hot switching. Asset/parser tests retain mandatory-action validation while allowing optional writing. |
| 80 | ArtworkReloadFailureLeavesTypingAndNoteSavingWorking verifies retained artwork and continued accepted edits after a failed reload. Existing asset/settings/recovery boundaries remain in use. |
| 81 | Window-local activity notifications carry owner plus active/inactive state; they do not capture text, log keys, install a global hook, transmit input or request privileges. Source review confirms this boundary. |
| 82 | The README, Notes/configuration guides, animation reference and source preview docs describe the delivered option, typing-only triggers, timing, priority and legacy behavior. |

## Review decisions

- Reused the existing App/Notes/overlay/session and Settings boundaries; no new package dependency or durable typing state/schema was introduced.
- The inline-source regression exposed WPF splitting editable spans. A copied source-identity dependency property keeps those fragments in one logical field while actual departure still clears activity. Existing inline-math tests remained green.
- Final audit found that post-handler key observation could credit Ctrl+Enter to the body field created by that key. Activity is now reported before existing command handling. The window handler accepts explicit event modifiers, allowing deterministic testing without changing system key state.
- Suspend/resume and overlay close explicitly clear writing's owner/suppression. Other presentation owners, needs, care progress, note data and settings rollback retain their established responsibilities.
- The optional Write artwork action is deliberately rejected by durable snapshot validation. Ordinary session actions retain their identifiers and recovery behavior; transient activity cannot be manufactured by restoring a saved Write action.

## Native acceptance

Artwork acknowledgement is complete. Packaged physical-keyboard, clipboard, foreground/focus, live audio, transition smoothness and multi-monitor/DPI acceptance have not been performed by automation. Use the [native checklist](../../tools/manual-acceptance/notes-writing-animations-manual-acceptance.md) against the candidate identified above, retaining unperformed items as pending.
