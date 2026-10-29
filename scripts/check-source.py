"""Cross-platform configuration checks; these do not replace Xcode compilation."""
from pathlib import Path
import json
import plistlib
import sys
import xml.etree.ElementTree as ET
import importlib.util
import os

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from app_group_config import synchronize
from validate_ipa import validate_scene_manifest
from check_widget_metadata import validate_widget_metadata
from widget_slot_config import synchronize as synchronize_slots


def source_files(root, suffixes):
    # Prune before descending: local SDKs/build outputs can contain thousands of
    # unrelated projects. Filtering the results of rglob still visits every tree.
    ignored = {".git", ".tools", ".tmp", "artifacts", "bin", "obj", ".build", ".swiftpm", "__pycache__"}
    for directory, children, files in os.walk(root):
        children[:] = sorted(child for child in children if child not in ignored)
        for name in sorted(files):
            if Path(name).suffix in suffixes:
                yield Path(directory) / name


def main():
    synchronize_slots(check=True)
    validate_widget_metadata()
    for path in [*source_files(ROOT, {".csproj"}), ROOT / "Directory.Build.props"]:
        ET.parse(path)
    for folder in [ROOT / "src", ROOT / "native"]:
        for path in source_files(folder, {".plist", ".xcprivacy"}):
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
    generator.generate(check=True)
    print("Source XML/plists, entitlements, pins, widget metadata and native project generation passed.")


if __name__ == "__main__":
    main()
