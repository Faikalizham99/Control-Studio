import Foundation

// Four bounded layout records, separate from the user's library. No images,
// sessions or diagnostics. A failed write must never prevent timeline delivery.
struct WidgetDisplayMeasurement: Encodable {
    let schemaVersion = 1
    let size: WidgetDesignSize
    let screenWidth: Double
    let screenHeight: Double
    let screenScale: Double
    let osMajor: Int
    let width: Double
    let height: Double
}

struct WidgetDisplayMetricsStore {
    let directory: URL
    static func valid(_ value: WidgetDisplayMeasurement) -> Bool {
        guard value.size.isSupported, value.screenWidth.isFinite, value.screenHeight.isFinite,
            (250...600).contains(value.screenWidth), (value.screenWidth...1400).contains(value.screenHeight),
            value.screenScale.isFinite, (1...4).contains(value.screenScale), (17...99).contains(value.osMajor),
            value.width.isFinite, value.height.isFinite, (50...600).contains(value.width), (50...1100).contains(value.height),
            value.width <= value.screenWidth * (value.size == .small ? 0.6 : 1.01),
            value.size == .small || value.width >= value.screenWidth * 0.6 else { return false }
        let ratio = value.width / value.height
        switch value.size {
        case .small: return (0.98...1.02).contains(ratio)
        case .medium: return (1.5...2.8).contains(ratio)
        case .large: return (0.75...1.2).contains(ratio)
        case .extraLargePortrait: return (0.35...0.8).contains(ratio)
        case .extraLarge: return false
        }
    }
    func record(_ value: WidgetDisplayMeasurement) throws {
        guard Self.valid(value) else { return }
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys]
        let bytes = try encoder.encode(value)
        let url = directory.appendingPathComponent("dimensions-\(value.size.rawValue).json")
        if let attributes = try? FileManager.default.attributesOfItem(atPath: url.path),
            let length = attributes[.size] as? NSNumber, length.intValue <= 2048,
            let previous = try? Data(contentsOf: url), previous == bytes { return }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try bytes.write(to: url, options: .atomic)
    }
}

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

struct WidgetCanvasDimensions: Decodable, Equatable {
    let width: Double
    let height: Double
    func matches(width actualWidth: Double, height actualHeight: Double) -> Bool {
        width.isFinite && height.isFinite && (50...600).contains(width) && (50...1100).contains(height)
            && abs(width - actualWidth) < 0.5 && abs(height - actualHeight) < 0.5
    }
}

struct WidgetDesignSummary: Decodable, Equatable {
    let id: String
    let name: String
    let size: WidgetDesignSize
    let renderKey: String
    let revision: Int
    let transparentBackground: Bool?
    let preserveCanvas: Bool?
    let backgroundHex: String?
    let canvasDimensions: WidgetCanvasDimensions?
    var layers: [WidgetLayerMarker]? = nil
    var hasLiveClock: Bool { layers?.contains(where: { $0.liveClock }) ?? false }
    var canvasBackgroundRGB: (red: Double, green: Double, blue: Double)? {
        guard let hex = backgroundHex, hex.utf8.count == 7, hex.first == "#",
              hex.dropFirst().utf8.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) }),
              let number = UInt32(hex.dropFirst(), radix: 16) else { return nil }
        return (Double((number >> 16) & 255) / 255, Double((number >> 8) & 255) / 255, Double(number & 255) / 255)
    }
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
                !design.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, design.name.utf16.count <= 60, (design.layers?.count ?? 0) <= 8
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
    func boundedData(_ url: URL, maximum: Int) throws -> Data {
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard let size = attributes[.size] as? NSNumber, size.intValue <= maximum else { throw WidgetDesignError.invalidCatalog }
        let data = try Data(contentsOf: url)
        guard data.count <= maximum else { throw WidgetDesignError.invalidCatalog }
        return data
    }
}
