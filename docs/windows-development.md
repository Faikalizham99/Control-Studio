# Windows 11 development

Install the exact .NET SDK **10.0.401**, Git, and optionally Python 3.13.7 and GitHub CLI. The repository's `global.json` deliberately refuses other SDK versions. SDK discovery searches `.tools/dotnet27`, then `.tools/dotnet`, then the running `dotnet` host's installation (`$host$`). The new ignored local toolchain sits beside the older SDK so running development processes are not interrupted. CI and machines with the exact SDK installed normally use their host installation.

If you see **“A compatible .NET SDK was not found”**, run `dotnet --list-sdks` and `dotnet --version` from this repository. A system SDK such as 10.0.201 does not satisfy the 10.0.401 pin. The local SDK can be checked directly with `& .\.tools\dotnet27\dotnet.exe --version`; normal `dotnet --version` should also return **10.0.401**. Fresh clones do not include `.tools`: install the exact SDK before building. Keep the pin aligned with the iOS toolchain.

The [SDK search paths](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#paths) require a .NET 10 or later host and affect SDK commands. They do not switch the host's runtime/workload installation. For local MAUI tooling, use `& .\.tools\dotnet27\dotnet.exe ...` to use the workloads installed alongside that local SDK. Install `maui-ios --version 10.0.401.1` with `dotnet workload install` for managed iOS compilation. IDE integration was not verified by the CLI checks; if an IDE still reports the SDK missing, restart it and ensure its SDK installation/configuration includes 10.0.401. Full Swift/iOS compilation requires macOS with the pinned Xcode 27.0.

Run in PowerShell from the repository:

```powershell
dotnet --version
dotnet restore src/ControlStudio.Core/ControlStudio.Core.csproj --locked-mode
dotnet build src/ControlStudio.Infrastructure/ControlStudio.Infrastructure.csproj -c Release
dotnet test tests/ControlStudio.Core.Tests -c Release -p:RestoreLockedMode=true
dotnet test tests/ControlStudio.Infrastructure.Tests -c Release -p:RestoreLockedMode=true
python -m unittest discover -s tests/packaging -v
python scripts/check-source.py
```

The current App Group is selected in `config/app-group.json`. After changing it to a group your real profiles authorize, run `python scripts/app_group_config.py` to update the Swift/C# constants and all target manifests/entitlements. The source checker and IPA validator reject targets configured for a different group. See the [provider test configuration](widget-app-group-fix.md).

The shared libraries and managed tests build on Windows. The solution includes the iOS app, so a normal whole-solution build requires iOS tooling and a Mac; use the specific commands above for Windows checks. Python tests use clearly labeled fixtures, not actual IPAs or signing credentials. Swift SQLite tests and all native/iOS compilation run on GitHub's macOS runner. There is no Windows iOS simulator in this workflow.

Native project changes are source changes too. If changing the target list/build settings, edit `scripts/generate-xcode-project.py`, then run:

```powershell
python scripts/generate-xcode-project.py
```

Include both the generator and generated `.pbxproj` in a commit only after the owner explicitly approves committing. Regeneration is deterministic; the source check catches drift. CI does not install CocoaPods, XcodeGen or third-party Swift packages.

## Review, commit and push

**Never commit or push without the owner's explicit permission for that action**, including tag pushes. Editing/building/testing approval does not grant commit or push permission. Review the local changes first:

```powershell
git status --short
git diff --check
```

After explicit permission to commit, stage the intended files and commit. Obtain permission before pushing the commit. A regular branch push or pull request runs managed/Python tests only; it does not compile/package/upload an IPA.

## Build an IPA with a new version tag

Only pushing a **new** tag matching `v*.*.*` runs **Build Control Studio IPA**. Manual workflow dispatch is disabled. The build job checks the push payload's `created` and `deleted` flags, so moving or deleting an existing tag does not produce a new IPA. The tagged commit must contain the updated workflow. Tagging an older commit uses that commit's workflow rules.

The default output is **unsigned**, suitable for the existing FlareStore signing route. No CI signing secrets are needed for this mode. To use CI signing, first configure all documented secrets/profiles and set the GitHub Actions **repository variable** `IOS_SIGNING_MODE` to `signed`. Unset or `unsigned` uses unsigned mode; any other value fails explicitly. The workflow never silently falls back from failed signing. See [signing](ios-signing.md).

After the owner approves the exact commit and new tag push, the release commands are:

```powershell
# Example only: choose a fresh version and match all embedded app/extension versions.
git tag -a v0.2.1 -m "Control Studio 0.2.1"
git push origin v0.2.1
```

Do not reuse or force-move an existing version tag. This document does not authorize running the commands.

The tag build runs managed/Python/Swift tests, compiles the native extensions and MAUI app on macOS, packages and validates the IPA, uploads the artifact and creates a **draft** release. The artifact/release filenames follow the selected signing mode. Publishing the draft is separate.

With GitHub CLI installed and authenticated, inspect the resulting run:

```powershell
gh run list --workflow build-ios.yml
# Replace 123456789 with the actual run ID.
gh run watch 123456789 --exit-status
gh run view 123456789 --log-failed
gh run download 123456789 --dir artifacts/download
```

Open the log step that failed; do not regard an uploaded ZIP as signing or device proof. GitHub documents [tag filters](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#onpushbranchestagsbranches-ignoretags-ignore) and the push payload's [created/deleted flags](https://docs.github.com/en/webhooks/webhook-events-and-payloads#push).

The original embedded version was 0.1.0/build 1; the App Group correction is 0.1.1/build 2 and the first dashboard is 0.2.0/build 3. Update versions consistently in the MAUI project and native generator before subsequent releases, regenerate the native project, and commit those changes before tagging. Tag pattern triggers are GitHub glob matching, not a full semantic-version validator.
