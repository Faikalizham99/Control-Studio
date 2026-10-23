"""Reject dynamic gallery labels that can assert inside WidgetKit at startup.

This is a narrow source policy, not a Swift compiler or a runtime widget test.
Gallery names/descriptions use ordinary string literals; dynamic Text inside
the widget's content is unaffected.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
TOKEN = re.compile(
    r'//[^\n]*|/\*.*?\*/|#+""".*?"""#+|""".*?"""|'
    r'#+"(?:\\.|[^"\\])*"#+|"(?:\\.|[^"\\])*"|[A-Za-z_]\w*|[^\s]',
    re.DOTALL,
)
LITERAL = re.compile(r'"(?:\\.|[^"\\])*"\Z', re.DOTALL)
MODIFIERS = {"configurationDisplayName", "description"}


def metadata_errors(source: str, label: str) -> list[str]:
    tokens = [m for m in TOKEN.finditer(source)
              if not m.group().startswith(("//", "/*"))]
    errors = []
    for i in range(len(tokens) - 2):
        if (tokens[i].group() != "." or tokens[i+1].group() not in MODIFIERS
                or tokens[i+2].group() != "("):
            continue
        argument = tokens[i+3].group() if i+3 < len(tokens) else ""
        end = tokens[i+4].group() if i+4 < len(tokens) else ""
        if (not LITERAL.fullmatch(argument) or argument.startswith('"""')
                or r"\(" in argument or end != ")"):
            line = source.count("\n", 0, tokens[i].start()) + 1
            errors.append(f"{label}:{line}: {tokens[i+1].group()} must use a fixed "
                          "string literal without interpolation; WidgetKit gallery "
                          "metadata can assert while registering the extension.")
    return errors


def validate_widget_metadata():
    errors = []
    for path in sorted((ROOT / "native/ControlStudioWidgets").rglob("*.swift")):
        errors.extend(metadata_errors(path.read_text(encoding="utf-8"),
                                      path.relative_to(ROOT).as_posix()))
    if errors:
        raise ValueError("\n".join(errors))


if __name__ == "__main__":
    validate_widget_metadata()
    print("Widget gallery names and descriptions use fixed string literals.")
