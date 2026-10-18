# Milestone 0 implementation and validation

## Implemented scope

- MAUI Home/Settings, persistent counter, Update Widget, refresh/resume synchronization, theme preference, semantic accessibility and exception/log handling.
- Shared SQLite store with cross-process transactions, schema version/validation, timestamp and overflow checks; explicit host private fallback only for missing App Group container.
- Static Swift C ABI bridge with versioned JSON and explicit allocation/free ownership.
- Small/medium SwiftUI WidgetKit timeline and shared-data/error rendering.
- Dedicated native App Intents ExtensionKit executable with Increment Control Studio Counter and App Shortcuts provider.
- Native Xcode project generation, pinned CI toolchain, unsigned and profile-validated signed IPA paths, sanitized validation reports and draft release support.
- Managed/native/packaging tests, signing/install docs and device checklist. Future modules are planned only.

## Evidence as of 18 October 2026

| Stage | Actual result |
|---|---|
| Core C# compiled on Windows | PASS, .NET SDK 10.0.302 |
| Core contract tests | PASS, 9 tests |
| Infrastructure C# compiled on Windows | PASS |
| Native bridge adapter tests | PASS, 8 tests |
| Python structure/profile validation tests | PASS, 17 tests using synthetic fixtures |
| XML/plists/entitlement/pin/project-generation checks | PASS |
| MAUI managed iOS compilation | PASS on Windows, Compile/CoreCompile targets; 0 warnings, 0 errors; native linking still requires macOS |
| Swift SQLite tests | PASS, 7 tests on macOS CI |
| Native bridge/widget/intent compilation | PASS on macOS CI; expected action metadata extracted |
| GitHub Actions build | PASS, final run 39133946833 on isolated `codex/milestone-0` branch; full MAUI device publish and native embedding succeeded |
| IPA structure and Mach-O validation | PASS on final real IPA, macOS checks before and after packaging |
| Code signatures and provisioning capabilities | NOT VERIFIED; credentials absent |
| Installation, widget and App Intent discovery/execution | **NOT VERIFIED** |

The fixture tests exercise negative cases such as missing executables, wrong IDs/extension points, misplaced ExtensionKit targets, missing metadata, ZIP traversal, wrong teams, missing App Groups, expired profiles, unsupported distribution and missing device eligibility. They do **not** prove the app is signed or installable. Swift tests cover database persistence/reopening, concurrent connections, schema refusal, overflow rollback, invalid data, inaccessible storage and JSON null serialization.

Final CI evidence: [run 39133946833](https://github.com/Faikalizham99/Control-Studio/actions/runs/39133946833), commit `ba282216abbf7cab987170f788034e216152db97`. Both Windows and macOS jobs passed, with 41 automated tests total (9 core, 8 bridge/repository, 17 packaging/profile fixtures and 7 native SQLite tests). The default branch has not been changed.

Download [ControlStudio-m0-unsigned-3](https://github.com/Faikalizham99/Control-Studio/actions/runs/39133946833/artifacts/12138538904), containing `ControlStudio-m0-unsigned.ipa` and `validation-unsigned.json`. IPA SHA-256: `b2b38268f632c00098c25d6b4b38f36bfd23ad904c7e0aa565d4e57de761c3a6`. GitHub retains this artifact for 14 days.

The final packaged App Intents metadata contains `IncrementControlStudioCounter`, `isDiscoverable: true`, and the App Shortcuts provider/phrase. Both extension executables have the intended IDs, package types, version/build and iOS 17.0 minimum. This does not establish system registration or execution on a device.

The unsigned publish emits expected warning **MT7139**: an App Group entitlement is requested without a provisioning profile. It is retained rather than suppressed. The separate signed path requires actual profile authorization before signing. There were no C#/Swift source compiler errors; the managed Windows compile reported zero warnings. Deprecated Node action warnings from the first successful run were removed by pinning maintained actions in the final successful run.

## macOS build sequence used by CI

```bash
sudo xcode-select -s /Applications/Xcode_26.6.app/Contents/Developer
dotnet workload install maui-ios --version 10.0.302.1
dotnet test tests/ControlStudio.Core.Tests -c Release -p:RestoreLockedMode=true
dotnet test tests/ControlStudio.Infrastructure.Tests -c Release -p:RestoreLockedMode=true
swift test --package-path native
bash scripts/build-native.sh
dotnet restore src/ControlStudio.App/ControlStudio.App.csproj --locked-mode
dotnet publish src/ControlStudio.App/ControlStudio.App.csproj -c Release --no-restore \
  -p:ArchiveOnBuild=false -p:BuildIpa=false -p:EnableCodeSigning=false
bash scripts/package-ios.sh unsigned
```

Signing is a separate explicit final-bundle step using real profiles, not a claim that zipping products makes an installable IPA. See [signing](ios-signing.md).

Expected IPA contents:

```text
Payload/
  ControlStudio.app/
    Info.plist
    ControlStudio
    embedded.mobileprovision              # signed mode
    PlugIns/
      ControlStudioWidgets.appex/
        Info.plist
        ControlStudioWidgets
        embedded.mobileprovision          # signed mode
    Extensions/
      ControlStudioIntents.appex/
        Info.plist
        ControlStudioIntents
        Metadata.appintents/
          extract.actionsdata
        embedded.mobileprovision          # signed mode
```

CI validates the final ZIP after packaging as well as the assembled bundle before packaging. The report keeps structure, Mach-O, signing/profile, device, widget and intent results separate. Signed tag builds create a draft release; unsigned manual builds never produce a release.

## Acceptance gate

Complete the [device checklist](device-test-checklist.md) using the final installed signature configuration. Preserve the workflow/commit/hash in the test record. Milestone 0 has passed unsigned CI validation; signing and owner device verification remain pending. Milestone 1 has not been started.
