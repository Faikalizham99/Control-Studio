import Foundation

enum WidgetDesignSize: String, Codable {
    // extraLarge is decode-only compatibility for old shared files. It is never
    // returned to a provider or registered as a supported Home Screen family.
    case small, medium, large, extraLarge, extraLargePortrait
    var isSupported: Bool { self != .extraLarge }
    var title: String {
        switch self {
        case .small: "Small"
        case .medium: "Medium"
        case .large: "Large"
        case .extraLarge: "Saved design needs resize"
        case .extraLargePortrait: "Extra Large"
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

struct WidgetSlotAssignment: Decodable, Equatable {
    let size: WidgetDesignSize
    let number: Int
    let designId: String
}

struct WidgetSlotCatalog: Decodable {
    let schemaVersion: Int
    let slots: [WidgetSlotAssignment]
}

struct WidgetDesignStore {
    static let slotsPerSize = 10
    static func slotKind(_ number: Int) -> String { "ControlStudio.Slot.\(number)" }
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
    func readSlots() throws -> [WidgetSlotAssignment] {
        let url = directory.appendingPathComponent("slots.json")
        guard FileManager.default.fileExists(atPath: url.path) else { return [] }
        let catalog = try JSONDecoder().decode(WidgetSlotCatalog.self, from: boundedData(url, maximum: 16_384))
        guard catalog.schemaVersion == 1 else { throw WidgetDesignError.unsupportedSchema }
        guard catalog.slots.count <= 50 else { throw WidgetDesignError.invalidCatalog }
        var addresses = Set<String>()
        for slot in catalog.slots {
            guard (1...Self.slotsPerSize).contains(slot.number), UUID(uuidString: slot.designId) != nil,
                slot.designId != "00000000-0000-0000-0000-000000000000",
                addresses.insert("\(slot.size.rawValue).\(slot.number)").inserted else { throw WidgetDesignError.invalidCatalog }
        }
        return catalog.slots.filter { $0.size.isSupported }
    }
    func assignedDesign(size: WidgetDesignSize, number: Int) throws -> WidgetDesignSummary? {
        guard size.isSupported, (1...Self.slotsPerSize).contains(number) else { throw WidgetDesignError.invalidCatalog }
        guard let assignment = try readSlots().first(where: { $0.size == size && $0.number == number }) else { return nil }
        // Missing/changed-size IDs leave this slot empty; never substitute another photo.
        return try read().first { $0.id == assignment.designId && $0.size == size }
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
        return catalog.designs.filter { $0.size.isSupported }
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
