import AppIntents
import SwiftUI
import WidgetKit
import UIKit
import OSLog

struct StudioDesignOptionsProvider: DynamicOptionsProvider {
    func results() async throws -> IntentItemCollection<String> {
        let trace = StudioWidgetTrace(source: "query")
        trace.record("options.begin", "selector=string")
        do {
            let designs = try WidgetDesignStore.shared().read()
            let items = [IntentItem(WidgetDesignSelection.automaticID, title: "Automatic",
                subtitle: "First saved design matching this widget's size")] + designs.map {
                IntentItem($0.id, title: "\($0.name)", subtitle: "\($0.size.title)")
            }
            trace.record("options.ready", "selector=string;count=\(designs.count)")
            return IntentItemCollection(promptLabel: "Choose a design", usesIndexedCollation: false,
                sections: [IntentItemSection(items: items)])
        } catch { trace.failure(error); throw error }
    }
    func defaultResult() async -> String? { WidgetDesignSelection.automaticID }
}

// A new intent identity avoids changing the stored value type of the old entity parameter.
// Existing configurable instances must be removed/re-added once; saved designs are unchanged.
struct StudioDesignSelectionIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource = "Choose a widget design"
    static var description = IntentDescription("Display a design saved in Control Studio. Choose a design that matches this widget's size.")
    @Parameter(title: "Design", optionsProvider: StudioDesignOptionsProvider()) var designID: String?
    static var parameterSummary: some ParameterSummary { Summary { \.$designID } }
    var selectedID: String? { designID == WidgetDesignSelection.automaticID ? nil : designID }
}

struct StudioDesignEntry: TimelineEntry {
    let date: Date
    var image: UIImage? = nil
    var diagnostic = false
    var name = "Studio Widget"
    var message = "Create a design in Control Studio, then touch and hold this widget and choose Edit Widget."
}

struct StudioDesignProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> StudioDesignEntry { StudioDesignEntry(date: .now) }
    func snapshot(for configuration: StudioDesignSelectionIntent, in context: Context) async -> StudioDesignEntry {
        let trace = StudioWidgetTrace(source: "snapshot")
        trace.record("provider.begin", "path=configurable;selector=string;family=\(context.family.rawValue);selected=\(configuration.selectedID != nil ? 1 : 0)")
        let entry = StudioDesignLoader.current(configuration.selectedID, context.family, trace)
        trace.record("entry.ready", entry.image == nil ? "image=0" : "image=1")
        return entry
    }
    func timeline(for configuration: StudioDesignSelectionIntent, in context: Context) async -> Timeline<StudioDesignEntry> {
        let trace = StudioWidgetTrace(source: "timeline")
        trace.record("provider.begin", "path=configurable;selector=string;family=\(context.family.rawValue);selected=\(configuration.selectedID != nil ? 1 : 0)")
        let entry = StudioDesignLoader.current(configuration.selectedID, context.family, trace)
        trace.record("entry.ready", entry.image == nil ? "image=0" : "image=1")
        return Timeline(entries: [entry], policy: .after(Date().addingTimeInterval(900)))
    }
}

/// Both providers deliberately share selection checks, PNG loading and entry creation.
/// The static provider passes no ID and never constructs or resolves an App Intent.
private enum StudioDesignLoader {
    static func current(_ selectedID: String?, _ family: WidgetFamily, _ trace: StudioWidgetTrace) -> StudioDesignEntry {
        let size: WidgetDesignSize
        if #available(iOS 27.0, *), family == .systemExtraLargePortrait {
            size = .extraLargePortrait
        } else {
            switch family {
            case .systemSmall: size = .small
            case .systemMedium: size = .medium
            case .systemLarge: size = .large
            case .systemExtraLarge: size = .extraLarge
            default:
                trace.record("family.unsupported")
                return StudioDesignEntry(date: .now)
            }
        }
        do {
            let store = try WidgetDesignStore.shared()
            trace.record("catalog.read-start")
            let designs = try store.read()
            trace.record("catalog.read-ok", "count=\(designs.count)")
            let design: WidgetDesignSummary
            switch WidgetDesignSelection.resolve(designs, size: size, selectedID: selectedID) {
            case .design(let found): design = found
            case .unavailable:
                trace.record("selection.deleted")
                return StudioDesignEntry(date: .now, message: "This design is no longer available. Edit Widget to choose another design.")
            case .empty:
                trace.record("selection.empty")
                return StudioDesignEntry(date: .now, message: "Create and save a \(size.title) design in Control Studio, then tap Refresh in Widgets.")
            case .wrongSize(let savedSize):
                trace.record("selection.wrong-size", "saved=\(savedSize.rawValue);widget=\(size.rawValue)")
                return StudioDesignEntry(date: .now, message: "Choose a \(size.title) design in Edit Widget, or add this design in its \(savedSize.title) size.")
            }
            trace.record("selection.matched", "size=\(size.rawValue)")
            if trace.textOnly {
                trace.record("image.bypassed")
                return StudioDesignEntry(date: .now, diagnostic: true,
                    message: "Text test succeeded.\nDesign resolved.\n\(size.title)\nRequest \(trace.request.prefix(8))")
            }
            trace.record("image.read-start")
            let bytes = try store.imageData(for: design)
            trace.record("image.read-ok", "bytes=\(bytes.count)")
            trace.record("image.create-start")
            guard let image = UIImage(data: bytes) else { throw WidgetDesignError.imageUnavailable }
            trace.record("image.created", "width=\(image.cgImage?.width ?? 0);height=\(image.cgImage?.height ?? 0)")
            return StudioDesignEntry(date: .now, image: image, name: design.name)
        } catch CounterError.storageUnavailable {
            trace.failure(CounterError.storageUnavailable)
            return StudioDesignEntry(date: .now, message: "App Group access is unavailable. Open Control Studio and check signing in Settings.")
        } catch {
            trace.failure(error)
            return StudioDesignEntry(date: .now, message: "Open Control Studio and tap Refresh in Widgets. Unlock your device if needed.")
        }
    }
}

struct StudioDesignDiagnosticProvider: TimelineProvider {
    func placeholder(in context: Context) -> StudioDesignEntry {
        StudioDesignEntry(date: .now, message: "Tests the first saved design matching this size. Start a text or photo test in Control Studio's Widget diagnostics.")
    }
    func getSnapshot(in context: Context, completion: @escaping (StudioDesignEntry) -> Void) {
        completion(current(context.family, source: "snapshot"))
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<StudioDesignEntry>) -> Void) {
        let entry = current(context.family, source: "timeline")
        completion(Timeline(entries: [entry], policy: .after(Date().addingTimeInterval(900))))
    }
    private func current(_ family: WidgetFamily, source: String) -> StudioDesignEntry {
        let trace = StudioWidgetTrace(source: source)
        trace.record("provider.begin", "path=static;family=\(family.rawValue);selected=0")
        let entry = StudioDesignLoader.current(nil, family, trace)
        trace.record("entry.ready", entry.image == nil ? "image=0" : "image=1")
        return entry
    }
}

struct StudioDesignView: View {
    let entry: StudioDesignEntry
    var body: some View {
        Group {
            if let image = entry.image {
                GeometryReader { geometry in
                    photo(image).scaledToFill()
                        .frame(width: geometry.size.width, height: geometry.size.height).clipped()
                        .accessibilityLabel("Widget design: \(entry.name)")
                }
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    Text(entry.diagnostic ? "Studio text test" : "Studio Widget").font(.headline)
                    Text(entry.message).font(.caption).foregroundStyle(.secondary).minimumScaleFactor(0.7)
                    if entry.diagnostic { Text(entry.date.formatted(date: .omitted, time: .standard)).font(.caption2) }
                }.padding(16).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
            }
        }
        .containerBackground(.background, for: .widget)
    }
    @ViewBuilder private func photo(_ image: UIImage) -> some View {
        if #available(iOS 18.0, *) { Image(uiImage: image).resizable().widgetAccentedRenderingMode(.fullColor) }
        else { Image(uiImage: image).resizable() }
    }
}

/// Breadcrumbs describe extension execution, not successful delivery by the separate system renderer.
private final class StudioWidgetTrace {
    let request = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
    private let source: String
    private let diagnostics: WidgetDiagnostics?
    private let settings: WidgetDiagnosticSettings?
    private let lock = NSLock()
    private var sequence = 0
    private static let logger = Logger(subsystem: "com.faikalizham.controlstudio", category: "StudioWidgetDiagnostics")
    var textOnly: Bool { settings?.enabled == true && settings?.textOnly == true }
    init(source: String) {
        self.source = source
        do {
            let store = try WidgetDesignStore.shared()
            let diagnostics = WidgetDiagnostics(directory: store.directory.appendingPathComponent("diagnostics", isDirectory: true))
            let settings = try diagnostics.settings()
            self.settings = settings
            self.diagnostics = diagnostics
        } catch {
            self.settings = nil; self.diagnostics = nil
            Self.logger.error("Diagnostic settings unavailable; continuing the normal widget path.")
        }
    }
    func record(_ stage: String, _ detail: String = "") {
        guard let settings, settings.enabled, let diagnostics else { return }
        guard WidgetDiagnostics.validToken(stage, maximum: 60), WidgetDiagnostics.validToken(detail, maximum: 160, allowEmpty: true) else {
            Self.logger.error("Diagnostic breadcrumb rejected invalid fields.")
            return
        }
        lock.lock(); defer { lock.unlock() }; sequence += 1
        do { try diagnostics.record(settings, request: request, sequence: sequence, source: source, stage: stage, detail: detail) }
        catch { Self.logger.error("Diagnostic breadcrumb write failed.") }
        Self.logger.info("Source=\(self.source, privacy: .public) Stage=\(stage, privacy: .public) Detail=\(detail, privacy: .public)")
    }
    func failure(_ error: Error) {
        let code: String
        switch error {
        case CounterError.storageUnavailable: code = "app-group-unavailable"
        case WidgetDesignError.invalidCatalog: code = "invalid-catalog"
        case WidgetDesignError.unsupportedSchema: code = "unsupported-schema"
        case WidgetDesignError.imageUnavailable: code = "image-unavailable"
        case is DecodingError: code = "catalog-decode"
        default: code = "io-or-native-error"
        }
        let native = error as NSError
        let domain = native.domain == NSCocoaErrorDomain ? "cocoa" : native.domain == NSPOSIXErrorDomain ? "posix" : "other"
        record("failure", "code=\(code);domain=\(domain);number=\(native.code)")
    }
}

private enum StudioDesignFamilies {
    static var supported: [WidgetFamily] {
        var result: [WidgetFamily] = [.systemSmall, .systemMedium, .systemLarge, .systemExtraLarge]
        if #available(iOS 27.0, *) { result.append(.systemExtraLargePortrait) }
        return result
    }
}

struct StudioDesignWidget: Widget {
    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: WidgetDesignStore.widgetKind, intent: StudioDesignSelectionIntent.self, provider: StudioDesignProvider()) { entry in
            StudioDesignView(entry: entry)
        }
        .configurationDisplayName("Studio Widget")
        .description("Your own layered widget design, created in Control Studio.")
        .supportedFamilies(StudioDesignFamilies.supported)
        .contentMarginsDisabled()
    }
}

/// A separate kind keeps installed configurable widgets and their saved selections intact.
struct StudioDesignDiagnosticWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: WidgetDesignStore.diagnosticWidgetKind, provider: StudioDesignDiagnosticProvider()) { entry in
            StudioDesignView(entry: entry)
        }
        .configurationDisplayName("Studio Widget Test")
        .description("Tests saved designs without a selector. Shows the first design matching this size. Use Widget diagnostics in Control Studio to test text or photos.")
        .supportedFamilies(StudioDesignFamilies.supported)
        .contentMarginsDisabled()
    }
}
