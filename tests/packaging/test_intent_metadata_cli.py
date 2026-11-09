"""Keep the native build's counter metadata CLI usable outside the repository."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class IntentMetadataCliTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.bundle = Path(temporary.name) / "ControlStudioIntents.appex"
        self.metadata = self.bundle / "Metadata.appintents/extract.actionsdata"
        self.metadata.parent.mkdir(parents=True)

    def cli(self, *arguments):
        return subprocess.run([sys.executable, str(ROOT / "scripts/validate-intent-metadata.py"), *map(str, arguments)],
                              cwd=self.bundle.parent, capture_output=True, text=True, timeout=15)

    def test_counter_metadata_validates_outside_repository_and_rejects_wrong_action(self):
        self.metadata.write_text(json.dumps({"id": "IncrementControlStudioCounter",
                                             "title": "Increment Control Studio Counter"}))
        result = self.cli(self.bundle)
        self.assertEqual(0, result.returncode, result.stderr)
        self.metadata.write_text(json.dumps({"id": "OtherAction", "title": "Other Action"}))
        result = self.cli(self.bundle)
        self.assertEqual(1, result.returncode)
        self.assertIn("Missing App Intents metadata", result.stderr)
        self.assertNotIn("Traceback", result.stderr)

    def test_cli_rejects_incomplete_or_unrecognized_arguments(self):
        for arguments in ((), ("--unknown", self.bundle), (self.bundle, "extra")):
            with self.subTest(arguments=arguments):
                result = self.cli(*arguments)
                self.assertEqual(2, result.returncode)
                self.assertIn("usage:", result.stderr)
                self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
