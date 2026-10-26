"""Keep the native and managed battery label paths connected to the shared layout."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class BatteryLabelLayoutTests(unittest.TestCase):
    def test_native_renderer_draws_a_single_centred_label_with_contrast_outline(self):
        source = (ROOT / "native/ControlStudioWidgets/StudioSlotWidget.swift").read_text(encoding="utf-8")
        self.assertIn("style.labelLayout", source)
        self.assertIn("style.usesWhiteLabel(reading)", source)
        self.assertIn("style.shape == \"percentage\" || style.showPercentage || !reading.known", source)
        self.assertIn("rect(12, 18, 136, 30, 15, track)", source)
        self.assertNotIn("text(reading.text, 20, 68", source)
        self.assertIn("let centre = CGPoint(x: box.x + box.width / 2, y: box.y + box.height / 2)", source)

    def test_managed_preview_and_export_share_label_geometry_and_group_opacity(self):
        managed = (ROOT / "src/ControlStudio.App/Views/WidgetBatteryGlyph.cs").read_text(encoding="utf-8")
        native = (ROOT / "src/ControlStudio.App/Platforms/iOS/AppleWidgetBatteryDrawing.cs").read_text(encoding="utf-8")
        self.assertIn("var box = LabelBox(options.Shape)", managed)
        self.assertIn("AppearanceColors.ButtonForeground(colour)", managed)
        self.assertNotIn("reading.Text, 20, 68", managed)
        self.assertIn("WidgetBatteryDrawing.Draw(new Canvas(), layer.Battery!, reading)", native)
        self.assertIn("graphics.SetAlpha((nfloat)layer.Opacity); graphics.BeginTransparencyLayer()", native)
        self.assertIn("attributes.ForegroundColor = tint", native)
