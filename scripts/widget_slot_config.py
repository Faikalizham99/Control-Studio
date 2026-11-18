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
    if set(config) != {"slotsPerSize", "extraLargePortraitSlots", "storedSizeCount", "maximumCatalogBytes"}:
        raise ValueError("Unknown or missing widget slot configuration fields")
    if any(type(value) is not int for value in config.values()):
        raise ValueError("Widget slot limits must be integers")
    # Groups of five keep the root bundle below legacy builder overload limits.
    if not 1 <= config["slotsPerSize"] <= config["extraLargePortraitSlots"] <= 40 or config["storedSizeCount"] != 5:
        raise ValueError("Unsupported slot range or stored size count (includes retired iPad size)")
    if config["maximumCatalogBytes"] != 16384:
        raise ValueError("Review catalog payload tests before changing the byte bound")
    worst_case = {"schemaVersion": 1, "slots": [
        {"size": "extraLargePortrait", "number": number, "designId": "f" * 36}
        for count in [config["slotsPerSize"]] * (config["storedSizeCount"] - 1) + [config["extraLargePortraitSlots"]]
        for number in range(1, count + 1)
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
    extra = config["extraLargePortraitSlots"]
    capacity = count * (config["storedSizeCount"] - 1) + extra
    bound = config["maximumCatalogBytes"]
    types, groups = [], []
    for number in range(1, extra + 1):
        available = '@available(iOS 27.0, *)\n' if number > count else ''
        maker = 'makeExtraLarge' if number > count else 'make'
        types.append(f'''{available}struct StudioSlot{number_name(number)}Widget: Widget {{
    var body: some WidgetConfiguration {{
        StudioSlotConfiguration.{maker}(slot: {number}).configurationDisplayName("Studio {number}")
    }}
}}''')
    def bundle(name, members, available=False):
        annotation = '@available(iOS 27.0, *)\n' if available else ''
        body = "\n".join('        ' + member for member in members)
        groups.append(f"{annotation}struct {name}: WidgetBundle {{\n    var body: some Widget {{\n{body}\n    }}\n}}")
        return name + '().body'

    def subtree(name, members, available=False):
        # Every level, including the root, stays within five builder expressions.
        # Existing concrete widget types/kinds are never renamed by this tree.
        level = 0
        while len(members) > 5:
            level += 1
            members = [bundle(f'{name}Level{level}Group{number_name(i // 5 + 1)}', members[i:i+5], available)
                       for i in range(0, len(members), 5)]
        return bundle(name, members, available)

    def range_groups(first, last, available=False):
        result = []
        for start in range(first, last + 1, 5):
            end = min(start + 4, last)
            name = f"StudioSlots{number_name(start)}To{number_name(end)}"
            result.append(bundle(name, [f'StudioSlot{number_name(number)}Widget()' for number in range(start, end + 1)], available))
        return result

    standard = subtree('StudioStandardSlots', range_groups(1, count))
    members = [standard]
    if extra > count:
        xl = subtree('StudioExtraLargeSlots', range_groups(count + 1, extra, True), True)
        members.append(f'if #available(iOS 27.0, *) {{\n            {xl}\n        }}')
    root = bundle('StudioRegisteredSlots', members)
    kinds = ",\n".join(f'            @"ControlStudio.Slot.{number}"' for number in range(1, extra + 1))
    return {
        "src/ControlStudio.Core/Widgets/WidgetSlots.cs": ("limits", f"""    public const int SlotsPerSize = {count}, ExtraLargePortraitSlots = {extra}, MaximumSlotNumber = {extra}, MaximumAssignments = {capacity}, MaximumCatalogBytes = {bound};
    public static int SlotsFor(WidgetSize size) => size switch
    {{
        WidgetSize.ExtraLargePortrait => ExtraLargePortraitSlots,
        WidgetSize.Small or WidgetSize.Medium or WidgetSize.Large or WidgetSize.ExtraLarge => SlotsPerSize,
        _ => throw new IOException("Choose a supported widget size.")
    }};"""),
        "native/Shared/WidgetDesignStore.swift": ("limits", f"    static let slotsPerSize = {count}\n    static let extraLargePortraitSlots = {extra}\n    static let maximumSlotNumber = {extra}\n    static let maximumSlotAssignments = {capacity}\n    static let maximumSlotCatalogBytes = {bound}\n    static func slots(for size: WidgetDesignSize) -> Int {{ size == .extraLargePortrait ? extraLargePortraitSlots : slotsPerSize }}"),
        "native/WidgetRuntime/CSWidgetBackgroundAdapter.m": ("kinds", f"        kinds = [NSSet setWithArray:@[\n{kinds}\n        ]];"),
        "native/ControlStudioWidgets/StudioSlotWidget.swift": ("widgets", "\n\n".join(types + groups)),
        "native/ControlStudioWidgets/ControlStudioWidgets.swift": ("bundle", "        " + root),
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
