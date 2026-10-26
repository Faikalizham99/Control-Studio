# Populated widget preview failure

Included in **0.2.41 (43)** following the preview-unavailable report on the latest IPA. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. Signing, widget registration and workflow triggers are unchanged.

## Findings

The rendering-memory changes included in v0.2.40 switched two image snapshots to `CreateCGImage(image, bounds, CIFormat.Rgba8, colourSpace, false)`. Inspection of the installed Microsoft.iOS 27.0.10722 runtime assembly confirms that this five-argument binding passes the managed enum ordinal directly to Objective-C. The four-argument typed overload calls `CIImage.CIFormatToInt`, which resolves the exported native format constant. These values are not interchangeable. The binding difference is also visible in [Microsoft's wrapper](https://github.com/dotnet/macios/blob/main/src/CoreImage/CIContext.cs) and [binding declarations](https://github.com/dotnet/macios/blob/main/src/coreimage.cs). This is a confirmed SDK call defect in our renderer; the exact exception raised on the reporting iPhone has not been captured.

The new disposal sequence also assumed crop/blend operations always returned independent managed wrappers. An identity operation can instead share an existing wrapper. Disposing a temporary could invalidate the retained result or a cached raster, and the group-return path could dispose the value it returned. The [runtime reuses existing NSObject wrappers](https://github.com/dotnet/macios/blob/main/src/ObjCRuntime/Runtime.cs). This is a separate ownership hazard addressed by the fix; native aliasing on the reporting phone is not independently confirmed.

## Corrections

- Widget preview, saved full images, thumbnails and scene image groups use the mapped four-argument pixel-format API. Each materialization draws into a fresh, explicit sRGB RGBA8 bitmap before taking its snapshot. Eager copying preserves bounded four-layer checkpoints even if Core Image's initial CGImage is deferred. It preserves full widget bounds and alpha without applying layer opacity or blends a second time.
- A shared render batch distinguishes borrowed cached rasters from owned intermediate images using reference identity. It releases each owned wrapper once, keeps recipe intermediates until a snapshot exists, and transfers returned snapshots without disposing them. Rasters and source caches retain their limits of four and two respectively. Checkpoints never accept a borrowed input.
- Blend-mode example images use the same ownership rules. Clipboard conceal/reveal blur keeps its base inputs alive across frames and gives each frame an alias-aware temporary batch. Both paths already used the correct four-argument pixel-format overload.
- Preview failures identify the rendering stage and exception type, plus a bounded native exception identifier when available. Existing actionable IO/validation messages remain. Generic failures no longer suggest replacing a valid photo. Logs use structured stage/type/native-name fields; the UI does not copy arbitrary native exception reasons, paths or layer content. Failure still clears stale imagery and stops live observation/timers; reopening or changing the draft retries normally.

No saved design format, photo assets, slots, counter behaviour, background choice, geometry or navigation settings change. Saving still renders before repository persistence, so a rendering failure does not overwrite a saved design. Cancellation and suspension retain their existing cleanup and recovery behaviour.

## Verification

Reference-identity tests cover identity/distinct blend and crop results, cached inputs, equal-but-distinct wrappers, return ownership, exception cleanup, checkpoint validation and cache eviction across 1, 4, 5, 8 and 30 layers. Error tests cover actionable IO failures, all stages, native identifiers and exclusion of private exception text. Source guards reject the problematic overload, require independent RGBA8 materialization, and cover the related sample/blur paths.

The managed and source checks do not execute Core Image. Xcode/iPhone verification remains necessary: empty and one-layer drafts; Photo/Clock/Date/Text/Battery; four/five/eight/thirty mixed layers; every blend mode; alpha, gradients, rotation and off-canvas clipping; save/reopen/slot/Home Screen images; cancellation/backgrounding; blend examples and Clipboard hide/reveal animations. Compare a coloured asymmetric image to catch orientation or colour-space regressions and inspect peak memory with a thirty-layer design.
