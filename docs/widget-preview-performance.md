# Dense widget editor previews — 13 November 2026

The owner reported choppy movement with more than ten layers. Source review found
that every gesture sample refreshed the settings controls and all layer geometry,
then queued synchronous full-widget composition on the main thread. Four cached
rasters and two decoded photos could churn during a sequential scan of a larger
design. Ten is not a special layer limit.

## Changes

- Preview requests coalesce on a one-shot display-link tick in the common run-loop
  mode, including while tracking a gesture. Idle previews pause the display link.
- One worker owns the editor's compositor, Core Image context and image caches.
  It receives immutable design/battery-option snapshots and a captured repository
  path resolver. File existence checks run on source-cache misses on that worker,
  rather than checking every photo on the UI thread for each requested frame.
  Controls, battery readings, timers and image-view assignments stay on the UI
  thread. Export already has its own independent compositor.
- There is at most one in-flight render, one replaceable pending request and one
  replaceable completed image. A slow UI cannot accumulate image callbacks or
  bitmaps. The latest completed frame may display while the next frame renders,
  so continuous movement cannot starve the preview by invalidating every frame.
  Superseded pending work is skipped; a superseded error cannot clear a newer draft.
- Gesture samples update the draft, selected geometry, outside ghost and handles.
  Unchanged layer controls/layout and canvas fitting are skipped. Settings and
  history synchronize at gesture completion; cancellation restores the starting
  layer, and Undo/Redo still treats the gesture as one edit.
  Text fitting is reused while moving/rotating; style, text or size changes still
  invalidate it, including live clock/date text changes.
- Preview rasters use a 64 MiB admission budget, measured from actual bitmap row
  stride and height. Once full, overflow images stay frame-owned, so a sequential
  scan does not evict every useful cached layer. Up to four decoded source photos
  are retained. Standard-range raster buffers match the final RGBA8 output.
- Temporary rasters are released after independent four-layer checkpoints, rather
  than accumulating all overflow images for a frame. Existing grouping, blend
  kernels, order, transparency, clipping and final resolution remain in use.
- Export retains its four-entry/16 MiB raster limit and two-source limit. No saved
  schema, asset format, slot count, native extension or workflow trigger changes.

## Lifecycle and memory review

Clear/unload/background cancels the worker lifetime, drops waiting/completed
frames and serializes cache disposal behind in-flight native work. Cancellation
is checked between layers and before the final snapshot; a native draw already
running must finish before release. Old callbacks cannot resurrect that lifetime.
Rendering failures release the worker's native state and remain retryable.

A memory warning keeps the current independent display image, retires the large
cache and queues the current design again. The handler then uses a 16 MiB raster
budget and two-source limit for its remaining lifetime. Observers, clocks and
display links are removed on suspension/disconnection. These budgets cover cached
bitmap pixels, not total process or GPU memory; actual peak memory needs a device.

The existing accessibility description, selection semantics, 44-point controls,
scroll preservation and gesture arbitration remain in place. No animation or
resolution downgrade is introduced. Full settings refreshes occur after gestures,
when those settings become available again.

Apple documents [UIImage's immutable cross-thread use](https://developer.apple.com/documentation/uikit/uiimage)
and recommends [reusing a Core Image context per rendering view/background task](https://developer.apple.com/documentation/coreimage/cicontext).
Neither reference substitutes for measuring this app on an iPhone.

## Verification and limits

All **325 Core, 132 Infrastructure, 598 App and 76 packaging tests** pass (1,131
total), along with source validation and managed iOS compilation. Tests exercise
burst replacement, continuous-movement presentation, cancellation, serial cleanup,
late callbacks, dispatcher/failure recovery, byte/entry limits, overflow ownership,
and mixed 10/20/30-layer editor gestures with final settings and Undo/Redo.

## Follow-up review

- Gesture start previously selected a layer (which can commit a typed colour)
  but retained its pre-commit snapshot. A regression reproduced movement restoring
  `#FFFFFF` over the newly committed `#ABCDEF`. Gestures now validate pending
  settings first and take the current committed layer as their origin. Another
  regression reproduced invalid HEX still starting a gesture; it now blocks
  movement and keeps the input for correction. Colour and movement retain
  separate Undo boundaries.
- A rejected completion dispatch released the image but identical-request
  suppression could prevent a retry. The worker now reports that missing delivery,
  and the next render request can retry identical pixels. A subsequent successful
  dispatch clears that state.
- Review found per-frame filesystem checks still running on the UI thread, and
  native paragraph fitting repeating during movement despite unchanged text size.
  File checks now run only on worker-side source misses; text fitting reuses its
  result until dimensions, text or style change. Tests cover font-fit invalidation
  for clock, date and text layers, plus pending-colour and delivery recovery.

The two gesture regressions failed before their fixes and passed afterward. The
final full App suite passes with 598 tests and zero skipped, packaging passes with
76 tests, source checks pass, and fresh managed iOS compilation reports zero
warnings/errors. The unchanged Core/Infrastructure suites retain their passing
325/132 results. This review covers UI/UX, history/persistence boundaries,
lifecycle, accessibility, ownership and regressions; native output and timing
remain device checks.

The cache-policy test models 60 movements with a 2 MiB raster per layer and one
changing layer. It creates 70/80/90 rasters for 10/20/30 layers, respectively,
instead of rebuilding each unchanged layer per movement. This tests reuse and
ownership, not native draw speed or frame rate. Existing tests cover rotations,
pinches, off-canvas movement, locks, visibility, live clocks/batteries, history,
scroll behavior and persistence. UIKit/Core Image cannot execute in these Windows
tests; managed compilation checks bindings but does not link an IPA.

On iPhone, check 10, 20 and 30 layers across all sizes with photos, gradient text,
seconds clocks and battery layers. Drag, pinch and rotate bottom/middle/top layers;
check blends and the final image after releasing. Test Undo/Redo, cancellation,
background/resume, repeated navigation and save/reopen/Home Screen output. Check
VoiceOver, larger text and both themes. Profile frame time and peak memory in a
Release build, including simulated memory pressure on a development device.

The owner subsequently authorized committing, pushing main and pushing the new
**v0.2.58** tag for **0.2.58 (60)**. Host and native extension metadata are aligned.
Only the new version tag triggers IPA generation; the owner will monitor the
build and test on iPhone. Device checks above remain pending.
