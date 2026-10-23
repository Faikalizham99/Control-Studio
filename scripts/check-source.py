"""Cross-platform configuration checks; these do not replace Xcode compilation."""
from pathlib import Path
import json
import plistlib
import sys
import xml.etree.ElementTree as ET
import importlib.util

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from app_group_config import synchronize
from validate_ipa import validate_scene_manifest
from check_widget_metadata import validate_widget_metadata


def main():
    validate_widget_metadata()
    for path in [*ROOT.rglob("*.csproj"), ROOT / "Directory.Build.props"]:
        if not {".tools", "obj", "bin"}.intersection(path.parts):
            ET.parse(path)
    for folder in [ROOT / "src", ROOT / "native"]:
        for path in [*folder.rglob("*.plist"), *folder.rglob("*.xcprivacy")]:
            if not {".build", "obj", "bin"}.intersection(path.parts):
                with path.open("rb") as stream:
                    plistlib.load(stream)
    synchronize(check=True)
    toolchain = json.loads((ROOT / "global.json").read_text())
    assert toolchain["sdk"]["version"] == "10.0.401"
    assert toolchain["workload"]["version"] == "10.0.401.1"
    host = plistlib.loads((ROOT / "src/ControlStudio.App/Platforms/iOS/Info.plist").read_bytes())
    validate_scene_manifest(host)
    spec = importlib.util.spec_from_file_location("generator", ROOT / "scripts/generate-xcode-project.py")
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    path = ROOT / "native/ControlStudioNative.xcodeproj/project.pbxproj"
    original = path.read_bytes()
    generator.generate()
    assert path.read_bytes() == original, "Generated Xcode project is out of date; regenerate and commit it"
    print("Source XML/plists, entitlements, pins, widget metadata and native project generation passed.")


if __name__ == "__main__":
    main()
