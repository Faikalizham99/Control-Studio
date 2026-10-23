"""Build-graph checks for the native widget background adapter; no Xcode required."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WidgetRuntimeTargetTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("native_generator", ROOT / "scripts/generate-xcode-project.py")
        generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(generator)
        with tempfile.TemporaryDirectory() as temporary:
            generator.ROOT = Path(temporary)
            self.model = generator.generate()
        self.objects = self.model["objects"]
        self.targets = {value["name"]: value for value in self.objects.values() if value["isa"] == "PBXNativeTarget"}

    def sources(self, target):
        files = []
        for phase in target["buildPhases"]:
            phase = self.objects[phase]
            if phase["isa"] == "PBXSourcesBuildPhase":
                files.extend(self.objects[self.objects[file]["fileRef"]] for file in phase["files"])
        return files

    def settings(self, target):
        configs = self.objects[target["buildConfigurationList"]]["buildConfigurations"]
        return [self.objects[config]["buildSettings"] for config in configs]

    def test_background_adapter_is_compiled_once_and_only_in_widget_extension(self):
        for name, target in self.targets.items():
            matches = [file for file in self.sources(target) if file["path"] == "WidgetRuntime/CSWidgetBackgroundAdapter.m"]
            self.assertEqual(1 if name == "ControlStudioWidgets" else 0, len(matches), name)
            for file in matches:
                self.assertEqual("sourcecode.c.objc", file["lastKnownFileType"])
                self.assertTrue((ROOT / "native" / file["path"]).is_file())

    def test_swift_bridge_is_widget_only_and_exists(self):
        for name, target in self.targets.items():
            for settings in self.settings(target):
                bridge = settings.get("SWIFT_OBJC_BRIDGING_HEADER")
                if name == "ControlStudioWidgets":
                    self.assertEqual("ControlStudioWidgets/ControlStudioWidgets-Bridging-Header.h", bridge)
                    self.assertTrue((ROOT / "native" / bridge).is_file())
                else:
                    self.assertIsNone(bridge)

    def test_adapter_uses_project_arc_and_extension_safe_build_settings(self):
        project = self.objects[self.model["rootObject"]]
        configs = self.objects[project["buildConfigurationList"]]["buildConfigurations"]
        for config in configs:
            self.assertEqual("YES", self.objects[config]["buildSettings"]["CLANG_ENABLE_OBJC_ARC"])
        for settings in self.settings(self.targets["ControlStudioWidgets"]):
            self.assertEqual("YES", settings["APPLICATION_EXTENSION_API_ONLY"])
            self.assertNotIn("CLANG_ENABLE_OBJC_ARC", settings)
