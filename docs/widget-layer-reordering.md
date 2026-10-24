# Animated widget layer reordering — unreleased, 24 October 2026

The owner authorized implementing lifted photo-layer rows, smoothly displaced neighbours, a settling drop, safe cancellation and Reduced Motion support, then authorized committing and pushing the update to main. This update remains unreleased; no version tag or IPA build is included in this push.

## Behaviour

Touch and hold the existing 44×48-point drag handle for 0.2 seconds, then move vertically. A single snapshot of the **whole row** follows the finger, with a small lift and shadow. The original row is hidden, and other rows slide into place over 180 milliseconds, leaving a gap for the dragged row. Releasing settles the snapshot into the gap before committing the order and updating the widget preview. Photo images, switches and row instances are retained rather than reconstructed on every movement.

The page scrolls automatically when the finger approaches the top/bottom of its visible scroll area. Speed increases toward the edge and is bounded. Normal finger scrolling is paused only during the drag; every enclosing scroll view's previous enabled state is restored afterward. Ordinary swipes outside the handle retain normal page scrolling.

Reordering is available with two or more layers. The layer menu's existing Move forward/Move backward actions remain available. VoiceOver handle descriptions follow renamed layers, and a completed move announces its visual position. Reduced Motion removes the lift scale and animated sliding/settling while retaining direct tracking and reordering. Names, thumbnails and selected/accent outlines use their existing Light/Dark presentation. The temporary snapshot is excluded from accessibility.

## Review and interruption handling

The frozen drag plan uses **measured row heights**, including wrapped layer names and larger text. Each moved row keeps its full height. Selection follows the finger's unconstrained position; constraining a tall row to fit wholly inside the list would otherwise make the first/last positions unreachable. The preview centre has a soft list-edge limit, and the drop settles to the calculated row position.

The draft and saved design are untouched while dragging. A valid drop applies the existing `WidgetLayerEdits.Reorder` operation, respecting the reversed topmost-first display order, and Save persists the resulting stack through the existing store. Cancelling the drag or releasing well outside the list/visible scroll area returns to the original order. No models, JSON fields, App Group data, widget slot assignments or rendering/export rules change.

Save, navigation, adding photos, canvas manipulation and other editing controls pause during the drag. Row switches, selection and menus also pause to prevent a second touch changing the model underneath the frozen layout. Their controls recover after drop/cancellation without sending visibility changes. The list itself remains enabled so its native recognizer can finish.

Backgrounding, page disappearance/departure, disabling/unloading the list, resizing the layout and handler disconnection cancel immediately. Cancellation releases the snapshot, restores row alpha/transforms and clipping, restores scroll states and disposes the timer/haptic generator. A generation token rejects delayed animation completions from an old drag. Row-ID validation and foreground/busy checks reject stale drops after list/activity changes. Failed snapshot capture never starts the busy state. Native callbacks catch/report failures and restore the editor.

The iOS handler uses Apple's public [row snapshot API](https://developer.apple.com/documentation/uikit/uiview/snapshotview%28afterscreenupdates%3A%29), a local long-press recognizer and view animations. One screen-sized row snapshot is retained during the gesture; no photo decoding, export, disk access or network work occurs per drag frame. A 60 Hz timer runs only during the active drag for edge scrolling and is stopped before settling. Existing cross-platform drag/drop remains a fallback outside iOS.

## Verification

Automated verification: **249 app tests**, **32 packaging/policy tests**, source/project checks, whitespace checks and managed iOS C# compilation pass with zero warnings/errors. New regression coverage includes deferred commit, one final reorder, cancellation/disable recovery, stale animation callbacks, changed rows/activity, different row heights, reaching both ends with a tall row/off-centre grip, invalid geometry, renamed handle descriptions, second-touch suppression, stable row reuse, save persistence, reversed visual/model order and background/page-disappearance recovery. The tests execute the same drag plan and page state transitions used by the native handler.

The Windows checks do **not** run UIKit animation or prove frame rate, native hit-testing, scroll gesture arbitration or pixel layout. Xcode/AOT and physical iPhone checks remain pending: drag first↔last with 2–8 layers, try long names/large text, drag rapidly in both directions and at scroll edges, cancel outside the list, interrupt while lifting/sliding/settling, rotate the device, exercise VoiceOver and Reduced Motion, inspect both themes/varied accents, and save/reopen to confirm the rendered layer order. Check normal scrolling, row menus/switches, photo picker cancellation and the editor's existing canvas gestures afterward.
