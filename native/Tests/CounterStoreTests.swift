import XCTest
import SQLite3
@testable import ControlStudioStorage

final class CounterStoreTests: XCTestCase {
    private var directory: URL!
    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    }
    override func tearDownWithError() throws {
        if FileManager.default.fileExists(atPath: directory.path) { try FileManager.default.removeItem(at: directory) }
    }
    private var store: CounterStore { CounterStore(directory: directory, storage: "shared") }

    func testFirstReadAndPersistenceAcrossConnections() throws {
        XCTAssertEqual(try store.read().value, 0)
        XCTAssertNil(try store.read().updatedAtUnixMilliseconds)
        XCTAssertEqual(try store.increment().value, 1)
        let reopened = CounterStore(directory: directory, storage: "shared")
        XCTAssertEqual(try reopened.read().value, 1)
        XCTAssertNotNil(try reopened.read().updatedAtUnixMilliseconds)
    }

    func testConcurrentConnectionsDoNotLoseIncrements() throws {
        _ = try store.read()
        let queue = DispatchQueue(label: "storage-test", attributes: .concurrent)
        let group = DispatchGroup()
        let errors = Errors()
        for _ in 0..<40 {
            group.enter()
            queue.async { [self] in
                defer { group.leave() }
                do { _ = try store.increment() } catch { errors.append(error) }
            }
        }
        group.wait()
        XCTAssertTrue(errors.isEmpty)
        XCTAssertEqual(try store.read().value, 40)
    }

    func testFutureSchemaIsRejectedWithoutReset() throws {
        _ = try store.increment()
        try sql("PRAGMA user_version=2")
        XCTAssertThrowsError(try store.increment()) { XCTAssertEqual($0 as? CounterError, .unsupportedSchema) }
        try sql("PRAGMA user_version=1")
        XCTAssertEqual(try store.read().value, 1)
    }

    func testOverflowRollsBack() throws {
        _ = try store.increment()
        try sql("UPDATE counter SET value=2147483647 WHERE id=1")
        XCTAssertThrowsError(try store.increment()) { XCTAssertEqual($0 as? CounterError, .counterOverflow) }
        XCTAssertEqual(try store.read().value, StudioConstants.maximumCounter)
    }

    func testInvalidTimestampIsRejected() throws {
        _ = try store.increment()
        try sql("UPDATE counter SET updated_at=-1 WHERE id=1")
        XCTAssertThrowsError(try store.increment()) { XCTAssertEqual($0 as? CounterError, .invalidData) }
    }

    func testJsonContractIncludesExplicitNullAndScope() throws {
        let data = try JSONEncoder().encode(store.read())
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertTrue(object["updatedAtUnixMilliseconds"] is NSNull)
        XCTAssertEqual(object["schemaVersion"] as? Int, 1)
        XCTAssertEqual(object["storage"] as? String, "shared")
        XCTAssertEqual(object["refreshRequested"] as? Bool, false)
        XCTAssertEqual(try JSONDecoder().decode(CounterSnapshot.self, from: data).value, 0)
    }

    func testUnavailableDirectoryFailsRatherThanResetting() throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let blocker = directory.appendingPathComponent("file")
        try Data("blocked".utf8).write(to: blocker)
        XCTAssertThrowsError(try CounterStore(directory: blocker, storage: "shared").increment())
    }

    private func sql(_ text: String) throws {
        var db: OpaquePointer?
        XCTAssertEqual(sqlite3_open(directory.appendingPathComponent("counter.sqlite3").path, &db), SQLITE_OK)
        defer { sqlite3_close(db) }
        XCTAssertEqual(sqlite3_exec(db, text, nil, nil, nil), SQLITE_OK)
    }
}

private final class Errors: @unchecked Sendable {
    private let lock = NSLock()
    private var values: [Error] = []
    func append(_ error: Error) { lock.lock(); defer { lock.unlock() }; values.append(error) }
    var isEmpty: Bool { lock.lock(); defer { lock.unlock() }; return values.isEmpty }
}
