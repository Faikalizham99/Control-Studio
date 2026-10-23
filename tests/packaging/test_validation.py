import datetime as dt
import json
from pathlib import Path
import plistlib
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
from validate_ipa import BUNDLES, GROUP, extract_safely, validate_metadata, validate_profile


class PackagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.app = self.root / "Payload/ControlStudio.app"
        for relative, (identifier, point) in BUNDLES.items():
            bundle = self.app / relative
            bundle.mkdir(parents=True)
            info = dict(CFBundleIdentifier=identifier, CFBundleExecutable="Executable", CFBundleName="Control Studio",
                        CFBundleShortVersionString="0.1.0", CFBundleVersion="1", MinimumOSVersion="17.0",
                        CFBundleSupportedPlatforms=["iPhoneOS"], CFBundlePackageType="XPC!" if relative else "APPL",
                        ControlStudioAppGroup=GROUP, UIDeviceFamily=[1, 2])
            if not relative:
                info["UIApplicationSceneManifest"] = {"UIApplicationSupportsMultipleScenes": False, "UISceneConfigurations": {
                    "UIWindowSceneSessionRoleApplication": [{"UISceneConfigurationName": "__MAUI_DEFAULT_SCENE_CONFIGURATION__",
                                                           "UISceneDelegateClassName": "SceneDelegate"}]}}
            if point:
                if relative.startswith("Extensions/"):
                    info["EXAppExtensionAttributes"] = {"EXExtensionPointIdentifier": point}
                else:
                    info["NSExtension"] = {"NSExtensionPointIdentifier": point}
            (bundle / "Info.plist").write_bytes(plistlib.dumps(info))
            (bundle / "Executable").write_bytes(b"test fixture only - not an actual executable")
            (bundle / "PrivacyInfo.xcprivacy").write_bytes(plistlib.dumps({"NSPrivacyAccessedAPITypes": [
                {"NSPrivacyAccessedAPIType": "NSPrivacyAccessedAPICategoryFileTimestamp", "NSPrivacyAccessedAPITypeReasons": ["C617.1"]}
            ]}))
        metadata = self.app / "Extensions/ControlStudioIntents.appex/Metadata.appintents"
        metadata.mkdir()
        (metadata / "extract.actionsdata").write_text(json.dumps({"id": "IncrementControlStudioCounter", "title": "Increment Control Studio Counter"}))

    def mutate(self, relative, key, value):
        path = self.app / relative / "Info.plist"
        info = plistlib.loads(path.read_bytes())
        info[key] = value
        path.write_bytes(plistlib.dumps(info))

    def profile(self):
        return {
            "ExpirationDate": dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=1),
            "TeamIdentifier": ["TEAM123456"], "ApplicationIdentifierPrefix": ["PREFIX1234"],
            "ProvisionedDevices": ["test-device"], "DeveloperCertificates": [b"fixture"],
            "Entitlements": {"application-identifier": "PREFIX1234.com.faikalizham.controlstudio",
                             "com.apple.developer.team-identifier": "TEAM123456",
                             "com.apple.security.application-groups": [GROUP]},
        }

    def test_accepts_expected_structure_only(self):
        self.assertEqual(len(validate_metadata(self.app)), 3)

    def test_rejects_missing_or_misconfigured_scene_lifecycle(self):
        for manifest in [{}, {"UIApplicationSupportsMultipleScenes": True},
                         {"UIApplicationSupportsMultipleScenes": False, "UISceneConfigurations": {
                             "UIWindowSceneSessionRoleApplication": [{"UISceneConfigurationName": "WrongName", "UISceneDelegateClassName": "SceneDelegate"}]}}]:
            with self.subTest(manifest=manifest):
                self.mutate("", "UIApplicationSceneManifest", manifest)
                with self.assertRaisesRegex(ValueError, "scene"):
                    validate_metadata(self.app)

    def test_rejects_bundle_id_change(self):
        self.mutate("PlugIns/ControlStudioWidgets.appex", "CFBundleIdentifier", "wrong.id")
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_extension_configured_for_different_app_group(self):
        self.mutate("PlugIns/ControlStudioWidgets.appex", "ControlStudioAppGroup", "group.other.app")
        with self.assertRaisesRegex(ValueError, "Configured App Group mismatch"): validate_metadata(self.app)

    def test_rejects_missing_group_configuration(self):
        path = self.app / "Info.plist"
        info = plistlib.loads(path.read_bytes())
        del info["ControlStudioAppGroup"]
        path.write_bytes(plistlib.dumps(info))
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_wrong_extension_point(self):
        self.mutate("PlugIns/ControlStudioWidgets.appex", "NSExtension", {"NSExtensionPointIdentifier": "wrong"})
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_missing_executable(self):
        (self.app / "Executable").unlink()
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_version_mismatch(self):
        self.mutate("Extensions/ControlStudioIntents.appex", "CFBundleVersion", "2")
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_simulator_bundle(self):
        self.mutate("", "CFBundleSupportedPlatforms", ["iPhoneSimulator"])
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_missing_intent_metadata(self):
        (self.app / "Extensions/ControlStudioIntents.appex/Metadata.appintents/extract.actionsdata").unlink()
        with self.assertRaises(ValueError): validate_metadata(self.app)

    def test_rejects_missing_container_metadata_privacy_reason(self):
        path = self.app / "PlugIns/ControlStudioWidgets.appex/PrivacyInfo.xcprivacy"
        path.write_bytes(plistlib.dumps({"NSPrivacyAccessedAPITypes": []}))
        with self.assertRaisesRegex(ValueError, "Container file metadata reason"): validate_metadata(self.app)

    def test_rejects_missing_ipad_support(self):
        for relative in BUNDLES:
            with self.subTest(bundle=relative):
                self.mutate(relative, "UIDeviceFamily", [1])
                with self.assertRaisesRegex(ValueError, "iPhone/iPad device support"): validate_metadata(self.app)
                self.mutate(relative, "UIDeviceFamily", [1, 2])

    def test_rejects_extensionkit_in_plugins(self):
        source = self.app / "Extensions/ControlStudioIntents.appex"
        source.rename(self.app / "PlugIns/ControlStudioIntents.appex")
        with self.assertRaises(OSError): validate_metadata(self.app)

    def test_rejects_zip_traversal(self):
        path = self.root / "bad.ipa"
        with zipfile.ZipFile(path, "w") as archive: archive.writestr("../escape", b"bad")
        with self.assertRaises(ValueError): extract_safely(path, self.root / "unpacked")
        self.assertFalse((self.root / "escape").exists())

    def test_rejects_duplicate_zip_entry(self):
        path = self.root / "duplicate.ipa"
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("duplicate", b"a")
                archive.writestr("duplicate", b"b")
        with self.assertRaises(ValueError): extract_safely(path, self.root / "unpacked")

    def test_explicit_app_group_profile_accepts_distinct_app_prefix(self):
        self.assertEqual(validate_profile(self.profile(), BUNDLES[""][0], device="test-device"), "TEAM123456")

    def test_rejects_missing_app_group(self):
        profile = self.profile()
        profile["Entitlements"]["com.apple.security.application-groups"] = []
        with self.assertRaises(ValueError): validate_profile(profile, BUNDLES[""][0])

    def test_rejects_expired_profile(self):
        profile = self.profile()
        profile["ExpirationDate"] = dt.datetime(2020, 1, 1)
        with self.assertRaises(ValueError): validate_profile(profile, BUNDLES[""][0])

    def test_rejects_mismatched_team(self):
        with self.assertRaises(ValueError): validate_profile(self.profile(), BUNDLES[""][0], "OTHERTEAM")

    def test_rejects_wrong_device(self):
        with self.assertRaises(ValueError): validate_profile(self.profile(), BUNDLES[""][0], device="other-device")

    def test_rejects_app_store_distribution_profile(self):
        profile = self.profile()
        profile.pop("ProvisionedDevices")
        with self.assertRaises(ValueError): validate_profile(profile, BUNDLES[""][0])

    def test_rejects_wildcard_app_profile(self):
        profile = self.profile()
        profile["Entitlements"]["application-identifier"] = "PREFIX1234.*"
        with self.assertRaises(ValueError): validate_profile(profile, BUNDLES[""][0])


if __name__ == "__main__":
    unittest.main()
