import Foundation

enum WidgetDesignSize: String, Codable, CaseIterable {
    case small, medium, large, extraLarge, extraLargePortrait
    var title: String {
        switch self {
        case .small: "Small"
        case .medium: "Medium"
        case .large: "Large"
        case .extraLarge: "Extra Large Landscape · iPad"
        case .extraLargePortrait: "Extra Large Portrait · iOS/iPadOS 27+"
        }
    }
}

struct WidgetDesignSummary: Decodable, Equatable {
    let id: String
    let name: String
    let size: WidgetDesignSize
    let renderKey: String
    let revision: Int
}

struct WidgetDesignCatalog: Decodable {
    let schemaVersion: Int
    let designs: [WidgetDesignSummary]
}

enum WidgetDesignError: Error { case invalidCatalog, unsupportedSchema, imageUnavailable }

enum WidgetDesignSelection: Equatable {
    static let automaticID = "automatic"
    case design(WidgetDesignSummary), empty, unavailable, wrongSize(WidgetDesignSize)

    static func resolve(_ designs: [WidgetDesignSummary], size: WidgetDesignSize, selectedID: String?) -> Self {
        if let selectedID, selectedID != automaticID {
            guard let design = designs.first(where: { $0.id == selectedID }) else { return .unavailable }
            guard design.size == size else { return .wrongSize(design.size) }
            return .design(design)
        }
        guard let design = designs.first(where: { $0.size == size }) else { return .empty }
        return .design(design)
    }
}

struct WidgetDesignStore {
    static let widgetKind = "ControlStudio.Design"
    static let diagnosticWidgetKind = "ControlStudio.DesignDiagnostic"
    let directory: URL
    static func shared() throws -> WidgetDesignStore {
        #if os(iOS)
        guard let container = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: StudioConstants.appGroup)
        else { throw CounterError.storageUnavailable }
        return WidgetDesignStore(directory: CounterStore.sharedDirectory(in: container, appGroup: StudioConstants.appGroup)
            .appendingPathComponent("WidgetDesigns", isDirectory: true))
        #else
        throw CounterError.storageUnavailable
        #endif
    }
    static func validKey(_ value: String) -> Bool {
        value.count == 32 && value.utf8.allSatisfy { (48...57).contains($0) || (97...102).contains($0) }
    }
    func read() throws -> [WidgetDesignSummary] {
        let url = directory.appendingPathComponent("catalog.json")
        guard FileManager.default.fileExists(atPath: url.path) else { return [] }
        let data = try boundedData(url, maximum: 1_000_000)
        let catalog = try JSONDecoder().decode(WidgetDesignCatalog.self, from: data)
        guard catalog.schemaVersion == 1 else { throw WidgetDesignError.unsupportedSchema }
        guard catalog.designs.count <= 50, Set(catalog.designs.map(\.id)).count == catalog.designs.count else { throw WidgetDesignError.invalidCatalog }
        for design in catalog.designs {
            guard UUID(uuidString: design.id) != nil, design.id != "00000000-0000-0000-0000-000000000000",
                Self.validKey(design.renderKey), design.revision >= 0,
                !design.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, design.name.utf16.count <= 60
                else { throw WidgetDesignError.invalidCatalog }
        }
        return catalog.designs
    }
    func imageData(for design: WidgetDesignSummary) throws -> Data {
        guard Self.validKey(design.renderKey) else { throw WidgetDesignError.imageUnavailable }
        let data = try boundedData(directory.appendingPathComponent("renders").appendingPathComponent(design.renderKey + ".png"), maximum: 4_000_000)
        guard data.count >= 24, Array(data.prefix(8)) == [137, 80, 78, 71, 13, 10, 26, 10] else { throw WidgetDesignError.imageUnavailable }
        let bytes = Array(data[16..<24])
        let width = bytes.prefix(4).reduce(UInt64(0)) { ($0 << 8) | UInt64($1) }
        let height = bytes.suffix(4).reduce(UInt64(0)) { ($0 << 8) | UInt64($1) }
        guard (1...1024).contains(width), (1...1024).contains(height) else { throw WidgetDesignError.imageUnavailable }
        return data
    }
    private func boundedData(_ url: URL, maximum: Int) throws -> Data {
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard let size = attributes[.size] as? NSNumber, size.intValue <= maximum else { throw WidgetDesignError.invalidCatalog }
        let data = try Data(contentsOf: url)
        guard data.count <= maximum else { throw WidgetDesignError.invalidCatalog }
        return data
    }
}
