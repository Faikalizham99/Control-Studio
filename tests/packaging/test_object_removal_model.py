"""Verify real model delivery boundaries without copying 61 MB per test fixture."""
import hashlib
import io
import plistlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
import object_removal_model as model


class ObjectRemovalModelTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.payload = b'fixture model, not an executable'
        self.spec = model.SPEC | {'bytes': len(self.payload), 'sha256': hashlib.sha256(self.payload).hexdigest()}

    def test_model_and_licence_configuration_and_host_only_packaging(self):
        model.check()
        import xml.etree.ElementTree as ET
        project = ET.parse(ROOT / 'src/ControlStudio.App/ControlStudio.App.csproj')
        resources = project.findall('.//BundleResource')
        self.assertEqual(1, sum(item.get('LogicalName') == 'aotgan.mlmodel' for item in resources))
        generator = (ROOT / 'scripts/generate-xcode-project.py').read_text()
        self.assertNotIn('aotgan', generator)
        self.assertIn('python3 scripts/object_removal_model.py --fetch', (ROOT / 'scripts/build-native.sh').read_text())

    def test_cached_verified_model_works_without_network_and_corruption_fails(self):
        target = self.root / 'artifacts/native/models' / self.spec['file']; target.parent.mkdir(parents=True)
        target.write_bytes(self.payload)
        with patch.object(model, 'ROOT', self.root), patch.object(model, 'SPEC', self.spec), patch.object(model.urllib.request, 'urlopen') as request:
            model.fetch(); request.assert_not_called()
            target.write_bytes(b'x' * len(self.payload))
            with self.assertRaisesRegex(ValueError, 'checksum'): model.validate_file(target)

    def test_source_validation_cannot_silently_skip_model_or_required_resources(self):
        import xml.etree.ElementTree as ET
        host = self.root / 'src/ControlStudio.App'
        (host / 'Platforms/iOS').mkdir(parents=True)
        original_plist = (ROOT / 'src/ControlStudio.App/Platforms/iOS/Info.plist').read_bytes()
        original_project = (ROOT / 'src/ControlStudio.App/ControlStudio.App.csproj').read_bytes()
        project_path = host / 'ControlStudio.App.csproj'
        plist_path = host / 'Platforms/iOS/Info.plist'
        project_path.write_bytes(original_project)
        info = plistlib.loads(original_plist); del info['ControlStudioObjectRemovalModel']
        plist_path.write_bytes(plistlib.dumps(info))
        with patch.object(model, 'ROOT', self.root), self.assertRaisesRegex(ValueError, 'declaration'):
            model.check()
        plist_path.write_bytes(original_plist)
        for name in ('aotgan.mlmodel', 'LICENSE-AOT-GAN.txt', 'NOTICE-AOT-GAN.txt'):
            with self.subTest(resource=name):
                tree = ET.ElementTree(ET.fromstring(original_project))
                for group in tree.findall('.//ItemGroup'):
                    for item in list(group):
                        if item.get('LogicalName') == name: group.remove(item)
                tree.write(project_path)
                with patch.object(model, 'ROOT', self.root), self.assertRaisesRegex(ValueError, 'resource'):
                    model.check()

    def test_oversized_and_invalid_downloads_never_install_partial_model(self):
        for payload in (self.payload + b'overflow', b'x' * len(self.payload)):
            with self.subTest(size=len(payload)), patch.object(model, 'ROOT', self.root), patch.object(model, 'SPEC', self.spec), patch.object(model.urllib.request, 'urlopen', return_value=io.BytesIO(payload)):
                with self.assertRaises(ValueError): model.fetch()
                self.assertFalse((self.root / 'artifacts/native/models' / self.spec['file']).exists())
                self.assertFalse(list(self.root.rglob('*.download')))

    def test_truncated_download_retries_and_can_recover_without_partial_install(self):
        with patch.object(model, 'ROOT', self.root), patch.object(model, 'SPEC', self.spec), patch.object(model.urllib.request, 'urlopen', side_effect=[io.BytesIO(self.payload[:-1]), io.BytesIO(self.payload)]) as request, patch.object(model.time, 'sleep'):
            model.fetch()
            self.assertEqual(2, request.call_count)
            self.assertEqual(self.payload, (self.root / 'artifacts/native/models' / self.spec['file']).read_bytes())
            self.assertFalse(list(self.root.rglob('*.download')))

    def test_truncated_download_exhaustion_leaves_no_install_or_temporary_file(self):
        with patch.object(model, 'ROOT', self.root), patch.object(model, 'SPEC', self.spec), patch.object(model.urllib.request, 'urlopen', side_effect=lambda *a, **k: io.BytesIO(self.payload[:-1])) as request, patch.object(model.time, 'sleep'):
            with self.assertRaises(OSError): model.fetch()
            self.assertEqual(3, request.call_count)
            self.assertFalse((self.root / 'artifacts/native/models' / self.spec['file']).exists())
            self.assertFalse(list(self.root.rglob('*.download')))

    def test_download_is_verified_then_installed_and_bundle_requires_attribution(self):
        with patch.object(model, 'ROOT', self.root), patch.object(model, 'SPEC', self.spec), patch.object(model.urllib.request, 'urlopen', return_value=io.BytesIO(self.payload)):
            model.fetch()
            bundle = self.root / 'artifacts/native/models'
            (bundle / self.spec['license']).write_bytes((model.FOLDER / self.spec['license']).read_bytes())
            (bundle / 'NOTICE-AOT-GAN.txt').write_bytes((model.FOLDER / 'NOTICE.txt').read_bytes())
            model.validate_bundled_model(bundle)
            (bundle / 'NOTICE-AOT-GAN.txt').write_text('changed')
            with self.assertRaisesRegex(ValueError, 'attribution'): model.validate_bundled_model(bundle)
