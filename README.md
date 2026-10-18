# Control Studio

An offline-first .NET MAUI iPhone app with a native SwiftUI counter widget and a native Shortcuts action. **Only Milestone 0 is implemented.** The dashboard and widget editor are future work.

The app increments a persistent SQLite counter, asks WidgetKit to refresh, and reads changes made by **Increment Control Studio Counter** in Shortcuts. SQLite transactions in an App Group serialize updates across processes. Without App Group access, the host reports that it is using a separate local counter; the extensions fail visibly instead of pretending sharing works.

## Status

| Stage | Evidence |
|---|---|
| C# source and automated tests | Implemented; see [validation record](docs/milestone-0.md) |
| MAUI managed iOS code | Compiled on Windows with pinned SDK/workload; native linking still requires macOS |
| Native Swift, WidgetKit, App Intents | Compiled and linked successfully on macOS CI; action metadata verified |
| CI workflows and IPA validation | PASS: [final run](https://github.com/Faikalizham99/Control-Studio/actions/runs/39133946833), 41 tests; unsigned IPA structure and arm64 iOS binary checks passed |
| Certificate/profile capabilities | NOT VERIFIED; credentials have not been supplied |
| Installation, widget sharing, Shortcuts discovery/execution | **NOT VERIFIED** until confirmed on the iPhone |

## Start here

1. Follow [Windows development](docs/windows-development.md) to build and test the shared C# code.
2. Read [signing](docs/ios-signing.md): a signing certificate alone does not grant App Group access. The host, widget and App Intents extension need **three explicit provisioning profiles**, with the same team and App Group.
3. Push this repository: **Test Control Studio** runs managed tests and an unsigned iOS build. Once the workflow is on the default branch, you can also open **Actions → Build Control Studio IPA → Run workflow**. Choose `unsigned` first to compile without secrets; this artifact requires a compatible signer before installation. Choose `signed` after configuring the documented secrets.
4. Download the IPA and validation report from the run's **Artifacts** section. See [FlareStore installation](docs/flarestore-installation.md), then complete the [device checklist](docs/device-test-checklist.md).

Unsigned output: `ControlStudio-m0-unsigned.ipa`. Signed output: `ControlStudio-m0-signed.ipa`. Artifact container: `ControlStudio-m0-<mode>-<run_number>`. An unsigned IPA **cannot be installed directly**. A signed validation result also does not establish successful installation.

Current verified build: [ControlStudio-m0-unsigned-3](https://github.com/Faikalizham99/Control-Studio/actions/runs/39133946833/artifacts/12138538904), from commit `ba282216abbf7cab987170f788034e216152db97` on `codex/milestone-0`. The artifact is retained for 14 days; save a copy. The default branch has not been changed. [Compare the implementation](https://github.com/Faikalizham99/Control-Studio/compare/main...codex/milestone-0) before merging it to enable default-branch manual dispatch.

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
