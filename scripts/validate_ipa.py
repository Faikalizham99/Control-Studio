"""Validate structure on any OS, and Mach-O/signatures/profiles on macOS.

Unsigned means suitable for a subsequent signer, never installable as-is.
No certificates or full profile contents are emitted in logs or reports.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import stat
import subprocess
import sys
import tempfile
import zipfile
from app_group_config import load_identifier

APP_ID = "com.faikalizham.controlstudio"
GROUP = load_identifier()
BUNDLES = {
    "": (APP_ID, None),
    "PlugIns/ControlStudioWidgets.appex": (APP_ID + ".widgets", "com.apple.widgetkit-extension"),
    "Extensions/ControlStudioIntents.appex": (APP_ID + ".intents", "com.apple.appintents-extension"),
}
CLOCK_FONTS = json.loads((Path(__file__).resolve().parents[1] / "assets/clock-fonts/manifest.json").read_text())["fonts"]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(*args):
    result = subprocess.run([str(a) for a in args], capture_output=True, check=False)
    require(result.returncode == 0, f"{Path(str(args[0])).name} failed for {args[-1]}")
    return result.stdout


def read_plist(path):
    with Path(path).open("rb") as stream:
        return plistlib.load(stream)


def validate_scene_manifest(info):
    manifest = info.get("UIApplicationSceneManifest", {})
    require(manifest.get("UIApplicationSupportsMultipleScenes") is False, "Host must use the single-window scene lifecycle")
    scenes = manifest.get("UISceneConfigurations", {}).get("UIWindowSceneSessionRoleApplication", [])
    require(any(scene.get("UISceneConfigurationName") == "__MAUI_DEFAULT_SCENE_CONFIGURATION__" and
                scene.get("UISceneDelegateClassName") == "SceneDelegate" and not scene.get("UISceneStoryboardFile")
                for scene in scenes), "Required MAUI scene configuration missing")


def validate_widget_link_scheme(info):
    entries = info.get("CFBundleURLTypes", [])
    require(isinstance(entries, list), "Widget tap URL registration missing")
    schemes = [scheme for entry in entries if isinstance(entry, dict)
               and isinstance(entry.get("CFBundleURLSchemes"), list)
               for scheme in entry["CFBundleURLSchemes"]]
    require(schemes.count("controlstudio") == 1, "Widget tap URL registration missing or duplicated")


def validate_metadata(app):
    app = Path(app)
    host = read_plist(app / "Info.plist")
    validate_scene_manifest(host)
    validate_widget_link_scheme(host)
    bundles = []
    for relative, (identifier, point) in BUNDLES.items():
        bundle = app / relative
        info = read_plist(bundle / "Info.plist")
        require(info.get("ControlStudioAppGroup") == GROUP, f"Configured App Group mismatch: {relative or 'host'}")
        require(info.get("CFBundleIdentifier") == identifier, f"Bundle ID mismatch: {relative or 'host'}")
        executable = info.get("CFBundleExecutable", "")
        require(executable and Path(executable).name == executable, "Invalid bundle executable name")
        require((bundle / executable).is_file(), f"Missing executable: {identifier}")
        for key in ["CFBundleName", "CFBundleShortVersionString", "CFBundleVersion", "MinimumOSVersion"]:
            require(bool(info.get(key)), f"Missing {key}: {identifier}")
        require(info.get("CFBundlePackageType") == ("XPC!" if relative else "APPL"), f"Invalid package type: {identifier}")
        require("iPhoneOS" in info.get("CFBundleSupportedPlatforms", []), f"Not an iPhoneOS product: {identifier}")
        require({1, 2}.issubset(set(info.get("UIDeviceFamily", []))), f"iPhone/iPad device support missing: {identifier}")
        privacy_path = bundle / "PrivacyInfo.xcprivacy"
        require(privacy_path.is_file(), f"Privacy manifest missing: {identifier}")
        privacy = read_plist(privacy_path)
        require(any(entry.get("NSPrivacyAccessedAPIType") == "NSPrivacyAccessedAPICategoryFileTimestamp" and
                    "C617.1" in entry.get("NSPrivacyAccessedAPITypeReasons", [])
                    for entry in privacy.get("NSPrivacyAccessedAPITypes", [])), f"Container file metadata reason missing: {identifier}")
        require(info.get("CFBundleVersion") == host.get("CFBundleVersion"), "Host/extension build versions differ")
        require(info.get("CFBundleShortVersionString") == host.get("CFBundleShortVersionString"), "Host/extension versions differ")
        if not relative or relative == "PlugIns/ControlStudioWidgets.appex":
            registered = info.get("UIAppFonts")
            require(isinstance(registered, list) and all(isinstance(name, str) for name in registered)
                    and set(registered) == {font["file"] for font in CLOCK_FONTS}, f"Clock font registration missing: {identifier}")
            for font in CLOCK_FONTS:
                path = bundle / font["file"]
                require(path.is_file() and path.stat().st_size <= 10_000_000, f"Bundled clock font missing/oversized: {font['file']}")
                require(hashlib.sha256(path.read_bytes()).hexdigest() == font["sha256"], f"Bundled clock font mismatch: {font['file']}")
                licence = bundle / font["licence"]
                require(licence.is_file() and licence.stat().st_size <= 65_536
                        and "SIL OPEN FONT LICENSE" in licence.read_text(encoding="utf-8"), f"Clock font licence missing: {font['file']}")
        if point:
            actual = (info.get("EXAppExtensionAttributes", {}).get("EXExtensionPointIdentifier")
                      if relative.startswith("Extensions/") else
                      info.get("NSExtension", {}).get("NSExtensionPointIdentifier"))
            require(actual == point, f"Wrong extension point: {identifier}")
        bundles.append((bundle, info))
    actual_extensions = {p.relative_to(app).as_posix() for p in app.rglob("*.appex")}
    require(actual_extensions == set(BUNDLES) - {""}, "Unexpected, missing or misplaced extensions")
    metadata = app / "Extensions/ControlStudioIntents.appex/Metadata.appintents/extract.actionsdata"
    require(metadata.is_file() and metadata.stat().st_size > 0, "App Intents metadata missing")
    data = json.dumps(json.loads(metadata.read_bytes()))
    require("IncrementControlStudioCounter" in data and "Increment Control Studio Counter" in data,
            "Increment action missing from extracted metadata")
    return bundles


def decode_profile(path):
    return plistlib.loads(run("security", "cms", "-D", "-i", path))


def validate_profile(profile, identifier, team=None, device=None):
    expiry = profile.get("ExpirationDate")
    require(isinstance(expiry, dt.datetime), "Missing provisioning expiration")
    require(expiry.replace(tzinfo=dt.timezone.utc) > dt.datetime.now(dt.timezone.utc), "Provisioning profile expired")
    teams = profile.get("TeamIdentifier", [])
    require(len(teams) == 1, "Ambiguous profile signing team")
    if team:
        require(teams[0] == team, "Signing teams do not match")
    entitlements = profile.get("Entitlements", {})
    prefixes = profile.get("ApplicationIdentifierPrefix", [])
    app_identifier = entitlements.get("application-identifier")
    require(any(app_identifier == prefix + "." + identifier for prefix in prefixes),
            f"An explicit profile for {identifier} is required")
    require(entitlements.get("com.apple.developer.team-identifier") == teams[0], "Profile team entitlement mismatch")
    require(GROUP in entitlements.get("com.apple.security.application-groups", []), "Profile does not authorize the App Group")
    # CI signing supports development, ad-hoc and enterprise distribution, not App Store profiles.
    require(bool(profile.get("ProvisionedDevices")) or profile.get("ProvisionsAllDevices") is True,
            "An on-device development, ad-hoc or enterprise profile is required")
    if device and not profile.get("ProvisionsAllDevices"):
        require(device in profile.get("ProvisionedDevices", []), "Device UDID is absent from a profile")
    require(bool(profile.get("DeveloperCertificates")), "Profile contains no signing certificates")
    return teams[0]


def signed_entitlements(bundle):
    return plistlib.loads(run("codesign", "-d", "--entitlements", ":-", bundle))


def signature_team(bundle):
    result = subprocess.run(["codesign", "-d", "--verbose=4", str(bundle)], capture_output=True, text=True)
    require(result.returncode == 0, f"Missing code signature: {bundle.name}")
    match = re.search(r"^TeamIdentifier=(\S+)$", result.stderr, re.MULTILINE)
    require(match is not None and match[1] != "not set", f"Signature has no signing team: {bundle.name}")
    return match[1]


def validate_macho(path):
    arch = run("lipo", "-archs", path).decode().split()
    require(arch == ["arm64"], f"Expected device arm64 binary: {path.name}")
    load = run("otool", "-l", path).decode()
    require(re.search(r"platform\s+(?:2|IOS)\b", load) is not None, f"Not an iOS device Mach-O: {path.name}")
    linked = run("otool", "-L", path).decode()
    for dependency in linked.splitlines()[1:]:
        library = dependency.strip().split(" (", 1)[0]
        require(library.startswith(("/System/Library/", "/usr/lib/", "@rpath/", "@loader_path/", "@executable_path/")),
                f"Machine-specific library dependency in {path.name}")


def validate_bundle(app, mode, device=None):
    bundles = validate_metadata(app)
    native_checked = sys.platform == "darwin"
    require(mode != "signed" or native_checked, "Signed validation requires macOS codesign and security")
    team = None
    for bundle, info in bundles:
        if native_checked:
            validate_macho(bundle / info["CFBundleExecutable"])
        if mode == "signed":
            run("codesign", "--verify", "--strict", bundle)
            current_team = signature_team(bundle)
            team = team or current_team
            require(team == current_team, "Host and extension signatures use different teams")
            profile = decode_profile(bundle / "embedded.mobileprovision")
            validate_profile(profile, info["CFBundleIdentifier"], team, device)
            signed = signed_entitlements(bundle)
            allowed = profile["Entitlements"]
            require(signed.get("application-identifier") == allowed["application-identifier"], "Signed app identifier mismatch")
            require(signed.get("com.apple.developer.team-identifier") == team, "Signed team mismatch")
            require(signed.get("com.apple.security.application-groups") == [GROUP], "Signed App Group mismatch")
            require(signed.get("get-task-allow", False) == allowed.get("get-task-allow", False), "Debug entitlement mismatch")
            with tempfile.TemporaryDirectory() as temp:
                prefix = str(Path(temp) / "certificate")
                run("codesign", "-d", "--extract-certificates", prefix, bundle)
                leaf = Path(prefix + "0").read_bytes()
                require(leaf in profile["DeveloperCertificates"], "Signing certificate is not authorized by profile")
        else:
            require(not (bundle / "embedded.mobileprovision").exists(), "Unsigned artifact unexpectedly contains a provisioning profile")
    for binary in Path(app).rglob("*.dylib"):
        if native_checked:
            validate_macho(binary)
        if mode == "signed":
            run("codesign", "--verify", "--strict", binary)
            require(signature_team(binary) == team, "Embedded library signing team mismatch")
    for framework in Path(app).rglob("*.framework"):
        info = read_plist(framework / "Info.plist")
        if native_checked:
            validate_macho(framework / info["CFBundleExecutable"])
        if mode == "signed":
            run("codesign", "--verify", "--strict", framework)
            require(signature_team(framework) == team, "Embedded framework signing team mismatch")
    if mode == "signed":
        run("codesign", "--verify", "--deep", "--strict", app)
    return {"mode": mode, "appGroup": GROUP, "structure": "passed", "nativeBinaryChecks": "passed" if native_checked else "NOT VERIFIED",
            "signaturesAndProfiles": "passed" if mode == "signed" else "NOT VERIFIED — requires signing",
            "deviceInstallation": "NOT VERIFIED", "widgetFunctional": "NOT VERIFIED", "intentFunctional": "NOT VERIFIED"}


def extract_safely(archive, destination):
    seen = set()
    with zipfile.ZipFile(archive) as ipa:
        for entry in ipa.infolist():
            name = PurePosixPath(entry.filename)
            require(not name.is_absolute() and ".." not in name.parts and "\\" not in entry.filename,
                    "Unsafe IPA archive path")
            require(entry.filename not in seen, "Duplicate IPA archive entry")
            seen.add(entry.filename)
            mode = entry.external_attr >> 16
            require(not stat.S_ISLNK(mode), "Unexpected symbolic link in iOS archive")
            target = Path(destination).joinpath(*name.parts)
            require(target.resolve().is_relative_to(Path(destination).resolve()), "Archive path escapes extraction directory")
            ipa.extract(entry, destination)
            if mode and not entry.is_dir():
                os.chmod(target, mode & 0o777)
    apps = list((Path(destination) / "Payload").glob("*.app"))
    require(len(apps) == 1 and apps[0].name == "ControlStudio.app", "Expected Payload/ControlStudio.app")
    return apps[0]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument("--mode", choices=["unsigned", "signed"], required=True)
    parser.add_argument("--device-udid")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as temp:
        app = args.path if args.path.is_dir() else extract_safely(args.path, temp)
        report = validate_bundle(app, args.mode, args.device_udid)
    if args.path.is_file():
        report["sha256"] = hashlib.sha256(args.path.read_bytes()).hexdigest()
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, plistlib.InvalidFileException, zipfile.BadZipFile) as error:
        print(f"IPA validation failed: {error}", file=sys.stderr)
        sys.exit(1)
