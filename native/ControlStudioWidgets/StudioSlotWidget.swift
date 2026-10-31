import SwiftUI
import WidgetKit
import UIKit

struct StudioSlotEntry: TimelineEntry {
    let date: Date
    let slot: Int
    var image: UIImage? = nil
    var name = ""
    var textDescription = ""
    var transparentBackground = false
    var preserveCanvas = false
    var canvasBackground: Color = .clear
    var canvasDimensions: WidgetCanvasDimensions? = nil
    var scene: WidgetClockScene? = nil
    var sceneImages: [Int: UIImage] = [:]
    var battery = WidgetBatteryReading()
    var message = "Assign a saved design to this slot in Control Studio, under Studio → My Widgets → Widget slots."
}

struct StudioSlotProvider: TimelineProvider {
    let slot: Int
    func placeholder(in context: Context) -> StudioSlotEntry { StudioSlotEntry(date: .now, slot: slot) }
    func getSnapshot(in context: Context, completion: @escaping (StudioSlotEntry) -> Void) {
        // Gallery entries must not decode every full-size photo at once. The app shows assigned previews.
        if context.isPreview {
            completion(StudioSlotEntry(date: .now, slot: slot, message: "Shows the design assigned to this slot and size. Choose it in Control Studio → Widget slots."))
            return
        }
        let entry = current(context.family)
        captureSize(context) { sampleBattery(entry, completion: completion) }
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<StudioSlotEntry>) -> Void) {
        let entry = current(context.family)
        let now = Date()
        let refresh = entry.scene?.parts.contains(where: { $0.date != nil }) == true
            ? min(now.addingTimeInterval(900), WidgetDateStyle.nextDay(after: now)) : now.addingTimeInterval(900)
        captureSize(context) { sampleBattery(entry) { sampled in completion(Timeline(entries: [sampled], policy: .after(refresh))) } }
    }
    private func sampleBattery(_ entry: StudioSlotEntry, completion: @escaping (StudioSlotEntry) -> Void) {
        guard entry.scene?.parts.contains(where: { $0.battery != nil }) == true else { completion(entry); return }
        DispatchQueue.main.async {
            let device = UIDevice.current; let enabled = device.isBatteryMonitoringEnabled
            if !enabled { device.isBatteryMonitoringEnabled = true }
            defer { if !enabled { device.isBatteryMonitoringEnabled = false } }
            let level = device.batteryLevel; let state = device.batteryState
            var sampled = entry
            let known = level.isFinite && (0...1).contains(level)
            sampled.battery = WidgetBatteryReading(percent: known ? min(100, max(0, Int((level * 100).rounded()))) : nil,
                charging: known && (state == .charging || state == .full), full: known && state == .full)
            completion(sampled)
        }
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
                guard #available(iOS 18.0, *) else { return StudioSlotEntry(date: .now, slot: slot, message: "Live layers require iOS 18 or later.") }
                let (scene, data) = try store.scene(for: design)
                var images: [Int: UIImage] = [:]
                for (index, bytes) in data {
                    guard let image = UIImage(data: bytes) else { throw WidgetDesignError.imageUnavailable }; images[index] = image
                }
                return StudioSlotEntry(date: .now, slot: slot, name: design.name, textDescription: design.accessibilityText, transparentBackground: design.transparentBackground ?? false,
                    preserveCanvas: true, canvasBackground: background, canvasDimensions: design.canvasDimensions, scene: scene, sceneImages: images)
            }
            guard let image = UIImage(data: try store.imageData(for: design)) else { throw WidgetDesignError.imageUnavailable }
            return StudioSlotEntry(date: .now, slot: slot, image: image, name: design.name, textDescription: design.accessibilityText,
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
    @Environment(\.widgetFamily) private var family
    private var tapURL: URL? {
        let size: WidgetDesignSize
        if #available(iOS 27.0, *), family == .systemExtraLargePortrait { size = .extraLargePortrait }
        else {
            switch family {
            case .systemSmall: size = .small
            case .systemMedium: size = .medium
            case .systemLarge: size = .large
            default: return nil
            }
        }
        return WidgetTapLinks.slot(size: size, number: entry.slot)
    }
    var body: some View {
        Group {
            if #available(iOS 18.0, *), let scene = entry.scene {
                StudioClockSceneView(scene: scene, images: entry.sceneImages, background: entry.transparentBackground ? .clear : entry.canvasBackground, battery: entry.battery)
                    .accessibilityLabel("Studio \(entry.slot): \(entry.name)")
                    .accessibilityValue(entry.textDescription + (scene.parts.contains(where: { $0.battery != nil }) ? ". Battery " + entry.battery.description : ""))
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
                        .accessibilityValue(entry.textDescription)
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
        .widgetURL(tapURL)
        .accessibilityHint("Opens Control Studio for this widget slot.")
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
    let battery: WidgetBatteryReading
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
                        textLayer(liveText(layer.clock), style: layer.clock, frame: layer.frame, opacity: layer.opacity, rotation: layer.rotationDegrees ?? 0, width: width, height: height)
                    } else if let layer = part.battery {
                        StudioBatteryArtwork(style: layer.battery, reading: battery)
                            .frame(width: layer.frame.width * width, height: layer.frame.height * height)
                            .clipped().opacity(layer.opacity).rotationEffect(.degrees(layer.rotationDegrees ?? 0))
                            .offset(x: layer.frame.x * width, y: layer.frame.y * height)
                    } else if let layer = part.date {
                        textLayer(Text(.currentDate, format: layer.date.formatStyle()), style: layer.date.textStyle,
                            frame: layer.frame, opacity: layer.opacity, rotation: layer.rotationDegrees ?? 0, width: width, height: height)
                    }
                }
            }.frame(width: width, height: height, alignment: .topLeading).clipped().position(x: geometry.size.width / 2, y: geometry.size.height / 2)
        }
    }
    private func textLayer(_ text: Text, style: WidgetClockStyle, frame: WidgetClockFrame, opacity: Double, rotation: Double, width: CGFloat, height: CGFloat) -> some View {
        styledText(text, style: style)
            .font(font(style, size: min(1024, frame.height * height * style.fontScale), height: frame.height * height))
            .lineLimit(1).minimumScaleFactor(0.01)
            .multilineTextAlignment(textAlignment(style.alignment))
            .frame(width: frame.width * width, height: frame.height * height, alignment: alignment(style.alignment))
            .clipped().opacity(opacity).rotationEffect(.degrees(rotation)).offset(x: frame.x * width, y: frame.y * height)
    }
    // Keep the date source and fill inside Text before any View-only modifiers.
    // The explicit return type selects Text.foregroundStyle, not View.foregroundStyle.
    // WidgetKit must retain the live text node instead of a view-level gradient recipe.
    private func styledText(_ text: Text, style: WidgetClockStyle) -> Text {
        if let options = style.gradient, options.enabled {
            return text.foregroundStyle(gradient(options))
        }
        return text.foregroundStyle(colour(style))
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
    private func gradient(_ options: WidgetTextGradient) -> LinearGradient {
        let colours = [options.startHex, options.middleHex, options.endHex].map { hex -> Color in
            let rgb = WidgetClockStyle.colourRGB(hex) ?? (1, 1, 1)
            return Color(red: rgb.0, green: rgb.1, blue: rgb.2)
        }
        return LinearGradient(stops: [.init(color: colours[0], location: 0), .init(color: colours[1], location: 0.5), .init(color: colours[2], location: 1)],
            startPoint: UnitPoint(x: options.startPoint.0, y: options.startPoint.1), endPoint: UnitPoint(x: options.endPoint.0, y: options.endPoint.1))
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
// BEGIN GENERATED SLOTS widgets (scripts/widget_slot_config.py)
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

struct StudioSlotElevenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 11).configurationDisplayName("Studio 11")
    }
}

struct StudioSlotTwelveWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 12).configurationDisplayName("Studio 12")
    }
}

struct StudioSlotThirteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 13).configurationDisplayName("Studio 13")
    }
}

struct StudioSlotFourteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 14).configurationDisplayName("Studio 14")
    }
}

struct StudioSlotFifteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 15).configurationDisplayName("Studio 15")
    }
}

struct StudioSlotSixteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 16).configurationDisplayName("Studio 16")
    }
}

struct StudioSlotSeventeenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 17).configurationDisplayName("Studio 17")
    }
}

struct StudioSlotEighteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 18).configurationDisplayName("Studio 18")
    }
}

struct StudioSlotNineteenWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 19).configurationDisplayName("Studio 19")
    }
}

struct StudioSlotTwentyWidget: Widget {
    var body: some WidgetConfiguration {
        StudioSlotConfiguration.make(slot: 20).configurationDisplayName("Studio 20")
    }
}

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

struct StudioSlotsElevenToFifteen: WidgetBundle {
    var body: some Widget {
        StudioSlotElevenWidget()
        StudioSlotTwelveWidget()
        StudioSlotThirteenWidget()
        StudioSlotFourteenWidget()
        StudioSlotFifteenWidget()
    }
}

struct StudioSlotsSixteenToTwenty: WidgetBundle {
    var body: some Widget {
        StudioSlotSixteenWidget()
        StudioSlotSeventeenWidget()
        StudioSlotEighteenWidget()
        StudioSlotNineteenWidget()
        StudioSlotTwentyWidget()
    }
}
// END GENERATED SLOTS widgets

// Same canonical 160 x 100 artwork as WidgetBatteryDrawing in the app.
private struct StudioBatteryArtwork: View {
    let style: WidgetBatteryStyle
    let reading: WidgetBatteryReading
    var body: some View {
        Canvas { context, size in
            let scale = min(size.width / 160, size.height / 100)
            guard scale > 0 else { return }
            context.translateBy(x: (size.width - 160 * scale) / 2, y: (size.height - 100 * scale) / 2)
            context.scaleBy(x: scale, y: scale)
            let rgb = WidgetClockStyle.colourRGB(style.colourHex(reading)) ?? (0.56, 0.56, 0.58)
            let colour = Color(red: rgb.0, green: rgb.1, blue: rgb.2)
            let track = Color(red: 142.0 / 255, green: 142.0 / 255, blue: 147.0 / 255).opacity(0.28)
            let fraction = reading.known ? Double(reading.percent!) / 100 : 0
            func rect(_ x: Double, _ y: Double, _ width: Double, _ height: Double, _ radius: Double, _ tint: Color, fill: Bool = true) {
                let path = Path(roundedRect: CGRect(x: x, y: y, width: width, height: height), cornerRadius: radius)
                if fill { context.fill(path, with: .color(tint)) } else { context.stroke(path, with: .color(tint), lineWidth: 3) }
            }
            func line(_ points: [CGPoint], _ tint: Color, _ stroke: Double, fill: Bool = false) {
                guard let first = points.first else { return }
                var path = Path(); path.move(to: first); for point in points.dropFirst() { path.addLine(to: point) }
                if fill { path.closeSubpath(); context.fill(path, with: .color(tint)) }
                else { context.stroke(path, with: .color(tint), style: StrokeStyle(lineWidth: stroke, lineCap: .round, lineJoin: .round)) }
            }
            func arc(_ cx: Double, _ cy: Double, _ radius: Double, _ start: Double, _ sweep: Double, _ tint: Color) {
                guard sweep > 0 else { return }
                line((0...64).map { i in
                    let angle = (start + sweep * Double(i) / 64) * Double.pi / 180
                    return CGPoint(x: cx + radius * cos(angle), y: cy + radius * sin(angle))
                }, tint, 7)
            }
            func text(_ value: String, _ box: WidgetBatteryLabelLayout, _ ink: Color, _ outline: Color? = nil) {
                let label = Text(value).font(.custom("Arial-BoldMT", fixedSize: box.fontSize))
                let centre = CGPoint(x: box.x + box.width / 2, y: box.y + box.height / 2)
                if let outline = outline {
                    for dx in [-0.75, 0, 0.75] {
                        for dy in [-0.75, 0, 0.75] where dx != 0 || dy != 0 {
                            context.draw(label.foregroundColor(outline), at: CGPoint(x: centre.x + dx, y: centre.y + dy), anchor: .center)
                        }
                    }
                }
                context.draw(label.foregroundColor(ink), at: centre, anchor: .center)
            }
            switch style.shape {
            case "battery":
                rect(30, 12, 94, 48, 8, colour, fill: false); rect(127, 28, 5, 16, 2, colour)
                rect(35, 17, 84, 38, 4, track)
                if fraction > 0 { rect(35, 17, 84 * fraction, 38, min(4, 42 * fraction), colour) }
            case "bar":
                rect(12, 18, 136, 30, 15, track)
                if fraction > 0 { rect(12, 18, 136 * fraction, 30, min(15, 68 * fraction), colour) }
            case "ring": arc(80, 34, 27, -90, 360, track); arc(80, 34, 27, -90, 360 * fraction, colour)
            case "arc": arc(80, 49, 37, -180, 180, track); arc(80, 49, 37, -180, 180 * fraction, colour)
            default: break
            }
            if style.shape == "percentage" || style.showPercentage || !reading.known {
                let standalone = style.shape == "percentage"
                let white = style.usesWhiteLabel(reading)
                text(reading.text, style.labelLayout, standalone ? colour : white ? .white : .black, standalone ? nil : white ? .black : .white)
            }
            if style.showCharging && reading.known && (reading.charging || reading.full) {
                if reading.full { line([CGPoint(x: 136, y: 80), CGPoint(x: 141, y: 85), CGPoint(x: 150, y: 73)], colour, 3) }
                else { line([CGPoint(x: 144, y: 69), CGPoint(x: 136, y: 81), CGPoint(x: 142, y: 81), CGPoint(x: 139, y: 91), CGPoint(x: 150, y: 77), CGPoint(x: 144, y: 77)], colour, 0, fill: true) }
            }
        }
    }
}
