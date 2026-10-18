"""Sign existing compiled products inside-out, using only profile-authorized entitlements."""
import argparse
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile
from validate_ipa import BUNDLES, GROUP, decode_profile, require, validate_metadata, validate_profile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("app", type=Path)
    parser.add_argument("--profiles", type=Path, required=True)
    parser.add_argument("--identity", required=True)
    parser.add_argument("--keychain", required=True)
    args = parser.parse_args()
    validate_metadata(args.app)
    profiles = {}
    team = None
    for relative, (identifier, _) in BUNDLES.items():
        name = "host" if not relative else "widgets" if "Widgets" in relative else "intents"
        path = args.profiles / (name + ".mobileprovision")
        profile = decode_profile(path)
        team = validate_profile(profile, identifier, team)
        profiles[relative] = (path, profile)
    with tempfile.TemporaryDirectory() as temp:
        # Dylibs first, frameworks second, extensions next, host last. Never codesign --deep to sign.
        for library in sorted(args.app.rglob("*.dylib")):
            sign(args, library)
        for framework in sorted(args.app.rglob("*.framework"), key=lambda p: len(p.parts), reverse=True):
            sign(args, framework)
        for index, relative in enumerate([r for r in BUNDLES if r] + [""]):
            bundle = args.app / relative
            source, profile = profiles[relative]
            allowed = profile["Entitlements"]
            entitlements = {k: allowed[k] for k in ["application-identifier", "com.apple.developer.team-identifier"]}
            entitlements["com.apple.security.application-groups"] = [GROUP]
            if "get-task-allow" in allowed:
                entitlements["get-task-allow"] = allowed["get-task-allow"]
            path = Path(temp) / f"entitlements-{index}.plist"
            path.write_bytes(plistlib.dumps(entitlements))
            shutil.copy2(source, bundle / "embedded.mobileprovision")
            sign(args, bundle, path)


def sign(args, target, entitlements=None):
    command = ["codesign", "--force", "--sign", args.identity, "--keychain", args.keychain, "--timestamp=none"]
    if entitlements:
        command += ["--entitlements", str(entitlements)]
    subprocess.run(command + [str(target)], check=True)


if __name__ == "__main__":
    main()
