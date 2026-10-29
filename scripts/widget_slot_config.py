"""Generate bounded slot definitions from one configuration; --check never writes.

Concrete Swift widget types and literal gallery names avoid the reconstruction
and interpolated-metadata failures documented in docs/widget-studio.md.
"""
from pathlib import Path
import argparse
import json
import re

ROOT = Path(__file__).resolve().parents[1]


def configuration(root=ROOT):
    config = json.loads((root / "contracts/widget-slot-config.json").read_text(encoding="utf-8"))
    if set(config) != {"slotsPerSize", "storedSizeCount", "maximumCatalogBytes"}:
        raise ValueError("Unknown or missing widget slot configuration fields")
    if any(type(value) is not int for value in config.values()):
        raise ValueError("Widget slot limits must be integers")
    # Groups of five keep the root bundle below legacy builder overload limits.
    if not 1 <= config["slotsPerSize"] <= 40 or config["storedSizeCount"] != 5:
        raise ValueError("Unsupported slot range or stored size count (includes retired iPad size)")
    if config["maximumCatalogBytes"] != 16384:
        raise ValueError("Review catalog payload tests before changing the byte bound")
    worst_case = {"schemaVersion": 1, "slots": [
        {"size": "extraLargePortrait", "number": number, "designId": "f" * 36}
        for _ in range(config["storedSizeCount"]) for number in range(1, config["slotsPerSize"] + 1)
    ]}
    if len(json.dumps(worst_case, separators=(",", ":")).encode("utf-8")) > config["maximumCatalogBytes"]:
        raise ValueError("Configured slot capacity exceeds the bounded catalog budget")
    return config


def number_name(number):
    # Preserve the existing concrete type names for slots 1-10 and their groups.
    names = ("Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
             "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen")
    if number < 20:
        return names[number]
    tens = {20: "Twenty", 30: "Thirty", 40: "Forty"}
    return tens[number // 10 * 10] + (names[number % 10] if number % 10 else "")


def generated_sections(config):
    count = config["slotsPerSize"]
    capacity = count * config["storedSizeCount"]
    bound = config["maximumCatalogBytes"]
    types, groups, registrations = [], [], []
    for number in range(1, count + 1):
        types.append(f'''struct StudioSlot{number_name(number)}Widget: Widget {{
    var body: some WidgetConfiguration {{
        StudioSlotConfiguration.make(slot: {number}).configurationDisplayName("Studio {number}")
    }}
}}''')
    for start in range(1, count + 1, 5):
        end = min(start + 4, count)
        name = f"StudioSlots{number_name(start)}To{number_name(end)}"
        members = "\n".join(f"        StudioSlot{number_name(number)}Widget()" for number in range(start, end + 1))
        groups.append(f"struct {name}: WidgetBundle {{\n    var body: some Widget {{\n{members}\n    }}\n}}")
        registrations.append(f"        {name}().body")
    kinds = ",\n".join(f'            @"ControlStudio.Slot.{number}"' for number in range(1, count + 1))
    return {
        "src/ControlStudio.Core/Widgets/WidgetSlots.cs": ("limits", f"    public const int SlotsPerSize = {count}, MaximumAssignments = {capacity}, MaximumCatalogBytes = {bound};"),
        "native/Shared/WidgetDesignStore.swift": ("limits", f"    static let slotsPerSize = {count}\n    static let maximumSlotAssignments = {capacity}\n    static let maximumSlotCatalogBytes = {bound}"),
        "native/WidgetRuntime/CSWidgetBackgroundAdapter.m": ("kinds", f"        kinds = [NSSet setWithArray:@[\n{kinds}\n        ]];"),
        "native/ControlStudioWidgets/StudioSlotWidget.swift": ("widgets", "\n\n".join(types + groups)),
        "native/ControlStudioWidgets/ControlStudioWidgets.swift": ("bundle", "\n".join(registrations)),
    }


def synchronize(*, check=False, root=ROOT):
    planned = []
    for name, (section, body) in generated_sections(configuration(root)).items():
        path = root / name
        source = path.read_text(encoding="utf-8")
        begin = f"// BEGIN GENERATED SLOTS {section} (scripts/widget_slot_config.py)"
        end = f"// END GENERATED SLOTS {section}"
        pattern = re.compile(re.escape(begin) + r"\n.*?\n" + re.escape(end), re.DOTALL)
        if source.count(begin) != 1 or source.count(end) != 1 or len(pattern.findall(source)) != 1:
            raise ValueError(f"Missing/duplicate generated slot markers in {name}")
        updated = pattern.sub(lambda _: begin + "\n" + body + "\n" + end, source)
        if updated != source:
            planned.append((path, updated))
    # Check every output before writing any; check mode is strictly read-only.
    if check and planned:
        raise ValueError("Stale widget slot definitions: " + ", ".join(str(path.relative_to(root)) for path, _ in planned))
    for path, updated in planned:
        path.write_text(updated, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    synchronize(check=parser.parse_args().check)
    print("Widget slot definitions are synchronized.")
