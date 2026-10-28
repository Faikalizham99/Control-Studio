# Widget editor Undo and Redo

The owner approved Undo/Redo above the live preview, separate from layer settings, then explicitly authorized committing, pushing main and pushing the new version tag. This is prepared for **0.2.46 (48)**; the owner will monitor the tag-triggered IPA build. The implementation/review below did not itself authorize publishing.

## Behaviour

- Two 48-point curved-arrow buttons sit beside **Live preview**, above the canvas. Rotate/Flip/Blend and other layer tools stay below the canvas. The buttons use the current accent when available and opaque, theme-aware grey icons when unavailable; VoiceOver names include Undo/Redo and the available step count.
- Up to **100 editing steps** are retained for the open editor. A completed preview drag, pinch, corner resize or rotation-handle movement is one step. Slider drags, including opacity, size, precision rotation/position/crop, background hue and nested text/battery colour controls, use the same start/end grouping. Separate accessibility slider adjustments remain separate edits.
- Native colour-picker previews are grouped until that picker finishes, including background/resume. Cancelling back to the original colour creates no edit and preserves existing Redo. Consecutive widget-name or Text-layer typing is grouped until focus/selection changes or another edit occurs.
- Typing back to the starting value removes the empty step. At the history limit, one provisional overflow snapshot retains the oldest step and its photo references until typing finishes; returning to the starting value reinstates that step instead of losing it to an empty edit.
- History covers all five layer types: adding/deleting/duplicating, order, names, visibility, placement locks, frame/crop/zoom/fit, flips, rotations, blending, opacity, fonts/weights/alignment, clock/date formats, gradients and battery shapes/thresholds/colours. Widget name, family and solid/transparent background edits are included.
- Undo and Redo restore the draft and a valid selection. A new edit after Undo clears the Redo branch. Selection/tool browsing, live clock/battery readings and refreshed device canvas measurements do not create undo steps. Restoring a state uses the current sizing information for its family.
- Actions are guarded during gestures, reorder, picker/save operations, backgrounding and after departure. Undo/Redo preserve the page viewport; changing the restored selection closes tools for the former layer. Native pickers/backgrounding keep existing recovery guards.
- Pending HEX/threshold strings that have not changed the preview are discarded before applying history. They cannot commit stale values through an Unfocused event after restoring a layer. Valid applied colours/thresholds are included normally.
- **Save widget** persists the currently displayed draft, clears history on success and keeps committed-source protection. A failed save retains the draft/history. Cancel/departure leaves the saved design unchanged, clears history and cleans session-owned imports. Reopening starts with an empty history; this is not a history of earlier saved versions.

## Ownership and implementation review

`WidgetEditHistory` stores immutable metadata snapshots with structural comparison of battery colour stops. The bounded undo/redo stacks contain no image bytes, UIImage/CIImage instances, controls, timers or database connections. Old and duplicated photo layers refer to their existing immutable asset keys.

Imported draft photos stay available while reachable from the current draft, Undo, Redo or an in-progress interaction. Dropping an old history step or discarding a Redo branch permits cleanup once no retained state needs that asset. Cleanup is serialized, catches/logs failures without private paths, and retains failed-cleanup ownership for retry. Save/departure release session ownership; the existing repository refuses to remove sources referenced by saved designs. Original Photos/Files assets are never deleted.

History integration lives in the editor's `WidgetEditorHistory` partial, alongside a portable history model and a small reusable button view. It reuses the existing render path, stable layer rows and scroll preservation. Repeated gesture updates do not rebind history icons or rebuild their controls when availability/count are unchanged. No persistence/JSON schema, App Group identity, native widget registration, export algorithm, dependency, font, workflow or version setting changed.

Review also found and corrected two related lifecycle cases: background colour callbacks now keep composition suspended while inactive; an interrupted hue drag releases its internal dragging flag before returning, so Undo restores the hue thumb as well as the colour. Save validation now builds its export draft locally, retaining the editable draft/history on failure.

## Review of uncommitted changes

The follow-up review was limited to this uncommitted Undo/Redo feature and its shared-control changes. It found and fixed:

1. **Slider cancellation could block the editor.** History ended on DragCompleted/backgrounding, but hiding/disabling a slider or replacing its handler could omit that event. Undo/Redo remained unavailable and Save was rejected by the active-transaction guard. Two editor regressions reproduced this before the fix. The editor now completes active steps on availability changes, unload, handler replacement, departure and explicit iOS touch cancellation. The pinned [MAUI 10.0.110 implementation](https://github.com/dotnet/maui/blob/10.0.110/src/Core/src/Handlers/Slider/SliderHandler.iOS.cs#L115) registers touch-down and touch-up callbacks without touch-cancel completion; the editor-local binding supplies that missing path without changing global slider handlers. Native listeners detach on unload/replacement/disposal, ignore obsolete native senders and guard disposed handles. Completion remains idempotent and finalizes the last accepted value.
2. **Cancelled typing left a no-effect step.** Returning a grouped name/Text value to its starting value left an Undo entry with no visible effect. The model now removes that entry and restores any provisionally evicted oldest snapshot at capacity. Its photo references stay retained throughout, preventing cleanup from deleting a photo that becomes undoable again. Pure and full-editor regressions cover this at 100 steps.
3. **Selection-only paths could merge separate typing edits.** The model updated selection without ending the typing merge. It now ends that merge while keeping selection changes out of the undo stack. Explicit selecting/deselecting and existing focus handling retain their behavior.

Nine additional review cases cover those failures, handler replacement, repeated interruption/disposal, hidden/disabled ancestor guards and the full-capacity photo lifetime. Five targeted tests were first run against the incorrect behavior and failed, then passed after production corrections. No change outside the feature's uncommitted scope, commit, push, tag or workflow execution was performed.

## Verification

The portable history tests cover exact state/order/selection restoration; 1/10/30-layer, 300-update gesture grouping; cancelled/no-op interactions; Redo branching; structural battery comparisons; defensive snapshot ownership; typing boundaries; the 100-step limit; photo references in Undo/Redo/active interactions; measurement rebasing and clear.

The MAUI tests execute actual editor controls for all five layer types: grouped pinches and sliders; background interruption/cancellation; native picker previews; sizing in all four families; backgrounds; delete/duplicate; visibility/locks/reorder; name/Text input; unapplied invalid colours/thresholds; import retention, branch discard and history eviction; save failure/current-state save; session departure; layer limits; header placement, accent, disabled contrast, semantics, viewport and reopening.

Local command results:

| Command | Result |
|---|---|
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Core.Tests -c Release --no-restore` | 272 passed |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Infrastructure.Tests -c Release --no-restore` | 120 passed |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.App.Tests -c Release --no-restore` | 499 passed, including 62 new history cases |
| `.\.tools\python\python.exe -m unittest discover -s tests/packaging` | 53 passed |
| `.\.tools\python\python.exe scripts/check-source.py` | Passed; generated native project unchanged |
| `.\.tools\dotnet27\dotnet.exe build src/ControlStudio.App/ControlStudio.App.csproj -c Release --no-restore -t:Compile -p:DesignTimeBuild=true -p:BuildIpa=false -p:ArchiveOnBuild=false -p:EnableCodeSigning=false` | Managed iOS C# compile passed, zero warnings/errors; no native linking |
| `git diff --check` | Passed |

These **944 local checks** are automated portable/packaging results. They do not execute native gestures, inspect UIKit pixels or establish device frame times. The initial feature test run had incorrect lookup IDs for existing layer-menu wrappers; correcting the test helper to activate their real native buttons made those tests pass. The review's five demonstrated failures were fixed in production code. No failing check was suppressed. No macOS workflow or IPA has been triggered during implementation.

## Device checks

Release preparation updates the host and both native extension versions to 0.2.46 (48) and regenerates the checked-in native project. Packaging/source checks and the managed iOS compile are repeated for those version changes. The new version tag requests an IPA build; it does not establish a successful macOS build, final signed artifact or iPhone behavior.

On the future authorized iPhone build, check Small/Medium/Large/XL with 1/10/30 mixed layers. Make many updates in one drag, pinch, slider or rotation-handle gesture, then confirm one Undo returns to the starting state and Redo to the final state. Check partial off-canvas photos and solid/gradient clocks, dates, text and batteries.

Delete/duplicate and undo/redo an imported photo; change its order/lock/visibility and confirm exact restoration and selection. Undo, then make a different edit and confirm Redo becomes unavailable. Save while undone, reopen and compare the saved preview and assigned Home Screen slot. Cancel a separate session and verify the saved design is unchanged.

Interrupt gestures and colour/photo pickers with backgrounding; verify previews remain suspended, controls recover and history groups correctly. Check narrow layouts, large text, VoiceOver labels/focus, both themes/varied accents, keyboard dismissal, readable disabled arrows and no automatic page scroll. Repeat editor entry/exit and long editing sessions for native memory/gesture smoothness; these remain physical-device checks.

Specifically cancel a slider touch by letting a scroll/system gesture take over, close its panel or select another layer during an interrupted adjustment, then verify Undo/Redo/Save/Back recover without requiring background/resume. Check native callback disposal on repeated entry/exit. The iOS touch-cancel adapter compiles here, but its physical event delivery and UIKit lifetime behavior still require the phone.
