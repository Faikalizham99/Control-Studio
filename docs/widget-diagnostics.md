# Studio Widget loading diagnostics

Version **0.2.14 (16)**, authorized for commit, main push and a new version tag on 22 October 2026. The owner will monitor the build and test on iPhone. This is a diagnostic experiment for custom Studio Widgets remaining on the grey placeholder on the owner's iPhone running iOS 27.0.1. The owner reports that the original counter widget still works, and both Small and Extra Large custom widgets fail even with a selected design. The exact root cause is not confirmed.

The four supplied JetsamEvent reports were generated at 06:04, 10:18, 11:10 and 13:16 Malaysia time on 22 October. All predate the v0.2.13 commit at 21:47. The widget appears with `long-idle-exit` at about 8.3 MiB in the first report and without a termination reason at about 3.4 MiB in the last. None records a memory-limit termination for ControlStudioWidgets. These older records do not diagnose the later custom-widget failure.

### Version 0.2.15 (17): static-widget comparison

The owner authorized committing, pushing main and pushing a new version tag on 23 October. The owner will monitor the tag-triggered IPA build and test on iPhone; build monitoring and manual dispatch are not part of this release task.

The owner's v0.2.14 reports captured successful `suggest.begin` / `suggest.ready count=2` records in both text and photo sessions, but no identifier resolution, snapshot, timeline or image-loading records. This proves the design list was read during configuration; it does not prove timeline dispatch, PNG access or rendering. Missing records alone do not prove absence of execution in every context. These reports do not establish a Photos permission or memory failure.

The follow-up adds **Studio Widget Test**, kind `ControlStudio.DesignDiagnostic`, alongside the unchanged Counter and configurable Studio Widget kinds. It uses `StaticConfiguration` / `TimelineProvider` without constructing a configuration intent, invoking an entity query or consulting a saved selection. It picks the first catalog design whose size matches the widget. Both providers call the same loader and use the same view, shared PNG validation, UIImage creation, family list and 15-minute refresh policy. This isolates the configuration path while retaining the existing image/rendering path for comparison. The original selected-design, deleted-design and wrong-size checks are retained.

## Device test

After installing a build containing these changes:

1. Save a **Small** design, then open **Studio → Widgets → Widget diagnostics → Start text test**. A size with just one saved design gives the clearest comparison; existing designs do not need to be deleted.
2. Add Control Studio's **Studio Widget Test** to the Home Screen in **Small**. Choose the new test widget, rather than the original **Studio Widget** or **Control Studio Counter**. The test widget has no design selector and automatically chooses the first saved design matching its size.
3. Note whether **Studio text test / Text test succeeded** appears, an error message appears, or grey placeholder bars remain. The text includes a request code and timestamp to distinguish it from a cached result.
4. Return to diagnostics, tap **Read results**, then **Copy report**. Share the report and the observed Home Screen result. Copy is confirmed with the existing app toast and VoiceOver announcement.
5. Select **Start photo test** and check the same test widget, copying the second report separately. Each start creates a fresh session; older/in-flight sessions are not shown as current results. Opening the test widget's gallery preview can also create snapshot records: a snapshot alone does not confirm a Home Screen timeline ran.
6. For an optional side-by-side comparison, keep the original **Studio Widget** in the same size and reselect that same design in **Edit Widget**. Both widget types follow the shared text/photo test mode. Copy each report before starting another session.
7. Select **Stop test** when finished. This turns recording off and restores the normal photo path for both widget types when iOS next refreshes. The active test is stored in the App Group and survives app backgrounding/relaunch until stopped. Remove **Studio Widget Test** from the Home Screen when finished; stopping diagnostics does not remove it, and it continues showing the first matching photo design with recording off.

Refresh requests are requests, not a delivery guarantee. Reading results is manual and does not repeatedly request widget reloads. Saving, deleting, publishing and starting/stopping diagnostics request reloads for both design widget kinds through the existing bridge. The Counter reload path is unchanged. Normal design saving/publication, the existing counter widget, original configured widget kind, App Intent/entity identities, family registration and saved selections are preserved. IPA generation remains restricted to a new pushed version tag.

## What the experiment changes

The original widget's text test uses AppIntentConfiguration and EntityQuery resolution. The new test widget uses StaticConfiguration with automatic size matching instead. Both use the same catalog reader and size check, then bypass image-file reads and UIImage creation in text mode, returning a plain text SwiftUI view with no GeometryReader or image modifiers. Photo mode follows the existing bounded PNG → UIImage → SwiftUI image path. Both record execution stages; the default without a settings file is recording off with the normal photo path.

Query records distinguish suggested-design enumeration from identifier resolution. Snapshot and timeline records are separate, with random request IDs and increasing per-request sequence numbers. Provider stages cover catalog reads, selected/fallback design matching, missing/deleted/wrong-size choices, image reads, UIImage creation and entry preparation. Error codes are fixed categories. No side effects are added to the SwiftUI view body. The synchronous placeholder performs no diagnostic disk I/O.

New `provider.begin` records identify `path=static` (Studio Widget Test) or `path=configurable` (Studio Widget), with family and selected-ID-present flag. All subsequent records in that request belong to that path. Source values and the schema remain unchanged, so existing stored sessions still read. Reports count captured provider-begin events separately from query events; zero counts mean no captured provider records, not proof of zero invocations.

`entry.ready` is written immediately before returning the snapshot/timeline. It proves entry preparation reached that point, **not** successful archive creation, delivery or display by iOS. `image.created` proves UIImage construction returned; it does not prove all deferred pixel decoding or system rendering completed. Empty results do not prove a provider was never invoked: unavailable diagnostic storage can also prevent records.

| Evidence | Next investigation |
|---|---|
| Static photo widget displays the matching design; configurable widget does not | Configuration/entity resolution, metadata, cached selection or dispatch. This isolates a path, not a specific Apple or application defect. |
| Static text displays but static photo does not | Shared image read/decode/render/archive; follow that static request's checkpoints. |
| Neither static mode displays | Inspect static snapshot vs timeline records and any error entry; no records still leaves registration/dispatch/storage logging uncertain. |
| Text appears; photo fails | Image handling, deferred decoding, SwiftUI image layout or the system view archive |
| Only query/snapshot records, no timeline | Configuration resolution/timeline dispatch; distinguish cached snapshots from live timeline calls |
| `resolve.begin` without `resolve.ready` | Entity-query disk read, thrown failure or process termination; inspect any failure record |
| `selection.wrong-size` or `selection.deleted` | Correct the saved selection; a later plain error entry should be visible |
| `image.read-start` followed by failure | Shared PNG access/validation; fixed error category narrows it further |
| `entry.ready image=1` but placeholder remains | Investigate archive/render/delivery after provider preparation, rather than assuming publication succeeded |
| No native records | Could be no provider invocation, stale installation/configuration, or diagnostic-storage failure; a live device console may still be necessary |

## Persistence, bounds and privacy review

Settings and events live separately under the shared `WidgetDesigns/diagnostics/` directory. Source-generated C# JSON and Swift Codable share schema-1 fixtures. Settings are atomic, bounded to 2,048 bytes and validated before replacement; corrupt/future settings are preserved and surfaced in the host. The widget falls back to its normal path if diagnostic settings cannot be read.

Each event owns an atomically installed session/UUID-named file. Concurrent requests do not append to a shared JSON document or overwrite each other's records. Writers prune recognised event files to the latest 128, best effort under concurrent writes; reads return at most 128 valid events for the active session. Corrupt/oversized/invalid event files are skipped and counted. An old in-flight request keeps its original session and cannot masquerade as the new test. Neither starting nor stopping changes design/photo/catalog data. Existing file-metadata privacy declarations cover this directory access.

Reports contain app/OS versions, test mode, random correlation IDs, UTC timestamps, stage codes, family/count/byte/dimension values and fixed error categories. Source names, photo contents, design IDs/names, user file paths and exception descriptions are not recorded. Copying is explicit. Status/controls use the app's existing readable Light/Dark surfaces and accent-aware primary action; busy taps are guarded, reports reload after page reappearance, and copy notices/subscriptions clear on backgrounding/disappearance.

The follow-up adds no settings fields, migrations, private photo-library reads, network requests or storage changes. The automatic test selection follows saved catalog order and can change after design deletion or catalog reordering; it is a comparison tool, not a replacement for per-instance selections. Gallery description, in-app instructions and report path labels distinguish it from the original widget. Both retain shared image accessibility labels and readable text styling. New native callbacks synchronously prepare one entry and call completion once on every handled success/error path; placeholders remain free of storage I/O. Requests share the existing bounded session/event storage, and gallery snapshots may use part of the 128-record limit.

## Verification

For the 23 October static-widget follow-up, the same 257 automated tests were rerun and passed, along with managed iOS compilation, source/project consistency, workflow lint and whitespace checks. Review covered distinct widget registration/kinds, shared size selection and rendering, one completion per native callback, reload coverage for save/delete/start/stop, old-session/schema compatibility, off-mode behavior, copy/background lifecycle, readable shared controls and accessible image/text output. The authorized release advances host and native versions together to 0.2.15 (17); workflow triggers are unchanged. Native Swift compilation, real static-vs-configurable dispatch/rendering, gallery discovery, Light/Dark/large-text/VoiceOver presentation and Counter/Shortcut device regressions remain unverified on Windows.

257 automated tests passed: 66 Core, 45 infrastructure, 119 app and 27 packaging. New tests cover shared fixture decoding, reflection-disabled JSON serialization, validation, persistent opt-in/start/stop, session isolation, concurrent bounded event writes, malformed records and preserved design/future settings data. Managed iOS compilation, source/project checks, workflow lint and whitespace checks pass.

Three new Swift diagnostics-storage tests cover the shared fixtures, bounded/disabled event recording and preserved bad/future settings. Swift/Xcode compilation and those tests cannot run in this Windows environment. Real Home Screen dispatch/rendering, native log delivery, VoiceOver, small-screen/large-text layout and the original counter/Shortcut regressions still require the next Mac/device build. This implementation does not claim to have fixed or reproduced the original placeholder failure.
