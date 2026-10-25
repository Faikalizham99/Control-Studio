"""Guard the no-auto-scroll policy where headless MAUI lacks native Loaded handlers."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WidgetEditorScrollPolicyTests(unittest.TestCase):
    def test_settings_and_strips_never_request_programmatic_scrolls(self):
        # A ScrollToRequested event alone cannot catch the previous bug: its
        # IsLoaded/Handler checks skipped the offending code in headless tests.
        for name in ["WidgetEditorPage", "WidgetEditorScrollView", "WidgetEditorToolbar", "WidgetClockControls",
                     "WidgetClockWeightPicker", "WidgetClockFontPicker", "WidgetBlendChoices", "WidgetBlendPanel"]:
            with self.subTest(component=name):
                source = (ROOT / f"src/ControlStudio.App/Views/{name}.cs").read_text(encoding="utf-8")
                self.assertIsNone(re.search(r"\b(?:ScrollToAsync|RequestScrollTo|SetScrolledPosition)\s*\(", source))
