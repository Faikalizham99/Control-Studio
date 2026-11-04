"""Reject the overload error even though it compiles successfully for iOS."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("native_layout", ROOT / "scripts/check_native_layout.py")
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)


class NativeLayoutTests(unittest.TestCase):
    def test_rejects_old_cutout_width_error_and_unnamed_dimension_arguments(self):
        for source in ["stack.WidthAnchor.ConstraintEqualTo(page.FrameLayoutGuide.WidthAnchor, -32)",
                       "view.HeightAnchor.ConstraintEqualTo(other.HeightAnchor, .5)",
                       "view.WidthAnchor.ConstraintGreaterThanOrEqualTo(other.WidthAnchor, 1, -20)",
                       "view.HeightAnchor.ConstraintLessThanOrEqualTo(\nother.HeightAnchor,\n1, constant: -20)",
                       "view.WidthAnchor.ConstraintEqualTo(other.WidthAnchor, multiplier: 1, -32)"]:
            with self.subTest(source=source), self.assertRaisesRegex(ValueError, "named multiplier:"):
                checker.validate_native_dimension_constraints(source)

    def test_rejects_explicit_non_positive_and_non_finite_multipliers(self):
        for multiplier in ["0", "-32", "-.5f", "-0.0", "double.NaN", "float.PositiveInfinity", "1e999"]:
            with self.subTest(multiplier=multiplier), self.assertRaisesRegex(ValueError, "finite and positive"):
                checker.validate_native_dimension_constraints(f"view.WidthAnchor.ConstraintEqualTo(other.WidthAnchor, multiplier: {multiplier}, constant: -32)")

    def test_allows_named_padding_proportions_and_unambiguous_dimensions(self):
        for source in ["stack.WidthAnchor.ConstraintEqualTo(page.FrameLayoutGuide.WidthAnchor, multiplier: 1, constant: -32)",
                       "view.WidthAnchor.ConstraintEqualTo(other.WidthAnchor, multiplier: .5)",
                       "view.HeightAnchor.ConstraintLessThanOrEqualTo(other.HeightAnchor, multiplier: 1, constant: -10)",
                       "view.WidthAnchor.ConstraintGreaterThanOrEqualTo(anchor: width, multiplier: 1.0f)",
                       "view.WidthAnchor.ConstraintEqualTo(anchor: width, constant: -32, multiplier: 1)",
                       "view.WidthAnchor.ConstraintEqualTo(GetAnchor(1, 2), multiplier: 1, constant: -GetPadding(16, 16))",
                       "view.WidthAnchor.ConstraintEqualTo(width)",
                       "view.HeightAnchor.ConstraintGreaterThanOrEqualTo(44)"]:
            with self.subTest(source=source):
                checker.validate_native_dimension_constraints(source)

    def test_position_offsets_and_comments_do_not_trigger_dimension_policy(self):
        checker.validate_native_dimension_constraints('''
            view.LeadingAnchor.ConstraintEqualTo(other.LeadingAnchor, 16);
            view.BottomAnchor.ConstraintEqualTo(other.BottomAnchor, -20);
            // view.WidthAnchor.ConstraintEqualTo(width, -32);
            /* view.WidthAnchor.ConstraintEqualTo(width, -32); */
            var text = "view.WidthAnchor.ConstraintEqualTo(width, -32)";
            var verbatim = @"view.WidthAnchor.ConstraintEqualTo(width, -32)";
        ''')

    def test_multi_line_comments_preserve_arguments_and_error_location(self):
        checker.validate_native_dimension_constraints("view.WidthAnchor /* gap */ .ConstraintEqualTo(width, multiplier: /* gap */ 1, constant: -32)")
        with self.assertRaisesRegex(ValueError, "Example.cs:3:"):
            checker.validate_native_dimension_constraints("// comment\n\nview.WidthAnchor.ConstraintEqualTo(width, -32)", "Example.cs")

    def test_current_native_components_pass_and_reverting_fix_is_rejected(self):
        for path in (ROOT / "src/ControlStudio.App/Platforms/iOS").glob("*.cs"):
            with self.subTest(component=path.name):
                checker.validate_native_dimension_constraints(path.read_text(encoding="utf-8"), path.name)
        source = (ROOT / "src/ControlStudio.App/Platforms/iOS/PhotoCutoutEditor.cs").read_text(encoding="utf-8")
        self.assertIn("multiplier: 1, constant: -32", source)
        reverted = source.replace("multiplier: 1, constant: -32", "-32")
        with self.assertRaisesRegex(ValueError, "second argument is not padding"):
            checker.validate_native_dimension_constraints(reverted)
