"""Guard direct UIKit width/height anchor calls against misleading overloads.

Dimension overloads use (anchor, multiplier, constant); position anchors use
(anchor, constant). Require named numeric arguments for multi-argument dimension
calls, and reject known non-positive/non-finite multipliers. This checks the
direct source pattern, not aliased anchors or the full C# type system.
"""
import math
import re

DIMENSION_CALL = re.compile(
    r"\b(?:WidthAnchor|HeightAnchor)\s*\.\s*"
    r"Constraint(?:EqualTo|GreaterThanOrEqualTo|LessThanOrEqualTo)\s*\("
)
COMMENTS_AND_LITERALS = re.compile(
    r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\r\n]*|/\*[\s\S]*?\*/'
)
NUMBER = re.compile(r"[+-]?(?:\d[\d_]*(?:\.[\d_]*)?|\.\d[\d_]*)(?:[eE][+-]?\d[\d_]*)?[fFdDmM]?")


def _arguments(source, start):
    depth, mark, arguments = 0, start, []
    for index in range(start, len(source)):
        char = source[index]
        if char == ")" and depth == 0:
            arguments.append(source[mark:index].strip())
            return arguments
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        elif char == "," and depth == 0:
            arguments.append(source[mark:index].strip())
            mark = index + 1
    raise ValueError("Unclosed dimension constraint call")


def validate_native_dimension_constraints(source, name="source"):
    # Preserve offsets/newlines for useful diagnostics; don't inspect example
    # code inside ordinary C# comments and string/character literals.
    code = COMMENTS_AND_LITERALS.sub(lambda match: re.sub(r"[^\r\n]", " ", match.group()), source)
    for call in DIMENSION_CALL.finditer(code):
        arguments = _arguments(code, call.end())
        if len(arguments) <= 1:
            continue  # Constant dimensions and plain anchor equality have no ambiguity.
        named = {}
        for argument in arguments:
            match = re.match(r"^(\w+)\s*:\s*(.+)$", argument, re.S)
            if match:
                named[match[1]] = match[2].strip()
        location = f"{name}:{code.count(chr(10), 0, call.start()) + 1}"
        if "multiplier" not in named or (len(arguments) >= 3 and "constant" not in named):
            raise ValueError(f"{location}: dimension constraints require named multiplier: and constant: arguments; the second argument is not padding")
        multiplier = named["multiplier"]
        if re.search(r"\b(?:NaN|PositiveInfinity|NegativeInfinity)\b", multiplier):
            raise ValueError(f"{location}: dimension multiplier must be finite and positive")
        if NUMBER.fullmatch(multiplier):
            value = float(multiplier.replace("_", "").rstrip("fFdDmM"))
            if not math.isfinite(value) or value <= 0:
                raise ValueError(f"{location}: dimension multiplier must be finite and positive")
