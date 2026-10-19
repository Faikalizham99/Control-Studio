# Widget App Group fix — 0.1.1 (build 2)

The owner's iPhone screenshots show the MAUI host displaying counter 3 in **local-only storage**, while both installed widget sizes display a shared-storage error. This confirms host launch and native widget rendering, and demonstrates that sharing has failed. It does not establish counter persistence or Shortcuts execution.

Local inspection of the owner's installed, FlareStore-signed 0.1.0 IPA found:

- The compiled app requests `group.com.faikalizham.controlstudio`.
- The host profile and XML/DER signatures instead list five provider groups: `group.23b5a5355f697168.1` through `.5`.
- The requested original group is not authorized by that supplied profile/signatures.
- The app and both extensions retain their original bundle IDs, but all three signatures use the provider's single, different application identifier.
- The host has an embedded profile; neither extension has one. Both native executables and the App Intents metadata were retained.

The signed IPA, profile, device identifiers and certificate contents are kept local and are not committed or uploaded. The local sanitized report is `artifacts/diagnostics/signed-ipa-inspection.json`. Its observations are static inspection, not full macOS code-signature/trust verification.

## Current test configuration

Version 0.1.1 explicitly selects **`group.23b5a5355f697168.1`**, a group present in the supplied profile and all three signatures. This changes which authorized container the app requests; it does not create or forge a capability. The bundle IDs remain the original Control Studio IDs.

`config/app-group.json` is the single source for the selected group. `python scripts/app_group_config.py` generates the C# and Swift constants, all three requested entitlement files, and each Info.plist's `ControlStudioAppGroup` key. Source/build/IPA checks reject drift across targets. To select a different legitimately authorized group:

```powershell
python scripts/app_group_config.py --identifier group.your.actual.authorized.group
python scripts/check-source.py
```

The provider configuration stores data under `Library/Application Support/ControlStudio/counter.sqlite3` inside the selected group. This avoids colliding with another app's generic counter filename. It does **not** provide privacy isolation from other apps entitled to the same provider group. A dedicated App Group remains preferable for private future modules.

The original dedicated group retains its original storage path if explicitly selected again. The host's existing private fallback database is untouched. Its value 3 is not automatically merged with the new shared counter, which may begin at 0. Do not uninstall just to reset the test; it can remove private app data.

Settings now shows the configured group and installed version/build. Widget errors distinguish missing container access, database busy, schema/data failure and other storage errors, instead of attributing every failure to signing.

## What still needs verification

The corrected group addresses the demonstrated mismatch. FlareStore's reused application identifier and missing extension profiles remain separate signing concerns. The strict signed validator continues to reject those conditions; it has not been weakened to make the supplied IPA appear properly provisioned. A successful unsigned CI build is not proof that FlareStore will retain the corrected capabilities or that iOS will allow the extensions to execute.

Re-sign the new unsigned IPA using the same existing FlareStore setup, install as an update with original bundle IDs, then:

1. In Settings, confirm version **0.1.1 (2)** and configured group **`group.23b5a5355f697168.1`**.
2. Tap **Update Widget**. The required result is **Saved. Widget refresh requested.** rather than local-only storage.
3. Allow iOS to refresh both installed widget sizes. Confirm their shared value and Last Updated. If an old error timeline remains, remove/re-add the widgets after the app confirms shared access.
4. Find/run the Shortcuts action, then resume/refresh the app and check the counter and widgets.
5. If storage is still unavailable, inspect the final re-signed IPA again to check that the selected group remains present. If host sharing works but an extension fails, investigate its actual profile/signature/registration separately.

On **19 October 2026**, the owner reported **"ITRS WORKING NOW"** after the 0.1.1 correction. The app-to-widget sharing issue is therefore recorded as resolved on the owner's device. Both widget families, timestamps, persistence, accessibility, and Shortcuts discovery/execution have not been individually confirmed; see the [device checklist](device-test-checklist.md).

## Build evidence

[CI run 39136235978](https://github.com/Faikalizham99/Control-Studio/actions/runs/39136235978) passed on commit `cc950e76b4ffcc11ef8a74e470ca34f601896043`: 9 core tests, 8 bridge/repository tests, 21 configuration/packaging tests and 10 Swift storage tests, **48 tests total**. The host and both native executables compiled and linked. The final IPA passed structure, configured-group consistency, metadata and arm64 iOS binary checks.

Artifact: [ControlStudio-m0-unsigned-4](https://github.com/Faikalizham99/Control-Studio/actions/runs/39136235978/artifacts/12139887970), containing `ControlStudio-m0-unsigned.ipa` and `validation-unsigned.json`. IPA SHA-256: `0ce2c08a950cc2dbd04faa17361bf9e84b6055a854841e230e66f06d63b500f4`. A local delivery copy is named `ControlStudio-0.1.1-widget-fix-unsigned.ipa`; renaming does not change its contents or hash.

The expected unsigned-build warning MT7139 remains because compilation has no provisioning profile. No capability was stripped to suppress it. The owner has confirmed app-to-widget sharing; full final-signature validation and the remaining detailed device tests are unverified.
