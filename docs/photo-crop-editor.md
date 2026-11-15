# Reversible photo cropping — 15 November 2026

Select an unlocked photo layer, then tap **Crop photo**, the crop icon beside
Edit photo in the existing horizontal toolbar. The full-screen editor provides:

- Eight draggable corner/edge handles and a draggable selection.
- Free, Original, Square, 4:3 and 16:9 aspect ratios. Original sets the source
  aspect ratio; Reset crop restores the entire source photo.
- Pinch/Zoom-slider magnification up to 8× and two-finger viewport panning.
- Crop-local Undo/Redo (30 steps), Reset crop, Cancel and Apply.
- Checkerboard transparency, dimming outside the crop, thirds guides and pixel
  dimensions. Zoom changes the view only, not the crop or history.

Apply uses exact source pixels without resampling. The selected crop is fitted
inside the layer's existing frame; position, frame, rotation, flips, blend mode,
name, visibility and opacity are retained. Zoom resets to 1 and the old fill-frame
crop offsets reset to centre so the entire selected crop is visible. The image's
pixel dimensions update to match its PNG. Apply is one widget Undo/Redo step.
The crop editor displays the source's orientation; existing layer rotation is
applied afterward by the widget renderer. Save widget publishes the result.

## Source preservation and interoperability

Each applied revision uses the existing immutable photo-key mechanism. The widget
receives only `<key>.png`, now cropped to the selected dimensions. Private files
retain the uncropped `.original.png`, full-size `.mask`, and optional repaired
`.working.png`/`.working.sha256` pair. Additional `.crop.json` and `.crop.sha256`
files describe and verify the source-pixel crop. No widget catalog/scene schema,
native extension, App Group, entitlement or publishing format changes are needed.

Reopening Crop uses the full original, cut-out mask and repaired colours; it never
crops an already-cropped PNG again. Reset crop reveals the whole edited photo,
including cut-outs and object removal. It does not discard those edits. Edit photo
also uses the full source, outlines the saved crop, and reapplies that crop when
saving a new mask/repair. The widget toolbar's Reset selected photo remains a
placement reset; Reset crop is inside the Crop editor.

The repository validates bounded metadata, its paired checksum, source bounds,
mask/working dimensions and display-PNG dimensions. Missing or modified crop
metadata fails instead of silently changing the visible area. Files are installed
before the new immutable asset is accepted. Original and cropped revisions are
protected by widget history and saved/duplicated layers; retiring the last
reference removes all private sidecars. Cancel, unchanged Apply and failures do
not replace the current layer. Partial imports and unaccepted late results are
cleaned up through the existing repository and editor operation guards.

## Review findings and behavior

- Fixed-ratio corners project movement onto the aspect diagonal. A regression
  reproduced a 21-pixel jump from a one-pixel pointer change with the earlier
  dominant-axis calculation, then passed after the projection fix. Returning to
  the gesture origin restores the exact original rectangle without an Undo step.
- Crop gestures hit-test the starting point before UIKit's pan threshold, keeping
  fast corner drags attached to the correct handle. A fitted halo leaves room for
  edge targets. A second finger cancels an in-progress crop change before zoom/pan.
- Crop and cut-out editors share safe presenter selection and scroll arbitration.
  They recheck ownership after presentation; an inactive/detached owner is not
  handed a new edit. The widget preview remains visible through opening and is
  suspended only after native layout validation succeeds.
- Apply guards re-entry and waits for encoding/storage work before releasing the
  source. A storage failure keeps the selection available for retry. Cancel and
  backgrounding during a save request cancellation; unaccepted installed assets
  are removed. Backgrounding while idle cancels only the unfinished drag. Dismissal
  skips animation when inactive. Controls recover through the existing finally
  path; leaving the owner page cannot update another draft.
- Both themes use shared accent contrast rules. Controls have at least 44-point
  height; toolbar targets remain 48×48. Labels use Dynamic Type, the page scrolls,
  and busy/error messages announce to VoiceOver. Canvas actions move the crop one
  pixel and grow/shrink it, with status reporting source position and dimensions.

Follow-up review corrected fractional edge rounding: rounding the origin and
width separately could move a fixed opposite edge from pixel 301 to 302. Absolute
edges are now rounded together. Free/square regression cases failed before this
fix and passed afterward. Selecting an already-active ratio is also a no-op;
another regression reproduced a 6×4 crop becoming 5×4 on a repeated 4:3 selection.
Seeded checks cover 2,000 varied fractional crop changes with bounds, opposite
anchors and Undo/Redo verified at each step.

The crop controller now observes app activation and refreshes control availability
after foregrounding, preserving its status and selection. Previously, controls
calculated while inactive could remain disabled without a new layout pass.
The observer is removed on disposal and cannot reopen a closing/dismissed editor.
This wiring compiles against the iOS bindings; actual background/resume behavior
still needs a phone check.

## Verification

All **339 Core, 135 Infrastructure, 603 App and 76 packaging tests** pass (1,153
total). Source checks pass and managed iOS compilation reports zero warnings and
errors. Automated coverage includes exact RGBA/alpha row copying, all handles and
aspect modes on landscape/portrait/1-pixel images, extreme drags, diagonal
continuity, anchors, bounded grouped history, cancellation, reopening, source and
repair preservation, metadata loss/tampering, invalid sizes, import cleanup,
toolbar order/accent/lock state, widget placement, Undo/Redo, saving, errors,
duplicate taps and departure cleanup. No dependencies, workflow triggers or
version metadata were changed.

Windows tests do not execute UIKit, encode/decode the native PNG output, link an
IPA or validate touch/VoiceOver appearance on a phone. On iPhone verify:

1. Portrait, landscape, oriented HEIC imports and transparent PNGs; confirm the
   selected corners match the saved crop and Home Screen result.
2. Every handle at fitted and 8× zoom, fast drags, selection movement, two-finger
   panning, ratio switching and Reset/Undo/Redo. No crop shift during view zoom.
3. Existing background removal and repaired photos: crop, save, restart, reopen,
   uncrop, refine the cut-out and save again. Duplicated layers stay independent.
4. Cancel and failed saves; rapid taps; background/resume while opening or saving;
   both themes, bright/dark accents, larger text and VoiceOver. Controls recover.

Review references: Apple's [drawing coordinate guide](https://developer.apple.com/library/archive/documentation/2DDrawing/Conceptual/DrawingPrintingiOS/GraphicsDrawingOverview/GraphicsDrawingOverview.html)
explains the UIKit/Core Graphics origin difference, while its [image scale API](https://developer.apple.com/documentation/uikit/uiimage/imagewithcgimage:scale:orientation:)
documents scale-one pixel sizing. Preview/source coordinate mapping was reviewed;
these references do not verify this app's native encoded output or orientation.

The owner subsequently authorized committing, pushing main and pushing the new
**v0.2.59** tag for **0.2.59 (61)**. Host and native extension metadata are aligned.
Only the new version tag triggers IPA generation; the owner will monitor the
build and test on iPhone. Native and device checks above remain pending.
