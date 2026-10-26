import XCTest
@testable import ControlStudioStorage

final class WidgetBatterySceneTests: XCTestCase {
    private func fixture() throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/widget-scene-v3-battery.example.json"))
    }
    private func object() throws -> [String: Any] { try JSONSerialization.jsonObject(with: fixture()) as! [String: Any] }
    private func decode(_ object: [String: Any]) throws -> WidgetClockScene {
        try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object))
    }
    func testSharedFixtureKeepsBatteryLiveAndSmoothStopsMatchManagedColours() throws {
        let scene = try JSONDecoder().decode(WidgetClockScene.self, from: fixture()); XCTAssertTrue(scene.valid)
        let layer = try XCTUnwrap(scene.parts.first?.battery); XCTAssertEqual(layer.kind, "battery"); XCTAssertEqual(layer.battery.shape, "ring")
        let style = layer.battery
        for (percent, hex) in [(0, "#FF0000"), (25, "#808000"), (50, "#00FF00"), (75, "#008080"), (100, "#0000FF")] {
            XCTAssertEqual(style.colourHex(WidgetBatteryReading(percent: percent)), hex)
        }
        for percent in [nil, -1, 101] as [Int?] {
            let reading = WidgetBatteryReading(percent: percent, charging: true, full: true)
            XCTAssertFalse(reading.known); XCTAssertEqual(reading.text, "—"); XCTAssertEqual(style.colourHex(reading), "#8E8E93")
            XCTAssertEqual(reading.description, "unavailable")
        }
        XCTAssertEqual(WidgetBatteryReading(percent: 50, charging: true).description, "50%, charging")
        XCTAssertEqual(WidgetBatteryReading(percent: 100, charging: true, full: true).description, "100%, fully charged")
        let source = try object()
        let marker = try JSONDecoder().decode(WidgetLayerMarker.self, from: JSONSerialization.data(withJSONObject: (source["parts"] as! [[String: Any]])[0]["battery"]!))
        XCTAssertTrue(marker.liveText); XCTAssertFalse(marker.liveClock)
    }
    func testFixedBoundariesEveryShapeAndHiddenMarkers() throws {
        let original = try object(); let layer = (original["parts"] as! [[String: Any]])[0]["battery"] as! [String: Any]
        var options = layer["battery"] as! [String: Any]; options["colourMode"] = "ranges"
        options["stops"] = [["fromPercent": 0, "colourHex": "#FF453A"], ["fromPercent": 21, "colourHex": "#FF9F0A"], ["fromPercent": 51, "colourHex": "#0A84FF"], ["fromPercent": 81, "colourHex": "#30D158"]]
        for shape in ["battery", "bar", "ring", "arc", "percentage"] {
            options["shape"] = shape
            let style = try JSONDecoder().decode(WidgetBatteryStyle.self, from: JSONSerialization.data(withJSONObject: options)); XCTAssertTrue(style.valid)
            for percent in 0...100 {
                XCTAssertEqual(style.colourHex(WidgetBatteryReading(percent: percent)), percent < 21 ? "#FF453A" : percent < 51 ? "#FF9F0A" : percent < 81 ? "#0A84FF" : "#30D158")
            }
        }
        var hidden = layer; hidden["visible"] = false
        XCTAssertFalse(try JSONDecoder().decode(WidgetLayerMarker.self, from: JSONSerialization.data(withJSONObject: hidden)).liveText)
        hidden["visible"] = true; hidden["opacity"] = 0
        XCTAssertFalse(try JSONDecoder().decode(WidgetLayerMarker.self, from: JSONSerialization.data(withJSONObject: hidden)).liveText)
    }
    func testInvalidRangesPayloadsSchemasAndDuplicateIdsAreRejected() throws {
        let original = try object(); let layer = (original["parts"] as! [[String: Any]])[0]["battery"] as! [String: Any]
        let base = layer["battery"] as! [String: Any]
        let invalidStops: [[[String: Any]]] = [[], [["fromPercent": 0, "colourHex": "#FFFFFF"]], [["fromPercent": 1, "colourHex": "#FFFFFF"], ["fromPercent": 50, "colourHex": "#000000"]], [["fromPercent": 0, "colourHex": "#FFFFFF"], ["fromPercent": 0, "colourHex": "#000000"]], [["fromPercent": 0, "colourHex": "#FFFFFF"], ["fromPercent": 101, "colourHex": "#000000"]], [["fromPercent": 0, "colourHex": "#ffffff"], ["fromPercent": 100, "colourHex": "#000000"]]]
        for stops in invalidStops {
            var options = base; options["stops"] = stops
            XCTAssertFalse(try JSONDecoder().decode(WidgetBatteryStyle.self, from: JSONSerialization.data(withJSONObject: options)).valid)
        }
        for schema in [1, 2, 4] { var scene = original; scene["schemaVersion"] = schema; XCTAssertFalse(try decode(scene).valid) }
        var duplicate = original; duplicate["parts"] = [["battery": layer], ["battery": layer]]; XCTAssertFalse(try decode(duplicate).valid)
        var mixed = original; mixed["parts"] = [["imageIndex": 0, "battery": layer]]; XCTAssertFalse(try decode(mixed).valid)
        for key in ["shape", "colourMode"] { var options = base; options[key] = "unknown"; XCTAssertFalse(try JSONDecoder().decode(WidgetBatteryStyle.self, from: JSONSerialization.data(withJSONObject: options)).valid) }
    }
}
