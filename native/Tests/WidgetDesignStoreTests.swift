import XCTest
@testable import ControlStudioStorage

final class WidgetDesignStoreTests: XCTestCase {
    private var directory: URL!
    private var store: WidgetDesignStore { WidgetDesignStore(directory: directory) }
    private var fixture: Data {
        get throws {
            let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            return try Data(contentsOf: root.appendingPathComponent("contracts/widget-designs-v1.example.json"))
        }
    }
    private var slotFixture: Data {
        get throws {
            let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            return try Data(contentsOf: root.appendingPathComponent("contracts/widget-slots-v1.example.json"))
        }
    }
    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }
    override func tearDownWithError() throws { try FileManager.default.removeItem(at: directory) }
    func testCSharpContractAndImageBounds() throws {
        try fixture.write(to: directory.appendingPathComponent("catalog.json"), options: .atomic)
        let designs = try store.read()
        XCTAssertEqual(designs.count, 1); XCTAssertEqual(designs[0].size, .extraLargePortrait)
        XCTAssertEqual(designs[0].id, "4f44be38-c517-4a65-9637-23f2e381747a")
        let folder = directory.appendingPathComponent("renders")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let png = Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=")!
        try png.write(to: folder.appendingPathComponent(designs[0].renderKey + ".png"))
        XCTAssertEqual(try store.imageData(for: designs[0]), png)
    }
    func testTransparencyIsOptionalForLegacyCatalogsAndReadFromUpdatedDesigns() throws {
        let url = directory.appendingPathComponent("catalog.json")
        try fixture.write(to: url)
        XCTAssertNil(try store.read().first?.transparentBackground)
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        var design = (catalog["designs"] as! [[String: Any]])[0]
        for transparent in [true, false] {
            design["transparentBackground"] = transparent; catalog["designs"] = [design]
            let bytes = try JSONSerialization.data(withJSONObject: catalog)
            try bytes.write(to: url)
            XCTAssertEqual(try store.read().first?.transparentBackground, transparent)
            XCTAssertEqual(try Data(contentsOf: url), bytes)
        }
    }
    func testCanvasPreservationIsOptInAndNewGeometryDoesNotChangeNativeSummaryReading() throws {
        let url = directory.appendingPathComponent("catalog.json")
        try fixture.write(to: url)
        XCTAssertNil(try store.read().first?.preserveCanvas)
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        var design = (catalog["designs"] as! [[String: Any]])[0]
        design["preserveCanvas"] = true; design["backgroundHex"] = "#12ABEF"
        var layers = design["layers"] as! [[String: Any]]
        layers[0]["fitPhoto"] = true
        layers[0]["blendMode"] = "darkerColor"
        layers[0]["frame"] = ["x": -0.4, "y": 1.2, "width": 2.4, "height": 1.6]
        design["layers"] = layers; catalog["designs"] = [design]
        let bytes = try JSONSerialization.data(withJSONObject: catalog); try bytes.write(to: url)
        let summary = try XCTUnwrap(store.read().first)
        XCTAssertEqual(summary.preserveCanvas, true)
        XCTAssertEqual(summary.canvasBackgroundRGB?.red, Double(0x12) / 255)
        XCTAssertEqual(summary.canvasBackgroundRGB?.green, Double(0xAB) / 255)
        XCTAssertEqual(summary.canvasBackgroundRGB?.blue, Double(0xEF) / 255)
        XCTAssertEqual(try Data(contentsOf: url), bytes)
        for hex in ["#GGGGGG", "#123", "123456", "#12345678", "#+00001"] {
            design["backgroundHex"] = hex; catalog["designs"] = [design]
            try JSONSerialization.data(withJSONObject: catalog).write(to: url)
            XCTAssertNil(try store.read().first?.canvasBackgroundRGB)
        }
    }
    func testMissingCatalogIsEmptyButCorruptAndFutureCatalogAreNeverReset() throws {
        XCTAssertTrue(try store.read().isEmpty)
        let url = directory.appendingPathComponent("catalog.json")
        for content in ["{broken", "{\"schemaVersion\":2,\"designs\":[]}"] {
            let bytes = Data(content.utf8); try bytes.write(to: url)
            XCTAssertThrowsError(try store.read()); XCTAssertEqual(try Data(contentsOf: url), bytes)
        }
    }
    func testRetiredCatalogsDecodeWithoutOverwritingOrReplacingPhoneExtraLarge() throws {
        let text = String(data: try fixture, encoding: .utf8)!
        let url = directory.appendingPathComponent("catalog.json")
        try fixture.write(to: url)
        XCTAssertEqual(try store.read().first?.size, .extraLargePortrait)
        let retired = Data(text.replacingOccurrences(of: "\"extraLargePortrait\"", with: "\"extraLarge\"").utf8)
        try retired.write(to: url)
        XCTAssertTrue(try store.read().isEmpty); XCTAssertEqual(try Data(contentsOf: url), retired)
        let slots = directory.appendingPathComponent("slots.json")
        let oldSlots = Data(String(data: try slotFixture, encoding: .utf8)!.replacingOccurrences(of: "\"extraLargePortrait\"", with: "\"extraLarge\"").utf8)
        try oldSlots.write(to: slots)
        XCTAssertEqual(try store.readSlots().count, 2)
        XCTAssertNil(try store.assignedDesign(size: .extraLargePortrait, number: 1))
        XCTAssertThrowsError(try store.assignedDesign(size: .extraLarge, number: 1))
        XCTAssertEqual(try Data(contentsOf: slots), oldSlots)
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        let phone = (catalog["designs"] as! [[String: Any]])[0]
        var old = phone; old["id"] = "c1f7f5de-73a9-4f4d-b8a7-8c30c9a7a3b8"; old["size"] = "extraLarge"
        catalog["designs"] = [old, phone]
        let mixed = try JSONSerialization.data(withJSONObject: catalog)
        try mixed.write(to: url)
        try slotFixture.write(to: slots)
        XCTAssertEqual(try store.read().count, 1)
        XCTAssertEqual(try store.assignedDesign(size: .extraLargePortrait, number: 1)?.id, phone["id"] as? String)
        XCTAssertEqual(try Data(contentsOf: url), mixed)
    }
    func testSharedSlotsContractAndEmptySlotsNeverSubstituteADesign() throws {
        try fixture.write(to: directory.appendingPathComponent("catalog.json"))
        XCTAssertTrue(try store.readSlots().isEmpty)
        XCTAssertNil(try store.assignedDesign(size: .extraLargePortrait, number: 1))
        try slotFixture.write(to: directory.appendingPathComponent("slots.json"))
        XCTAssertEqual(try store.readSlots().count, 4)
        XCTAssertEqual(try store.assignedDesign(size: .extraLargePortrait, number: 1)?.id, "4f44be38-c517-4a65-9637-23f2e381747a")
        XCTAssertNil(try store.assignedDesign(size: .extraLargePortrait, number: 2))
        XCTAssertNil(try store.assignedDesign(size: .small, number: 1))
        XCTAssertNil(try store.assignedDesign(size: .extraLargePortrait, number: 10))
    }
    func testIndependentSlotsSurviveRenameAndReorderButMissingOrWrongSizeIdsStayEmpty() throws {
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        var first = (catalog["designs"] as! [[String: Any]])[0]
        first["size"] = "small"; first["name"] = "First photo"; first["id"] = "8a730e54-60a8-4404-b162-465be62c6fbe"
        var second = first; second["id"] = "b84d9192-b3bf-4698-9acf-374cf2594896"; second["name"] = "Second photo"
        catalog["designs"] = [first, second]
        let url = directory.appendingPathComponent("catalog.json")
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        try slotFixture.write(to: directory.appendingPathComponent("slots.json"))
        let before = try XCTUnwrap(store.assignedDesign(size: .small, number: 1))
        XCTAssertEqual(try store.assignedDesign(size: .small, number: 2)?.name, "Second photo")
        first["name"] = "Renamed photo"; catalog["designs"] = [second, first]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        XCTAssertEqual(try store.assignedDesign(size: .small, number: 1)?.id, before.id)
        XCTAssertEqual(try store.assignedDesign(size: .small, number: 1)?.name, "Renamed photo")
        first["size"] = "medium"; catalog["designs"] = [second, first]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        XCTAssertNil(try store.assignedDesign(size: .small, number: 1))
        catalog["designs"] = [second]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        XCTAssertNil(try store.assignedDesign(size: .small, number: 1))
        XCTAssertEqual(try store.assignedDesign(size: .small, number: 2)?.name, "Second photo")
    }
    func testBadFutureDuplicateAndOutOfRangeSlotsAreNeverReset() throws {
        let url = directory.appendingPathComponent("slots.json")
        for content in ["{broken", "{\"schemaVersion\":2,\"slots\":[]}", String(repeating: "x", count: 20_000)] {
            let bytes = Data(content.utf8); try bytes.write(to: url)
            XCTAssertThrowsError(try store.readSlots()); XCTAssertEqual(try Data(contentsOf: url), bytes)
        }
        var catalog = try JSONSerialization.jsonObject(with: slotFixture) as! [String: Any]
        let assignment = (catalog["slots"] as! [[String: Any]])[0]
        catalog["slots"] = [assignment, assignment]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url); XCTAssertThrowsError(try store.readSlots())
        for number in [0, 11] {
            var invalid = assignment; invalid["number"] = number; catalog["slots"] = [invalid]
            try JSONSerialization.data(withJSONObject: catalog).write(to: url); XCTAssertThrowsError(try store.readSlots())
        }
        XCTAssertThrowsError(try store.assignedDesign(size: .small, number: 11))
    }
    func testTraversalDuplicateAndUnknownSizeAreRejected() throws {
        XCTAssertFalse(WidgetDesignStore.validKey("../secret")); XCTAssertFalse(WidgetDesignStore.validKey(String(repeating: "G", count: 32)))
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        let designs = catalog["designs"] as! [[String: Any]]
        catalog["designs"] = [designs[0], designs[0]]
        let url = directory.appendingPathComponent("catalog.json")
        try JSONSerialization.data(withJSONObject: catalog).write(to: url); XCTAssertThrowsError(try store.read())
        var invalid = designs[0]; invalid["size"] = "futureSize"; catalog["designs"] = [invalid]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url); XCTAssertThrowsError(try store.read())
    }
}
