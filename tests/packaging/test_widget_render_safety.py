"""Native rendering guards; these do not claim to execute Core Image on Windows."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
IOS = ROOT / "src/ControlStudio.App/Platforms/iOS"


class WidgetRenderSafetyTests(unittest.TestCase):
    def test_no_renderer_uses_the_unmapped_five_argument_ciformat_overload(self):
        for path in IOS.glob("*.cs"):
            with self.subTest(source=path.name):
                source = path.read_text(encoding="utf-8")
                self.assertIsNone(re.search(r"CreateCGImage\([^;]*\bCIFormat\.\w+\s*,\s*\w+\s*,\s*(?:true|false)\s*\)", source))

    def test_widget_snapshot_has_explicit_eight_bit_independent_storage(self):
        source = (IOS / "WidgetPhotoCompositor.cs").read_text(encoding="utf-8")
        self.assertIn("context.CreateCGImage(input, bounds, CIFormat.Rgba8, colorSpace)", source)
        self.assertIn("new CGBitmapContext(IntPtr.Zero, width, height, 8, checked(width * 4)", source)
        self.assertIn("CGBitmapFlags.ByteOrder32Big | CGBitmapFlags.PremultipliedLast", source)
        self.assertIn("pixels.DrawImage", source)
        self.assertIn("pixels.ToImage()", source)
        self.assertIn("images.Borrow(Rasterize(", source)
        self.assertIn("images.Checkpoint(Flatten(", source)
        self.assertIn("images.Detach(group)", source)
        self.assertNotIn("using var blended", source)
        self.assertNotIn("finally { group.Dispose(); }", source)

    def test_related_native_paths_share_identity_aware_ownership(self):
        for name in ["AppleWidgetBlendSamples.cs", "ClipboardBlurRenderer.cs"]:
            source = (IOS / name).read_text(encoding="utf-8")
            self.assertIn("new ImageRenderBatch<CIImage>()", source)
            self.assertNotIn("using var cropped", source)
            self.assertNotIn("using var blended", source)
        handler = (IOS / "WidgetCompositeHandler.cs").read_text(encoding="utf-8")
        self.assertIn("compositor?.Stage", handler)
        self.assertIn("WidgetRenderFailure.Describe", handler)
        self.assertNotIn("Try changing the layer or choosing the photo again.", handler)
