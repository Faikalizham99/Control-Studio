"""Protect the UIKit invariant that headless managed tests cannot exercise."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("gesture_ownership", ROOT / "scripts/check_gesture_ownership.py")
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)


class GestureOwnershipTests(unittest.TestCase):
    def test_rejects_scroll_view_callbacks_including_teardown_and_multiline(self):
        for expression in ["page.PanGestureRecognizer.ShouldBegin = _ => true;",
                           "page.PanGestureRecognizer.ShouldBegin = null;",
                           "zoom.PinchGestureRecognizer.Delegate = custom;",
                           "page.PanGestureRecognizer\n .\n ShouldReceiveTouch += HandleTouch;",
                           "page.PanGestureRecognizer.WeakDelegate = custom;"]:
            with self.subTest(expression=expression):
                with self.assertRaisesRegex(ValueError, "built-in gesture delegate"):
                    checker.validate_builtin_gesture_ownership(expression)

    def test_allows_app_owned_callbacks_scroll_configuration_and_view_override(self):
        for expression in ["pan.ShouldBegin = _ => Begin(pan, false);",
                           "zoom.PanGestureRecognizer.MinimumNumberOfTouches = 2;",
                           "public override bool GestureRecognizerShouldBegin(UIGestureRecognizer gesture) { return base.GestureRecognizerShouldBegin(gesture); }"]:
            with self.subTest(expression=expression):
                checker.validate_builtin_gesture_ownership(expression)

    def test_current_native_components_preserve_ownership(self):
        for path in (ROOT / "src/ControlStudio.App/Platforms/iOS").glob("*.cs"):
            with self.subTest(component=path.name):
                checker.validate_builtin_gesture_ownership(path.read_text(encoding="utf-8"), path.name)
