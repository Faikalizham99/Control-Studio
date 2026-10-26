"""Guard the no-auto-scroll policy where headless MAUI lacks native Loaded handlers."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WidgetEditorScrollPolicyTests(unittest.TestCase):
    def test_settings_and_strips_never_request_programmatic_scrolls(self):
        # A ScrollToRequested event alone cannot catch the previous bug: its
        # IsLoaded/Handler checks skipped the offending code in headless tests.
        for name in ["WidgetEditorPage", "WidgetEditorForm", "WidgetLayerRow", "WidgetLayerList", "WidgetEditorToolbar", "WidgetClockControls",
                     "WidgetClockWeightPicker", "WidgetClockFontPicker", "WidgetDateFormatPicker", "WidgetLayerNudgeControls", "WidgetBlendChoices", "WidgetBlendPanel"]:
            with self.subTest(component=name):
                source = (ROOT / f"src/ControlStudio.App/Views/{name}.cs").read_text(encoding="utf-8")
                self.assertIsNone(re.search(r"\b(?:ScrollToAsync|RequestScrollTo|SetScrolledPosition)\s*\(", source))

    def test_only_numeric_nonanimated_anchor_compensation_is_allowed(self):
        source = (ROOT / "src/ControlStudio.App/Views/WidgetEditorScrollView.cs").read_text(encoding="utf-8")
        self.assertEqual(1, len(re.findall(r"\bRequestScrollTo\s*\(", source)))
        self.assertIn("RequestScrollTo(ScrollX, target, true)", source)
        self.assertIsNone(re.search(r"\bScrollToAsync\s*\(", source))
        self.assertNotIn("ScrollToPosition.", source)
