import XCTest
@testable import ControlStudioStorage

final class WidgetDisplayMetricsTests: XCTestCase {
    private var directory: URL!
    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }
    override func tearDownWithError() throws { try FileManager.default.removeItem(at: directory) }
    private func record(_ size: WidgetDesignSize = .medium, width: Double = 338, height: Double = 158) -> WidgetDisplayMeasurement {
        WidgetDisplayMeasurement(size: size, screenWidth: 393, screenHeight: 852, screenScale: 3, osMajor: 27, width: width, height: height)
    }
    func testNativeWriterMatchesTheManagedContractAndDoesNotRewriteAnUnchangedSize() throws {
        let store = WidgetDisplayMetricsStore(directory: directory); try store.record(record())
        let url = directory.appendingPathComponent("dimensions-medium.json")
        let bytes = try Data(contentsOf: url)
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let fixture = try Data(contentsOf: root.appendingPathComponent("contracts/widget-display-measurement-v1.example.json"))
        XCTAssertEqual(try JSONSerialization.jsonObject(with: bytes) as? NSDictionary, try JSONSerialization.jsonObject(with: fixture) as? NSDictionary)
        let stamp = Date(timeIntervalSince1970: 1_700_000_000)
        try FileManager.default.setAttributes([.modificationDate: stamp], ofItemAtPath: url.path)
        try store.record(record())
        XCTAssertEqual(try FileManager.default.attributesOfItem(atPath: url.path)[.modificationDate] as? Date, stamp)
        try Data(repeating: 65, count: 4096).write(to: url)
        try store.record(record()); XCTAssertEqual(try Data(contentsOf: url), bytes)
    }
    func testFourFixedFilesAndInvalidOrStandBySizesCannotReplaceThem() throws {
        let store = WidgetDisplayMetricsStore(directory: directory)
        for value in [record(.small, width: 158, height: 158), record(), record(.large, width: 338, height: 354), record(.extraLargePortrait, width: 338, height: 550)] {
            XCTAssertTrue(WidgetDisplayMetricsStore.valid(value)); try store.record(value)
        }
        let url = directory.appendingPathComponent("dimensions-small.json"); let original = try Data(contentsOf: url)
        for value in [record(.small, width: 350, height: 350), record(.small, width: .nan, height: 158), record(.small, width: 158, height: 0), record(.extraLarge)] {
            XCTAssertFalse(WidgetDisplayMetricsStore.valid(value)); try store.record(value)
        }
        XCTAssertEqual(try Data(contentsOf: url), original)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: directory.path).count, 4)
    }
    func testCanvasDimensionsRemainOptionalAndOnlyMatchTheirActualSize() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let fixture = try Data(contentsOf: root.appendingPathComponent("contracts/widget-designs-v1.example.json"))
        let url = directory.appendingPathComponent("catalog.json"); try fixture.write(to: url)
        let store = WidgetDesignStore(directory: directory); XCTAssertNil(try store.read().first?.canvasDimensions)
        var catalog = try JSONSerialization.jsonObject(with: fixture) as! [String: Any]
        var design = (catalog["designs"] as! [[String: Any]])[0]
        design["canvasDimensions"] = ["width": 338, "height": 550]; catalog["designs"] = [design]
        let bytes = try JSONSerialization.data(withJSONObject: catalog); try bytes.write(to: url)
        let canvas = try XCTUnwrap(store.read().first?.canvasDimensions)
        XCTAssertTrue(canvas.matches(width: 338, height: 550))
        XCTAssertFalse(canvas.matches(width: 364, height: 594)); XCTAssertFalse(canvas.matches(width: 338, height: .nan))
        XCTAssertEqual(try Data(contentsOf: url), bytes)
    }
}
