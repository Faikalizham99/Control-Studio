# Widget performance follow-up

28 October 2026. Baseline: `28d6a54` / 0.2.44 (46). This implements the safe, locally measurable part of audit finding A07 after the owner confirmed the v0.2.44 blending fix on their phone. No release version, tag, workflow or signing setting changes are part of this work.

## Changes and evidence

**Repeated library publication:** previously every My Widgets appearance read all private full-render PNGs and live scene images into new byte arrays, even if nothing changed. `WidgetDesignPublisher` now offers `PublishIfChangedAsync`. A successful publication retains only each supported design's metadata and its source/installed file paths, sizes, creation times and modification times. The next publication still reads and validates both shared manifests and the private library. It skips payload reads only when the full design and every artifact stamp match. The cache is replaced after success, pruned to the current library (maximum 50 designs), and cleared after a failed attempt. There is no image-byte cache.

New saves, slot changes and deletion use this path; saves' new immutable render keys invalidate the relevant entry. Deleted or changed source/installed files and a different App Group directory trigger full validation/installation. Full validation also compares installed bytes in a bounded 64 KiB pooled buffer and repairs damaged installed files. It does not allocate a second full PNG for that comparison. Unchanged catalog/slot bytes are not rewritten. Existing generation retention, catalog-before-slots ordering and old scene byte preservation remain.

**Explicit Refresh** on My Widgets and Widget slots continues to use `PublishAsync`, bypassing the metadata cache. File stamps are an optimization for app-owned immutable artifacts, not content hashes; a deliberate same-size change with restored timestamps can evade a passive cache hit. Explicit Refresh compares actual bytes and bypasses this assumption. No persisted cache/schema or migration was added.

**Editor redraws:** `WidgetRenderQueue` coalesces multiple same-turn main-thread requests into one queued render of the latest draft. It does not debounce gestures or delay the final state by an arbitrary timer. Null designs, unload and disconnect cancel queued work and release the current native compositor/image/timer/battery observation. An unloaded handler cannot enqueue new paints until loaded again.

`WidgetRenderIdentity` validates the complete draft, then compares actual pixel inputs against the last successful paint. Names, lock states, save metadata and hidden/zero-opacity layers do not cause another full composition. Geometry, order, crop, rotation, blending, background, canvas dimensions, text styles/gradients, formatted live text and visible battery state still invalidate it. A single timestamp feeds the identity and compositor. Errors clear both the previous image and identity, preserving truthful failure display and retry. Clock overlays update only when displayed live text changes; battery overlays update when their reading changes.

This avoids repeated paints within an unchanged clock second/minute or date. A clock displaying seconds still paints each changed second. Static photo groups are still recomposed for a necessary full paint; no extra group bitmap cache, cache-size increase, worker-thread UIKit rendering or native image ownership change was introduced. The four-raster/two-source limits and independent checkpoint snapshots remain intact.

**Gallery lifecycle:** My Widgets releases its bounded visible image sources on window deactivation and restores visible sources on return. The common Studio page cancels old reads, discards late results and queues a fresh read if activation occurs while a prior operation finishes. Both activation/deactivation handlers are removed on departure. Counter preview pages share the same read/action guards. Existing card proportions, themes, accent styling, semantics and tap targets are unchanged.

## Local publication measurement

Harness: `benchmarks/ControlStudio.Performance`, a console project referencing existing Infrastructure/Core with no new package dependencies. It creates deterministic valid 1024-by-1024 PNGs (3,318,751 bytes each), saves real repository designs and invokes the real publisher. Cases have 1/10/50 saved designs with 1/10/30 photo layers per design. Each case installs first, warms once, then measures seven consecutive repeated publications. Generated data is restricted to a unique child of `.tmp/widget-performance` and removed afterward.

Same Windows workspace: Microsoft Windows 10.0.26200, .NET 10.0.12, 10 logical processors. Baseline used the original `PublishAsync`; after uses `PublishIfChangedAsync` for the corresponding unchanged reload. Values below are medians of seven samples, not iPhone measurements.

| Saved designs / layers per design | Before elapsed | After elapsed | Before managed allocation | After managed allocation |
|---|---:|---:|---:|---:|
| 1 / 1 | 6.437 ms | 0.517 ms | 3,344,952 B | 20,368 B |
| 10 / 10 | 22.931 ms | 5.007 ms | 33,724,744 B | 403,368 B |
| 50 / 30 | 132.402 ms | 28.309 ms | 173,322,416 B | 5,280,240 B |

Elapsed samples in milliseconds, in recorded order:

| Case | Before | After |
|---|---|---|
| 1 / 1 | 8.057, 5.833, 6.509, 7.550, 6.437, 4.898, 5.908 | 0.696, 0.612, 0.517, 0.491, 0.513, 0.525, 0.499 |
| 10 / 10 | 22.351, 22.318, 22.931, 23.539, 22.972, 22.127, 24.429 | 4.892, 5.236, 4.868, 4.904, 5.021, 5.298, 5.007 |
| 50 / 30 | 135.501, 132.402, 145.899, 138.493, 127.364, 128.426, 125.467 | 29.178, 27.864, 31.475, 28.309, 29.882, 27.881, 25.476 |

Managed-allocation samples in bytes:

| Case | Before | After |
|---|---|---|
| 1 / 1 | 3342936, 3342936, 3362600, 3342936, 3369552, 3344952, 3363656 | 20368, 20368, 20368, 20368, 20368, 20368, 20368 |
| 10 / 10 | 33792624, 33724744, 33724744, 33724744, 33724744, 33791224, 33732656 | 403368, 519248, 403368, 403368, 403368, 403368, 403368 |
| 50 / 30 | 173320584, 173307712, 173329000, 173329736, 174352952, 173319384, 173322416 | 5281040, 5278616, 7363144, 5280240, 7366296, 5279416, 5278840 |

The final `--force` run measured these separate full-revalidation medians:

| Saved designs / layers per design | Full Refresh elapsed | Full Refresh managed allocation |
|---|---:|---:|
| 1 / 1 | 4.455 ms | 3,344,440 B |
| 10 / 10 | 39.695 ms | 33,813,616 B |
| 50 / 30 | 241.064 ms | 174,290,400 B |

This is a tradeoff: full revalidation now reads installed payloads as well as private sources, so the larger explicit Refresh cases take more I/O/time than the old source-only validation. A new publisher instance with an already populated shared directory also needs this full first validation; the fast path benefits subsequent publications within that service lifetime. No cold-start or full-refresh speedup is claimed.

`GC.GetTotalAllocatedBytes(precise: true)` measures process-wide cumulative managed allocation during each call, including asynchronous work. It is not retained heap, peak memory or physical footprint. Filesystem cache, JIT, GC and other system activity affect elapsed results; this is a small development benchmark, not a controlled device study. Initial installation, forced Refresh and live-scene export are not represented by the optimized table. The harness's `--force` option separately exercises repeated full revalidation.

From the repository root:

```powershell
.\.tools\dotnet27\dotnet.exe restore benchmarks/ControlStudio.Performance --locked-mode
.\.tools\dotnet27\dotnet.exe run --project benchmarks/ControlStudio.Performance -c Release --no-restore
# Optional full-revalidation comparison, not the unchanged-reload fast path:
.\.tools\dotnet27\dotnet.exe run --project benchmarks/ControlStudio.Performance -c Release --no-restore -- --force
```

## Regression verification

Targeted tests exercise real filesystem/cache behavior using exclusive payload locks, corruption/removal, same-stamp installed corruption with forced refresh, future shared metadata, changed layer data with the same render key, container remapping, new revisions, assignment/deletion, old generation retention and cancellation/retry. Legacy scenes still publish byte-for-byte on full and cached reloads; only an editor save upgrades grouping.

Portable render tests cover 1/10/30-layer identity comparison; every existing pixel-edit category, gradients and live text/battery changes; hidden invalid data rejection; same-turn bursts; unload cancellation; a new request after cancellation; failed-post recovery; and requests raised during a render. Lifecycle tests cover image-source release, late background reads, rapid deactivation/activation, renewed reads and action recovery. These execute production portable helpers/page code, not UIKit's actual rendering or queue integration.

Final verification, from the repository root:

| Command | Result |
|---|---|
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Core.Tests -c Release --no-restore` | 272 passed |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Infrastructure.Tests -c Release --no-restore` | 120 passed |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.App.Tests -c Release --no-restore` | 437 passed |
| `.\.tools\python\python.exe -m unittest discover -s tests/packaging` | 53 passed |
| `.\.tools\python\python.exe scripts/check-source.py` | Passed; native generated project unchanged |
| `.\.tools\dotnet27\dotnet.exe build src/ControlStudio.App/ControlStudio.App.csproj -c Release --no-restore -t:Compile -p:DesignTimeBuild=true -p:BuildIpa=false -p:ArchiveOnBuild=false -p:EnableCodeSigning=false` | Managed iOS C# compile passed, 0 warnings/errors; no native linking |
| `.\.tools\dotnet27\dotnet.exe restore benchmarks/ControlStudio.Performance --locked-mode` | Passed; no new package dependency |
| Benchmark commands above (default and `--force`) | Both completed with valid generated fixtures and cleanup |
| `git diff --check` | Passed |

Total: **882 local checks**, zero failed/skipped, including 31 added cases (15 Infrastructure and 16 App). A first lifecycle test run correctly exposed an uncancelled earlier appearance token; after cancelling it before replacing the lifetime, the targeted and complete App suites passed. No failed check is represented as a passing native/device result.

No native Swift code, widget provider registration, JSON contract, App Group capability, bundle identifier, SQLite schema, font resource or transparency adapter was changed. No files/dependencies were removed or moved. The optional benchmark was added to the solution; workflows remain unchanged and do not run it automatically. No Git commit, push, tag or workflow run was requested by this implementation step.

## iPhone verification still required

1. Compare the previous IPA with the future authorized build on the same phone, brightness, thermal state and OS. Use matching 1-, 10- and 30-layer mixed designs. Drag, pinch, rotate, toggle visibility/lock, reorder, switch tools and confirm the final released gesture position. Check small, medium, large and iPhone XL preview proportions.
2. Run solid/gradient second clocks, minute clocks, dates and batteries. Verify ticking, overlays, charging updates, hidden layers and the device-confirmed blending behavior, then Save/reopen/assign and compare the Home Screen widget.
3. Open/close the editor and gallery 20 times, including background/resume, interrupted/cancelled photo and colour pickers, and rapid navigation. Confirm controls recover, old reads do not restore images and the correct design appears. Check light/dark themes and VoiceOver descriptions.
4. With 50 designs, compare returning to My Widgets and explicit Refresh. Change/delete a design and reassign its slot; confirm old cached timelines remain valid until replaced.
5. On a Mac, use Instruments to record frame times/hitches and physical footprint during those scenarios. Look for a stable post-navigation memory plateau, rather than inferring a leak from one high-water mark. This Windows session cannot establish native FPS, leak freedom, peak footprint, app startup, battery drain or WidgetKit delivery timing. Necessary 30-layer/seconds composition remains a profiling candidate before increasing native caches or adding bitmap-group caching.
