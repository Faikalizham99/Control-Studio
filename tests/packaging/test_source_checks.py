"""Read-only validation must preserve developer files, including stale outputs."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


def load_script(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SourceCheckTests(unittest.TestCase):
    def test_current_native_project_check_does_not_write(self):
        generator = load_script("generate-xcode-project")
        with tempfile.TemporaryDirectory() as temporary:
            generator.ROOT = Path(temporary)
            expected = generator.generate()
            output = generator.ROOT / "native/ControlStudioNative.xcodeproj/project.pbxproj"
            content, stamp = output.read_bytes(), output.stat().st_mtime_ns
            self.assertEqual(expected, generator.generate(check=True))
            self.assertEqual(content, output.read_bytes())
            self.assertEqual(stamp, output.stat().st_mtime_ns)

    def test_stale_or_missing_project_check_preserves_original(self):
        generator = load_script("generate-xcode-project")
        with tempfile.TemporaryDirectory() as temporary:
            generator.ROOT = Path(temporary)
            output = generator.ROOT / "native/ControlStudioNative.xcodeproj/project.pbxproj"
            with self.assertRaisesRegex(ValueError, "out of date"):
                generator.generate(check=True)
            self.assertFalse(output.parent.exists())
            output.parent.mkdir(parents=True)
            original = b"developer changes must survive a failed check\r\n"
            output.write_bytes(original)
            stamp = output.stat().st_mtime_ns
            with self.assertRaisesRegex(ValueError, "out of date"):
                generator.generate(check=True)
            self.assertEqual(original, output.read_bytes())
            self.assertEqual(stamp, output.stat().st_mtime_ns)

    def test_source_enumeration_prunes_generated_and_local_tool_trees(self):
        checker = load_script("check-source")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            expected = {"Directory.Build.props", "src/App/App.csproj", "tests/Unit/Unit.csproj"}
            ignored = {".tools/sdk/tool.csproj", "src/App/obj/generated.csproj", "artifacts/export.csproj",
                       "native/.build/tool.csproj", ".tmp/scratch.csproj", ".git/fixture.csproj", "tests/Unit/bin/test.csproj"}
            for relative in expected | ignored:
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("invalid XML if accidentally parsed")
            actual = {path.relative_to(root).as_posix() for path in checker.source_files(root, {".csproj", ".props"})}
            self.assertEqual(expected, actual)
