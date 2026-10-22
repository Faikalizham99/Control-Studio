import XCTest
@testable import ControlStudioStorage

final class WidgetDiagnosticsTests: XCTestCase {
    private var directory: URL!
    private var store: WidgetDiagnostics { WidgetDiagnostics(directory: directory) }
    private func fixture(_ name: String) throws -> Data {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        return try Data(contentsOf: root.appendingPathComponent("contracts/widget-diagnostics-v1.\(name).json"))
    }
    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }
    override func tearDownWithError() throws { try FileManager.default.removeItem(at: directory) }
    func testDefaultIsOffAndCSharpSessionAndEventContractsAgree() throws {
        XCTAssertNil(try store.settings())
        try fixture("settings").write(to: directory.appendingPathComponent("settings.json"))
        let settings = try XCTUnwrap(store.settings())
        XCTAssertTrue(settings.enabled); XCTAssertTrue(settings.textOnly)
        let event = try JSONDecoder().decode(WidgetDiagnosticEvent.self, from: fixture("event"))
        XCTAssertEqual(settings.session, event.session); XCTAssertEqual(event.stage, "image.bypassed"); XCTAssertEqual(event.sequence, 2)
    }
    func testEventsUseUniqueFilesStayBoundedAndDoNotWriteWhenDisabled() throws {
        let settings = try JSONDecoder().decode(WidgetDiagnosticSettings.self, from: fixture("settings"))
        let request = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
        for sequence in 1...140 { try store.record(settings, request: request, sequence: sequence, source: "timeline", stage: "entry.ready", detail: "image=0") }
        let folder = directory.appendingPathComponent("events")
        let files = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil)
        XCTAssertEqual(files.count, 128)
        let events = try files.map { try JSONDecoder().decode(WidgetDiagnosticEvent.self, from: Data(contentsOf: $0)) }
        XCTAssertEqual(Set(events.map(\.sequence)).count, 128)
        let disabled = WidgetDiagnosticSettings(schemaVersion: 1, session: settings.session, enabled: false, textOnly: false, startedAtUnixMilliseconds: settings.startedAtUnixMilliseconds)
        try store.record(disabled, request: request, sequence: 141, source: "timeline", stage: "entry.ready")
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil).count, 128)
        XCTAssertThrowsError(try store.record(settings, request: request, sequence: 142, source: "timeline", stage: "failure", detail: "/private/photo.png"))
    }
    func testFutureCorruptAndOversizedSettingsAreNeverOverwritten() throws {
        let url = directory.appendingPathComponent("settings.json")
        let original = String(data: try fixture("settings"), encoding: .utf8)!
        for content in ["{broken", original.replacingOccurrences(of: "\"schemaVersion\":1", with: "\"schemaVersion\":2"), String(repeating: "x", count: 3000)] {
            let bytes = Data(content.utf8); try bytes.write(to: url)
            XCTAssertThrowsError(try store.settings()); XCTAssertEqual(try Data(contentsOf: url), bytes)
        }
    }
}
