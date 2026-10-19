# Milestone 1 — Shortcut Control Center

The owner accepted Milestone 0 and approved this work on **19 October 2026**, after confirming widget sharing and native Shortcuts increment execution. Version **0.2.0 (3)** adds the first complete dashboard flow. Milestone 2 / Widget Studio requires separate approval.

## Included

- Shortcuts tab with a meaningful empty state and explicit Add, Run and Manage controls.
- Create/edit/delete actions with stable UUIDs, names, six categories, six icons, six tile colors and favorites.
- Name search, category/favorite filters, and persisted ordering via Manage → Move earlier/later. Ordering applies to the full dashboard, including while filtered.
- Three typed actions: open an HTTP(S) URL through iOS; copy predefined text; increment the existing counter and request its widget refresh.
- Private SQLite persistence, bounded input, versioned source-generated JSON, cancellation before effects and user-visible failures.
- Existing Home counter, Settings appearance, small/medium widgets, App Intent, App Group and shared counter schema retained.

The dashboard's **Shortcuts** tab contains foreground app actions. It is distinct from Apple's **Shortcuts** app, which continues to expose the existing native counter action. Saved dashboard configurations are not yet App Entities or dynamically discoverable Apple Shortcuts actions. Widget rendering remains the counter widget.

## Storage and execution

`IActionDefinition`, `IActionValidator`, `IActionExecutor` and `IActionRepository` live in Core. Typed parameters use an explicit JSON discriminator; unsupported types fail closed. Validation runs when saving and immediately before execution. Names are bounded to 60 characters, web links to 2,048, clipboard text to 10,000 and dashboards to 200 actions. URL schemes other than HTTP(S), embedded credentials, raw whitespace/control characters and backslashes are rejected. Copy text supports line breaks and Unicode. SQL values use bound parameters.

The dashboard uses `<private app data>/ControlStudio/actions.sqlite3`, separate from the provider App Group. It stores a singleton versioned dashboard document inside SQLite, not in a standalone JSON file. Each read/write opens a system SQLite connection and uses a three-second busy timeout and `BEGIN IMMEDIATE` transaction. Edits and reordering commit atomically. Database `user_version` and JSON `schemaVersion` are both 1. Unknown schemas or corrupt data fail without a reset. Tests exercise the real OS SQLite library on Windows and macOS. iOS links the already-used system SQLite library without a new package.

Action data is plain text within the app sandbox; it is not a password vault. No action names, URLs or clipboard contents are logged. Running Copy writes to the system clipboard, which follows iOS clipboard policies. Website navigation may contact the chosen site; storing/editing actions and counter/copy operations work locally. The app does not read the clipboard. No new permission or signing capability is requested.

Cancellation is checked before platform calls and before database commit. An already-dispatched iOS operation or committed counter increment cannot be undone. UI controls prevent duplicate executions while an operation is running; failures are never automatically retried.

Platform APIs follow Microsoft's [Launcher](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/appmodel/launcher?view=net-maui-10.0) and [Clipboard](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/clipboard?view=net-maui-10.0) documentation. The list uses MAUI [CollectionView](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/?view=net-maui-10.0).

## Validation record

- Windows: 27 Core and 17 Infrastructure tests passed. Coverage includes typed JSON, URL rejection, execution routing, cancellation, privacy of errors, real SQLite reopening, concurrent saves, CRUD/order/favorites and preservation of unknown database/document schemas, unsupported saved action types and corrupt databases. Managed MAUI iOS code compiled on Windows; native linking requires macOS.
- Python: all 21 packaging/configuration tests and deterministic source checks passed on Windows.
- macOS Swift tests, full MAUI/native compile and IPA validation: pending CI.
- New phone installation, dashboard UI and regression tests: **NOT VERIFIED**.
- Prior 0.1.1 app-to-widget sharing and Apple Shortcuts increment: **owner confirmed**, 19 October 2026; this does not verify the new binary.

## iPhone acceptance checklist

Keep the same signing identity and app bundle identifier when updating. An uninstall or a signer-assigned identity change can remove private dashboard data. Use the same FlareStore settings that made version 0.1.1 work and preserve/sign both embedded extensions.

| Check | Result |
|---|---|
| Update to 0.2.0 (3); app opens; previous counter remains available | NOT VERIFIED |
| Shortcuts initially shows an empty state; Add opens the editor | NOT VERIFIED |
| Create an HTTPS action, select name/category/icon/color/favorite; save and Run opens intended site | NOT VERIFIED |
| Blank name, javascript/file/shortcuts URL, credentials in URL and oversized input fail without execution | NOT VERIFIED |
| Create Copy text with Unicode and line breaks; Run then paste into Notes gives exact text | NOT VERIFIED |
| Create Increment counter; one Run increments once; Home shows the new value; widget eventually follows | NOT VERIFIED |
| Manage → Edit preserves identity and position; changed parameters work | NOT VERIFIED |
| Search, category and Favorites only show matching actions | NOT VERIFIED |
| Move earlier/later changes order; force-close/reopen preserves records, appearance, favorites and ordering | NOT VERIFIED |
| Delete asks for confirmation; Cancel preserves the action; Delete removes it after relaunch | NOT VERIFIED |
| Cancel editor leaves records unchanged; rapidly tapping Run does not cause simultaneous operations | NOT VERIFIED |
| Airplane Mode permits local dashboard changes, Copy and counter; failed web opening gives a useful outcome | NOT VERIFIED |
| Light/Dark and large accessibility text remain readable; VoiceOver distinguishes Run from Manage | NOT VERIFIED |
| Small/medium widget and Increment Control Studio Counter in Apple Shortcuts still work after updating | NOT VERIFIED |

## Deferred

Drag-and-drop, images, tile sizes, extra URL schemes, external Shortcuts launching, App Entities, utility actions, accent settings, widget editing and interactive widget actions are later work. No unsupported background execution is advertised.
