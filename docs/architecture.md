# Architecture

The MAUI UI depends on `ICounterRepository`; the repository adapts a small native C ABI and decodes a versioned JSON result using a source-generated `System.Text.Json` context. C# core/infrastructure have no platform dependency or external runtime package. DI supplies the iOS bridge. The application logs operation type, storage scope and error type; it does not log database contents, file paths or signing credentials.

The bridge and both native extensions compile the **same** `native/Shared/CounterStore.swift`. This keeps counter SQL/schema behavior in one implementation. No database synchronization is necessary for the counter. Native calls run on a worker thread; the UI disables duplicate button taps and catches errors. The Home screen reads again when it appears, when the app resumes, or when Refresh is selected.

Primary UI actions use shared `AccentButton`/`AccentSurface` styling with foreground contrast tied to the effective fill. Neutral card/field gradients are separate. See [shared surface rules and regression coverage](ui-components.md).

## Private Clipboard library

Studio's Clipboard library uses Core's typed action definitions and validator for Text, Photo and Link. Text/link copying and viewing are explicit foreground operations; opening validated web links uses the executor and `IActionPlatform`. Photos use private PNG assets and GUID references, with native image copying and the iOS share sheet. The private SQLite document accepts versions 1/2/3/4 and writes version 4 on mutation for optional per-item header RGB colours (null follows the app accent). A transactional upgrade of versions 1/2 removes the known retired record type at the owner's request; remaining records retain their data and metadata. Other unknown types and future schemas fail without rewriting data. Settings and item editors share `ColourPickerView`; its hosts own persistence, keeping draft item colours separate from global preferences. See [Clipboard details](clipboard.md). The counter action still delegates to `ICounterRepository`, preserving the widget refresh path. Only the statically declared counter App Intent is exposed to Apple's Shortcuts app.

`SqliteActionRepository` stores a versioned dashboard document in a private SQLite database, separate from the provider group and counter database. A small C# interop layer calls the existing iOS system SQLite library; Windows/macOS tests resolve the corresponding OS library. No new NuGet/native dependency is added. Prepared statements bind all user content. `BEGIN IMMEDIATE` serializes mutations, including across independent connections; a semaphore prevents overlapping operations per repository instance. Connections/statements close after each operation. Unknown database/document versions and invalid data are preserved. See [Milestone 1](milestone-1.md) for bounds, cancellation and acceptance checks.

The Clipboard UI has Text/Photo/Link segments, per-item action icons, pinning, a separate Arrange page, persistent concealment, expandable content and optional header colours. Its CollectionView virtualizes item controls. The add/edit page saves a draft only after validation; private photo copies are removed only after replacement/deletion commits. App activation and page lifetime guards prevent stale results from changing a departed page. Light/dark appearance and accent come from Settings; there is no public Shortcuts dashboard tab.

## Personal widget designs

`WidgetDesignRepository` owns a private JSON library and immutable photo/render files, separate from Clipboard SQLite. A shared per-directory semaphore, revision checks and atomic catalog replacement serialize edits and reject stale saves. Schema 1 libraries upgrade to schema 2 on mutation to add size-specific slot assignments. Photo/Clock/Date/Text/Battery layers share Core geometry and validation; retired iPad size identifiers remain readable for recovery without being offered as new sizes.

`WidgetStudioService` renders and saves a design privately, then asks `WidgetDesignPublisher` to install immutable artwork and live scene files into the App Group before replacing shared `catalog.json` and `slots.json`. Failed publication leaves the private save available for retry. Catalog and slots are individually atomic, not a cross-file transaction. Recent shared renders are retained for cached timelines. Original photo sources are not copied to the shared render folder.

Ten static Studio widget kinds each support independent Small/Medium/Large/iPhone XL assignments. There is no App Intent design selector. Swift reads the slot/catalog and validates live scenes; photo/custom-text groups are exported PNGs, Clock/Date use native live Text, and Battery samples at timeline generation with an advisory refresh policy. C# and Swift contracts have shared fixtures and separate behavioral tests. The isolated iOS 27 Objective-C background adapter preserves the owner's working transparent display with ABI checks and a normal-rendering fallback; it is a private-API compatibility risk.

The editor uses bounded Core Image graphs, four raster/two source caches, and explicit wrapper ownership. Saved-library/slot previews hold at most six visible image sources. These are safeguards, not measurements of frame rate or device memory. A 30-layer editor, rapid gestures and repeated publication still require physical profiling.

Scene grouping version 2 omits hidden and zero-opacity layers before splitting static groups. Older scene boundaries remain validated/readable under grouping version 1; publication copies their original bytes unchanged. Editing/saving produces a new immutable export with corrected grouping while retaining the design and its assignments. See [hidden-layer blending](widget-hidden-layer-blending.md) for compatibility and verification.

## Composition and lifecycle

`MauiProgram` registers long-lived services and the three root pages through DI. Root navigation is a TabbedPage containing Home, Studio and Settings NavigationPages. Child editors/pages are constructed explicitly with their required stores and callbacks; native handlers own platform gesture/notification/timer teardown. The UI is code-built C#, with no XAML, Shell routes or mandatory MVVM framework. Core and Infrastructure contain no UIKit dependency.

Profile/appearance use local preferences; Profile photo source/cropped copies remain private. Profile controls distinguish failed persistence from a successful save followed by failed navigation. Preferences are separate keys, not a crash-atomic profile transaction.

See the [repository-wide audit and dependency map](architecture-audit.md) and [implemented improvements/verification](architecture-optimization-report.md). Historical milestone documents describe their release state; they are not the current feature inventory.

## Counter storage

The App Group identifier comes from `config/app-group.json`; its C#/Swift constants, three requested entitlement files and three Info.plist keys are generated by `scripts/app_group_config.py`. The current provider test group is `group.23b5a5355f697168.1`, already present in the owner's supplied signed IPA/profile. Provider-group storage is `Library/Application Support/ControlStudio/counter.sqlite3` inside the group container, obtained through Apple's App Group API. The app-specific subfolder avoids filename collisions but is not a privacy boundary from other apps entitled to the same group. Selecting the original dedicated `group.com.faikalizham.controlstudio` preserves its legacy `Library/Application Support/counter.sqlite3` path.

Each operation opens its own connection, applies a 3-second busy timeout and executes a `BEGIN IMMEDIATE` transaction. Initial schema creation, validation, increment, timestamp and commit occur together. Concurrent app/Shortcuts operations cannot overwrite each other's increments. The small database uses the default SQLite rollback journal; no connections remain open between operations.

`PRAGMA user_version = 1` versions the database. A singleton row enforces nonnegative, signed 32-bit counter values. A newer schema, corrupt/missing row, invalid timestamp, overflow, inaccessible directory, or database error causes an error without resetting data. Future migrations must preserve this behavior. The initial value is 0 with no timestamp.

Only the MAUI host permits fallback to its private Application Support directory, and only if the App Group container cannot be resolved. Shared database errors never trigger fallback. The UI explicitly identifies private storage. Widget and App Intent always require the shared container. Private values are not automatically copied or merged when signing changes, avoiding ambiguous merges with an existing shared database.

This counter is not sensitive data. SQLite is protected by the system container; there is no custom encryption or background monitoring. Data may be temporarily inaccessible while the device is locked. Secure storage and retention/export controls for sensitive future modules are separate work.

## JSON/C ABI

See [example](../contracts/counter-v1.example.json). All five fields are mandatory:

| Field | Meaning |
|---|---|
| `schemaVersion` | Integer, exactly 1 |
| `value` | Integer, 0 through 2147483647 |
| `updatedAtUnixMilliseconds` | Explicit null for initial state; otherwise UTC Unix milliseconds, 0 through 253402300799999 |
| `storage` | `shared` or `local` |
| `refreshRequested` | Whether the current increment requested WidgetKit reload; must be false for local storage |

Both languages validate these invariants. Unknown schemas fail. Extra fields may be added compatibly; new meanings require a schema bump. Error responses are `{ "error": "storageUnavailable" }` with stable codes; no raw SQLite errors/paths cross the ABI. The database itself is SQLite, not a JSON file.

`cs_counter_read` and `cs_counter_increment` return a UTF-8 JSON string allocated with `strdup`. C# copies it and calls `cs_counter_free` in `finally`. Null allocation is an error. The ABI does not pass Swift types, throw Swift exceptions through C, or require Objective-C runtime lookup. Cancellation is honored before the operation; a committed increment is not reported as cancelled.

## Widget and App Intent integration

The static bridge contains no App Intents declarations. It calls `WidgetCenter.reloadTimelines(ofKind:)` after shared commits. The small/medium widget reads SQLite and displays value plus **data update** time. Its 15-minute timeline request is advisory; iOS controls actual scheduling and may coalesce refresh requests. The widget has a truthful error state when shared storage fails. It does not embed MAUI views.

The dedicated `ControlStudioIntents.appex` is an Apple ExtensionKit product, with `@main AppIntentsExtension`, `isDiscoverable = true`, an App Shortcuts provider, and an extracted `Metadata.appintents` bundle generated by Xcode. Its action increments the same store and returns an integer plus a useful dialog. It does not need to launch the MAUI UI. This follows Apple's [AppIntentsExtension](https://developer.apple.com/documentation/appintents/appintentsextension) arrangement and [WWDC23 discussion of extension-hosted App Shortcuts](https://developer.apple.com/videos/play/wwdc2023/10103/?time=265).

WidgetKit's `.appex` is embedded by the .NET SDK's documented [AdditionalAppExtensions](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-items#additionalappextensions) into `PlugIns`. The SDK hardcodes that location, so a small explicit MSBuild target copies the ExtensionKit product to `Extensions` after the SDK copies normal extensions. The native product type is `com.apple.product-type.extensionkit-extension`, not a renamed SiriKit extension. Signing occurs only after the final bundle is assembled. The validator rejects missing/misplaced targets and absent action metadata.

Metadata extraction is build evidence, not proof of Shortcuts indexing, registration, background execution or widget operation. Those remain device tests. The exact metadata JSON layout is Apple-owned; its checker asserts the expected action identifier/title without pretending to validate the entire private format.
