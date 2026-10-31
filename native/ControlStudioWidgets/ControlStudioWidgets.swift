import SwiftUI
import WidgetKit
import OSLog

struct CounterEntry: TimelineEntry {
    let date: Date
    let snapshot: CounterSnapshot?
    var failure: CounterError? = nil
    var appearance: CounterAppearance = .standard
}

struct CounterProvider: TimelineProvider {
    func placeholder(in context: Context) -> CounterEntry {
        CounterEntry(date: .now, snapshot: CounterSnapshot(schemaVersion: 1, value: 12,
            updatedAtUnixMilliseconds: Int64(Date().timeIntervalSince1970 * 1000), storage: "shared"), appearance: appearance())
    }

    func getSnapshot(in context: Context, completion: @escaping (CounterEntry) -> Void) {
        completion(context.isPreview ? placeholder(in: context) : current())
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<CounterEntry>) -> Void) {
        completion(Timeline(entries: [current()], policy: .after(Date().addingTimeInterval(900))))
    }

    private func current() -> CounterEntry {
        let style = appearance()
        do { return CounterEntry(date: .now, snapshot: try CounterStore.shared().read(), appearance: style) }
        catch {
            Logger(subsystem: "com.faikalizham.controlstudio", category: "Widget")
                .error("Shared counter unavailable. Code=\((error as? CounterError)?.rawValue ?? "unknown", privacy: .public)")
            return CounterEntry(date: .now, snapshot: nil, failure: (error as? CounterError) ?? .databaseFailure, appearance: style)
        }
    }
    private func appearance() -> CounterAppearance {
        (try? CounterAppearanceStore.shared().readOrDefault()) ?? .standard
    }
}

struct CounterWidgetView: View {
    let entry: CounterEntry
    @Environment(\.widgetFamily) private var family
    @Environment(\.colorScheme) private var systemScheme
    private var dark: Bool { entry.appearance.usesDark(systemDark: systemScheme == .dark) }
    private var secondary: Color { Color(red: dark ? 174 / 255.0 : 108 / 255.0,
                                        green: dark ? 174 / 255.0 : 108 / 255.0,
                                        blue: dark ? 178 / 255.0 : 112 / 255.0) }
    private var background: Color { dark ? Color(red: 28 / 255.0, green: 28 / 255.0, blue: 30 / 255.0) : .white }
    private func icon(size: CGFloat) -> some View {
        let rgb = entry.appearance.rgb
        return Image(systemName: "square.grid.2x2.fill")
            .font(.system(size: size))
            .foregroundStyle(Color(red: rgb.red, green: rgb.green, blue: rgb.blue))
            .padding(4)
            .background(entry.appearance.usesWhiteIconBackground ? Color.white : Color.black,
                        in: RoundedRectangle(cornerRadius: size > 20 ? 12 : 6))
            .accessibilityHidden(true)
    }

    var body: some View {
        HStack(spacing: 18) {
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: 4) {
                    icon(size: 12)
                    Text("Control Studio").font(.caption.weight(.semibold)).foregroundStyle(secondary).lineLimit(1)
                }
                if let snapshot = entry.snapshot {
                    Text("\(snapshot.value)").font(.system(size: 46, weight: .bold, design: .rounded))
                        .minimumScaleFactor(0.4).lineLimit(1).accessibilityLabel("Counter \(snapshot.value)")
                    Text("Last Updated").font(.caption2).foregroundStyle(secondary)
                    if let milliseconds = snapshot.updatedAtUnixMilliseconds {
                        Text(Date(timeIntervalSince1970: Double(milliseconds) / 1000), style: .time)
                            .font(.caption).lineLimit(1).foregroundStyle(secondary)
                    } else { Text("Not yet").font(.caption).foregroundStyle(secondary) }
                } else {
                    Text((entry.failure ?? .databaseFailure).widgetTitle).font(.headline)
                    Text((entry.failure ?? .databaseFailure).widgetMessage).font(.caption)
                    Text((entry.failure ?? .databaseFailure).rawValue).font(.caption2).foregroundStyle(secondary)
                }
            }
            if family == .systemMedium {
                Spacer(minLength: 0)
                icon(size: 42)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .foregroundStyle(dark ? Color.white : Color(red: 22 / 255.0, green: 23 / 255.0, blue: 28 / 255.0))
        .environment(\.colorScheme, dark ? .dark : .light)
        .widgetURL(WidgetTapLinks.counter)
        .accessibilityHint("Opens the counter in Control Studio.")
        .containerBackground(for: .widget) { background }
    }
}

struct CounterWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: StudioConstants.widgetKind, provider: CounterProvider()) { entry in
            CounterWidgetView(entry: entry)
        }
        .configurationDisplayName("Control Studio Counter")
        .description("Your locally shared counter and its last update.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

@main
struct ControlStudioWidgets: WidgetBundle {
    init() { CSInstallWidgetBackgroundAdapter() }
    var body: some Widget {
        CounterWidget()
// BEGIN GENERATED SLOTS bundle (scripts/widget_slot_config.py)
        StudioSlotsOneToFive().body
        StudioSlotsSixToTen().body
        StudioSlotsElevenToFifteen().body
        StudioSlotsSixteenToTwenty().body
// END GENERATED SLOTS bundle
    }
}
