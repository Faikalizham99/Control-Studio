import XCTest
@testable import ControlStudioStorage

final class WidgetDateSceneTests: XCTestCase {
    func testOptionalWeekdayAndBothStylesMatchEveryPresetWithoutDuplication() throws {
        let base = try object(); let layer = (base["parts"] as! [[String: Any]])[0]["date"] as! [String: Any]
        let original = layer["date"] as! [String: Any]; let instant = ISO8601DateFormatter().date(from: "2026-10-25T15:59:59Z")!
        let zone = TimeZone(secondsFromGMT: 8 * 3600)!
        for (format, dateText) in [("longDate", "25 October 2026"), ("shortDate", "25 Oct"), ("numeric", "25/10/2026"), ("month", "October")] {
            for (show, style, prefix) in [(false, "short", ""), (true, "short", "Sun, "), (true, "full", "Sunday, ")] {
                var options = original; options["format"] = format; options["showWeekday"] = show; options["weekdayStyle"] = style
                let decoded = try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: options))
                XCTAssertTrue(decoded.valid); XCTAssertEqual(decoded.includesWeekday, show)
                XCTAssertEqual(decoded.formatStyle(timeZone: zone).format(instant), prefix + dateText)
                if format == "numeric" && show && style == "full" {
                    XCTAssertEqual(decoded.formatStyle(timeZone: zone).format(instant.addingTimeInterval(1)), "Monday, 26/10/2026")
                }
            }
            var legacy = original; legacy["format"] = format
            let decoded = try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: legacy))
            XCTAssertEqual(decoded.includesWeekday, format == "shortDate"); XCTAssertEqual(decoded.weekdayStyle, "short")
        }
    }
    func testInvalidWeekdayFlagsAndStylesAreRejected() throws {
        let base = try object(); let layer = (base["parts"] as! [[String: Any]])[0]["date"] as! [String: Any]; let original = layer["date"] as! [String: Any]
        var unknown = original; unknown["weekdayStyle"] = "other"
        XCTAssertFalse(try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: unknown)).valid)
        for bad in [NSNull(), 2] as [Any] {
            var options = original; options["weekdayStyle"] = bad
            XCTAssertThrowsError(try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: options)))
        }
        for bad in ["true", 1] as [Any] {
            var options = original; options["showWeekday"] = bad
            XCTAssertThrowsError(try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: options)))
        }
        var legacyNull = original; legacyNull["format"] = "shortDate"; legacyNull["showWeekday"] = NSNull()
        XCTAssertTrue(try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: legacyNull)).includesWeekday)
    }
    private func fixture() throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/widget-scene-v2-date.example.json"))
    }
    private func object() throws -> [String: Any] { try JSONSerialization.jsonObject(with: fixture()) as! [String: Any] }
    private func decode(_ object: [String: Any]) throws -> WidgetClockScene { try JSONDecoder().decode(WidgetClockScene.self, from: JSONSerialization.data(withJSONObject: object)) }
    func testDateSceneContractSharesAppearanceWithoutClockOnlyFlags() throws {
        let scene = try JSONDecoder().decode(WidgetClockScene.self, from: fixture())
        XCTAssertTrue(scene.valid); XCTAssertEqual(scene.schemaVersion, 2)
        let layer = try XCTUnwrap(scene.parts.first?.date)
        XCTAssertEqual(layer.kind, "date"); XCTAssertEqual(layer.date.font, "sfProRounded"); XCTAssertEqual(layer.date.textStyle.resolvedWeight, "light")
        let data = String(decoding: try fixture(), as: UTF8.self)
        XCTAssertFalse(data.contains("twentyFourHour")); XCTAssertFalse(data.contains("showSeconds")); XCTAssertFalse(data.contains("showAmPm"))
    }
    func testFourPresetsMatchManagedFormattingAndFollowLocalMidnight() throws {
        let base = try object(); let layer = (base["parts"] as! [[String: Any]])[0]["date"] as! [String: Any]
        let original = layer["date"] as! [String: Any]
        let instant = ISO8601DateFormatter().date(from: "2026-10-25T15:59:59Z")!
        for (format, before, after) in [("longDate", "25 October 2026", "26 October 2026"),
                                       ("shortDate", "Sun, 25 Oct", "Mon, 26 Oct"),
                                       ("numeric", "25/10/2026", "26/10/2026"), ("month", "October", "October")] {
            var options = original; options["format"] = format
            let style = try JSONDecoder().decode(WidgetDateStyle.self, from: JSONSerialization.data(withJSONObject: options))
            XCTAssertTrue(style.valid)
            let zone = TimeZone(secondsFromGMT: 8 * 3600)!
            XCTAssertEqual(style.formatStyle(timeZone: zone).format(instant), before)
            XCTAssertEqual(style.formatStyle(timeZone: zone).format(instant.addingTimeInterval(1)), after)
        }
    }
    func testDayBoundaryUsesCalendarDaysAcrossDSTAndTimeZones() throws {
        let parser = ISO8601DateFormatter(); let zone = TimeZone(identifier: "America/New_York")!
        let spring = parser.date(from: "2026-03-08T05:00:00Z")!
        let autumn = parser.date(from: "2026-11-01T04:00:00Z")!
        XCTAssertEqual(WidgetDateStyle.nextDay(after: spring, timeZone: zone).timeIntervalSince(spring), 23 * 3600)
        XCTAssertEqual(WidgetDateStyle.nextDay(after: autumn, timeZone: zone).timeIntervalSince(autumn), 25 * 3600)
        let before = parser.date(from: "2026-10-25T15:59:59Z")!
        XCTAssertEqual(WidgetDateStyle.nextDay(after: before, timeZone: TimeZone(secondsFromGMT: 8 * 3600)!), before.addingTimeInterval(1))
    }
    func testSchemaOneDatesDuplicateIDsAndUnknownFormatsAreRejected() throws {
        let base = try object(); var old = base; old["schemaVersion"] = 1; XCTAssertFalse(try decode(old).valid)
        var repeated = base; let parts = base["parts"] as! [[String: Any]]; repeated["parts"] = parts + parts
        XCTAssertFalse(try decode(repeated).valid)
        var layer = parts[0]["date"] as! [String: Any]; var options = layer["date"] as! [String: Any]
        options["format"] = "custom"; layer["date"] = options
        var bad = base; bad["parts"] = [["date": layer]]; XCTAssertFalse(try decode(bad).valid)
        options["format"] = "longDate"; options["font"] = "rounded"; options["weight"] = "light"; layer["date"] = options
        bad["parts"] = [["date": layer]]; XCTAssertFalse(try decode(bad).valid)
    }
    func testDateOnlyStoreNeedsALiveSceneInsteadOfAStalePNG() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let renders = directory.appendingPathComponent("renders")
        try FileManager.default.createDirectory(at: renders, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let date = try object()["parts"] as! [[String: Any]]
        let layer = date[0]["date"] as! [String: Any]
        let key = "11111111222243338444555555555555"
        let design: [String: Any] = ["id": UUID().uuidString, "name": "Date widget", "size": "small", "renderKey": key, "revision": 1, "layers": [layer]]
        try JSONSerialization.data(withJSONObject: ["schemaVersion": 1, "designs": [design]]).write(to: directory.appendingPathComponent("catalog.json"))
        let store = WidgetDesignStore(directory: directory); let saved = try XCTUnwrap(store.read().first)
        XCTAssertTrue(saved.hasLiveText); XCTAssertFalse(saved.hasLiveClock); XCTAssertThrowsError(try store.scene(for: saved))
        try fixture().write(to: renders.appendingPathComponent(key + ".scene.json"))
        let (scene, images) = try store.scene(for: saved); XCTAssertTrue(scene.valid); XCTAssertTrue(images.isEmpty)
    }
}
