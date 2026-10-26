# Thirty layers per widget

Unreleased. The owner authorized committing and pushing main. No version/tag change is included in this request.

## Limit and UI

The shared limit is now **30 mixed Photo, Clock, Date and Text layers per widget**, for Small, Medium, Large and iPhone Extra Large. The editor counter shows the number used out of 30. Add layer disables at 30 and its accessible description explains the limit. Duplicate is available through 29 layers and its domain operation independently rejects a late callback at 30. Cancelling choosers does not consume a layer. Existing designs remain valid without migration.

Managed design validation, native catalog loading and native scene part validation use the same maximum. C# scene validation also validates the owning design. Thirty layers retain their IDs, content, styles, angles, order, locks and visibility through save/reopen/publication. Source image paths remain private and slots keep the same design IDs. Scene artifact addressing/cleanup now reaches part 29 through the shared managed maximum. Slot count remains ten per family.

## Rendering and budgets

Merely accepting thirty records would leave the editor retaining one full canvas raster and source image for every photo. The compositor now retains at most four recent layer rasters and two recent decoded sources. It disposes evicted native objects and drains temporary autorelease objects per layer/group. Static groups and consecutive live text are materialized in batches of at most four layers; pending live text is materialized before composing a static group. Explicit non-deferred CGImage creation prevents the flattened result from retaining the entire lazy graph. Disposal covers completed, failed and suspended compositions.

Blend groups stay in original stacking order: Photo/Text share static groups; Clock/Date remain live separators. Background fill is applied once. Rotation, crop, transparency, colour space and final widget proportions use the same paths. Materialization uses sRGB RGBA8, matching final output; physical comparisons are still needed for blend rounding/quality and large-design responsiveness. This bounds app-owned caches and graph depth, rather than guaranteeing a fixed process/GPU memory ceiling or a frame rate on every device. Apple's [Core Image context guidance](https://developer.apple.com/documentation/coreimage/cicontext) and [cacheIntermediates option](https://developer.apple.com/documentation/coreimage/cicontextoption/cacheintermediates) describe the context/graph tradeoffs.

Existing shared-output limits are retained: 32,768 bytes for a live manifest; 3,000,000 combined photo-group pixels; 4,000,000 combined encoded image bytes; 1,024-pixel image edges; and the existing 1,000,000-byte library catalog. Scene export checks the manifest before raster work and stops an oversized image attempt early before retrying smaller groups. A design that still cannot fit reports a clear error, rather than publishing an oversized scene. Thirty text layers are cheaper than thirty detailed photos. A thirty-layer allowance does not override native memory or size safeguards.

## Verification

Tests exercise the 30/31 boundary, all families and mixed kinds, duplicate/add late guards, stable 30-row reordering, saved content and rotation, unchanged slot assignments, publication of image part 29, failed-save preservation, and retained image budgets. A worst-case-name/precision sample confirms thirty live layers fit the existing manifest cap. Packaging checks guard agreement between managed and Swift maxima. The native Swift test covers thirty accepted catalog/scene records and thirty-one rejected records.

Completed automated checks: 232 Core, 376 headless MAUI App, 92 Infrastructure and 42 packaging tests passed (742 total), along with source consistency and whitespace checks. Managed iOS compilation passed without warnings/errors. The added Swift test remains unexecuted on Windows.

Physical iPhone work remains: open/save/edit/reorder thirty-layer designs; drag/pinch/rotate complex photos; compare every blend mode and transparency before/after batching; test background/picker recovery; assign to each family; inspect live time/date delivery and profile native/GPU peak memory, frame responsiveness and battery impact. Windows managed compilation and headless tests cannot establish those device results or run the added Swift test.
