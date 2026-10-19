# Windows 11 development

Install the exact .NET SDK **10.0.302**, Git, and optionally Python 3.13.7 and GitHub CLI. The repository's `global.json` deliberately refuses other SDK versions. The initial implementation used a workspace-local SDK in `.tools/dotnet`; that ignored download is not required if you install the SDK normally.

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

Commit both the generator and generated `.pbxproj`. Regeneration is deterministic; the source check catches drift. CI does not install CocoaPods, XcodeGen or third-party Swift packages.

## Commit and run CI

Review all changes and ensure no signing files are staged:

```powershell
git status --short
git diff --check
git add .
git diff --cached --stat
git commit -m "Implement Control Studio milestone 1"
git push origin HEAD
```

In GitHub, open **Actions → Build Control Studio IPA → Run workflow**. Select your branch and `unsigned` to run without signing secrets. The build compiles the two native executables/static bridge and MAUI host, packages an IPA, and validates it before upload. Only macOS can perform these checks.

With GitHub CLI installed and authenticated:

```powershell
gh workflow run build-ios.yml --ref main -f signing=unsigned
gh run list --workflow build-ios.yml
# Replace 123456789 with the actual run ID.
gh run watch 123456789 --exit-status
gh run view 123456789 --log-failed
gh run download 123456789 --dir artifacts/download
```

The workflow must already exist on the repository's default branch before manual dispatch is available. For a working branch, select it in the Actions UI/`--ref`. Open the log step that failed; do not regard an uploaded ZIP as signing or device proof.

## Release tags

Configure signing secrets and complete the desired validation before tagging. The tag workflow requires signed mode and fails if any secret/profile/capability is missing. It creates a **draft** release, which you review and publish separately.

```powershell
git tag -a v0.2.0 -m "Control Studio milestone 1"
git push origin v0.2.0
```

The original embedded version was 0.1.0/build 1; the App Group correction is 0.1.1/build 2 and the first dashboard is 0.2.0/build 3. Update versions consistently in the MAUI project and native generator before subsequent releases, regenerate the native project, and commit those changes before tagging. Tag pattern triggers are GitHub glob matching, not a full semantic-version validator.
