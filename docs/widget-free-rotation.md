# Free layer rotation

Included in **0.2.40 (42)**. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build.

## Interaction and controls

Selecting an unlocked visible Photo, Clock, Date or Text shows a circular rotate icon connected to the top of its selection box. Press and drag this handle around the layer's centre to rotate freely. The box and its corner controls turn with the layer; a small angle badge appears while dragging. A tap without dragging keeps the selection. Dragging inside the body still moves the layer, including very small layers near the handle's expanded touch area. Pinch remains a resize gesture; this change does not introduce a two-finger twist recognizer.

The handle has a 44-point gesture target and stays within the existing editing workspace. When the selected layer extends outside that workspace, its handle is brought back into reach and kept away from the pivot to avoid an undefined initial angle. Layer centre, dimensions, crop, opacity and content remain fixed during free rotation. Multiple turns and crossing ±180° use incremental signed angle changes rather than jumping to the finger's absolute angle. Passing directly through the pivot or receiving invalid pointer coordinates does not change the angle.

Position settings for Clock/Date/Text and Size, position & crop for Photo also include shared Rotation controls: an angle slider from −180° to 180° and Reset to 0°. Slider edits use 0.1° steps; dragging retains its precise angle, with a one-decimal readout. These controls reuse app accent, readable disabled surfaces, native slider accessibility and the existing scroll-preservation mechanism.

Locked layers hide the handle and disable angle edits. Hidden layers have no canvas handle; their position settings can still adjust saved placement. Gesture ownership excludes simultaneous pan/pinch edits. Native cancellation or editor suspension restores the starting layer, normal release keeps the last valid angle, and pointer release is not reinterpreted as a new rotation location. Ancestor scrolling is restored through the existing gesture owner/lifecycle paths.

Legacy Rotate Left/Right and photo flips continue to operate on photo content inside the frame. Free rotation turns that entire frame. Reset to 0° resets the whole-frame angle while retaining photo crop/quarter-turn/flip settings; the existing full photo Reset also clears them.

## Geometry, rendering and compatibility

`rotationDegrees` is optional-on-write, defaults to zero on old files, and is validated in [−180, 180]. Frames keep their unrotated normalized position/size and rotate around their centre in actual point space. Hit testing inverse-rotates the pointer; corner resizing and focal photo pinching transform their centre shifts back into widget space, retaining the opposite corner/focal anchor for non-square widgets. Centred Clock/Date/Text pinching retains the same centre and angle. Small, Medium, Large and iPhone XL share these calculations.

UIKit preview/export clips to the real widget first, rotates the layer context around the frame centre, then applies the layer's clipping/content transforms. The outside editing ghost rotates its whole frame as well. Static Text joins the existing raster groups. Live Clock/Date scenes retain their original schema numbers, adding an optional validated angle; SwiftUI rotates the already-sized/clipped text around its centre before applying frame position. This matches Apple's [rotationEffect centre-anchor behavior](https://developer.apple.com/documentation/swiftui/view/rotationeffect%28_%3Aanchor%3A%29). The gesture bridge uses one-finger [UIPanGestureRecognizer](https://developer.apple.com/documentation/uikit/uipangesturerecognizer) locations in a fixed canvas coordinate system.

Save/reopen, publish, duplicate, reorder, lock/hide and slot assignment retain the angle. No catalog schema, widget selector, registration, signing or workflow changes are needed. Old unrotated JSON omits the new field. Older apps with strict decoding cannot edit new records containing an angle; no lossy downgrade conversion is introduced.

## Review and verification

Automated coverage includes all layer kinds/families, arbitrary angles and full turns, centre/content stability, rotated hit testing, fixed resize/focal anchors, old-file compatibility and invalid angle rejection, handle/body touch distinction, tap retention, cancellation/bounds changes, lock/hidden behavior, scroll-preserving angle editing, reset and save/reopen/publication/slots. The native Swift regression test checks missing-angle defaults and valid/invalid Clock/Date angles.

Completed automated checks: 227 Core, 375 headless MAUI App, 91 Infrastructure and 41 packaging tests passed (734 total). Source consistency and whitespace checks passed. Managed iOS compilation completed with zero warnings/errors. The new Swift test remains unexecuted on Windows.

Physical iPhone checks remain required for rendered handle/angle placement and contrast in both themes, native touch responsiveness and scroll arbitration, off-canvas handles, rotated resize/pinch quality, saved/Home Screen matching and live Clock/Date rendering, plus VoiceOver slider/reset behavior. Windows can compile managed iOS code but cannot run the Swift test or validate native UIKit/WidgetKit output. No device result or successful iOS delivery is implied by local tests.
