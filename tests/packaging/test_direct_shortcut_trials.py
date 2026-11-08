"""Boundary checks for the trial widgets; these do not compile Swift or run iOS."""
import importlib.util
from pathlib import Path
import plistlib
import re
import shlex
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class DirectShortcutTrialTests(unittest.TestCase):
    def test_trial_source_is_compiled_only_in_the_widget_extension(self):
        spec = importlib.util.spec_from_file_location("trial_generator", ROOT / "scripts/generate-xcode-project.py")
        generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(generator)
        with tempfile.TemporaryDirectory() as folder:
            generator.ROOT = Path(folder)
            model = generator.generate()
        objects = model["objects"]
        path = "ControlStudioWidgets/DirectShortcutTrialWidgets.swift"
        for target in (value for value in objects.values() if value["isa"] == "PBXNativeTarget"):
            sources = [objects[objects[file]["fileRef"]]["path"]
                       for phase in target["buildPhases"] if objects[phase]["isa"] == "PBXSourcesBuildPhase"
                       for file in objects[phase]["files"]]
            self.assertEqual(1 if target["name"] == "ControlStudioWidgets" else 0, sources.count(path))
        self.assertTrue((ROOT / "native" / path).is_file())

    def test_older_os_and_existing_slots_do_not_adopt_the_experiment(self):
        root = (ROOT / "native/ControlStudioWidgets/ControlStudioWidgets.swift").read_text()
        self.assertEqual(1, root.count("DirectShortcutTrialWidgets().body"))
        self.assertRegex(root, r"if #available\(iOS 27\.0, \*\)\s*\{\s*DirectShortcutTrialWidgets\(\)\.body\s*\}")
        source = (ROOT / "native/ControlStudioWidgets/DirectShortcutTrialWidgets.swift").read_text()
        declarations = re.findall(r"(?:private )?struct (\w+):", source)
        self.assertTrue(declarations)
        for name in declarations:
            self.assertRegex(source, rf"@available\(iOS 27\.0, \*\)\s*(?:private )?struct {name}:")
        slots = (ROOT / "native/ControlStudioWidgets/StudioSlotWidget.swift").read_text()
        self.assertNotIn("AppIntentConfiguration", slots)
        self.assertNotIn("RunSystemShortcutIntent", slots)
        self.assertNotIn("DirectShortcutTrial", (ROOT / "native/WidgetRuntime/CSWidgetBackgroundAdapter.m").read_text())

    def test_execution_belongs_only_to_native_buttons_not_providers_or_app_routing(self):
        source = (ROOT / "native/ControlStudioWidgets/DirectShortcutTrialWidgets.swift").read_text()
        for forbidden in (".widgetURL(", "shortcuts://", "controlstudio://", "OpenURLIntent(", ".perform(",
                          "UIApplication", "WidgetDesignStore", "CounterStore", "UserDefaults"):
            self.assertNotIn(forbidden, source)
        calls = re.findall(r"Button\(intent: RunSystemShortcutIntent\((?:shortcut: action)?\)\)", source)
        self.assertEqual(3, len(calls))
        self.assertIn("var action: SystemShortcut?", source)
        self.assertIn(".disabled(true)", source)
        self.assertEqual(2, source.count(".supportedFamilies([.systemSmall, .systemMedium, .systemLarge, .systemExtraLargePortrait])"))

    def test_build_and_ipa_validation_require_the_picker_metadata(self):
        info = plistlib.loads((ROOT / "native/ControlStudioWidgets/Info.plist").read_bytes())
        self.assertIs(info["ControlStudioDirectShortcutTrials"], True)
        script = (ROOT / "scripts/build-native.sh").read_text().replace("\\\n", " ")
        commands = [shlex.split(line, comments=True) for line in script.splitlines()]
        self.assertIn(["python3", "scripts/validate-intent-metadata.py", "--widget-trials",
                       "artifacts/native/Release-iphoneos/ControlStudioWidgets.appex"], commands)


if __name__ == "__main__":
    unittest.main()
