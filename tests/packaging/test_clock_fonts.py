"""Check the actual font assets, glyphs and both bundle resource graphs without Xcode."""
import hashlib
import importlib.util
import json
from pathlib import Path
import plistlib
import struct
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / "assets/clock-fonts"
MANIFEST = json.loads((FOLDER / "manifest.json").read_text())


def table(data, name):
    for index in range(struct.unpack_from(">H", data, 4)[0]):
        tag, _, offset, length = struct.unpack_from(">4sIII", data, 12 + index * 16)
        if tag == name:
            return data[offset:offset + length]
    raise AssertionError(f"Missing font table {name}")


def names(data):
    value = table(data, b"name")
    _, count, offset = struct.unpack_from(">HHH", value)
    result = set()
    for index in range(count):
        platform, _, _, identifier, length, start = struct.unpack_from(">HHHHHH", value, 6 + index * 12)
        if identifier == 6:
            result.add(value[offset + start:offset + start + length].decode("utf-16-be" if platform in (0, 3) else "mac_roman"))
    return result


def glyph(data, character):
    value = table(data, b"cmap"); code = ord(character)
    for index in range(struct.unpack_from(">H", value, 2)[0]):
        platform, _, offset = struct.unpack_from(">HHI", value, 4 + index * 8)
        if platform not in (0, 3):
            continue
        fmt = struct.unpack_from(">H", value, offset)[0]
        if fmt == 12:
            for group in range(struct.unpack_from(">I", value, offset + 12)[0]):
                first, last, start = struct.unpack_from(">III", value, offset + 16 + group * 12)
                if first <= code <= last and start + code - first != 0:
                    return start + code - first
        elif fmt == 4:
            count = struct.unpack_from(">H", value, offset + 6)[0] // 2
            ends = offset + 14; starts = ends + count * 2 + 2; deltas = starts + count * 2; ranges = deltas + count * 2
            for segment in range(count):
                first = struct.unpack_from(">H", value, starts + segment * 2)[0]
                last = struct.unpack_from(">H", value, ends + segment * 2)[0]
                if not first <= code <= last:
                    continue
                delta = struct.unpack_from(">h", value, deltas + segment * 2)[0]
                distance = struct.unpack_from(">H", value, ranges + segment * 2)[0]
                found = (code + delta) & 65535 if not distance else struct.unpack_from(">H", value, ranges + segment * 2 + distance + (code - first) * 2)[0]
                if distance and found:
                    found = (found + delta) & 65535
                if found:
                    return found
    return 0


class ClockFontTests(unittest.TestCase):
    def test_fonts_match_pinned_bytes_postscript_names_and_clock_characters(self):
        for font in MANIFEST["fonts"]:
            with self.subTest(font=font["id"]):
                data = (FOLDER / font["file"]).read_bytes()
                self.assertEqual(font["sha256"], hashlib.sha256(data).hexdigest())
                self.assertIn(font["postScriptName"], names(data))
                for character in "0123456789: AMPabcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ":
                    self.assertNotEqual(0, glyph(data, character), repr(character))
                licence = (FOLDER / font["licence"]).read_text(encoding="utf-8")
                self.assertIn("SIL OPEN FONT LICENSE", licence)
                self.assertIn("Copyright", licence)

    def test_host_and_widget_register_and_package_the_same_fonts_and_licences(self):
        filenames = {font["file"] for font in MANIFEST["fonts"]}
        licences = {font["licence"] for font in MANIFEST["fonts"]}
        for path in ["src/ControlStudio.App/Platforms/iOS/Info.plist", "native/ControlStudioWidgets/Info.plist"]:
            info = plistlib.loads((ROOT / path).read_bytes())
            self.assertEqual(filenames, set(info["UIAppFonts"]))
        project = ROOT / "src/ControlStudio.App/ControlStudio.App.csproj"
        paths = {path.name for item in ET.parse(project).iter("BundleResource")
                 for path in project.parent.glob(item.attrib["Include"]) if path.parent.resolve() == FOLDER}
        self.assertEqual(filenames | licences, paths)
        spec = importlib.util.spec_from_file_location("font_project_generator", ROOT / "scripts/generate-xcode-project.py")
        generator = importlib.util.module_from_spec(spec); spec.loader.exec_module(generator)
        with tempfile.TemporaryDirectory() as temporary:
            generator.ROOT = Path(temporary)
            folder = generator.ROOT / "assets/clock-fonts"; folder.mkdir(parents=True)
            for filename in filenames | licences:
                (folder / filename).touch()
            objects = generator.generate()["objects"]
        for target in [item for item in objects.values() if item["isa"] == "PBXNativeTarget"]:
            resources = [objects[phase] for phase in target["buildPhases"] if objects[phase]["isa"] == "PBXResourcesBuildPhase"]
            paths = [objects[objects[ref]["fileRef"]]["path"] for phase in resources for ref in phase["files"]]
            expected = ["../assets/clock-fonts/" + name for name in filenames | licences] if target["name"] == "ControlStudioWidgets" else []
            self.assertCountEqual(expected, [path for path in paths if "clock-fonts" in path])

    def test_registered_postscript_names_are_used_by_both_renderers(self):
        managed = (ROOT / "src/ControlStudio.App/WidgetClockFonts.cs").read_text()
        native = (ROOT / "native/Shared/WidgetClockScene.swift").read_text()
        for font in MANIFEST["fonts"]:
            self.assertIn('"' + font["postScriptName"] + '"', managed)
            self.assertIn('"' + font["id"] + '": "' + font["postScriptName"] + '"', native)
