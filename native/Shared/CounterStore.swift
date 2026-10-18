import Foundation
import SQLite3

enum StudioConstants {
    static let appGroup = StudioAppGroup.identifier
    static let widgetKind = "ControlStudioCounter"
    static let maximumCounter: Int64 = 2_147_483_647
}

enum CounterError: String, Error, LocalizedError {
    case storageUnavailable, databaseFailure, databaseBusy, unsupportedSchema, invalidData, counterOverflow

    var errorDescription: String? {
        switch self {
        case .storageUnavailable: "Shared counter unavailable. Open Control Studio and check App Group signing."
        case .databaseBusy: "The counter is busy. Please try again."
        case .unsupportedSchema: "This database requires a newer version of Control Studio."
        case .counterOverflow: "The counter has reached its supported maximum."
        default: "The counter could not be read or saved. Please try again."
        }
    }

    var widgetTitle: String {
        switch self {
        case .storageUnavailable: "App Group unavailable"
        case .databaseBusy: "Counter is busy"
        case .unsupportedSchema: "Update Control Studio"
        case .invalidData: "Counter data error"
        case .counterOverflow: "Counter limit reached"
        case .databaseFailure: "Counter storage error"
        }
    }

    var widgetMessage: String {
        switch self {
        case .storageUnavailable: "Open the app and check the configured App Group in Settings."
        case .databaseBusy: "Another update is in progress. Please try again."
        case .unsupportedSchema: "The shared database needs a newer app version."
        case .invalidData: "Open the app to check the stored counter. Data was not reset."
        case .counterOverflow: "The counter cannot be incremented further."
        case .databaseFailure: "Unlock your iPhone and open the app to check storage."
        }
    }
}

struct CounterSnapshot: Codable, Equatable {
    let schemaVersion: Int
    let value: Int64
    let updatedAtUnixMilliseconds: Int64?
    let storage: String
    var refreshRequested: Bool = false

    func validate() throws {
        guard schemaVersion == 1 else { throw CounterError.unsupportedSchema }
        guard (0...StudioConstants.maximumCounter).contains(value),
              ["local", "shared"].contains(storage),
              value == 0 || updatedAtUnixMilliseconds != nil,
              !refreshRequested || storage == "shared" else { throw CounterError.invalidData }
        if let timestamp = updatedAtUnixMilliseconds,
           !(0...253_402_300_799_999).contains(timestamp) { throw CounterError.invalidData }
    }

    // Explicit null is required by the C# JSON contract, including the initial state.
    func encode(to encoder: Encoder) throws {
        var values = encoder.container(keyedBy: CodingKeys.self)
        try values.encode(schemaVersion, forKey: .schemaVersion)
        try values.encode(value, forKey: .value)
        if let time = updatedAtUnixMilliseconds { try values.encode(time, forKey: .updatedAtUnixMilliseconds) }
        else { try values.encodeNil(forKey: .updatedAtUnixMilliseconds) }
        try values.encode(storage, forKey: .storage)
        try values.encode(refreshRequested, forKey: .refreshRequested)
    }
}

/// One short-lived connection per operation. SQLite serializes writers across all app processes.
struct CounterStore {
    let directory: URL
    let storage: String

    static func shared() throws -> CounterStore {
        #if os(iOS)
        guard let container = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: StudioConstants.appGroup)
        else { throw CounterError.storageUnavailable }
        return CounterStore(directory: sharedDirectory(in: container, appGroup: StudioConstants.appGroup), storage: "shared")
        #else
        throw CounterError.storageUnavailable
        #endif
    }

    static func sharedDirectory(in container: URL, appGroup: String) -> URL {
        let base = container.appendingPathComponent("Library/Application Support", isDirectory: true)
        // Preserve the original dedicated-group path. Provider groups can be shared by
        // several signed apps, so use an app-specific directory to avoid filename collisions.
        return appGroup == "group.com.faikalizham.controlstudio"
            ? base : base.appendingPathComponent("ControlStudio", isDirectory: true)
    }

    static func forHost() throws -> CounterStore {
        // Fall back only when the entitlement/container is unavailable. Never hide shared DB corruption.
        do { return try shared() }
        catch CounterError.storageUnavailable {
            let directory = try FileManager.default.url(for: .applicationSupportDirectory, in: .userDomainMask,
                                                       appropriateFor: nil, create: true)
            return CounterStore(directory: directory, storage: "local")
        }
    }

    func read() throws -> CounterSnapshot { try transaction(increment: false) }
    func increment() throws -> CounterSnapshot { try transaction(increment: true) }

    private func transaction(increment: Bool) throws -> CounterSnapshot {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        var connection: OpaquePointer?
        let path = directory.appendingPathComponent("counter.sqlite3").path
        let openResult = sqlite3_open_v2(path, &connection, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX, nil)
        guard openResult == SQLITE_OK, let db = connection else {
            if let connection { sqlite3_close(connection) }
            throw CounterError.databaseFailure
        }
        defer { sqlite3_close(db) }
        try check(sqlite3_busy_timeout(db, 3_000))
        // Rollback journal avoids managing WAL sidecars through suspend/termination in this tiny POC.
        try execute(db, "BEGIN IMMEDIATE")
        var committed = false
        defer { if !committed { _ = sqlite3_exec(db, "ROLLBACK", nil, nil, nil) } }
        let version = try scalar(db, "PRAGMA user_version")
        guard version == 0 || version == 1 else { throw CounterError.unsupportedSchema }
        if version == 0 {
            try execute(db, "CREATE TABLE IF NOT EXISTS counter (id INTEGER PRIMARY KEY CHECK(id=1), value INTEGER NOT NULL CHECK(value BETWEEN 0 AND 2147483647), updated_at INTEGER)")
            try execute(db, "INSERT OR IGNORE INTO counter(id,value,updated_at) VALUES(1,0,NULL)")
            try execute(db, "PRAGMA user_version=1")
        }
        let existing = try load(db)
        try existing.validate()
        if increment {
            guard existing.value < StudioConstants.maximumCounter else { throw CounterError.counterOverflow }
            let timestamp = Int64((Date().timeIntervalSince1970 * 1_000).rounded(.down))
            guard (0...253_402_300_799_999).contains(timestamp) else { throw CounterError.invalidData }
            // Only trusted integers are interpolated; there is no user-provided SQL.
            try execute(db, "UPDATE counter SET value=value+1, updated_at=\(timestamp) WHERE id=1")
        }
        let snapshot = try load(db)
        try snapshot.validate()
        try execute(db, "COMMIT")
        committed = true
        return snapshot
    }

    private func load(_ db: OpaquePointer) throws -> CounterSnapshot {
        var statement: OpaquePointer?
        try check(sqlite3_prepare_v2(db, "SELECT value, updated_at FROM counter WHERE id=1", -1, &statement, nil))
        defer { sqlite3_finalize(statement) }
        guard sqlite3_step(statement) == SQLITE_ROW else { throw CounterError.invalidData }
        guard sqlite3_column_type(statement, 0) == SQLITE_INTEGER,
              [SQLITE_INTEGER, SQLITE_NULL].contains(sqlite3_column_type(statement, 1)) else { throw CounterError.invalidData }
        return CounterSnapshot(schemaVersion: 1, value: sqlite3_column_int64(statement, 0),
                               updatedAtUnixMilliseconds: sqlite3_column_type(statement, 1) == SQLITE_NULL ? nil : sqlite3_column_int64(statement, 1),
                               storage: storage)
    }

    private func scalar(_ db: OpaquePointer, _ sql: String) throws -> Int64 {
        var statement: OpaquePointer?
        try check(sqlite3_prepare_v2(db, sql, -1, &statement, nil))
        defer { sqlite3_finalize(statement) }
        guard sqlite3_step(statement) == SQLITE_ROW else { throw CounterError.databaseFailure }
        return sqlite3_column_int64(statement, 0)
    }

    private func execute(_ db: OpaquePointer, _ sql: String) throws {
        try check(sqlite3_exec(db, sql, nil, nil, nil))
    }

    private func check(_ result: Int32) throws {
        if result == SQLITE_BUSY || result == SQLITE_LOCKED { throw CounterError.databaseBusy }
        guard result == SQLITE_OK else { throw CounterError.databaseFailure }
    }
}
