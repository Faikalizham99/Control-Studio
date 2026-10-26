"""The managed editor and native widget must agree on the shared layer contract."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WidgetLayerLimitContractTests(unittest.TestCase):
    def test_editor_catalog_and_scene_use_the_same_limit(self):
        core = (ROOT / "src/ControlStudio.Core/Widgets/WidgetDesign.cs").read_text(encoding="utf-8")
        native = (ROOT / "native/Shared/WidgetDesignStore.swift").read_text(encoding="utf-8")
        scene = (ROOT / "native/Shared/WidgetClockScene.swift").read_text(encoding="utf-8")
        managed_limit = int(re.search(r"MaximumLayers\s*=\s*(\d+)", core).group(1))
        native_limit = int(re.search(r"maximumLayers\s*=\s*(\d+)", native).group(1))
        self.assertEqual(30, managed_limit)
        self.assertEqual(managed_limit, native_limit)
        self.assertIn("(design.layers?.count ?? 0) <= Self.maximumLayers", native)
        self.assertIn("parts.count <= WidgetDesignStore.maximumLayers", scene)
