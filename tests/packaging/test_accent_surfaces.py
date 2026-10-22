"""Keep future primary controls on the shared fill path rather than restyling cards."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class AccentSurfacePolicyTests(unittest.TestCase):
    def test_only_shared_primary_controls_bind_primary_background_resources(self):
        allowed = {
            ("Views/AccentButton.cs", "BackgroundColorProperty", "AccentButton"),
            ("Views/AccentSurface.cs", "BackgroundProperty", "AccentButtonBrush"),
        }
        pattern = re.compile(r'\bSetDynamicResource\s*\(\s*(?:\w+\.)?(Background(?:Color)?Property)\s*,\s*"(AccentButton(?:Brush)?)"\s*\)')
        found = set()
        app = ROOT / "src/ControlStudio.App"
        for path in app.rglob("*.cs"):
            if {"bin", "obj"}.intersection(path.relative_to(app).parts):
                continue
            for match in pattern.finditer(path.read_text(encoding="utf-8-sig")):
                binding = (path.relative_to(app).as_posix(), *match.groups())
                self.assertIn(binding, allowed, "Use AccentButton or AccentSurface for primary actions; never overlay an accent colour on a themed card brush.")
                found.add(binding)
        self.assertEqual(allowed, found)


if __name__ == "__main__":
    unittest.main()
