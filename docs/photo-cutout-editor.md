# Photo cut-out editor

Open an unlocked photo layer's actions and choose **Edit photo / cut-out**.
The full-screen editor supports:

- On-device **Remove background**, using Apple's public Vision foreground instance mask request (iOS 17+). All detected foreground subjects are retained. Unavailable processing or no detected subject leaves the existing mask unchanged and offers manual editing.
- **Erase / Restore**, brush diameter 2–160 image pixels and adjustable soft edges.
- Pinch zoom up to 8×, two-finger panning, and an adjustable Zoom slider. One finger paints, including single taps. The controls outside the canvas remain scrollable.
- **Show original / Show cut-out**, **Restore all**, and session-local Undo/Redo. A stroke, automatic removal, or Restore all is one step. The history retains up to 30 steps within a combined 16 MiB compressed snapshot budget.
- **Apply / Cancel**. Apply writes the new immutable asset before dismissing; a write failure keeps the editor and mask open for retry. Cancel discards the cut-out draft. Cancel during processing waits for that operation to release its source, then removes any unaccepted imported asset.

Painting is in the original imported image's coordinate space, independently of the widget layer's frame, crop, rotation, flips and opacity. The existing normalized photo limit (1024 px maximum edge) still applies. Masking multiplies all premultiplied RGBA components, preserving original transparency rather than restoring previously transparent source pixels to opaque colours. The output keeps the source dimensions, so the widget layer does not move or change size.

## Persistence and ownership

The widget layer schema and extension renderer are unchanged. Each applied cut-out receives a new random immutable photo key:

- `<key>.png`: transparent PNG used by the existing renderer.
- `<key>.original.png`: a copy of the unchanged normalized source photo.
- `<key>.mask`: one alpha byte per source pixel, row-major, with dimensions from the validated original PNG.

Original/mask sidecars are private app files; they are not published to the widget App Group. Both must exist together. Reads and writes are bounded, and partial imports are removed on failure. Reopening a saved cut-out loads its original and mask; applying another revision copies the same original, without progressively degrading the image or depending on an older asset key.

Widget Undo/Redo records a cut-out as one asset replacement, retaining the previous asset and new asset while either is restorable. Duplicated layers can share a cut-out until edited independently. The repository protects all three files while a saved design references the key and removes the sidecars with an explicitly retired source. Layer thumbnails refresh only when the asset key changes, including widget Undo/Redo; ordinary position changes do not reload them.

## Lifecycle and accessibility review

The existing widget operation guard prevents re-entry, save, reorder and navigation links from competing with the modal. Its `finally` restores availability after Apply, Cancel or failure. A result returning after the widget editor leaves navigation is cleaned up instead of applied. The modal freezes callbacks before dismissal and waits for its background work before disposing its source image, preventing a second Apply/Cancel tap from starting work during teardown. Interrupted brush gestures and app deactivation cancel the current stroke, preserving completed edits and Redo. Rotating the device cancels an active stroke and refits the image without changing its mask.

The screen follows the app window's appearance, uses the app accent with contrast-aware foregrounds, native Dynamic Type labels, 44-point minimum control heights, and scrolling on smaller screens. VoiceOver has named sliders and canvas custom actions for painting at the visible centre and moving the viewport. Actual VoiceOver usability and visual layout need device verification.

## Verification

Automated coverage includes interpolated/soft brush strokes, Restore, stroke cancellation, no-op/invalid input, history limits, original/mask reopening, private cleanup, saved duplicate references, invalid dimensions and incomplete sidecars. App regression checks cover placement preservation, one widget Undo/Redo step, both photo versions retained through history, thumbnail replacement, cancelled/failed operation recovery, locked/live-layer menu exclusions, re-entry and editor departure.

The implementation passed 1,034 automated checks: Core 307, Infrastructure 125, App 544, and Packaging 58 (zero skipped). Source/configuration validation also passed. Managed iOS compilation checks the pinned .NET bindings; it is not a native link or launch test. Windows cannot execute the native UIKit/Vision implementation or verify its rendered output. Before release acceptance on an iPhone, check:

1. A portrait and landscape photo with recognisable top-left/bottom-right details: erase those details and ensure the brush, preview, saved output and Home Screen agree (orientation/row alignment).
2. JPEG, oriented HEIC, anime art, hair, and an already-transparent PNG. Automatic removal may vary; manual Restore must recover only original source pixels.
3. Tap/drag with a small brush at 8× zoom, then introduce a second finger, pinch and pan. No unintended brush marks or outer-page scrolling.
4. Automatic removal, Restore all, several strokes, Undo/Redo, comparison, Cancel, Apply, widget Undo/Redo, Save, app restart and reopening for refinement.
5. Duplicate a layer, edit one copy, save, remove one copy and verify the other remains editable. Delete the last saved reference and verify its private sidecars are cleaned up.
6. Cancel while automatic removal/Apply is busy; background/resume; rotate; try both themes, a bright/dark accent, larger text and VoiceOver. Confirm errors keep the current photo/mask intact and buttons recover.

Primary API references: [Apple subject lifting](https://developer.apple.com/videos/play/wwdc2023/10176/), [Vision request](https://developer.apple.com/documentation/vision/vngenerateforegroundinstancemaskrequest), and [.NET binding](https://learn.microsoft.com/en-us/dotnet/api/vision.vngenerateforegroundinstancemaskrequest).

## Release authorization

The owner subsequently authorized committing, pushing main and pushing a new version tag. This feature is released as **0.2.50 (52)** with host and extension metadata aligned. The `v0.2.50` tag push triggers IPA generation; branch pushes do not. The owner will monitor the build. Native and physical iPhone checks above remain pending.
