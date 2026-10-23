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
        XCTAssertEqual(designs.count, 1); XCTAssertEqual(designs[0].size, .extraLarge)
        XCTAssertEqual(designs[0].id, "4f44be38-c517-4a65-9637-23f2e381747a")
        let folder = directory.appendingPathComponent("renders")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let png = Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=")!
        try png.write(to: folder.appendingPathComponent(designs[0].renderKey + ".png"))
        XCTAssertEqual(try store.imageData(for: designs[0]), png)
    }
    func testMissingCatalogIsEmptyButCorruptAndFutureCatalogAreNeverReset() throws {
        XCTAssertTrue(try store.read().isEmpty)
        let url = directory.appendingPathComponent("catalog.json")
        for content in ["{broken", "{\"schemaVersion\":2,\"designs\":[]}"] {
            let bytes = Data(content.utf8); try bytes.write(to: url)
            XCTAssertThrowsError(try store.read()); XCTAssertEqual(try Data(contentsOf: url), bytes)
        }
    }
    func testPortraitFamilyDoesNotReplaceLandscapeFamily() throws {
        let text = String(data: try fixture, encoding: .utf8)!
        let url = directory.appendingPathComponent("catalog.json")
        try Data(text.replacingOccurrences(of: "\"extraLarge\"", with: "\"extraLargePortrait\"").utf8).write(to: url)
        XCTAssertEqual(try store.read().first?.size, .extraLargePortrait)
        try fixture.write(to: url)
        XCTAssertEqual(try store.read().first?.size, .extraLarge)
    }
    func testSharedSlotsContractAndEmptySlotsNeverSubstituteADesign() throws {
        try fixture.write(to: directory.appendingPathComponent("catalog.json"))
        XCTAssertTrue(try store.readSlots().isEmpty)
        XCTAssertNil(try store.assignedDesign(size: .extraLarge, number: 1))
        try slotFixture.write(to: directory.appendingPathComponent("slots.json"))
        XCTAssertEqual(try store.readSlots().count, 4)
        XCTAssertEqual(try store.assignedDesign(size: .extraLarge, number: 1)?.id, "4f44be38-c517-4a65-9637-23f2e381747a")
        XCTAssertNil(try store.assignedDesign(size: .extraLarge, number: 2))
        XCTAssertNil(try store.assignedDesign(size: .small, number: 1))
        XCTAssertNil(try store.assignedDesign(size: .extraLargePortrait, number: 1))
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
