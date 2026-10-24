import XCTest
@testable import ControlStudioStorage

final class WidgetClockSceneTests: XCTestCase {
    private func fixture(_ name: String) throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/" + name))
    }
    func testCSharpSceneContractAndLiveDateFormat() throws {
        let scene = try JSONDecoder().decode(WidgetClockScene.self, from: fixture("widget-scene-v1.example.json"))
        XCTAssertTrue(scene.valid); XCTAssertEqual(scene.parts.count, 2); XCTAssertEqual(scene.parts[0].imageIndex, 0)
        let clock = try XCTUnwrap(scene.parts[1].clock); XCTAssertTrue(clock.valid); XCTAssertEqual(clock.clock.font, "rounded")
        let midnight = Date(timeIntervalSince1970: 0)
        XCTAssertEqual(clock.clock.formatStyle(timeZone: TimeZone(secondsFromGMT: 0)!).format(midnight), "00:00:00")
        var object = try JSONSerialization.jsonObject(with: fixture("widget-scene-v1.example.json")) as! [String: Any]
        var parts = object["parts"] as! [[String: Any]]; var layer = parts[1]["clock"] as! [String: Any]; var style = layer["clock"] as! [String: Any]
        style["twentyFourHour"] = false; style["showSeconds"] = false; layer["clock"] = style; parts[1]["clock"] = layer; object["parts"] = parts
        let updated = try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object))
        let text = updated.parts[1].clock!.clock.formatStyle(timeZone: TimeZone(secondsFromGMT: 0)!).format(midnight)
        XCTAssertTrue(text.contains("12:00")); XCTAssertTrue(text.contains("AM")); XCTAssertFalse(text.contains("12:00:00"))
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
