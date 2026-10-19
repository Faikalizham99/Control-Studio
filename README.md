# Control Studio

An offline-first .NET MAUI iPhone app with a private shortcut dashboard, native SwiftUI counter widget and native Apple Shortcuts action. **Milestone 1 is approved and implemented:** create, edit, run, favorite, categorize, search, reorder and delete web-link, copy-text and counter actions. The widget editor remains future work.

Version **0.2.0 (3)** adds a Shortcuts tab with private SQLite persistence, typed action parameters, bounded validation and source-generated JSON. Web links support HTTP(S); additional URL schemes and dynamic Apple Shortcuts actions are deferred. See the [Milestone 1 build record and phone checklist](docs/milestone-1.md). The existing App Group and counter schema are retained.

The app increments a persistent SQLite counter, asks WidgetKit to refresh, and reads changes made by **Increment Control Studio Counter** in Shortcuts. SQLite transactions in an App Group serialize updates across processes. Without App Group access, the host reports that it is using a separate local counter; the extensions fail visibly instead of pretending sharing works.

**Current 0.1.1 test build:** the owner's signed IPA authorized provider groups instead of the original provisional group. The selected group is now `group.23b5a5355f697168.1`, configured in `config/app-group.json` and generated consistently for all targets. **The owner confirmed app-to-widget sharing and Shortcuts increment execution are working on 19 October 2026.** See the [widget fix and remaining tests](docs/widget-app-group-fix.md). Detailed widget-family checks, app/widget updates after a Shortcut and full final-signature validation remain unverified.

## Status

| Stage | Evidence |
|---|---|
| C# source and automated tests | Implemented; see [validation record](docs/milestone-0.md) |
| MAUI managed iOS code | Compiled on Windows with pinned SDK/workload; native linking still requires macOS |
| Native Swift, WidgetKit, App Intents | Compiled and linked successfully on macOS CI; action metadata verified |
| CI workflows and IPA validation | PASS: [0.1.1 run](https://github.com/Faikalizham99/Control-Studio/actions/runs/39136235978), 48 tests; unsigned IPA structure, configured App Group and arm64 iOS binary checks passed |
| Certificate/profile capabilities | Supplied signed IPA/profile inspected; selected group is listed. Final signatures and per-extension provisioning remain NOT VERIFIED |
| App-to-widget sharing | CONFIRMED by owner, 19 October 2026, after the 0.1.1 correction; detailed family/timestamp tests remain unverified |
| Shortcuts action availability and increment execution | CONFIRMED by owner, 19 October 2026 |
| App/widget updates after a Shortcut and remaining detailed device checks | **NOT VERIFIED** |

## Start here

1. Follow [Windows development](docs/windows-development.md) to build and test the shared C# code.
2. Read [signing](docs/ios-signing.md): a signing certificate alone does not grant App Group access. The host, widget and App Intents extension need **three explicit provisioning profiles**, with the same team and App Group.
3. Push this repository: **Test Control Studio** runs managed tests and an unsigned iOS build. Once the workflow is on the default branch, you can also open **Actions → Build Control Studio IPA → Run workflow**. Choose `unsigned` first to compile without secrets; this artifact requires a compatible signer before installation. Choose `signed` after configuring the documented secrets.
4. Download the IPA and validation report from the run's **Artifacts** section. See [FlareStore installation](docs/flarestore-installation.md), then complete the [device checklist](docs/device-test-checklist.md).

Current output: `ControlStudio-m1-unsigned.ipa` or `ControlStudio-m1-signed.ipa`; artifact container `ControlStudio-m1-<mode>-<run_number>`. Older Milestone 0 runs retain `m0` filenames. An unsigned IPA **cannot be installed directly**. A signed validation result also does not establish successful installation.

Current verified build: [ControlStudio-m0-unsigned-4](https://github.com/Faikalizham99/Control-Studio/actions/runs/39136235978/artifacts/12139887970), version **0.1.1 (2)** from commit `cc950e76b4ffcc11ef8a74e470ca34f601896043` on `codex/widget-app-group`. The artifact is retained for 14 days; save a copy. The default branch has not been changed. [Compare the implementation](https://github.com/Faikalizham99/Control-Studio/compare/main...codex/widget-app-group) before merging it to enable default-branch manual dispatch.

## Toolchain

Pinned baseline, checked against upstream documentation on 18 October 2026:

| Component | Version |
|---|---|
| .NET SDK | 10.0.302 |
| Workload set | 10.0.302.1 |
| Microsoft.Maui.Controls | 10.0.110 |
| Xcode / iPhoneOS SDK | 26.6 (17F113) / 26.5 |
| Runner | `macos-26` (arm64 hosted image) |
| App / extension deployment minimum | iOS 17.0 |
| Device binary | arm64 |

This uses established APIs on the newer iOS 27 device; it does not use an iOS 27 SDK or claim device compatibility has been verified. The [Microsoft release](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode26.5-10315) explicitly pairs SDK 10.0.302/workload 10.0.302.1 with Xcode 26.6. [MAUI 10.0.110](https://github.com/dotnet/maui/releases/tag/10.0.110) is a stable service release. The [runner inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-26-arm64-Readme.md) lists the selected Xcode. CI checks these versions and fails if the runner drops them; it does not switch silently to a new Xcode.

## Repository

- `src/ControlStudio.App`: iOS MAUI host, Home and Settings, theme preference, C interop.
- `src/ControlStudio.Core`: validated counter contract and repository interface.
- `src/ControlStudio.Infrastructure`: native response decoding and async adapter.
- `native/Shared`: SQLite implementation shared by all three processes.
- `native/ControlStudioBridge`: static Swift library with a small C ABI.
- `native/ControlStudioWidgets`: native small/medium WidgetKit extension.
- `native/ControlStudioIntents`: dedicated ExtensionKit App Intents executable and App Shortcuts provider.
- `tests`: managed contract tests and Python packaging/profile validation tests.
- `native/Tests`: Swift SQLite persistence, validation and concurrency tests.
- `scripts`: deterministic Xcode project generation, native build, signing, IPA packaging and validation.

Read [architecture](docs/architecture.md), [Milestone 0 and validation](docs/milestone-0.md), [limitations](docs/known-limitations.md), and [future milestones](docs/roadmap.md). No account, network backend, tracking SDK or iOS permission prompt is required by this proof of concept.
