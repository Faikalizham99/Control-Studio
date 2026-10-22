# Studio Widget loading diagnostics

Version **0.2.14 (16)**, authorized for commit, main push and a new version tag on 22 October 2026. The owner will monitor the build and test on iPhone. This is a diagnostic experiment for custom Studio Widgets remaining on the grey placeholder on the owner's iPhone running iOS 27.0.1. The owner reports that the original counter widget still works, and both Small and Extra Large custom widgets fail even with a selected design. The exact root cause is not confirmed.

The four supplied JetsamEvent reports were generated at 06:04, 10:18, 11:10 and 13:16 Malaysia time on 22 October. All predate the v0.2.13 commit at 21:47. The widget appears with `long-idle-exit` at about 8.3 MiB in the first report and without a termination reason at about 3.4 MiB in the last. None records a memory-limit termination for ControlStudioWidgets. These older records do not diagnose the later custom-widget failure.

## Device test

After installing a build containing these changes:

1. Open **Studio → Widgets → Widget diagnostics → Start text test**.
2. Return to the Home Screen, touch and hold **Studio Widget → Edit Widget**, and reselect a saved design matching the installed widget's size/layout. The original counter widget is a separate widget and is not this test.
3. Note whether **Studio text test / Text test succeeded** appears, an error message appears, or grey placeholder bars remain. The text includes a request code and timestamp to distinguish it from a cached result.
4. Return to diagnostics, tap **Read results**, then **Copy report**. Share the report and the observed Home Screen result. Copy is confirmed with the existing app toast and VoiceOver announcement.
5. Select **Start photo test** and repeat, copying the second report separately. Each start creates a fresh session; older/in-flight sessions are not shown as current results.
6. Select **Stop test** when finished. This turns diagnostics off and restores the normal image path when iOS next refreshes. The active test is stored in the App Group and survives app backgrounding/relaunch until stopped. It applies to all custom Studio Widget instances on this device.

Refresh requests are requests, not a delivery guarantee. Reading results is manual and does not repeatedly request widget reloads. Normal design saving/publication, the existing counter widget, configured widget kind, App Intent/entity identities, family registration and saved selections are preserved. IPA generation is triggered only by the new version tag; no manual dispatch or build monitoring is part of this release task.

## What the experiment changes

The text test uses the same AppIntentConfiguration, EntityQuery resolution, catalog reader, selection lookup and size check. It then bypasses image-file reads and UIImage creation, returning a plain text SwiftUI view with no GeometryReader or image modifiers. The photo test follows the existing bounded PNG → UIImage → SwiftUI image path. Both tests record execution stages; the default without a settings file is diagnostics off with the normal photo path.

Query records distinguish suggested-design enumeration from identifier resolution. Snapshot and timeline records are separate, with random request IDs and increasing per-request sequence numbers. Provider stages cover catalog reads, selected/fallback design matching, missing/deleted/wrong-size choices, image reads, UIImage creation and entry preparation. Error codes are fixed categories. No side effects are added to the SwiftUI view body. The synchronous placeholder performs no diagnostic disk I/O.

`entry.ready` is written immediately before returning the snapshot/timeline. It proves entry preparation reached that point, **not** successful archive creation, delivery or display by iOS. `image.created` proves UIImage construction returned; it does not prove all deferred pixel decoding or system rendering completed. Empty results do not prove a provider was never invoked: unavailable diagnostic storage can also prevent records.

| Evidence | Next investigation |
|---|---|
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

## Verification

257 automated tests passed: 66 Core, 45 infrastructure, 119 app and 27 packaging. New tests cover shared fixture decoding, reflection-disabled JSON serialization, validation, persistent opt-in/start/stop, session isolation, concurrent bounded event writes, malformed records and preserved design/future settings data. Managed iOS compilation, source/project checks, workflow lint and whitespace checks pass.

Three new Swift diagnostics-storage tests cover the shared fixtures, bounded/disabled event recording and preserved bad/future settings. Swift/Xcode compilation and those tests cannot run in this Windows environment. Real Home Screen dispatch/rendering, native log delivery, VoiceOver, small-screen/large-text layout and the original counter/Shortcut regressions still require the next Mac/device build. This implementation does not claim to have fixed or reproduced the original placeholder failure.
