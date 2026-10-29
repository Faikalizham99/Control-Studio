"""Keep host, native storage, literal registration and transparency in sync."""
from pathlib import Path
import json
import re
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
from widget_slot_config import ROOT, configuration, generated_sections, number_name, synchronize
from check_widget_metadata import metadata_errors


class WidgetSlotConfigTests(unittest.TestCase):
    def test_checked_in_definitions_are_current(self):
        synchronize(check=True)
        self.assertEqual(20, configuration()["slotsPerSize"])

    def test_each_kind_has_one_literal_name_and_one_bundle_registration(self):
        sections = generated_sections(configuration())
        widgets = sections["native/ControlStudioWidgets/StudioSlotWidget.swift"][1]
        bundle = sections["native/ControlStudioWidgets/ControlStudioWidgets.swift"][1]
        background = sections["native/WidgetRuntime/CSWidgetBackgroundAdapter.m"][1]
        self.assertEqual([], metadata_errors(widgets, "slots.swift"))
        self.assertEqual(20, len(re.findall(r"struct StudioSlot\w+Widget: Widget", widgets)))
        for number in range(1, 21):
            self.assertEqual(1, widgets.count(f'make(slot: {number}).configurationDisplayName("Studio {number}")'))
            self.assertEqual(1, widgets.count(f"        StudioSlot{number_name(number)}Widget()"))
            self.assertEqual(1, background.count(f'@"ControlStudio.Slot.{number}"'))
        groups = re.findall(r"struct (StudioSlots\w+To\w+): WidgetBundle", widgets)
        self.assertEqual(4, len(groups))
        for group in groups:
            self.assertEqual(1, bundle.count(f"{group}().body"))
        self.assertNotIn("Counter", background)
        self.assertNotIn("AppIntentConfiguration", widgets)

    def test_stale_output_in_any_language_fails_without_writing(self):
        for stale in generated_sections(configuration()):
            with self.subTest(path=stale), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                paths = ["contracts/widget-slot-config.json", *generated_sections(configuration())]
                for name in paths:
                    destination = root / name
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(ROOT / name, destination)
                path = root / stale
                source = path.read_text(encoding="utf-8")
                section, body = generated_sections(configuration())[stale]
                path.write_text(source.replace(body, body + "\n// stale"), encoding="utf-8")
                before = {name: (root / name).read_bytes() for name in paths}
                with self.assertRaisesRegex(ValueError, "Stale widget slot definitions"):
                    synchronize(check=True, root=root)
                self.assertEqual(before, {name: (root / name).read_bytes() for name in paths})
                synchronize(root=root)
                synchronize(check=True, root=root)

    def test_configuration_rejects_invalid_values_and_handles_partial_groups(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "contracts/widget-slot-config.json"
            path.parent.mkdir()
            for replacement in [{"slotsPerSize": 0}, {"slotsPerSize": True}, {"slotsPerSize": 41}, {"slotsPerSize": 40},
                                {"storedSizeCount": 4}, {"maximumCatalogBytes": 0}, {"unknown": 1}]:
                path.write_text(json.dumps(configuration() | replacement), encoding="utf-8")
                with self.assertRaises(ValueError):
                    configuration(root)
            # Repetition is generated even for a final group shorter than five.
            sections = generated_sections(configuration() | {"slotsPerSize": 21})
            widgets = sections["native/ControlStudioWidgets/StudioSlotWidget.swift"][1]
            self.assertIn("struct StudioSlotsTwentyOneToTwentyOne", widgets)
            self.assertIn('make(slot: 21).configurationDisplayName("Studio 21")', widgets)


if __name__ == "__main__":
    unittest.main()
