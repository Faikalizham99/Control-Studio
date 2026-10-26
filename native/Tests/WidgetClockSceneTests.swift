import XCTest
#if canImport(CoreText)
import CoreText
#endif
@testable import ControlStudioStorage

final class WidgetClockSceneTests: XCTestCase {
    func testFreeRotationOnLiveClockAndDateKeepsOldDefaultsAndRejectsOutOfRangeAngles() throws {
        let oldClock = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let oldDate = try JSONSerialization.jsonObject(with: fixture("widget-scene-v2-date.example.json")) as! [String: Any]
        let clock = (oldClock["parts"] as! [[String: Any]])[1]["clock"] as! [String: Any]
        let date = (oldDate["parts"] as! [[String: Any]])[0]["date"] as! [String: Any]
        let decoder = JSONDecoder()
        XCTAssertNil(try decoder.decode(WidgetLiveClock.self, from: JSONSerialization.data(withJSONObject: clock)).rotationDegrees)
        XCTAssertNil(try decoder.decode(WidgetLiveDate.self, from: JSONSerialization.data(withJSONObject: date)).rotationDegrees)
        for (kind, original) in [("clock", clock), ("date", date)] {
            for angle in [-180.0, -33.25, 0, 45.5, 180, -181, 181] {
                var rotated = original; rotated["rotationDegrees"] = angle
                let bytes = try JSONSerialization.data(withJSONObject: rotated)
                if kind == "clock" {
                    let layer = try decoder.decode(WidgetLiveClock.self, from: bytes)
                    XCTAssertEqual(layer.rotationDegrees, angle); XCTAssertEqual(layer.valid, abs(angle) <= 180)
                } else {
                    let layer = try decoder.decode(WidgetLiveDate.self, from: bytes)
                    XCTAssertEqual(layer.rotationDegrees, angle); XCTAssertEqual(layer.valid, abs(angle) <= 180)
                }
            }
        }
    }
    func testCustomTextCatalogUsesTheSavedPNGWithoutRequestingALiveScene() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let renders = directory.appendingPathComponent("renders")
        try FileManager.default.createDirectory(at: renders, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let catalog = try fixture("widget-text-v1.example.json")
        try catalog.write(to: directory.appendingPathComponent("catalog.json"))
        let store = WidgetDesignStore(directory: directory); let design = try XCTUnwrap(store.read().first)
        XCTAssertFalse(design.hasLiveText); XCTAssertFalse(design.hasLiveClock)
        XCTAssertEqual(design.layers?.first?.kind, "text")
        XCTAssertEqual(design.accessibilityText, "Hello\n世界 🌸")
        let png = try XCTUnwrap(Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII="))
        try png.write(to: renders.appendingPathComponent(design.renderKey + ".png"))
        XCTAssertEqual(try store.imageData(for: design), png)
    }
    func testHiddenZeroOpacityAndBlankTextAreExcludedFromWidgetAccessibility() throws {
        let original: [String: Any] = ["kind": "text", "visible": true, "opacity": 1, "text": ["content": "Hello"]]
        let decoder = JSONDecoder()
        let visible = try decoder.decode(WidgetLayerMarker.self, from: JSONSerialization.data(withJSONObject: original))
        XCTAssertEqual(visible.accessibilityText, "Hello"); XCTAssertFalse(visible.liveText)
        let cases: [[String: Any]] = [["visible": false], ["opacity": 0], ["text": ["content": " \n "]], ["kind": "clock"]]
        for changes in cases {
            let hidden = original.merging(changes) { _, new in new }
            XCTAssertNil(try decoder.decode(WidgetLayerMarker.self, from: JSONSerialization.data(withJSONObject: hidden)).accessibilityText)
        }
    }
    func testWeightDefaultsPreserveOldScenesAndRoundedSystemSupportsAllSixWeights() throws {
        let scene = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let layer = (scene["parts"] as! [[String: Any]])[1]["clock"] as! [String: Any]
        let original = layer["clock"] as! [String: Any]
        for (font, expected) in ["system": "semibold", "rounded": "bold", "monospaced": "bold", "serif": "bold",
                                 "rocknRollOne": "regular", "delaGothicOne": "regular", "kaiseiDecol": "regular", "sfProRounded": "semibold"] {
            var options = original; options["font"] = font; options.removeValue(forKey: "weight")
            var clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertTrue(clock.valid); XCTAssertEqual(clock.resolvedWeight, expected)
            options["weight"] = NSNull()
            clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertTrue(clock.valid); XCTAssertEqual(clock.resolvedWeight, expected)
        }
        for font in ["system", "sfProRounded"] {
            for weight in ["light", "regular", "medium", "semibold", "bold", "heavy"] {
                var options = original; options["font"] = font; options["weight"] = weight
                let clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
                XCTAssertTrue(clock.valid); XCTAssertEqual(clock.resolvedWeight, weight); XCTAssertNil(clock.postScriptName)
            }
        }
        for (font, face) in ["monospaced": "Menlo-Regular", "serif": "Georgia"] {
            var options = original; options["font"] = font; options["weight"] = "regular"
            let clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertTrue(clock.valid); XCTAssertEqual(clock.postScriptName, face)
        }
    }
    func testUnknownAndUnavailableFontWeightsAreRejected() throws {
        let scene = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let layer = (scene["parts"] as! [[String: Any]])[1]["clock"] as! [String: Any]
        let original = layer["clock"] as! [String: Any]
        for (font, weight) in [("rounded", "light"), ("kaiseiDecol", "bold"), ("system", "imaginary"), ("monospaced", "heavy")] {
            var options = original; options["font"] = font; options["weight"] = weight
            XCTAssertFalse(try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options)).valid)
        }
        for invalid in [42, true] as [Any] {
            var options = original; options["weight"] = invalid
            XCTAssertThrowsError(try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options)))
        }
    }
    func testBundledFontSceneNamesAreAcceptedAndRetainTheirTypeface() throws {
        let scene = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let layer = (scene["parts"] as! [[String: Any]])[1]["clock"] as! [String: Any]
        let original = layer["clock"] as! [String: Any]
        for (name, family) in ["rocknRollOne": "RocknRollOne-Regular", "delaGothicOne": "DelaGothicOne-Regular", "kaiseiDecol": "KaiseiDecol-Regular"] {
            var options = original; options["font"] = name
            let clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertTrue(clock.valid); XCTAssertEqual(clock.font, name); XCTAssertEqual(WidgetClockStyle.fontNames[name], family)
        }
    }
#if canImport(CoreText)
    func testBundledFilesRegisterWithTheirExactPostScriptNames() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        for name in ["RocknRollOne-Regular", "DelaGothicOne-Regular", "KaiseiDecol-Regular"] {
            let url = root.appendingPathComponent("assets/clock-fonts/" + name + ".ttf")
            var error: Unmanaged<CFError>?
            XCTAssertTrue(CTFontManagerRegisterFontsForURL(url as CFURL, .process, &error), name)
            if let error = error { XCTFail(String(describing: error.takeRetainedValue())) }
            defer { CTFontManagerUnregisterFontsForURL(url as CFURL, .process, nil) }
            let font = CTFontCreateWithName(name as CFString, 36, nil)
            XCTAssertEqual(CTFontCopyPostScriptName(font) as String, name)
        }
    }
#endif
    private func fixture(_ name: String) throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/" + name))
    }
    func testCSharpSceneContractAndLiveDateFormat() throws {
        let scene = try JSONDecoder().decode(WidgetClockScene.self, from: fixture("widget-scene-v1.example.json"))
        XCTAssertTrue(scene.valid); XCTAssertEqual(scene.parts.count, 2); XCTAssertEqual(scene.parts[0].imageIndex, 0)
        let clock = try XCTUnwrap(scene.parts[1].clock); XCTAssertTrue(clock.valid); XCTAssertEqual(clock.clock.font, "rounded")
        XCTAssertTrue(clock.clock.showAmPm) // Older scenes omit the switch and retain their suffix.
        let midnight = Date(timeIntervalSince1970: 0)
        XCTAssertEqual(clock.clock.formatStyle(timeZone: TimeZone(secondsFromGMT: 0)!).format(midnight), "00:00:00")
        var object = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        var parts = object["parts"] as! [[String: Any]]; var layer = parts[1]["clock"] as! [String: Any]; var style = layer["clock"] as! [String: Any]
        style["twentyFourHour"] = false; style["showSeconds"] = false; layer["clock"] = style; parts[1]["clock"] = layer; object["parts"] = parts
        let updated = try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object))
        let text = updated.parts[1].clock!.clock.formatStyle(timeZone: TimeZone(secondsFromGMT: 0)!).format(midnight)
        XCTAssertTrue(text.contains("12:00")); XCTAssertTrue(text.contains("AM")); XCTAssertFalse(text.contains("12:00:00"))
    }
    func testExplicitAmPmVisibilityPreservesTwelveHourCycleAndSeconds() throws {
        let scene = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let layer = (scene["parts"] as! [[String: Any]])[1]["clock"] as! [String: Any]
        let original = layer["clock"] as! [String: Any]
        let time = Date(timeIntervalSince1970: 13 * 3600 + 8 * 60 + 12)
        for (twentyFour, seconds, amPm, expected) in [
            (false, true, true, "1:08:12 PM"), (false, true, false, "1:08:12"),
            (false, false, true, "1:08 PM"), (false, false, false, "1:08"),
            (true, true, true, "13:08:12"), (true, true, false, "13:08:12"),
            (true, false, true, "13:08"), (true, false, false, "13:08")
        ] {
            var options = original; options["twentyFourHour"] = twentyFour; options["showSeconds"] = seconds; options["showAmPm"] = amPm
            let clock = try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertEqual(clock.showAmPm, amPm)
            XCTAssertEqual(clock.formatStyle(timeZone: TimeZone(secondsFromGMT: 0)!).format(time), expected)
        }
        for invalid in [NSNull(), "false"] as [Any] {
            var options = original; options["showAmPm"] = invalid
            XCTAssertThrowsError(try JSONDecoder().decode(WidgetClockStyle.self, from: JSONSerialization.data(withJSONObject: options)))
        }
    }
    func testSceneStoreReadsBoundedPhotoGroupsAndNeverUsesTheSnapshotForAClock() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        var catalog = try JSONSerialization.jsonObject(with: fixture("widget-designs-v1.example.json")) as! [String: Any]
        var designs = catalog["designs"] as! [[String: Any]]; designs[0]["size"] = "small"
        let sceneObject = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        let clock = (sceneObject["parts"] as! [[String: Any]])[1]["clock"]!
        var layers = designs[0]["layers"] as! [[String: Any]]; layers.append(clock as! [String: Any]); designs[0]["layers"] = layers; catalog["designs"] = designs
        try JSONSerialization.data(withJSONObject: catalog).write(to: directory.appendingPathComponent("catalog.json"))
        let store = WidgetDesignStore(directory: directory); let design = try XCTUnwrap(store.read().first); XCTAssertTrue(design.hasLiveClock)
        XCTAssertThrowsError(try store.scene(for: design))
        let renders = directory.appendingPathComponent("renders"); try FileManager.default.createDirectory(at: renders, withIntermediateDirectories: true)
        try fixture("widget-scene-v1.example.json").write(to: renders.appendingPathComponent(design.renderKey + ".scene.json"))
        XCTAssertThrowsError(try store.scene(for: design))
        let png = Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=")!
        try png.write(to: renders.appendingPathComponent(design.renderKey + ".part-0.png"))
        let (scene, images) = try store.scene(for: design); XCTAssertTrue(scene.valid); XCTAssertEqual(images[0], png)
        // There is deliberately no full .png: a clock entry requires the live scene, not a frozen fallback.
        XCTAssertThrowsError(try store.imageData(for: design))
    }
    func testInvalidSceneGeometryStyleAndPartAddressesAreRejected() throws {
        let original = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        for width in [0, 601] {
            var object = original; object["width"] = width
            XCTAssertFalse(try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object)).valid)
        }
        var object = original; var parts = object["parts"] as! [[String: Any]]; parts[0]["imageIndex"] = -1; object["parts"] = parts
        XCTAssertFalse(try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object)).valid)
        parts = original["parts"] as! [[String: Any]]; var layer = parts[1]["clock"] as! [String: Any]; var style = layer["clock"] as! [String: Any]
        style["font"] = "unknown"; layer["clock"] = style; parts[1]["clock"] = layer; object["parts"] = parts
        XCTAssertFalse(try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object)).valid)
    }
}
