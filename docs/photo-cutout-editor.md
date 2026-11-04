# Photo cut-out editor

Select an unlocked photo layer, then tap the **Edit photo** pencil at the start of the photo toolbar beneath Live preview. The layer's three-dot menu no longer contains a cut-out entry.
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

## Toolbar and opening correction (0.2.51)

The owner reported that choosing the original menu entry briefly flashed the photo and showed “The widget could not be updated. Try again.” No native exception details were supplied, so that message alone does not establish the exact device exception.

Review found an unsafe native setup: assigning `page.PanGestureRecognizer.ShouldBegin` installs a managed gesture delegate over a scroll view's built-in delegate. The assignment and its matching teardown reset have been removed. A `UIScrollView` subclass now overrides `GestureRecognizerShouldBegin` to filter canvas touches while leaving UIKit's delegate intact. A source guard rejects direct built-in pan/pinch delegate replacements across the app's iOS components; it is a regression guard for this pattern, not native execution or full semantic analysis. Other gesture callbacks found in the app belong to recognizers the app creates itself.

The new first toolbar icon uses the shared 48-point target, dynamic accent and accessible action description. It works only with an unlocked selected photo, and remains in the existing single horizontal scrolling row. The duplicate three-dot entry is removed. Errors and opening status appear beneath that toolbar, with VoiceOver announcements. Unexpected exceptions do not expose raw native details in the UI; logs retain exception and cause types. Cancel and failures preserve the draft and restore buttons; a subsequent tap can retry.

The native opening path captures the initiating MAUI window, runs presentation on the main thread, waits only while a controller/alert transition remains active (bounded to two seconds), and checks that the widget editor still owns navigation before presenting. A tab/page change or backgrounding before presentation abandons opening. The widget composition remains visible until the modal finishes its opening animation, then releases its resources before removal work; it is restored on return. Saved originals, masks, PNG keys and widget Undo/Redo ownership are unchanged.

Follow-up verification: **549 App tests and 61 Packaging checks passed, zero skipped**, source validation passed, and managed iOS compilation passed with zero warnings/errors. Tests cover the real toolbar ordering/target size/accent in both themes, selected-photo dispatch, lock/unlock and empty/live-layer exclusions, removal from the old menu, specific nearby errors, retry, duplicate-tap prevention, deferred preview suspension, background cancellation, departure cleanup and existing Undo/Redo. Packaging tests initially could not create temporary fixture files in the restricted runner; rerunning with normal local temporary-folder access passed.

Native UIKit execution is unavailable on Windows. On iPhone, verify the pencil opens the full cut-out screen with Remove background visible, ordinary controls scroll, one-finger brush strokes stay on the canvas, two-finger pan/pinch work, Cancel/Apply return correctly, and rapid taps/backgrounding do not leave controls disabled. Native exception evidence and actual removal quality remain device checks.

The owner authorized committing, pushing main and pushing the new **v0.2.51** tag for **0.2.51 (53)**. Host and native extension metadata are aligned. Only the new version tag triggers IPA generation, and the owner will monitor the build.
