# iOS signing

## Required capability evidence

The proposed identifiers are:

| Target | Explicit bundle ID |
|---|---|
| Host | `com.faikalizham.controlstudio` |
| Widget | `com.faikalizham.controlstudio.widgets` |
| App Intents extension | `com.faikalizham.controlstudio.intents` |
| Shared App Group (current provider test configuration) | `group.23b5a5355f697168.1` |

Register the three explicit App IDs under the same Apple team and enable the exact App Group for each. Generate three development, ad-hoc or eligible enterprise provisioning profiles. For development/ad-hoc profiles, the iPhone's UDID must be included in **every** profile. Each profile must authorize the certificate used for signing. The CI signer rejects App Store profiles because these IPAs are intended for direct device installation.

The original provisional group was `group.com.faikalizham.controlstudio`. Inspection of the owner's installed IPA confirmed it was absent, while the provider group now selected was present in all signatures and the host profile. The supplied IPA still lacks extension profiles and uses mismatched signed app identifiers, so it does not pass this repository's strict signed validator. See [the diagnosis and test configuration](widget-app-group-fix.md). For your own developer account, register a dedicated group you control, select it with `python scripts/app_group_config.py --identifier group.your.authorized.group`, and regenerate before building. A provider-owned group must be authorized by that provider.

A `.p12` certificate/private key identifies a signer. A provisioning profile authorizes an app ID, device/distribution scope and entitlements. A `.p12` alone cannot add App Groups. A reseller/enterprise certificate, wildcard profile or claim of widget support does not prove that these exact identifiers and the shared group are allowed. App Intents has no blanket capability that substitutes for correct target membership, metadata and registration. This POC does not request the legacy SiriKit entitlement.

Until the actual profiles pass validation, identifiers and sharing support remain provisional. If your signing provider cannot issue the profiles, use unsigned CI to validate compilation and the local counter configuration after compatible re-signing. Shared widgets and Shortcuts remain blocked. This repository does not silently strip the App Group or fake a provisioning profile to bypass that limitation. Changing IDs requires updating the C#/Swift constants, native project generator, manifests, validator, docs and newly issued profiles together.

## CI encrypted secrets

New version-tag pushes build an **unsigned** IPA by default for later FlareStore signing. CI signing is opt-in: set the Actions repository variable `IOS_SIGNING_MODE` to `signed` and configure all secrets below before the approved tag push. The build fails if signed mode cannot validate its credentials/profiles; it never switches silently to unsigned output. Ordinary branch pushes and manual dispatch do not generate an IPA.

In GitHub, open **Repository → Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `IOS_CERTIFICATE_P12_BASE64` | Base64 bytes of your `.p12` including its private key |
| `IOS_CERTIFICATE_PASSWORD` | Password protecting the `.p12` |
| `IOS_HOST_PROFILE_BASE64` | Base64 host provisioning profile |
| `IOS_WIDGET_PROFILE_BASE64` | Base64 widget provisioning profile |
| `IOS_INTENTS_PROFILE_BASE64` | Base64 App Intents extension provisioning profile |

To copy an encoded file on Windows without committing it:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\Private\host.mobileprovision')) | Set-Clipboard
```

Repeat using your actual private file paths and corresponding secret fields. Clear the clipboard afterward. Never paste signing values into source files, issues or workflow logs.

The workflow compiles all products without signing, then imports the certificate into a random-password temporary keychain. The signer validates all profiles before signing, copies the matching profile to each bundle, and derives only authorized app/team/group/debug entitlements. It signs embedded libraries/frameworks first, both extensions next, and the host last. It never uses `codesign --deep` to perform signing; `--deep --strict` is used for verification only.

The final IPA validator checks bundle metadata, exact extension placement/points, device arm64 Mach-O platform, dependency paths, individual code signatures, matching teams, signed entitlements, profile expiration, app IDs, distribution scope and that each signature certificate is listed in its profile. Optional `--device-udid` additionally checks device eligibility. CI does not know your UDID by default, so device eligibility is still a manual check. Cryptographic checks run only on macOS.

The cleanup step always deletes the temporary keychain and signing source files. Upload is restricted to the IPA and a sanitized validation report. An appropriately signed IPA necessarily contains each bundle's `embedded.mobileprovision` (public authorization metadata including registered UDIDs); private keys, `.p12` files, passwords and standalone provisioning files are never uploaded. Draft GitHub Releases should be reviewed before any publication.

## Unsigned / FlareStore path

The unsigned file is a compiled device bundle packaged for a later signer, **not an installable app** and not cryptographically validated. Requested entitlements remain in source. It carries no provisioning profiles. FlareStore must preserve both extension executables and their metadata and sign all three bundles with matching profiles, identities and App Group authorization.

The [FlareStore API](https://flarestore.app/api) documents certificate/profile verification and a signing endpoint using a profile. Its public documentation does not establish that a single supplied profile can correctly authorize this app's three distinct IDs or that ExtensionKit products will be preserved. Do not assume that path works for this app. Prefer the CI-signed artifact if the necessary profiles are available, and verify any FlareStore re-signing output again on macOS. A signer removing extensions, rewriting bundle IDs or stripping App Groups invalidates Milestone 0.
