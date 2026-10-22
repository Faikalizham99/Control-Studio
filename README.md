# Control Studio

Release **0.2.10 (12)** adds Clipboard pinning and a native Arrange editor with drag handles, separate pinned/unpinned groups and Done/Cancel. Pins and saved order survive relaunch. Each card also has an animated eye toggle to conceal its text, photo or link. Existing photo paste/import, link previews, coloured headers and accent actions remain available. See [Clipboard behavior](docs/clipboard.md) and [arrangement review evidence](docs/clipboard-arrangement.md). Native build and iPhone verification remain pending; the owner monitors the release build.

Release **0.2.4 (7)** replaces the Shortcuts tab with **Studio**, a two-card hub for **Widgets** and **Clipboard**. Widgets previews the existing Small/Medium counter; Clipboard keeps saved-text creation, editing, copying and deletion using the existing private data. The owner authorized the release and will monitor the tag build. See [Studio behavior and pending device checks](docs/studio.md); native CI and phone verification remain pending.

Release **0.2.3 (6)** includes Photo Library/Files selection with crop preview, consistent app accents, clearer Light-mode borders, a compact hex editor and the reference-inspired Edit profile layout on plain Light/Dark backgrounds. The owner authorized the release tag and will monitor its build; final native CI and phone verification remain pending. See the [Settings and profile validation record](docs/settings-appearance.md).

An offline-first .NET MAUI iPhone app with a private shortcut dashboard, native SwiftUI counter widget and native Apple Shortcuts action. **Milestone 1 is approved and implemented:** create, edit, run, favorite, categorize, search, reorder and delete web-link, copy-text and counter actions. The widget editor remains future work.

Version **0.2.0 (3)** adds a Shortcuts tab with private SQLite persistence, typed action parameters, bounded validation and source-generated JSON. Web links support HTTP(S); additional URL schemes and dynamic Apple Shortcuts actions are deferred. See the [Milestone 1 build record and phone checklist](docs/milestone-1.md). The existing App Group and counter schema are retained.

Version **0.2.1 (4)** added the reference Settings cards with a plain light/black background, editable local profile, theme tiles, preset/custom accents and a live preview. Its [tag build](https://github.com/Faikalizham99/Control-Studio/actions/runs/39442674709) passed 88 tests and IPA validation, but the owner reported an immediate launch crash on 21 October 2026. A reproduced RGB-conversion exception is corrected for **0.2.2 (5)**. The owner authorized the release and will monitor its tag build; native CI and phone verification remain pending. See [Settings appearance, crash diagnosis and pending device checks](docs/settings-appearance.md).

The app increments a persistent SQLite counter, asks WidgetKit to refresh, and reads changes made by **Increment Control Studio Counter** in Shortcuts. SQLite transactions in an App Group serialize updates across processes. Without App Group access, the host reports that it is using a separate local counter; the extensions fail visibly instead of pretending sharing works.

**Device-confirmed 0.1.1 baseline:** the owner's signed IPA authorized provider groups instead of the original provisional group. The selected group remains `group.23b5a5355f697168.1`, configured in `config/app-group.json` and generated consistently for all targets. **The owner confirmed app-to-widget sharing and Shortcuts increment execution are working on 19 October 2026.** See the [widget fix and remaining tests](docs/widget-app-group-fix.md). Detailed widget-family checks, app/widget updates after a Shortcut and full final-signature validation remain unverified. These confirmations apply to 0.1.1; the new dashboard binary requires device regression checks.

## Status

| Stage | Evidence |
|---|---|
| C# source and automated tests | Implemented; see [validation record](docs/milestone-0.md) |
| MAUI managed iOS code | Compiled on Windows with pinned SDK/workload; native linking still requires macOS |
| Native Swift, WidgetKit, App Intents | Compiled and linked successfully on macOS CI; action metadata verified |
| CI workflows and IPA validation | PASS: [0.2.0 run](https://github.com/Faikalizham99/Control-Studio/actions/runs/39258396606), 75 tests; full iOS compile, unsigned IPA structure, configured App Group and arm64 iOS binary checks passed |
| Certificate/profile capabilities | Supplied signed IPA/profile inspected; selected group is listed. Final signatures and per-extension provisioning remain NOT VERIFIED |
| App-to-widget sharing | CONFIRMED by owner, 19 October 2026, after the 0.1.1 correction; detailed family/timestamp tests remain unverified |
| Shortcuts action availability and increment execution | CONFIRMED by owner, 19 October 2026 |
| App/widget updates after a Shortcut and remaining detailed device checks | **NOT VERIFIED** |

## Start here

1. Follow [Windows development](docs/windows-development.md) to build and test the shared C# code.
2. Read [signing](docs/ios-signing.md): a signing certificate alone does not grant App Group access. The host, widget and App Intents extension need **three explicit provisioning profiles**, with the same team and App Group.
3. Regular branch pushes and pull requests run **Test Control Studio** for managed/Python tests only. **Build Control Studio IPA** runs only when a new version tag matching `v*.*.*` is pushed; there is no manual dispatch. It defaults to unsigned output for FlareStore. To enable CI signing, set repository variable `IOS_SIGNING_MODE=signed` and configure the documented secrets/profiles first. Every commit and push requires the owner's explicit permission; see [repository rules](AGENTS.md).
4. Download the IPA and validation report from the run's **Artifacts** section. See [FlareStore installation](docs/flarestore-installation.md), then complete the [device checklist](docs/device-test-checklist.md).

Current output: `ControlStudio-m1-unsigned.ipa` or `ControlStudio-m1-signed.ipa`; artifact container `ControlStudio-m1-<mode>-<run_number>`. Older Milestone 0 runs retain `m0` filenames. An unsigned IPA **cannot be installed directly**. A signed validation result also does not establish successful installation.

Current CI-verified build: [ControlStudio-m1-unsigned-6](https://github.com/Faikalizham99/Control-Studio/actions/runs/39258396606/artifacts/12188459102), version **0.2.0 (3)** from commit `43786fa6fb98ab918a66f05a471188cfad00640c` on `codex/milestone-1`. The artifact is retained for 14 days; save a copy. The default branch has not been changed. [Compare the implementation](https://github.com/Faikalizham99/Control-Studio/compare/main...codex/milestone-1) before merging it to enable default-branch manual dispatch. New device checks remain unverified; the prior 0.1.1 device evidence is preserved in [Milestone 0](docs/milestone-0.md).

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

- `src/ControlStudio.App`: iOS MAUI host, Home, Shortcuts dashboard/editor, Settings, theme preference, platform adapters and C interop.
- `src/ControlStudio.Core`: counter contract, typed actions, validation, execution and repository interfaces.
- `src/ControlStudio.Infrastructure`: native counter response decoding and private SQLite dashboard repository.
- `native/Shared`: SQLite implementation shared by all three processes.
- `native/ControlStudioBridge`: static Swift library with a small C ABI.
- `native/ControlStudioWidgets`: native small/medium WidgetKit extension.
- `native/ControlStudioIntents`: dedicated ExtensionKit App Intents executable and App Shortcuts provider.
- `tests`: managed contract tests and Python packaging/profile validation tests.
- `native/Tests`: Swift SQLite persistence, validation and concurrency tests.
- `scripts`: deterministic Xcode project generation, native build, signing, IPA packaging and validation.

Read [architecture](docs/architecture.md), [Milestone 0 and validation](docs/milestone-0.md), [limitations](docs/known-limitations.md), and [future milestones](docs/roadmap.md). No account, network backend, tracking SDK or iOS permission prompt is required by this proof of concept.
