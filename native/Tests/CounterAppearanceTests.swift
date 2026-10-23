import XCTest
@testable import ControlStudioStorage

final class CounterAppearanceTests: XCTestCase {
    private var directory: URL!
    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    }
    override func tearDownWithError() throws {
        if FileManager.default.fileExists(atPath: directory.path) { try FileManager.default.removeItem(at: directory) }
    }
    private var store: CounterAppearanceStore { CounterAppearanceStore(directory: directory) }
    private var file: URL { directory.appendingPathComponent("counter-appearance.json") }

    func testSavedThemesAndAccentsPersistAndIdenticalPublicationDoesNotRequestReload() throws {
        for theme in 0...2 {
            let appearance = CounterAppearance(schemaVersion: 1, themeIndex: theme, accentHex: "#00FFD2")
            XCTAssertTrue(try store.save(appearance))
            XCTAssertEqual(try CounterAppearanceStore(directory: directory).read(), appearance)
            let before = try Data(contentsOf: file)
            XCTAssertFalse(try store.save(appearance))
            XCTAssertEqual(try Data(contentsOf: file), before)
            for systemDark in [false, true] {
                XCTAssertEqual(appearance.usesDark(systemDark: systemDark), theme == 2 || (theme == 0 && systemDark))
            }
        }
    }
    func testMissingCorruptFutureAndOversizedFilesUseSafeDefaultsAndCanBeRepublished() throws {
        XCTAssertEqual(store.readOrDefault(), .standard)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        for data in [Data("{".utf8), Data("{\"schemaVersion\":2,\"themeIndex\":2,\"accentHex\":\"#FFFFFF\"}".utf8),
                     Data("{\"schemaVersion\":1,\"themeIndex\":9,\"accentHex\":\"#FFFFFF\"}".utf8),
                     Data("{\"schemaVersion\":1,\"themeIndex\":0,\"accentHex\":\"#GGGGGG\"}".utf8),
                     Data(repeating: 65, count: 5_000)] {
            try data.write(to: file)
            XCTAssertEqual(store.readOrDefault(), .standard)
            XCTAssertTrue(try store.save(.standard)); XCTAssertEqual(try store.read(), .standard)
        }
    }
    func testInvalidPublicationsCannotOverwriteSavedAppearance() throws {
        try store.save(.standard)
        for appearance in [CounterAppearance(schemaVersion: 2, themeIndex: 0, accentHex: "#007AFF"),
                           CounterAppearance(schemaVersion: 1, themeIndex: 3, accentHex: "#007AFF"),
                           CounterAppearance(schemaVersion: 1, themeIndex: 1, accentHex: "#00ffD2"),
                           CounterAppearance(schemaVersion: 1, themeIndex: 1, accentHex: "#FFFFFF00")] {
            XCTAssertThrowsError(try store.save(appearance)); XCTAssertEqual(try store.read(), .standard)
        }
    }
    func testIconWellsKeepExactAccentsReadableAndCounterDataUntouched() throws {
        let counter = CounterStore(directory: directory, storage: "shared")
        let snapshot = try counter.increment()
        for hex in ["#000000", "#FFFFFF", "#007AFF", "#00FFD2", "#AF52DE", "#FFFF00"] {
            let appearance = CounterAppearance(schemaVersion: 1, themeIndex: 2, accentHex: hex)
            try store.save(appearance)
            let rgb = appearance.rgb
            func linear(_ value: Double) -> Double { value <= 0.04045 ? value / 12.92 : pow((value + 0.055) / 1.055, 2.4) }
            let luminance = 0.2126 * linear(rgb.red) + 0.7152 * linear(rgb.green) + 0.0722 * linear(rgb.blue)
            let contrast = appearance.usesWhiteIconBackground ? 1.05 / (luminance + 0.05) : (luminance + 0.05) / 0.05
            XCTAssertGreaterThanOrEqual(contrast, 4.5)
            XCTAssertEqual(try counter.read(), snapshot)
        }
    }
}
