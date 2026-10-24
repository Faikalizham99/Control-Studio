"""Build-graph checks for the native widget background adapter; no Xcode required."""
import importlib.util
from pathlib import Path
import shlex
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
            for source in ["CSWidgetBackgroundAdapter.m"]:
                matches = [file for file in self.sources(target) if file["path"] == "WidgetRuntime/" + source]
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

    def test_clock_scene_dependency_is_compiled_once_in_both_extensions(self):
        for name, target in self.targets.items():
            paths = [file["path"] for file in self.sources(target)]
            for source in ["Shared/WidgetDesignStore.swift", "Shared/WidgetClockScene.swift"]:
                self.assertEqual(1, paths.count(source), f"{name}: {source}")
                self.assertTrue((ROOT / "native" / source).is_file())

    def test_standalone_bridge_includes_every_shared_swift_source(self):
        # Swift Package tests discover Shared automatically; the standalone iOS
        # archive must explicitly include the same files and their dependencies.
        script = (ROOT / "scripts/build-native.sh").read_text().replace("\\\n", " ")
        commands = [shlex.split(line, comments=True) for line in script.splitlines()]
        builds = [command for command in commands if command[:4] == ["xcrun", "--sdk", "iphoneos", "swiftc"]]
        self.assertEqual(1, len(builds))
        inputs = [path for path in builds[0] if path.endswith(".swift")]
        self.assertEqual(len(inputs), len(set(inputs)), "Duplicate bridge source input")
        expected = {path.relative_to(ROOT).as_posix() for path in (ROOT / "native/Shared").glob("*.swift")}
        expected.add("native/ControlStudioBridge/CounterBridge.swift")
        self.assertEqual(expected, set(inputs), "Bridge source list differs from the shared Swift module")
