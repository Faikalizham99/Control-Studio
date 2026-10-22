import Foundation

struct WidgetDiagnosticSettings: Codable {
    let schemaVersion: Int
    let session: String
    let enabled: Bool
    let textOnly: Bool
    let startedAtUnixMilliseconds: Int64
}

struct WidgetDiagnosticEvent: Codable {
    let schemaVersion: Int
    let session: String
    let request: String
    let sequence: Int
    let timestampUnixMilliseconds: Int64
    let source: String
    let stage: String
    let detail: String
}

struct WidgetDiagnostics {
    let directory: URL
    static let maximumEvents = 128
    func settings() throws -> WidgetDiagnosticSettings? {
        let url = directory.appendingPathComponent("settings.json")
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }
        let size = try FileManager.default.attributesOfItem(atPath: url.path)[.size] as? NSNumber
        guard let size, size.intValue <= 2048 else { throw WidgetDesignError.invalidCatalog }
        let bytes = try Data(contentsOf: url)
        guard bytes.count <= 2048 else { throw WidgetDesignError.invalidCatalog }
        let settings = try JSONDecoder().decode(WidgetDiagnosticSettings.self, from: bytes)
        guard settings.schemaVersion == 1, Self.validId(settings.session), (1...253402300799999).contains(settings.startedAtUnixMilliseconds),
              settings.enabled || !settings.textOnly else { throw WidgetDesignError.invalidCatalog }
        return settings
    }
    static func validId(_ id: String) -> Bool { WidgetDesignStore.validKey(id) && id != String(repeating: "0", count: 32) }
    static func validToken(_ value: String, maximum: Int, allowEmpty: Bool = false) -> Bool {
        value.utf8.count <= maximum && (allowEmpty || !value.isEmpty) && value.utf8.allSatisfy {
            (48...57).contains($0) || (65...90).contains($0) || (97...122).contains($0) || [46, 45, 95, 61, 59, 44].contains($0)
        }
    }
    func record(_ settings: WidgetDiagnosticSettings, request: String, sequence: Int, source: String, stage: String, detail: String = "") throws {
        guard settings.enabled else { return }
        guard settings.schemaVersion == 1, Self.validId(settings.session), Self.validId(request), sequence > 0,
              ["host", "query", "snapshot", "timeline"].contains(source),
              Self.validToken(stage, maximum: 60), Self.validToken(detail, maximum: 160, allowEmpty: true) else { throw WidgetDesignError.invalidCatalog }
        let event = WidgetDiagnosticEvent(schemaVersion: 1, session: settings.session, request: request, sequence: sequence,
            timestampUnixMilliseconds: Int64(Date().timeIntervalSince1970 * 1000), source: source, stage: stage, detail: detail)
        let folder = directory.appendingPathComponent("events", isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let name = settings.session + "-" + UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased() + ".json"
        try JSONEncoder().encode(event).write(to: folder.appendingPathComponent(name), options: .atomic)
        // Each event owns a unique file, avoiding lost read-modify-write updates across extension requests.
        if let urls = try? FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: [.contentModificationDateKey]) {
            let files = urls.filter { url in
                let parts = url.deletingPathExtension().lastPathComponent.split(separator: "-")
                return url.pathExtension == "json" && parts.count == 2 && parts.allSatisfy { Self.validId(String($0)) }
            }.sorted { lhs, rhs in
                ((try? lhs.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast)
                    < ((try? rhs.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast)
            }
            for url in files.prefix(max(0, files.count - Self.maximumEvents)) { try? FileManager.default.removeItem(at: url) }
        }
    }
}
