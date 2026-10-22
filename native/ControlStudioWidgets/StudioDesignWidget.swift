import AppIntents
import SwiftUI
import WidgetKit
import UIKit

struct StudioDesignEntity: AppEntity {
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Widget design"
    static var defaultQuery = StudioDesignQuery()
    var id: String
    var name: String
    var size: String
    var displayRepresentation: DisplayRepresentation { DisplayRepresentation(title: "\(name)", subtitle: "\(size)") }
}

struct StudioDesignQuery: EntityQuery {
    func entities(for identifiers: [String]) async throws -> [StudioDesignEntity] {
        let designs = try await suggestedEntities()
        return designs.filter { identifiers.contains($0.id) }
    }
    func suggestedEntities() async throws -> [StudioDesignEntity] {
        try WidgetDesignStore.shared().read().map { StudioDesignEntity(id: $0.id, name: $0.name, size: $0.size.title) }
    }
}

struct StudioDesignIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource = "Choose a widget design"
    static var description = IntentDescription("Display a design saved in Control Studio. Choose a design that matches this widget's size.")
    @Parameter(title: "Design") var design: StudioDesignEntity?
}

struct StudioDesignEntry: TimelineEntry {
    let date: Date
    var image: UIImage? = nil
    var name = "Studio Widget"
    var message = "Create a design in Control Studio, then touch and hold this widget and choose Edit Widget."
}

struct StudioDesignProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> StudioDesignEntry { StudioDesignEntry(date: .now) }
    func snapshot(for configuration: StudioDesignIntent, in context: Context) async -> StudioDesignEntry { current(configuration, context.family) }
    func timeline(for configuration: StudioDesignIntent, in context: Context) async -> Timeline<StudioDesignEntry> {
        Timeline(entries: [current(configuration, context.family)], policy: .after(Date().addingTimeInterval(900)))
    }
    private func current(_ configuration: StudioDesignIntent, _ family: WidgetFamily) -> StudioDesignEntry {
        let size: WidgetDesignSize
        if #available(iOS 27.0, *), family == .systemExtraLargePortrait {
            size = .extraLargePortrait
        } else {
            switch family {
            case .systemSmall: size = .small
            case .systemMedium: size = .medium
            case .systemLarge: size = .large
            case .systemExtraLarge: size = .extraLarge
            default: return StudioDesignEntry(date: .now)
            }
        }
        do {
            let store = try WidgetDesignStore.shared()
            let designs = try store.read()
            let design: WidgetDesignSummary?
            if let chosen = configuration.design {
                design = designs.first { $0.id == chosen.id }
                if design == nil { return StudioDesignEntry(date: .now, message: "This design was deleted. Edit Widget to choose another design.") }
            } else { design = designs.first { $0.size == size } }
            guard let design else { return StudioDesignEntry(date: .now, message: "Create a \(size.title) design in Control Studio, then choose it in Edit Widget.") }
            guard design.size == size else { return StudioDesignEntry(date: .now, message: "Choose a \(size.title) design in Edit Widget, or add this design in its \(design.size.title) size.") }
            guard let image = UIImage(data: try store.imageData(for: design)) else { throw WidgetDesignError.imageUnavailable }
            return StudioDesignEntry(date: .now, image: image, name: design.name)
        } catch CounterError.storageUnavailable {
            return StudioDesignEntry(date: .now, message: "App Group access is unavailable. Open Control Studio and check signing in Settings.")
        } catch {
            return StudioDesignEntry(date: .now, message: "Open Control Studio and tap Refresh in Widgets. Unlock your device if needed.")
        }
    }
}

struct StudioDesignView: View {
    let entry: StudioDesignEntry
    var body: some View {
        GeometryReader { geometry in
            if let image = entry.image {
                photo(image).scaledToFill()
                    .frame(width: geometry.size.width, height: geometry.size.height).clipped()
                    .accessibilityLabel("Widget design: \(entry.name)")
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    Label("Studio Widget", systemImage: "square.stack.3d.up").font(.headline)
                    Text(entry.message).font(.caption).foregroundStyle(.secondary).minimumScaleFactor(0.7)
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

struct StudioDesignWidget: Widget {
    private var families: [WidgetFamily] {
        var result: [WidgetFamily] = [.systemSmall, .systemMedium, .systemLarge, .systemExtraLarge]
        if #available(iOS 27.0, *) { result.append(.systemExtraLargePortrait) }
        return result
    }
    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: WidgetDesignStore.widgetKind, intent: StudioDesignIntent.self, provider: StudioDesignProvider()) { entry in
            StudioDesignView(entry: entry)
        }
        .configurationDisplayName("Studio Widget")
        .description("Your own layered widget design, created in Control Studio.")
        .supportedFamilies(families)
        .contentMarginsDisabled()
    }
}
