# Control Studio

An **unreleased off-canvas photo workspace** lets photos extend beyond widget edges, preserves proportions during pinch/corner resizing, shows the selected photo faintly outside the widget while editing, and adds full-photo/crop choices plus Centre on widget recovery. Existing saved crops remain intact until explicitly edited. All 386 local automated checks and managed iOS C# compilation pass; new native Swift behavior and iPhone UI/export verification are pending. This branch update keeps version 0.2.26 unchanged and does not include a new release tag. See [behavior, compatibility and verification](docs/widget-editor.md#off-canvas-photo-workspace-unreleased-24-october-2026).

Version **0.2.26 (28)** restores the widget editor after cancelling photo selection, hardens profile-editor departure and shared colour-picker busy recovery, and audits Clipboard picker flows. It also includes the Widget slots redesign and diagnostics removal below. All 192 app tests (including 14 new regressions), 32 packaging checks and managed iOS C# compilation pass; native iPhone verification is pending. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. See [findings, affected components and verification](docs/picker-cancellation.md).

A **Widget slots redesign**, included in 0.2.26, adds proportion-correct slot cards and a visual Assign design page locked to its slot's size. Small/Medium designs use two columns at normal phone widths; Large/iPhone XL use one. Search, explicit assignment confirmation, current accent and Light/Dark styling are included. At implementation, all 178 app tests, 57 persistence tests, 32 packaging checks and managed iOS C# compilation passed; device UI verification is pending. See [behavior and review](docs/widget-slots-ui.md).

The owner confirmed that **0.2.25 transparency works on their iPhone**. A follow-up included in 0.2.26 removes the temporary Widget diagnostics UI, recorder and diagnostic-only tests, and cleans up old report files once per App Group. The working signed-integer adapter and its native regression tests remain. At implementation, all 155 app tests, 32 packaging checks and managed iOS C# compilation passed; the removal build still needs native/device verification. See [removal and cleanup](docs/widget-native-transparency.md#diagnostics-removal-and-cleanup-unreleased).

Version **0.2.25 (27)** corrects the `expected=Q actual=q` rejection identified by the owner's 0.2.24 diagnostic report. The adapter now uses signed `NSInteger` for the background-style setter and its ABI check, with native regression coverage for signed acceptance and unsigned rejection. The owner authorized committing, pushing main and pushing the new tag, and will monitor the IPA build. All 32 packaging checks and source checks pass; native compilation and actual iPhone transparency still need verification. See [confirmed failure and correction](docs/widget-native-transparency.md#signed-background-style-signature-correction-0225).

Version **0.2.24 (26)** adds **Settings → Widget diagnostics** to investigate the still-opaque background reported after 0.2.23. It provides a local, copyable report of adapter installation, callbacks and fallback reasons, labels stale-build evidence, and does not claim successful iOS delivery from a descriptor rewrite. The owner authorized committing, pushing main and pushing the new tag, and will monitor the IPA build. All 162 app tests, 32 packaging checks and managed iOS C# compilation pass; native compilation and iPhone capture remain pending. See [diagnostic behavior and verification](docs/widget-native-transparency.md#diagnostic-report-follow-up-0224).

Version **0.2.23 (25)** requests native clear backgrounds for the ten Studio widget slots on iOS 27 through a guarded private descriptor adapter. It also includes the Rotate/Flip menu follow-up. The owner authorized committing, pushing main and pushing the new tag, and will monitor the IPA build. All 32 local packaging checks and source checks pass; native tests, Xcode compilation and iPhone transparency remain pending. See [implementation and verification limits](docs/widget-native-transparency.md).

Version **0.2.22 (24)** redesigns the photo-layer widget editor with size-aware live previews, drag/pinch/corner resizing, rotation, flips, opacity, layer visibility/reordering and optional solid or transparent backgrounds. It also includes the shared button-caption alignment fix. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. All 310 local automated tests and managed iOS C# compilation pass; native/device UI, gestures and actual wallpaper-through transparency still require verification. See [editor behavior and verification limits](docs/widget-editor.md).

The button alignment fix shares vertical centring of text and icons across widget, Clipboard editor, profile and common page actions, and corrects the Counter preview header. See [affected controls and verification](docs/ui-components.md). This is included in the authorized 0.2.22 release.

Version **0.2.21 (23)** includes the My Widgets clarity fix, Counter theme/accent sharing, Studio icon accents and iPhone widget-size cleanup. My Widgets loads the saved full render in large preview cards instead of enlarging the 256-pixel thumbnail. Images load around the visible scroll area with at most six active sources, and release on departure/rebuild. Existing saved designs need no recreation. See [root cause and validation](docs/widget-preview-clarity.md). The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. Native/device verification remains pending.

The widget-size cleanup offers **Small, Medium, Large and Extra Large for iPhone**. The former iPad-only XL layout is removed from creation, filters, slots and native registration. A read-only compatibility path keeps older designs/photos recoverable for explicit resize. See [size cleanup and verification](docs/iphone-widget-sizes.md). This is included in the authorized 0.2.21 release.

The Counter appearance enhancement shares Settings' System/Light/Dark choice and saved accent with Small/Medium Counter widgets and matching in-app previews. Custom widget designs retain their own appearance. See [behavior, verification and device checks](docs/counter-widget-appearance.md). This is included in the authorized 0.2.21 release.

Version **0.2.20 (22)** separates **My Widgets** and **Counter Widgets**, retains Clipboard, adds size filters and correctly proportioned full previews, and keeps **Widget slots** directly accessible. See the [UI behavior, review and pending device checks](docs/widget-studio-ui.md). The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build.

Home also adds a confirmed **red Reset counter** action below **Increment (+1)**, backed by the shared native counter transaction. See [reset behavior and verification](docs/home-counter-reset.md). The combined changes pass 280 automated tests and managed iOS C# compilation; native/device verification remains pending.

Version **0.2.19 (21)** addresses the confirmed 0.2.18 WidgetKit startup assertion by using literal gallery names **Studio 1–10** and a fixed description, with a source regression check in existing CI validation. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. Corrected Xcode compilation and iPhone verification remain pending. Assign saved designs to **Widget slots**, then add the matching numbered widget and size on your Home Screen; Small 1 and Medium 1 have independent assignments. See [Widget Studio and slots](docs/widget-studio.md) for crash evidence, behavior and device checks.

Release **0.2.11 (13)** replaces Clipboard's solid concealment cover with a transparent Gaussian blur of the actual text, photo or link, removing the old material/tint transition. It also includes readable disabled actions, shared header buttons and explanations when Arrange is unavailable. Clipboard pinning, saved ordering, photo paste/import, link previews and coloured headers remain available. See [Clipboard behavior](docs/clipboard.md), [shared action styles](docs/ui-components.md) and [arrangement review evidence](docs/clipboard-arrangement.md). Native build, animation matching and white-flash verification on an iPhone remain pending; the owner monitors the release build.

Release **0.2.4 (7)** replaces the Shortcuts tab with **Studio**, a two-card hub for **Widgets** and **Clipboard**. Widgets previews the existing Small/Medium counter; Clipboard keeps saved-text creation, editing, copying and deletion using the existing private data. The owner authorized the release and will monitor the tag build. See [Studio behavior and pending device checks](docs/studio.md); native CI and phone verification remain pending.

Release **0.2.3 (6)** includes Photo Library/Files selection with crop preview, consistent app accents, clearer Light-mode borders, a compact hex editor and the reference-inspired Edit profile layout on plain Light/Dark backgrounds. The owner authorized the release tag and will monitor its build; final native CI and phone verification remain pending. See the [Settings and profile validation record](docs/settings-appearance.md).

An offline-first .NET MAUI iPhone app with a private shortcut dashboard, native SwiftUI counter widget and native Apple Shortcuts action. **Milestone 1 is approved and implemented:** create, edit, run, favorite, categorize, search, reorder and delete web-link, copy-text and counter actions. The first widget editor is implemented in the working tree; see the Widget Studio notes above.

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
3. Regular branch pushes and pull requests run **Test Control Studio** for managed/Python/native storage tests and Swift extension compilation, without packaging or uploading an IPA. **Build Control Studio IPA** runs only when a new version tag matching `v*.*.*` is pushed; there is no manual dispatch. It defaults to unsigned output for FlareStore. To enable CI signing, set repository variable `IOS_SIGNING_MODE=signed` and configure the documented secrets/profiles first. Every commit and push requires the owner's explicit permission; see [repository rules](AGENTS.md).
4. Download the IPA and validation report from the run's **Artifacts** section. See [FlareStore installation](docs/flarestore-installation.md), then complete the [device checklist](docs/device-test-checklist.md).

Current output: `ControlStudio-m1-unsigned.ipa` or `ControlStudio-m1-signed.ipa`; artifact container `ControlStudio-m1-<mode>-<run_number>`. Older Milestone 0 runs retain `m0` filenames. An unsigned IPA **cannot be installed directly**. A signed validation result also does not establish successful installation.

Historical Milestone 1 CI evidence: [ControlStudio-m1-unsigned-6](https://github.com/Faikalizham99/Control-Studio/actions/runs/39258396606/artifacts/12188459102), version **0.2.0 (3)** from commit `43786fa6fb98ab918a66f05a471188cfad00640c` on the former `codex/milestone-1` branch. That artifact had a 14-day retention period. Current development uses `main`; IPA generation runs only for newly pushed version tags. New device checks remain unverified; the prior 0.1.1 device evidence is preserved in [Milestone 0](docs/milestone-0.md).

## Toolchain

Pinned baseline, checked against upstream documentation on 22 October 2026:

| Component | Version |
|---|---|
| .NET SDK | 10.0.401 |
| Workload set | 10.0.401.1 |
| Microsoft.Maui.Controls | 10.0.110 |
| Xcode / iPhoneOS SDK | 27.0 (27A266a) / 27.0 |
| Runner | `xcode-27` (arm64 hosted image) |
| App / extension deployment minimum | iOS 17.0 |
| Device binary | arm64 |

The iOS 27 SDK is required for the Extra Large Portrait family. The [Microsoft release](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode27.0-10722) pairs SDK 10.0.401/workload 10.0.401.1 with Xcode 27.0 and recommends MAUI 10.0.110+. The [GitHub runner inventory](https://github.com/actions/runner-images/blob/main/images/macos/xcode-27-arm64-Readme.md) lists the selected Xcode. CI checks the exact Xcode/SDK and fails if unavailable. The host adopts the required single-window UIKit scene lifecycle using MauiUISceneDelegate. Minimum deployment remains iOS 17; portrait XL registration is guarded for iOS 27+. Xcode compilation and device compatibility remain unverified for this working tree.

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
