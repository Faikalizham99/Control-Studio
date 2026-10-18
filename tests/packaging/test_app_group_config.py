import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
from app_group_config import validate_identifier


class AppGroupConfigurationTests(unittest.TestCase):
    def test_accepts_explicit_provider_and_dedicated_identifiers(self):
        for identifier in ["group.provider.slot1", "group.com.example.my-app"]:
            self.assertEqual(validate_identifier(identifier), identifier)

    def test_rejects_wildcards_paths_and_source_injection(self):
        for identifier in [None, "", "com.example.app", "group.*", "group.provider/slot", 'group.test"', "group.test\n", "group..test"]:
            with self.subTest(identifier=identifier), self.assertRaises(ValueError):
                validate_identifier(identifier)
