# Install with FlareStore

**Installation and FlareStore's handling of these two extension types are NOT VERIFIED.** No IPA has been built during the initial Windows implementation. Follow these steps once CI produces an artifact.

1. Open the successful **Build Control Studio IPA** run in GitHub. In **Artifacts**, download `ControlStudio-m0-signed-<run_number>` (preferred) or `ControlStudio-m0-unsigned-<run_number>`. Extract the artifact ZIP; the contained `.ipa` is the file to import. Read `validation-<mode>.json` and distinguish structure, native binary checks and signing results.
2. Confirm your certificate is valid and the three provisioning profiles cover this iPhone, the correct bundle IDs and the same App Group. The exact group must be authorized; extension support alone is insufficient. See [signing](ios-signing.md).
3. Transfer the `.ipa` to Files on the iPhone using your normal trusted transfer method, then open FlareStore and use its IPA import/sign/install flow. UI labels depend on the installed FlareStore version; this project has not verified a specific UI release.
4. If signing inside FlareStore, select your certificate and the appropriate profiles for the host and each extension. Preserve the original bundle IDs, `PlugIns/ControlStudioWidgets.appex`, `Extensions/ControlStudioIntents.appex`, App Intents metadata and the App Group entitlements. Do not enable duplicate-install ID rewriting or extension removal. If FlareStore cannot configure these independently, stop this route and use CI signing or ask the provider for an extension-aware installation flow. A single wildcard profile does not provide the required group.
5. For a CI-signed IPA, prefer installing without changing its signatures/capabilities if FlareStore offers that flow. If it re-signs automatically, its output needs the same checks; the original CI validation report no longer describes the modified file. Export the final IPA if supported and validate it again on macOS using `bash scripts/validate-ipa.sh <file.ipa> --mode signed`.
6. Choose installation and follow the iOS prompts. Enable Developer Mode/restart if required for development-signed apps. Trust an enterprise developer in Settings only if required for your own known signing configuration; this is not a universal step for every profile type. Never disable system protections or assume a revoked certificate can be fixed in application code.
7. Open **Control Studio** once. Tap **Update Widget** and confirm the counter changes and persists after closing/reopening. Confirm the UI says shared storage is connected. If it says local-only, installation may work but the shared capability has failed.
8. Long-press the Home Screen, enter its edit mode, open the widget picker, and search **Control Studio**. Add the small widget, then the medium widget. Check value and Last Updated after iOS permits a refresh. Do not repeatedly tap expecting an immediate timeline reload.
9. Open **Shortcuts**, create a shortcut, add an action and search **Increment Control Studio Counter** (also look under Control Studio). Run it and inspect its integer result/dialog. Return to the app or tap Refresh to see the new shared value; check both widgets after a permitted refresh.
10. Complete every item in the [device checklist](device-test-checklist.md), recording OS build, FlareStore version, signing route, workflow URL and result. Only the owner can mark device tests verified.

## Troubleshooting

| Symptom | Next check |
|---|---|
| Install rejected | Certificate revocation/expiry, all profiles and device UDID, changed bundle IDs, invalid nested signatures |
| App opens, widget missing | Widget `.appex` retained under `PlugIns`, valid point ID/profile, one host launch, extension registration |
| Widget present, shared data unavailable | Exact App Group entitlement in all actual signatures and profiles; private counter is separate |
| Shortcuts action missing | ExtensionKit `.appex` retained under `Extensions`, extracted metadata, discoverability, correct signature; launch app once and allow indexing |
| Action fails | Shared-container access, locked-device availability, database busy/error, extension execution logs |
| App value stale after Shortcut | Resume the app or select Refresh; the native counter is authoritative |
| Widget value stale | Scheduling/budget, extension failures; compare Last Updated with app; a reload request is not a refresh guarantee |

[FlareStore](https://flarestore.app/) describes signing/sideloading using certificates and device-eligible profiles. Its documentation is not proof of this app's widget/App Group/ExtensionKit behavior. Do not purchase a different certificate on the assumption that it will fix these targets without the required capability evidence.
