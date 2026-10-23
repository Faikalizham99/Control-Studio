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
    func testTwoSameSizeSelectionsSurviveRenameAndCatalogReorder() throws {
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        var first = (catalog["designs"] as! [[String: Any]])[0]
        first["size"] = "small"; first["name"] = "First photo"
        var second = first; second["id"] = "8a730e54-60a8-4404-b162-465be62c6fbe"; second["name"] = "Second photo"
        catalog["designs"] = [first, second]
        let url = directory.appendingPathComponent("catalog.json")
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        let before = try store.read()
        XCTAssertEqual(WidgetDesignSelection.resolve(before, size: .small, selectedID: before[0].id), .design(before[0]))
        XCTAssertEqual(WidgetDesignSelection.resolve(before, size: .small, selectedID: before[1].id), .design(before[1]))
        first["name"] = "Renamed photo"; catalog["designs"] = [second, first]
        try JSONSerialization.data(withJSONObject: catalog).write(to: url, options: .atomic)
        let after = try store.read()
        XCTAssertEqual(WidgetDesignSelection.resolve(after, size: .small, selectedID: before[0].id), .design(after[1]))
        XCTAssertEqual(WidgetDesignSelection.resolve(after, size: .small, selectedID: before[1].id), .design(after[0]))
        XCTAssertEqual(after[1].name, "Renamed photo")
    }
    func testDeletedInvalidAndWrongSizeSelectionsNeverShowAnotherPhoto() throws {
        try fixture.write(to: directory.appendingPathComponent("catalog.json"), options: .atomic)
        let designs = try store.read()
        for id in ["", "not-a-design", "8a730e54-60a8-4404-b162-465be62c6fbe"] {
            XCTAssertEqual(WidgetDesignSelection.resolve(designs, size: .extraLarge, selectedID: id), .unavailable)
        }
        XCTAssertEqual(WidgetDesignSelection.resolve(designs, size: .small, selectedID: designs[0].id), .wrongSize(.extraLarge))
        XCTAssertEqual(WidgetDesignSelection.resolve([], size: .extraLarge, selectedID: designs[0].id), .unavailable)
    }
    func testAutomaticAndStaticSelectionOnlyUseMatchingFamily() throws {
        try fixture.write(to: directory.appendingPathComponent("catalog.json"), options: .atomic)
        let designs = try store.read()
        let ids: [String?] = [nil, WidgetDesignSelection.automaticID]
        for id in ids {
            XCTAssertEqual(WidgetDesignSelection.resolve(designs, size: .extraLarge, selectedID: id), .design(designs[0]))
            XCTAssertEqual(WidgetDesignSelection.resolve(designs, size: .extraLargePortrait, selectedID: id), .empty)
            XCTAssertEqual(WidgetDesignSelection.resolve([], size: .small, selectedID: id), .empty)
        }
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
