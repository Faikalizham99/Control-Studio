import Foundation

/// Independent of counter data and custom widget designs. Missing/invalid files
/// use System/blue so appearance never prevents delivery of a counter timeline.
struct CounterAppearance: Codable, Equatable {
    let schemaVersion: Int
    let themeIndex: Int // 0 System, 1 Light, 2 Dark
    let accentHex: String
    static let standard = CounterAppearance(schemaVersion: 1, themeIndex: 0, accentHex: "#007AFF")

    func validate() throws {
        guard schemaVersion == 1, (0...2).contains(themeIndex), accentHex.utf8.count == 7,
              accentHex.first == "#", accentHex.dropFirst().utf8.allSatisfy({
                  (48...57).contains($0) || (65...70).contains($0)
              }) else { throw CounterError.invalidData }
    }
    func usesDark(systemDark: Bool) -> Bool { themeIndex == 2 || (themeIndex == 0 && systemDark) }
    var rgb: (red: Double, green: Double, blue: Double) {
        let number = UInt32(accentHex.dropFirst(), radix: 16) ?? 0x007AFF
        return (Double((number >> 16) & 255) / 255, Double((number >> 8) & 255) / 255, Double(number & 255) / 255)
    }
    /// Exact accent on a contrasting icon well, including black/white accents.
    var usesWhiteIconBackground: Bool {
        func linear(_ value: Double) -> Double { value <= 0.04045 ? value / 12.92 : pow((value + 0.055) / 1.055, 2.4) }
        let color = rgb
        let luminance = 0.2126 * linear(color.red) + 0.7152 * linear(color.green) + 0.0722 * linear(color.blue)
        return 1.05 / (luminance + 0.05) >= 4.5
    }
}

struct CounterAppearanceStore {
    let directory: URL
    private var url: URL { directory.appendingPathComponent("counter-appearance.json") }
    func read() throws -> CounterAppearance {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let data = try handle.read(upToCount: 4_097) ?? Data()
        guard data.count <= 4_096 else { throw CounterError.invalidData }
        let appearance = try JSONDecoder().decode(CounterAppearance.self, from: data)
        try appearance.validate()
        return appearance
    }
    func readOrDefault() -> CounterAppearance { (try? read()) ?? .standard }
    @discardableResult func save(_ appearance: CounterAppearance) throws -> Bool {
        try appearance.validate()
        if (try? read()) == appearance { return false }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONEncoder().encode(appearance).write(to: url, options: .atomic)
        return true
    }
    static func shared() throws -> CounterAppearanceStore {
        CounterAppearanceStore(directory: try CounterStore.shared().directory)
    }
}
