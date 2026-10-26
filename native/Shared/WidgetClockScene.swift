import Foundation

struct WidgetStaticTextContent: Decodable, Equatable { let content: String }
struct WidgetLayerMarker: Decodable, Equatable {
    let kind: String?
    let visible: Bool?
    let opacity: Double?
    var text: WidgetStaticTextContent? = nil
    var accessibilityText: String? {
        guard kind == "text", visible != false, (opacity ?? 1) > 0,
              let content = text?.content, content.utf16.count <= 500,
              !content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        return content
    }
    var liveClock: Bool { kind == "clock" && visible != false && (opacity ?? 1) > 0 }
    var liveText: Bool { (kind == "clock" || kind == "date") && visible != false && (opacity ?? 1) > 0 }
}
struct WidgetClockFrame: Decodable, Equatable {
    let x: Double, y: Double, width: Double, height: Double
    var valid: Bool { x.isFinite && y.isFinite && width.isFinite && height.isFinite
        && (-4...4).contains(x) && (-4...4).contains(y) && (0.05...4).contains(width) && (0.05...4).contains(height) }
}
struct WidgetClockStyle: Decodable, Equatable {
    static let fontNames = [
        "rounded": "ArialRoundedMTBold", "monospaced": "Menlo-Bold", "serif": "Georgia-Bold",
        "rocknRollOne": "RocknRollOne-Regular", "delaGothicOne": "DelaGothicOne-Regular", "kaiseiDecol": "KaiseiDecol-Regular"
    ]
    let twentyFourHour: Bool
    let showSeconds: Bool
    let font: String
    let colourHex: String
    let fontScale: Double
    let alignment: String
    let showAmPm: Bool
    let weight: String?
    static func availableWeights(for font: String) -> [String] {
        switch font {
        case "system", "sfProRounded": return ["light", "regular", "medium", "semibold", "bold", "heavy"]
        case "monospaced", "serif": return ["regular", "bold"]
        case "rounded": return ["bold"]
        case "rocknRollOne", "delaGothicOne", "kaiseiDecol": return ["regular"]
        default: return []
        }
    }
    var resolvedWeight: String {
        if let weight = weight { return weight }
        switch font {
        case "system", "sfProRounded": return "semibold"
        case "rounded", "monospaced", "serif": return "bold"
        default: return "regular"
        }
    }
    var postScriptName: String? {
        if font == "monospaced" && resolvedWeight == "regular" { return "Menlo-Regular" }
        if font == "serif" && resolvedWeight == "regular" { return "Georgia" }
        return Self.fontNames[font]
    }
    private enum CodingKeys: String, CodingKey {
        case twentyFourHour, showSeconds, font, colourHex, fontScale, alignment, showAmPm, weight
    }
    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        twentyFourHour = try values.decode(Bool.self, forKey: .twentyFourHour)
        showSeconds = try values.decode(Bool.self, forKey: .showSeconds)
        font = try values.decode(String.self, forKey: .font)
        colourHex = try values.decode(String.self, forKey: .colourHex)
        fontScale = try values.decode(Double.self, forKey: .fontScale)
        alignment = try values.decode(String.self, forKey: .alignment)
        // Older saved scenes predate this switch and always showed AM/PM.
        if values.contains(.showAmPm) { showAmPm = try values.decode(Bool.self, forKey: .showAmPm) }
        else { showAmPm = true }
        // Missing/null weight retains the native face used before weight editing.
        weight = try values.decodeIfPresent(String.self, forKey: .weight)
    }
    init(date: WidgetDateStyle) {
        twentyFourHour = true; showSeconds = false; showAmPm = false
        font = date.font; colourHex = date.colourHex; fontScale = date.fontScale
        alignment = date.alignment; weight = date.weight
    }
    var valid: Bool {
        !Self.availableWeights(for: font).isEmpty && (weight == nil || Self.availableWeights(for: font).contains(resolvedWeight))
            && ["left", "centre", "right"].contains(alignment)
            && fontScale.isFinite && (0.2...1).contains(fontScale) && rgb != nil
    }
    func formatStyle(timeZone: TimeZone = .autoupdatingCurrent) -> Date.VerbatimFormatStyle {
        // An explicit hour cycle preserves the chosen 12/24-hour setting even when
        // the device's locale or system clock preference uses the other cycle.
        let pattern: Date.FormatString
        switch (twentyFourHour, showSeconds, showAmPm) {
        case (true, true, _): pattern = "\(hour: .twoDigits(clock: .twentyFourHour, hourCycle: .zeroBased)):\(minute: .twoDigits):\(second: .twoDigits)"
        case (true, false, _): pattern = "\(hour: .twoDigits(clock: .twentyFourHour, hourCycle: .zeroBased)):\(minute: .twoDigits)"
        case (false, true, true): pattern = "\(hour: .defaultDigits(clock: .twelveHour, hourCycle: .oneBased)):\(minute: .twoDigits):\(second: .twoDigits) \(dayPeriod: .standard(.abbreviated))"
        case (false, false, true): pattern = "\(hour: .defaultDigits(clock: .twelveHour, hourCycle: .oneBased)):\(minute: .twoDigits) \(dayPeriod: .standard(.abbreviated))"
        case (false, true, false): pattern = "\(hour: .defaultDigits(clock: .twelveHour, hourCycle: .oneBased)):\(minute: .twoDigits):\(second: .twoDigits)"
        case (false, false, false): pattern = "\(hour: .defaultDigits(clock: .twelveHour, hourCycle: .oneBased)):\(minute: .twoDigits)"
        }
        return Date.VerbatimFormatStyle(format: pattern, locale: Locale(identifier: "en_US_POSIX"), timeZone: timeZone, calendar: Calendar(identifier: .gregorian))
    }
    var rgb: (Double, Double, Double)? {
        guard colourHex.utf8.count == 7, colourHex.first == "#", colourHex.dropFirst().utf8.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) }),
            let value = UInt32(colourHex.dropFirst(), radix: 16) else { return nil }
        return (Double((value >> 16) & 255) / 255, Double((value >> 8) & 255) / 255, Double(value & 255) / 255)
    }
}
struct WidgetLiveClock: Decodable, Equatable {
    let id: String
    let name: String
    let kind: String
    let frame: WidgetClockFrame
    let visible: Bool
    let opacity: Double
    let clock: WidgetClockStyle
    var rotationDegrees: Double? = nil
    var valid: Bool { UUID(uuidString: id) != nil && id != "00000000-0000-0000-0000-000000000000" && kind == "clock"
        && !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && name.utf16.count <= 40
        && frame.valid && visible && opacity.isFinite && opacity > 0 && opacity <= 1 && clock.valid
        && (rotationDegrees.map { $0.isFinite && (-180...180).contains($0) } ?? true) }
}
struct WidgetClockScenePart: Decodable, Equatable {
    let imageIndex: Int?
    let clock: WidgetLiveClock?
    let date: WidgetLiveDate?
}
struct WidgetDateStyle: Decodable, Equatable {
    let format: String
    let font: String
    let colourHex: String
    let fontScale: Double
    let alignment: String
    let weight: String?
    let showWeekday: Bool?
    let weekdayStyle: String
    var includesWeekday: Bool { showWeekday ?? (format == "shortDate") }
    private enum CodingKeys: String, CodingKey { case format, font, colourHex, fontScale, alignment, weight, showWeekday, weekdayStyle }
    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        format = try values.decode(String.self, forKey: .format)
        font = try values.decode(String.self, forKey: .font)
        colourHex = try values.decode(String.self, forKey: .colourHex)
        fontScale = try values.decode(Double.self, forKey: .fontScale)
        alignment = try values.decode(String.self, forKey: .alignment)
        weight = try values.decodeIfPresent(String.self, forKey: .weight)
        showWeekday = try values.decodeIfPresent(Bool.self, forKey: .showWeekday)
        if values.contains(.weekdayStyle) { weekdayStyle = try values.decode(String.self, forKey: .weekdayStyle) }
        else { weekdayStyle = "short" }
    }
    var textStyle: WidgetClockStyle { WidgetClockStyle(date: self) }
    var valid: Bool { ["longDate", "shortDate", "numeric", "month"].contains(format)
        && ["short", "full"].contains(weekdayStyle) && textStyle.valid }
    func formatStyle(timeZone: TimeZone = .autoupdatingCurrent) -> Date.VerbatimFormatStyle {
        let pattern: Date.FormatString
        switch (format, includesWeekday, weekdayStyle == "full") {
        case ("shortDate", true, true): pattern = "\(weekday: .wide), \(day: .defaultDigits) \(month: .abbreviated)"
        case ("shortDate", true, false): pattern = "\(weekday: .abbreviated), \(day: .defaultDigits) \(month: .abbreviated)"
        case ("shortDate", false, _): pattern = "\(day: .defaultDigits) \(month: .abbreviated)"
        case ("numeric", true, true): pattern = "\(weekday: .wide), \(day: .twoDigits)/\(month: .twoDigits)/\(year: .defaultDigits)"
        case ("numeric", true, false): pattern = "\(weekday: .abbreviated), \(day: .twoDigits)/\(month: .twoDigits)/\(year: .defaultDigits)"
        case ("numeric", false, _): pattern = "\(day: .twoDigits)/\(month: .twoDigits)/\(year: .defaultDigits)"
        case ("month", true, true): pattern = "\(weekday: .wide), \(month: .wide)"
        case ("month", true, false): pattern = "\(weekday: .abbreviated), \(month: .wide)"
        case ("month", false, _): pattern = "\(month: .wide)"
        case (_, true, true): pattern = "\(weekday: .wide), \(day: .defaultDigits) \(month: .wide) \(year: .defaultDigits)"
        case (_, true, false): pattern = "\(weekday: .abbreviated), \(day: .defaultDigits) \(month: .wide) \(year: .defaultDigits)"
        default: pattern = "\(day: .defaultDigits) \(month: .wide) \(year: .defaultDigits)"
        }
        return Date.VerbatimFormatStyle(format: pattern, locale: Locale(identifier: "en_US_POSIX"), timeZone: timeZone, calendar: Calendar(identifier: .gregorian))
    }
    static func nextDay(after date: Date, timeZone: TimeZone = .autoupdatingCurrent) -> Date {
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = timeZone
        return calendar.date(byAdding: .day, value: 1, to: calendar.startOfDay(for: date)) ?? date.addingTimeInterval(3600)
    }
}
struct WidgetLiveDate: Decodable, Equatable {
    let id: String
    let name: String
    let kind: String
    let frame: WidgetClockFrame
    let visible: Bool
    let opacity: Double
    let date: WidgetDateStyle
    var rotationDegrees: Double? = nil
    var valid: Bool { UUID(uuidString: id) != nil && id != "00000000-0000-0000-0000-000000000000" && kind == "date"
        && !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && name.utf16.count <= 40
        && frame.valid && visible && opacity.isFinite && opacity > 0 && opacity <= 1 && date.valid
        && (rotationDegrees.map { $0.isFinite && (-180...180).contains($0) } ?? true) }
}
struct WidgetClockScene: Decodable, Equatable {
    let schemaVersion: Int
    let width: Double
    let height: Double
    let parts: [WidgetClockScenePart]
    var valid: Bool {
        guard (schemaVersion == 1 || schemaVersion == 2), width.isFinite, height.isFinite, (50...600).contains(width), (50...1100).contains(height),
            !parts.isEmpty, parts.count <= 8 else { return false }
        var clocks = Set<String>()
        for (index, part) in parts.enumerated() {
            if let image = part.imageIndex { if image != index || part.clock != nil || part.date != nil { return false } }
            else if let clock = part.clock { if part.date != nil || !clock.valid || !clocks.insert(clock.id.lowercased()).inserted { return false } }
            else if let date = part.date { if schemaVersion != 2 || !date.valid || !clocks.insert(date.id.lowercased()).inserted { return false } }
            else { return false }
        }
        return !clocks.isEmpty
    }
}

extension WidgetDesignStore {
    func scene(for design: WidgetDesignSummary) throws -> (WidgetClockScene, [Int: Data]) {
        guard Self.validKey(design.renderKey) else { throw WidgetDesignError.invalidCatalog }
        let folder = directory.appendingPathComponent("renders")
        let scene = try JSONDecoder().decode(WidgetClockScene.self, from: boundedData(folder.appendingPathComponent(design.renderKey + ".scene.json"), maximum: 32_768))
        guard scene.valid else { throw WidgetDesignError.invalidCatalog }
        if let saved = design.canvasDimensions {
            guard abs(saved.width - scene.width) < 0.001, abs(saved.height - scene.height) < 0.001 else { throw WidgetDesignError.invalidCatalog }
        }
        var images: [Int: Data] = [:]; var pixels: UInt64 = 0; var encodedBytes = 0
        for part in scene.parts {
            if let index = part.imageIndex {
                let bytes = try boundedData(folder.appendingPathComponent(design.renderKey + ".part-\(index).png"), maximum: 4_000_000)
                guard bytes.count >= 24, Array(bytes.prefix(8)) == [137,80,78,71,13,10,26,10] else { throw WidgetDesignError.imageUnavailable }
                let w = bytes[16..<20].reduce(UInt64(0)) { ($0 << 8) | UInt64($1) }
                let h = bytes[20..<24].reduce(UInt64(0)) { ($0 << 8) | UInt64($1) }
                guard (1...1024).contains(w), (1...1024).contains(h), abs(Double(w) / Double(h) - scene.width / scene.height) < 0.02 else { throw WidgetDesignError.imageUnavailable }
                pixels += w * h; guard pixels <= 3_000_000 else { throw WidgetDesignError.imageUnavailable }
                encodedBytes += bytes.count; guard encodedBytes <= 4_000_000 else { throw WidgetDesignError.imageUnavailable }
                images[index] = bytes
            }
        }
        return (scene, images)
    }
}
