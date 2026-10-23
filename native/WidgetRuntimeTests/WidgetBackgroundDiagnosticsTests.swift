import Foundation
import XCTest
import ControlStudioWidgetRuntime

final class WidgetBackgroundDiagnosticsTests: XCTestCase {
    private func temporary() throws -> URL {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }
    private func snapshot(_ directory: URL) throws -> [String: Any] {
        let file = try XCTUnwrap(FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil).first { $0.pathExtension == "json" })
        return try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: file)) as? [String: Any])
    }
    func testPersistsVersionSessionStagesAndBoundedHistoryWithoutLosingInstallSummary() throws {
        let directory = try temporary(); defer { try? FileManager.default.removeItem(at: directory) }
        let recorder = CSWidgetBackgroundDiagnosticRecorder(directory: directory, version: "0.2.23", build: "25", os: "27.0")
        recorder.record(stage: "install", reason: "installed", detail: "")
        for _ in 0..<100 { recorder.record(stage: "callback", reason: "received", detail: "") }
        recorder.flush()
        let root = try snapshot(directory)
        XCTAssertEqual(root["schema"] as? Int, 1); XCTAssertEqual(root["version"] as? String, "0.2.23")
        XCTAssertEqual(root["build"] as? String, "25"); XCTAssertNotNil(UUID(uuidString: root["session"] as? String ?? ""))
        let events = try XCTUnwrap(root["events"] as? [[String: Any]])
        XCTAssertEqual(events.count, 64); XCTAssertEqual(events.last?["sequence"] as? Int, 101)
        let latest = try XCTUnwrap(root["latest"] as? [String: [String: Any]])
        XCTAssertEqual(latest["install"]?["reason"] as? String, "installed")
        XCTAssertEqual(latest["callback"]?["sequence"] as? Int, 101)
    }
    func testConcurrentEventsHaveMonotonicUniqueSequencesAndProduceValidAtomicJSON() throws {
        let directory = try temporary(); defer { try? FileManager.default.removeItem(at: directory) }
        let recorder = CSWidgetBackgroundDiagnosticRecorder(directory: directory, version: "test", build: "1", os: "test")
        DispatchQueue.concurrentPerform(iterations: 100) { _ in recorder.record(stage: "rewrite", reason: "fallback", detail: "") }
        recorder.flush()
        let events = try XCTUnwrap(try snapshot(directory)["events"] as? [[String: Any]])
        let sequences = events.compactMap { $0["sequence"] as? Int }
        XCTAssertEqual(sequences, Array(37...100))
    }
    func testPrunesOnlyOldOwnedSnapshotsAndRetainsUnrelatedFiles() throws {
        let directory = try temporary(); defer { try? FileManager.default.removeItem(at: directory) }
        try Data("leave alone".utf8).write(to: directory.appendingPathComponent("unrelated.json"))
        for _ in 0..<20 {
            let recorder = CSWidgetBackgroundDiagnosticRecorder(directory: directory, version: "test", build: "1", os: "test")
            recorder.record(stage: "install", reason: "installed", detail: ""); recorder.flush()
        }
        let files = try FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)
        XCTAssertLessThanOrEqual(files.filter { UUID(uuidString: $0.deletingPathExtension().lastPathComponent) != nil }.count, 16)
        XCTAssertEqual(try String(contentsOf: directory.appendingPathComponent("unrelated.json"), encoding: .utf8), "leave alone")
    }
    func testFailedStorageDoesNotThrowOrOverwriteTheBlockingFile() throws {
        let directory = try temporary(); defer { try? FileManager.default.removeItem(at: directory) }
        let blocked = directory.appendingPathComponent("blocked")
        try Data("original".utf8).write(to: blocked)
        let recorder = CSWidgetBackgroundDiagnosticRecorder(directory: blocked, version: "test", build: "1", os: "test")
        recorder.record(stage: "install", reason: "installed", detail: ""); recorder.flush()
        XCTAssertEqual(try String(contentsOf: blocked, encoding: .utf8), "original")
    }
    func testBoundsTextAndRemovesControlCharacters() throws {
        let directory = try temporary(); defer { try? FileManager.default.removeItem(at: directory) }
        let recorder = CSWidgetBackgroundDiagnosticRecorder(directory: directory, version: "test", build: "1", os: "test")
        recorder.record(stage: "abi", reason: "mismatch", detail: "line\n" + String(repeating: "x", count: 1024)); recorder.flush()
        let events = try XCTUnwrap(try snapshot(directory)["events"] as? [[String: Any]])
        let detail = try XCTUnwrap(events.first?["detail"] as? String)
        XCTAssertEqual(detail.count, 256); XCTAssertFalse(detail.contains("\n"))
    }
}
