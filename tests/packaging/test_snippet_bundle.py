import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("clang_bundle", ROOT / "scripts/bundle-clang.py")
bundle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bundle)


class SnippetBundleTests(unittest.TestCase):
    def test_checked_in_adapter_matches_pinned_vendor_and_source(self):
        self.assertEqual((bundle.ASSETS / "clang-formatter.js").read_text(encoding="utf-8"), bundle.bundle())

    def test_generated_classic_script_has_no_module_imports_or_javascript_eval(self):
        source = bundle.bundle()
        self.assertNotIn("new Function", source)
        self.assertNotIn("eval(", source)
        self.assertNotIn("fetch(", source)
