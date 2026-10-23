import SwiftUI
import WidgetKit
import UIKit

struct StudioSlotEntry: TimelineEntry {
    let date: Date
    let slot: Int
    var image: UIImage? = nil
    var name = ""
    var message = "Assign a saved design to this slot in Control Studio, under Studio → Widgets → Widget slots."
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
            case .systemExtraLarge: size = .extraLarge
            default: return StudioSlotEntry(date: .now, slot: slot, message: "Choose a supported Home Screen widget size.")
            }
        }
        do {
            let store = try WidgetDesignStore.shared()
            guard let design = try store.assignedDesign(size: size, number: slot) else {
                return StudioSlotEntry(date: .now, slot: slot,
                    message: "Assign a \(size.title) design to slot \(slot) in Control Studio → Studio → Widgets → Widget slots.")
            }
            guard let image = UIImage(data: try store.imageData(for: design)) else { throw WidgetDesignError.imageUnavailable }
            return StudioSlotEntry(date: .now, slot: slot, image: image, name: design.name)
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
                    photo(image).scaledToFill()
                        .frame(width: geometry.size.width, height: geometry.size.height).clipped()
                        .accessibilityLabel("Studio \(entry.slot): \(entry.name)")
                }
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Studio \(entry.slot)").font(.headline)
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

struct StudioSlotWidget: Widget {
    let slot: Int
    private var families: [WidgetFamily] {
        var result: [WidgetFamily] = [.systemSmall, .systemMedium, .systemLarge, .systemExtraLarge]
        if #available(iOS 27.0, *) { result.append(.systemExtraLargePortrait) }
        return result
    }
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: WidgetDesignStore.slotKind(slot), provider: StudioSlotProvider(slot: slot)) { entry in
            StudioSlotView(entry: entry)
        }
        .configurationDisplayName("Studio \(slot)")
        .description("Assign a design to slot \(slot) for each size inside Control Studio. Different slots can show different designs.")
        .supportedFamilies(families)
        .contentMarginsDisabled()
    }
}

// Small groups also work with older WidgetBundleBuilder overload limits.
struct StudioSlotsOneToFive: WidgetBundle {
    var body: some Widget {
        StudioSlotWidget(slot: 1)
        StudioSlotWidget(slot: 2)
        StudioSlotWidget(slot: 3)
        StudioSlotWidget(slot: 4)
        StudioSlotWidget(slot: 5)
    }
}
struct StudioSlotsSixToTen: WidgetBundle {
    var body: some Widget {
        StudioSlotWidget(slot: 6)
        StudioSlotWidget(slot: 7)
        StudioSlotWidget(slot: 8)
        StudioSlotWidget(slot: 9)
        StudioSlotWidget(slot: 10)
    }
}
