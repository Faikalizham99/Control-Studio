"""Regression for the 0.2.18 startup assertion in configurationDisplayName."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
from check_widget_metadata import metadata_errors, validate_widget_metadata


class WidgetMetadataTests(unittest.TestCase):
    def test_crashing_slot_name_and_description_are_rejected(self):
        source = r'''
            .configurationDisplayName("Studio \(slot)")
            .description("Assign a design to slot \(slot).")
        '''
        errors = metadata_errors(source, "regression.swift")
        self.assertEqual(2, len(errors))
        self.assertIn("regression.swift:2:", errors[0])
        self.assertIn("configurationDisplayName", errors[0])
        self.assertIn("description", errors[1])

    def test_literal_metadata_and_dynamic_widget_content_are_allowed(self):
        source = r'''
            // .configurationDisplayName(dynamicName)
            /* .description("Bad \(slot)") */
            .configurationDisplayName("Studio 10")
            .description("Assign a saved design to this slot for each size.")
            Text("Studio \(entry.slot): \(entry.name)")
            let example = ".description(dynamicName)"
        '''
        self.assertEqual([], metadata_errors(source, "valid.swift"))

    def test_dynamic_wrapped_and_concatenated_metadata_are_rejected(self):
        for argument in ["displayName", 'LocalizedStringKey("Studio \\(slot)")',
                         'Text("Studio \\(slot)")', '"Studio " + number',
                         r'#"Studio \#(slot)"#', '"""Studio \\(slot)"""']:
            with self.subTest(argument=argument):
                self.assertEqual(1, len(metadata_errors(
                    f".configurationDisplayName({argument})", "invalid.swift")))

    def test_shipped_widget_gallery_metadata_is_static(self):
        validate_widget_metadata()


if __name__ == "__main__":
    unittest.main()
