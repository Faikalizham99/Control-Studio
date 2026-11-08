"""Exercise real artifact reads and both native-build CLI modes."""
import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from validate_ipa import validate_direct_shortcut_metadata


class DirectShortcutMetadataTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.bundle = Path(temporary.name) / "ControlStudioWidgets.appex"
        self.metadata = self.bundle / "Metadata.appintents/extract.actionsdata"
        self.metadata.parent.mkdir(parents=True)

    def write(self, value):
        self.metadata.write_text(json.dumps(value), encoding="utf-8")

    def cli(self, *arguments):
        return subprocess.run([sys.executable, str(ROOT / "scripts/validate-intent-metadata.py"), *map(str, arguments)],
                              cwd=self.bundle.parent, capture_output=True, text=True, timeout=15)

    def test_rejects_scalar_json_instead_of_an_extracted_metadata_collection(self):
        for value in ("DirectShortcutPickerConfiguration", 7, None, True):
            with self.subTest(value=value):
                self.write(value)
                with self.assertRaisesRegex(ValueError, "metadata collection"):
                    validate_direct_shortcut_metadata(self.bundle)

    def test_enforces_actual_read_size_even_if_stat_is_stale(self):
        self.metadata.write_bytes(b" " * 2_000_001)
        # Simulate growth between inspecting the file and opening/reading it.
        with patch.object(Path, "stat", return_value=SimpleNamespace(st_size=1, st_mode=stat.S_IFREG)):
            with self.assertRaisesRegex(ValueError, "metadata missing or oversized"):
                validate_direct_shortcut_metadata(self.bundle)

    def test_reports_invalid_utf8_json_and_deeply_nested_documents_as_validation_errors(self):
        for raw in (b"\xff", b"{", b"[" * 2000 + b"0" + b"]" * 2000):
            with self.subTest(raw=raw[:12]):
                self.metadata.write_bytes(raw)
                with self.assertRaisesRegex(ValueError, "metadata is invalid"):
                    validate_direct_shortcut_metadata(self.bundle)

    def test_native_cli_keeps_counter_and_widget_modes_independent(self):
        self.write({"intents": [{"id": "ControlStudioWidgets.DirectShortcutPickerConfiguration"}]})
        result = self.cli("--widget-trials", self.bundle)
        self.assertEqual(0, result.returncode, result.stderr)
        result = self.cli(self.bundle)
        self.assertNotEqual(0, result.returncode)
        self.write({"id": "IncrementControlStudioCounter", "title": "Increment Control Studio Counter"})
        result = self.cli(self.bundle)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertNotEqual(0, self.cli("--widget-trials", self.bundle).returncode)

    def test_native_cli_rejects_incomplete_or_unrecognized_arguments(self):
        for arguments in ((), ("--widget-trials",), ("--unknown", self.bundle),
                          ("--widget-trials", self.bundle, "extra")):
            with self.subTest(arguments=arguments):
                result = self.cli(*arguments)
                self.assertEqual(2, result.returncode)
                self.assertIn("usage:", result.stderr)
                self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
