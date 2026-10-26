"""Protect the live Text boundary; actual ticking still requires WidgetKit on a device."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "native/ControlStudioWidgets/StudioSlotWidget.swift"


def function(source, name):
    start = re.search(r"private func " + name + r"\([^\n]+\{", source)
    if start is None:
        raise AssertionError(f"Missing {name}")
    opening = start.end() - 1
    depth = 1
    end = opening + 1
    while depth:
        if source[end] == "{":
            depth += 1
        elif source[end] == "}":
            depth -= 1
        end += 1
    return source[start.start():end]


class WidgetLiveTextTests(unittest.TestCase):
    def test_fill_is_applied_while_the_receiver_and_return_type_are_text(self):
        source = SOURCE.read_text(encoding="utf-8")
        fill = function(source, "styledText")
        self.assertIn("(_ text: Text, style: WidgetClockStyle) -> Text", fill)
        self.assertIn("options.enabled", fill)
        self.assertIn("return text.foregroundStyle(gradient(options))", fill)
        self.assertIn("return text.foregroundStyle(colour(style))", fill)
        self.assertNotIn("AnyShapeStyle", source)
        self.assertNotIn("AnyView", fill)
        layout = function(source, "textLayer")
        self.assertLess(layout.index("styledText(text, style: style)"), layout.index(".font("))
        self.assertLess(layout.index("styledText(text, style: style)"), layout.index(".lineLimit("))
        self.assertNotIn(".foregroundStyle", layout)

    def test_both_time_and_date_keep_system_managed_sources_and_existing_layout(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("textLayer(liveText(layer.clock)", source)
        self.assertIn("textLayer(Text(.currentDate, format: layer.date.formatStyle())", source)
        self.assertIn("return Text(.currentDate, format: clock.formatStyle())", function(source, "liveText"))
        layout = function(source, "textLayer")
        for modifier in [".minimumScaleFactor(0.01)", ".multilineTextAlignment", ".frame(", ".clipped()", ".opacity(opacity)", ".rotationEffect", ".offset("]:
            self.assertIn(modifier, layout)
        fill = function(source, "styledText")
        for forbidden in ["Date.now", "Date()", ".formatted(", "Image(", ".mask(", ".drawingGroup(", ".compositingGroup(", "TimelineView("]:
            self.assertNotIn(forbidden, fill)

    def test_gradient_keeps_three_saved_stops_and_explicit_direction(self):
        source = SOURCE.read_text(encoding="utf-8")
        gradient = function(source, "gradient")
        self.assertIn("-> LinearGradient", gradient)
        self.assertIn("[options.startHex, options.middleHex, options.endHex]", gradient)
        for index, location in [(0, "0"), (1, "0.5"), (2, "1")]:
            self.assertIn(f"color: colours[{index}], location: {location}", gradient)
        self.assertIn("options.startPoint", gradient)
        self.assertIn("options.endPoint", gradient)
