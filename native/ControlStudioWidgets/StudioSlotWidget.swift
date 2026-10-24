import SwiftUI
import WidgetKit
import UIKit

struct StudioSlotEntry: TimelineEntry {
    let date: Date
    let slot: Int
    var image: UIImage? = nil
    var name = ""
    var transparentBackground = false
    var preserveCanvas = false
    var canvasBackground: Color = .clear
    var message = "Assign a saved design to this slot in Control Studio, under Studio → My Widgets → Widget slots."
}

struct StudioSlotProvider: TimelineProvider {
    let slot: Int
    func placeholder(in context: Context) -> StudioSlotEntry { StudioSlotEntry(date: .now, slot: slot) }
    func getSnapshot(in context: Context, completion: @escaping (StudioSlotEntry) -> Void) {
        // Ten gallery entries should not decode ten full-size photos at once. The app shows assigned previews.
        if context.isPreview {
            completion(StudioSlotEntry(date: .now, slot: slot, message: "Shows the design assigned to this slot and size. Choose it in Control Studio → Widget slots."))
            return
        }
        completion(current(context.family))
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<StudioSlotEntry>) -> Void) {
        let entry = current(context.family)
        completion(Timeline(entries: [entry], policy: .after(Date().addingTimeInterval(900))))
    }
    private func current(_ family: WidgetFamily) -> StudioSlotEntry {
        let size: WidgetDesignSize
        if #available(iOS 27.0, *), family == .systemExtraLargePortrait { size = .extraLargePortrait }
        else {
            switch family {
            case .systemSmall: size = .small
            case .systemMedium: size = .medium
            case .systemLarge: size = .large
            default: return StudioSlotEntry(date: .now, slot: slot, message: "Choose a supported Home Screen widget size.")
            }
        }
        do {
            let store = try WidgetDesignStore.shared()
            guard let design = try store.assignedDesign(size: size, number: slot) else {
                return StudioSlotEntry(date: .now, slot: slot,
                    message: "Assign a \(size.title) design to slot \(slot) in Control Studio → Studio → My Widgets → Widget slots.")
            }
            guard let image = UIImage(data: try store.imageData(for: design)) else { throw WidgetDesignError.imageUnavailable }
            let rgb = design.canvasBackgroundRGB
            let background = rgb.map { Color(red: $0.red, green: $0.green, blue: $0.blue) } ?? Color(uiColor: .systemBackground)
            return StudioSlotEntry(date: .now, slot: slot, image: image, name: design.name,
                transparentBackground: design.transparentBackground ?? false, preserveCanvas: design.preserveCanvas ?? false, canvasBackground: background)
        } catch CounterError.storageUnavailable {
            return StudioSlotEntry(date: .now, slot: slot, message: "App Group access is unavailable. Open Control Studio and check signing in Settings.")
        } catch {
            return StudioSlotEntry(date: .now, slot: slot, message: "Open Control Studio and tap Refresh widgets. Unlock your device if needed.")
        }
    }
}

struct StudioSlotView: View {
    let entry: StudioSlotEntry
    var body: some View {
        Group {
            if let image = entry.image {
                GeometryReader { geometry in
                    // Retain every exported canvas edge when iOS supplies slightly different family proportions.
                    // Previously saved catalogs retain their legacy fill until explicitly saved again in the app.
                    photo(image).aspectRatio(contentMode: entry.preserveCanvas ? .fit : .fill)
                        .frame(width: geometry.size.width, height: geometry.size.height).clipped()
                        .background(entry.preserveCanvas && !entry.transparentBackground ? entry.canvasBackground : .clear)
                        .accessibilityLabel("Studio \(entry.slot): \(entry.name)")
                }
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Studio \(entry.slot)").font(.headline)
                    Text(entry.message).font(.caption).foregroundStyle(.secondary).minimumScaleFactor(0.7)
                }.padding(16).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
                    // This content background remains readable even when the slot descriptor is clear.
                    .background(Color(uiColor: .systemBackground))
            }
        }
        .containerBackground(for: .widget) {
            if entry.image != nil && entry.transparentBackground { Color.clear }
            else { Color(uiColor: .systemBackground) }
        }
    }
    @ViewBuilder private func photo(_ image: UIImage) -> some View {
        if #available(iOS 18.0, *) { Image(uiImage: image).resizable().widgetAccentedRenderingMode(.fullColor) }
        else { Image(uiImage: image).resizable() }
    }
}

@MainActor
private enum StudioSlotConfiguration {
    private static var families: [WidgetFamily] {
        var result: [WidgetFamily] = [.systemSmall, .systemMedium, .systemLarge]
        if #available(iOS 27.0, *) { result.append(.systemExtraLargePortrait) }
        return result
    }
    static func make(slot: Int) -> some WidgetConfiguration {
        StaticConfiguration(kind: WidgetDesignStore.slotKind(slot), provider: StudioSlotProvider(slot: slot)) { entry in
            StudioSlotView(entry: entry)
        }
        // WidgetKit processes gallery metadata during extension registration.
        // Interpolated LocalizedStringKey values assert here on iOS 27.
        .description("Assign a saved design to this slot for each size in Control Studio. Different slots can show different designs.")
        .supportedFamilies(families)
        .contentMarginsDisabled()
    }
}

// WidgetKit requires init(). Each concrete type keeps its slot fixed even when
// the system reconstructs it, while sharing the configuration and rendering.
struct StudioSlotOneWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 1).configurationDisplayName("Studio 1")
    }
}
struct StudioSlotTwoWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 2).configurationDisplayName("Studio 2")
    }
}
struct StudioSlotThreeWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 3).configurationDisplayName("Studio 3")
    }
}
struct StudioSlotFourWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 4).configurationDisplayName("Studio 4")
    }
}
struct StudioSlotFiveWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 5).configurationDisplayName("Studio 5")
    }
}
struct StudioSlotSixWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 6).configurationDisplayName("Studio 6")
    }
}
struct StudioSlotSevenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 7).configurationDisplayName("Studio 7")
    }
}
struct StudioSlotEightWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 8).configurationDisplayName("Studio 8")
    }
}
struct StudioSlotNineWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 9).configurationDisplayName("Studio 9")
    }
}
struct StudioSlotTenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 10).configurationDisplayName("Studio 10")
    }
}

// Small groups also work with older WidgetBundleBuilder overload limits.
struct StudioSlotsOneToFive: WidgetBundle {
    var body: some Widget {
        StudioSlotOneWidget()
        StudioSlotTwoWidget()
        StudioSlotThreeWidget()
        StudioSlotFourWidget()
        StudioSlotFiveWidget()
    }
}
struct StudioSlotsSixToTen: WidgetBundle {
    var body: some Widget {
        StudioSlotSixWidget()
        StudioSlotSevenWidget()
        StudioSlotEightWidget()
        StudioSlotNineWidget()
        StudioSlotTenWidget()
    }
}
