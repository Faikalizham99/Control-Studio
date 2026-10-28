import XCTest
@testable import ControlStudioStorage

final class WidgetSceneGroupingTests: XCTestCase {
    private func fixture(_ name: String) throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/" + name))
    }
    private func decode(_ value: [String: Any]) throws -> WidgetClockScene {
        try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: value))
    }

    func testNewGroupingAndLegacyClockDateBatteryFixturesRemainReadable() throws {
        let current = try JSONDecoder().decode(WidgetClockScene.self, from: fixture("widget-scene-grouping-v2.example.json"))
        XCTAssertTrue(current.valid); XCTAssertEqual(current.groupingVersion, 2)
        XCTAssertEqual(current.parts.map(\.imageIndex), [0, nil]); XCTAssertNotNil(current.parts[1].clock)
        for file in ["widget-scene-v1.example.json", "widget-scene-v2-date.example.json", "widget-scene-v3-battery.example.json"] {
            let bytes = try fixture(file)
            let old = try JSONDecoder().decode(WidgetClockScene.self, from: bytes)
            XCTAssertTrue(old.valid); XCTAssertEqual(old.groupingVersion, 1)
            var object = try JSONSerialization.jsonObject(with: bytes) as! [String: Any]
            object["groupingVersion"] = 1; XCTAssertTrue(try decode(object).valid)
            object["groupingVersion"] = 2; XCTAssertTrue(try decode(object).valid)
        }
    }

    func testUnknownGroupingAndMalformedExplicitVersionsAreRejected() throws {
        let original = try JSONSerialization.jsonObject(with: fixture("widget-scene-grouping-v2.example.json")) as! [String: Any]
        for version in [-1, 0, 3, Int.max] {
            var object = original; object["groupingVersion"] = version
            XCTAssertFalse(try decode(object).valid)
        }
        for invalid in [NSNull(), "2", true, 2.5] as [Any] {
            var object = original; object["groupingVersion"] = invalid
            XCTAssertThrowsError(try decode(object))
        }
    }

    func testStoreLoadsOldAndNewImageAddressesWithoutRegroupingCachedExports() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let renders = directory.appendingPathComponent("renders")
        try FileManager.default.createDirectory(at: renders, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let currentBytes = try fixture("widget-scene-grouping-v2.example.json")
        let current = try JSONSerialization.jsonObject(with: currentBytes) as! [String: Any]
        let parts = current["parts"] as! [[String: Any]]
        let clock = parts[1]["clock"] as! [String: Any]
        var hidden = clock; hidden["id"] = UUID().uuidString; hidden["visible"] = false
        func design(_ key: String) throws -> WidgetDesignSummary {
            let object: [String: Any] = ["id": UUID().uuidString, "name": "Grouping", "size": "small", "renderKey": key, "revision": 1,
                "layers": [["kind": "photo"], hidden, ["kind": "photo"], clock]]
            return try JSONDecoder().decode(WidgetDesignSummary.self, from: JSONSerialization.data(withJSONObject: object))
        }
        let oldKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", newKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        let oldDesign = try design(oldKey), newDesign = try design(newKey)
        var legacy = current; legacy.removeValue(forKey: "groupingVersion")
        legacy["parts"] = [["imageIndex": 0], ["imageIndex": 1], parts[1]]
        let oldBytes = try JSONSerialization.data(withJSONObject: legacy)
        try oldBytes.write(to: renders.appendingPathComponent(oldKey + ".scene.json"))
        try currentBytes.write(to: renders.appendingPathComponent(newKey + ".scene.json"))
        let png = try XCTUnwrap(Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII="))
        for path in [oldKey + ".part-0.png", oldKey + ".part-1.png", newKey + ".part-0.png"] {
            try png.write(to: renders.appendingPathComponent(path))
        }
        let store = WidgetDesignStore(directory: directory)
        let (oldScene, oldImages) = try store.scene(for: oldDesign)
        XCTAssertTrue(oldScene.valid); XCTAssertEqual(oldScene.groupingVersion, 1); XCTAssertEqual(oldImages.count, 2)
        XCTAssertEqual(oldImages[0], png); XCTAssertEqual(oldImages[1], png)
        let (newScene, newImages) = try store.scene(for: newDesign)
        XCTAssertTrue(newScene.valid); XCTAssertEqual(newScene.groupingVersion, 2); XCTAssertEqual(newImages.count, 1)
        XCTAssertEqual(newImages[0], png); XCTAssertNil(newImages[1])
        XCTAssertEqual(try Data(contentsOf: renders.appendingPathComponent(oldKey + ".scene.json")), oldBytes)
        XCTAssertEqual(try store.scene(for: oldDesign).1.count, 2)
    }
}
