# Architecture optimization report

28 October 2026. Audit baseline commit `5ed5904`, version **0.2.42 (44)**. Prepared for release **0.2.43 (45)** after the owner explicitly authorized committing, pushing main and pushing a new version tag. The audit itself did not publish changes. A new tag push requests the IPA build; macOS/device results remain pending and the owner will monitor the build.

## Architecture health

The existing App -> Infrastructure -> Core dependency direction is acyclic and appropriate for the project's current size. Core holds portable contracts/validation/geometry; Infrastructure owns native-counter adaptation and local persistence/publication; MAUI pages and iOS handlers own UI/platform lifetimes. Swift's shared module keeps the counter SQL identical across processes, and versioned JSON fixtures connect the C# and Swift widget implementations.

The project is not an MVVM/XAML/Shell application: it uses code-built pages, explicit child-page construction and DI for long-lived roots/services. No new framework, folder hierarchy or general-purpose interfaces were introduced. The [audit](architecture-audit.md) contains the dependency diagram, area-by-area assessment, significant evidence and removal classifications.

## Important findings

| Finding | Severity | Disposition |
|---|---|---|
| A02: Profile save can publish uncommitted state, let write errors escape, or disable Back after dismissal fails | High | Fixed and covered by failure/retry/ownership tests |
| A01: Source validation overwrites stale generated project; traverses local SDK/build trees | Medium | Fixed; read-only failure tests and measured traversal improvement |
| A06: Hidden live layers retain photo-group boundaries, affecting blends across them | Medium | Documented in the audit; subsequently addressed by the separately authorized [versioned grouping fix](widget-hidden-layer-blending.md) |
| A07: Entire catalog republished on library load; composition runs on UI thread with bounded but small caches | Medium | Subsequently addressed in part by [measured publication and redraw/lifecycle improvements](widget-performance.md); device profiling remains |
| A08: Working transparency uses private iOS 27 descriptors | High compatibility risk | Preserve guarded native adapter; device/OS regression risk remains |
| A03/A04/A05/A09: Duplicated input validation, uncached packages, obsolete internal helpers and stale documentation | Low | Safe cleanup implemented |

No new critical security issue was demonstrated. This is a bounded source/test audit, not proof that the app has no defects or a formal penetration test. The original 808 checks were green despite A01/A02, which is why targeted failure tests were necessary.

## Changes made

### Profile state and navigation recovery

- `AppearancePreferences.SaveProfile` updates in-memory name/photo and raises notifications only after both existing preference writes complete. A failed write no longer makes a draft photo appear committed to cleanup/observers.
- `ProfileEditorPage` catches save errors, keeps the draft editable for retry, and distinguishes a committed save from failed navigation. Back/Cancel can retry leaving a saved page without writing preferences again. Duplicate saves/system-back while the save/dismissal is busy are guarded.
- Invalid-name focus is restored after controls are re-enabled. Failed saves show actionable generic text rather than sensitive file paths/native errors. Successful appearance/layout and the existing crop flow are unchanged.
- Preferences are still two keys. If the second write fails or the process dies between writes, the persisted name may change while the photo key remains old. The fix protects in-memory state and photo ownership; it does **not** claim a crash-atomic pair. Introducing a versioned single profile record is separate work.

### Import and confirmed dead code cleanup

- `ProfilePhotoStore` now uses the existing bounded `PhotoImportInput.ReadAsync`, as Clipboard/Widget import already do. Removed the duplicate 8KB/10MB read loop. The same helper tests exercise exact bytes, short reads, oversized/empty input and cancellation. Crop dimensions, JPEG compression and paths are unchanged.
- Clipboard/Profile Files imports now mirror their Photo Library paths' conditional `IDisposable` FileResult cleanup, after the import stream closes. Native picker/crop behavior still requires device checks.
- Removed the unreferenced **internal** `DashboardStyle.Card` factory and its now-unused namespace import.
- Removed the **internal** `StudioContent.ClipboardDraft` forwarding wrapper, whose only callers were tests. Those tests retain their exact validation, content and metadata assertions against the production `StudioContent.Draft` path.
- **Whole files deleted: none. Files moved: none. Dependencies removed/upgraded: none.** No fonts, licences, image resources, migration code, public APIs, native exports, intent types or widget registrations were removed.

### Read-only tooling and CI

- `generate-xcode-project.py` retains normal generation and adds `generate(check=True)`: compare expected UTF-8 bytes without creating a directory or writing a file. It returns the same project model on success and reports a stale/missing output explicitly.
- `check-source.py` uses that check and prunes local SDK/generated/output trees **before** descending. Existing source XML/plist, manifest, entitlements, SDK pins, widget metadata and project-byte validation remain.
- Both existing setup-dotnet steps now cache global NuGet packages using the six committed `packages.lock.json` files, following the [pinned action's supported cache inputs](https://github.com/actions/setup-dotnet/blob/26b0ec14cb23fa6904739307f278c14f94c95bf1/README.md#caching-nuget-packages). Locked restores remain. No signing secret, profile, keychain, workload or native product cache was added.
- Build workflow trigger remains `push.tags: ['v*.*.*']`, with the existing created/not-deleted guard. Branch/PR test workflow still cannot package/upload an IPA. There is no manual IPA dispatch. No remote workflow run was requested.

### Current documentation

Updated README, architecture and known limitations to describe today's Studio, static slots and Photo/Clock/Date/Text/Battery layers. Corrected obsolete iOS 26.5/runner, missing reset and deferred widget-builder claims without rewriting historical milestone evidence. Updated the in-app About instructions' layer list and battery timing explanation; no new controls or visual redesign.

## Measured performance and limits

Same Windows workspace/Python binary; consecutive warmed executions of the complete `scripts/check-source.py` command, timed by PowerShell Stopwatch:

| Run | Before | After |
|---|---:|---:|
| 1 | 1.077 s | 0.118 s |
| 2 | 0.989 s | 0.117 s |
| 3 | 0.937 s | 0.115 s |
| Median | 0.989 s | 0.117 s |

This measures source-validation traversal on this development checkout, including its ignored tooling folders. It is not a controlled device benchmark or a guarantee for a clean CI checkout. There is **no measured app startup, FPS, heap, battery, database-latency, IPA-size or CI build-time improvement** in this report. NuGet cache benefit depends on eligible future hits; no workflow has run for these changes. Sharing the photo input reader reduces duplicated logic, not proven decode cost.

## Automated validation

All commands ran from `C:\Users\User\Documents\Project\Control-Studio`. Counts are final suite outcomes, not the sum of repeated focused runs.

| Command | Result |
|---|---|
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Core.Tests -c Release --no-restore` | **255 passed**, 0 failed/skipped |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Infrastructure.Tests -c Release --no-restore` | **96 passed**, 0 failed/skipped |
| `.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.App.Tests -c Release --no-restore` | **415 passed**, 0 failed/skipped |
| `.\.tools\python\python.exe -m unittest discover -s tests/packaging` | **53 passed** |
| `.\.tools\python\python.exe scripts/check-source.py` | Passed: source/configuration checks and identical generated native project bytes |
| `.\.tools\dotnet27\dotnet.exe build src/ControlStudio.App/ControlStudio.App.csproj -c Release --no-restore -t:Compile -p:DesignTimeBuild=true -p:BuildIpa=false -p:ArchiveOnBuild=false -p:EnableCodeSigning=false` | Managed iOS compile passed, **0 warnings / 0 errors**; not native linking |
| `.\.tools\dotnet27\dotnet.exe list ControlStudio.sln package --vulnerable --include-transitive --no-restore --format json` | Successful authorized-network retry; six projects, **no vulnerable packages reported** by NuGet at audit time |
| `git diff --check` | Passed |

**Final total: 819 local checks** (baseline 808). Eight additional App cases cover first/second profile-key failure, draft cancellation/retry, saved-photo ownership, failed dismissal, invalid names and rapid duplicate saves. Three Python tests cover current/stale/missing project byte/mtime preservation and source-tree pruning. Profile-state tests were run before the fix and both failed on the incorrect in-memory name, then passed after correction. The new tooling tests initially failed because the read-only/check-enumeration APIs did not exist.

One targeted Python run hit Windows temporary-directory permission errors; rerunning with authorized temp-folder access passed. The full final Python suite passed. The first package audit also hit restricted-network DNS failure; the successful authorized retry is the result reported above. No failures are suppressed or represented as passing.

Tests execute production Core/persistence/portable UI code where linked. Python packaging source guards are narrower than runtime tests; generated-project model/byte tests are actual Python execution. The shipping project compile additionally covers iOS C# adapters absent from the portable App tests. SDK compiler/default analyzers and existing configuration checks ran; no new third-party analyzer dependency was added.

## Native iOS compatibility

Bundle IDs, App Group configuration, entitlements, privacy declarations, C ABI signatures, SQLite schemas, JSON schemas, layer enum values, slots, WidgetKit timelines, native adapter and App Intents discovery/embedding were preserved. There are no Swift/Objective-C runtime changes. The audit's generator refactoring produced identical bytes; release preparation changes only host/extension versions to 0.2.43 (45), then revalidates the generated project. No newly built IPA was available for final package/signature inspection.

**Not run here:** `swift test --package-path native`, Xcode build/metadata extraction, native MAUI linking, `codesign`, IPA installation or physical iPhone tests. The workflow cache's actual service behavior is unverified until an owner-authorized future push. The prior release's gradient ticking/battery-label device checks are not silently certified by this audit.

## UI/UX, persistence and lifecycle review

- Successful Profile appearance remains the same in both themes; only failure recovery and explanatory About text change. Focus, busy/duplicate-tap guards, saved-vs-draft ownership, invalid input, cancellation and departure were reviewed and exercised where portable.
- Existing complete tests continue to cover accent contrast, spacing/disabled actions, editor scroll stability, layer gestures/locks/duplication, pin/reorder, concealment/copy feedback, size/slot proportions, old contracts, invalid/future data and SQLite concurrency.
- No destructive data migration, database reset, content sweep or unrelated working-tree change occurred. Test-created data uses test temporary directories. No telemetry or extra permissions were introduced.
- On an iPhone, check Profile Gallery and Files -> cancel, crop, adjust, save, reopen and Back/Cancel; background/resume while picking; invalid-name focus and VoiceOver error readability. Recheck Clipboard file import and the About page at large text sizes. Confirm counter/Shortcuts and existing slot widgets remain working after a future IPA build.

## Remaining work, in priority order

1. **Native/device verification:** run the unchanged native test/build pipeline after the owner authorizes publishing, then complete the above iPhone regression checks. Retest the private transparency adapter on each OS update.
2. **Blend semantics/compatibility (A06):** subsequently implemented as a separately authorized [follow-up](widget-hidden-layer-blending.md), retaining old exports and applying corrected grouping on save. The audit-release counts above remain historical; follow-up native/device verification is still needed.
3. **Device performance (A07):** the separately authorized [performance follow-up](widget-performance.md) measures and reduces repeated publication allocations, skips redundant editor paints and releases background gallery sources. Profile 1/10/30 mixed layers, seconds ticks, reorder/rotate/pinch and repeated navigation on the phone before adding native group caches or changing raster budgets. Follow [Microsoft's profiling guidance](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance).
4. **Persistence resilience:** a single versioned Profile preference record could make name/photo commit together. A future generation manifest could bind shared catalog/slots atomically. Both need migration/interruption tests; neither warrants an unplanned schema rewrite now.
5. **Maintainability:** extract editor responsibilities only as features change; split the large mixed UI regression fixture by feature with shared test utilities. Preserve the test-linked production-source build check. Consider a matched MAUI patch upgrade separately; no vulnerable pinned packages were reported by this audit.
6. **Product recovery:** general user-visible backup/restore would reduce uninstall/re-signing data-loss risk, but is a separate feature requiring owner approval and a design.
