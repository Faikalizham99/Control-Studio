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
    var canvasDimensions: WidgetCanvasDimensions? = nil
    var scene: WidgetClockScene? = nil
    var sceneImages: [Int: UIImage] = [:]
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
        let entry = current(context.family)
        captureSize(context) { completion(entry) }
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<StudioSlotEntry>) -> Void) {
        let entry = current(context.family)
        let now = Date()
        let refresh = entry.scene?.parts.contains(where: { $0.date != nil }) == true
            ? min(now.addingTimeInterval(900), WidgetDateStyle.nextDay(after: now)) : now.addingTimeInterval(900)
        captureSize(context) { completion(Timeline(entries: [entry], policy: .after(refresh))) }
    }
    private func captureSize(_ context: Context, completion: @escaping () -> Void) {
        guard !context.isPreview else { completion(); return }
        let size: WidgetDesignSize
        if #available(iOS 27.0, *), context.family == .systemExtraLargePortrait { size = .extraLargePortrait }
        else {
            switch context.family {
            case .systemSmall: size = .small
            case .systemMedium: size = .medium
            case .systemLarge: size = .large
            default: completion(); return
            }
        }
        let display = context.displaySize
        // UIKit's screen/device access belongs to the main actor. Attempt the
        // tiny atomic record before completion; a write failure never blocks delivery.
        DispatchQueue.main.async {
            if UIDevice.current.userInterfaceIdiom == .phone, let store = try? WidgetDesignStore.shared() {
                let screen = UIScreen.main
                let bounds = screen.bounds
                let record = WidgetDisplayMeasurement(size: size,
                    screenWidth: Double(min(bounds.width, bounds.height)), screenHeight: Double(max(bounds.width, bounds.height)),
                    screenScale: Double(screen.scale), osMajor: ProcessInfo.processInfo.operatingSystemVersion.majorVersion,
                    width: Double(display.width), height: Double(display.height))
                try? WidgetDisplayMetricsStore(directory: store.directory).record(record)
            }
            completion()
        }
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
            let rgb = design.canvasBackgroundRGB
            let background = rgb.map { Color(red: $0.red, green: $0.green, blue: $0.blue) } ?? Color(uiColor: .systemBackground)
            if design.hasLiveText {
                guard #available(iOS 18.0, *) else { return StudioSlotEntry(date: .now, slot: slot, message: "Live text layers require iOS 18 or later.") }
                let (scene, data) = try store.scene(for: design)
                var images: [Int: UIImage] = [:]
                for (index, bytes) in data {
                    guard let image = UIImage(data: bytes) else { throw WidgetDesignError.imageUnavailable }; images[index] = image
                }
                return StudioSlotEntry(date: .now, slot: slot, name: design.name, transparentBackground: design.transparentBackground ?? false,
                    preserveCanvas: true, canvasBackground: background, canvasDimensions: design.canvasDimensions, scene: scene, sceneImages: images)
            }
            guard let image = UIImage(data: try store.imageData(for: design)) else { throw WidgetDesignError.imageUnavailable }
            return StudioSlotEntry(date: .now, slot: slot, image: image, name: design.name,
                transparentBackground: design.transparentBackground ?? false, preserveCanvas: design.preserveCanvas ?? false, canvasBackground: background,
                canvasDimensions: design.canvasDimensions)
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
            if #available(iOS 18.0, *), let scene = entry.scene {
                StudioClockSceneView(scene: scene, images: entry.sceneImages, background: entry.transparentBackground ? .clear : entry.canvasBackground)
                    .accessibilityLabel("Studio \(entry.slot): \(entry.name)")
            } else if let image = entry.image {
                GeometryReader { geometry in
                    // Correct only integer-PNG rounding when the saved point canvas
                    // matches this actual widget. Older/different-device canvases keep fit.
                    let exact = entry.canvasDimensions?.matches(width: Double(geometry.size.width), height: Double(geometry.size.height)) == true
                    let ratio: CGFloat? = exact ? geometry.size.width / geometry.size.height : nil
                    // Retain every exported canvas edge when iOS supplies slightly different family proportions.
                    // Previously saved catalogs retain their legacy fill until explicitly saved again in the app.
                    photo(image).aspectRatio(ratio, contentMode: entry.preserveCanvas ? .fit : .fill)
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
            if (entry.image != nil || entry.scene != nil) && entry.transparentBackground { Color.clear }
            else { Color(uiColor: .systemBackground) }
        }
    }
    @ViewBuilder private func photo(_ image: UIImage) -> some View {
        if #available(iOS 18.0, *) { Image(uiImage: image).resizable().widgetAccentedRenderingMode(.fullColor) }
        else { Image(uiImage: image).resizable() }
    }
}

@available(iOS 18.0, *)
private struct StudioClockSceneView: View {
    let scene: WidgetClockScene
    let images: [Int: UIImage]
    let background: Color
    var body: some View {
        GeometryReader { geometry in
            let scale = min(geometry.size.width / scene.width, geometry.size.height / scene.height)
            let width = scene.width * scale; let height = scene.height * scale
            ZStack(alignment: .topLeading) {
                background
                ForEach(Array(scene.parts.enumerated()), id: \.offset) { _, part in
                    if let index = part.imageIndex, let image = images[index] {
                        Image(uiImage: image).resizable().widgetAccentedRenderingMode(.fullColor).frame(width: width, height: height)
                    } else if let layer = part.clock {
                        textLayer(liveText(layer.clock), style: layer.clock, frame: layer.frame, opacity: layer.opacity, width: width, height: height)
                    } else if let layer = part.date {
                        textLayer(Text(.currentDate, format: layer.date.formatStyle()), style: layer.date.textStyle,
                            frame: layer.frame, opacity: layer.opacity, width: width, height: height)
                    }
                }
            }.frame(width: width, height: height, alignment: .topLeading).clipped().position(x: geometry.size.width / 2, y: geometry.size.height / 2)
        }
    }
    private func textLayer(_ text: Text, style: WidgetClockStyle, frame: WidgetClockFrame, opacity: Double, width: CGFloat, height: CGFloat) -> some View {
        text.font(font(style, size: min(1024, frame.height * height * style.fontScale), height: frame.height * height))
            .lineLimit(1).minimumScaleFactor(0.01)
            .foregroundStyle(colour(style)).multilineTextAlignment(textAlignment(style.alignment))
            .frame(width: frame.width * width, height: frame.height * height, alignment: alignment(style.alignment))
            .clipped().opacity(opacity).offset(x: frame.x * width, y: frame.y * height)
    }
    private func liveText(_ clock: WidgetClockStyle) -> Text {
        if #available(iOS 18.0, *) {
            return Text(.currentDate, format: clock.formatStyle())
        }
        return Text("iOS 18+")
    }
    private func font(_ clock: WidgetClockStyle, size: CGFloat, height: CGFloat) -> Font {
        let nativeWeight: UIFont.Weight
        let textWeight: Font.Weight
        switch clock.resolvedWeight {
        case "light": nativeWeight = .light; textWeight = .light
        case "medium": nativeWeight = .medium; textWeight = .medium
        case "semibold": nativeWeight = .semibold; textWeight = .semibold
        case "bold": nativeWeight = .bold; textWeight = .bold
        case "heavy": nativeWeight = .heavy; textWeight = .heavy
        default: nativeWeight = .regular; textWeight = .regular
        }
        let family = clock.postScriptName
        let system = UIFont.monospacedDigitSystemFont(ofSize: size, weight: nativeWeight)
        let rounded = clock.font == "sfProRounded" ? system.fontDescriptor.withDesign(.rounded) : nil
        let metrics = family.flatMap { UIFont(name: $0, size: size) }
            ?? rounded.map { UIFont(descriptor: $0, size: size) } ?? system
        // Match UIKit export: keep tall display-font line metrics inside the layer.
        let fitted = max(0.5, size * min(1, height / max(1, metrics.lineHeight)))
        if let family = family { return .custom(family, fixedSize: fitted) }
        return .system(size: fitted, weight: textWeight, design: clock.font == "sfProRounded" ? .rounded : .default).monospacedDigit()
    }
    private func colour(_ clock: WidgetClockStyle) -> Color {
        let rgb = clock.rgb ?? (1,1,1); return Color(red: rgb.0, green: rgb.1, blue: rgb.2)
    }
    private func alignment(_ name: String) -> Alignment { name == "left" ? .leading : name == "right" ? .trailing : .center }
    private func textAlignment(_ name: String) -> TextAlignment { name == "left" ? .leading : name == "right" ? .trailing : .center }
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
