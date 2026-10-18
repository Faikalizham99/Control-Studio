import SwiftUI
import WidgetKit
import OSLog

struct CounterEntry: TimelineEntry {
    let date: Date
    let snapshot: CounterSnapshot?
}

struct CounterProvider: TimelineProvider {
    func placeholder(in context: Context) -> CounterEntry {
        CounterEntry(date: .now, snapshot: CounterSnapshot(schemaVersion: 1, value: 12,
            updatedAtUnixMilliseconds: nil, storage: "shared"))
    }

    func getSnapshot(in context: Context, completion: @escaping (CounterEntry) -> Void) {
        completion(context.isPreview ? placeholder(in: context) : current())
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<CounterEntry>) -> Void) {
        completion(Timeline(entries: [current()], policy: .after(Date().addingTimeInterval(900))))
    }

    private func current() -> CounterEntry {
        do { return CounterEntry(date: .now, snapshot: try CounterStore.shared().read()) }
        catch {
            Logger(subsystem: "com.faikalizham.controlstudio", category: "Widget")
                .error("Shared counter unavailable. Code=\((error as? CounterError)?.rawValue ?? "unknown", privacy: .public)")
            return CounterEntry(date: .now, snapshot: nil)
        }
    }
}

struct CounterWidgetView: View {
    let entry: CounterEntry
    @Environment(\.widgetFamily) private var family

    var body: some View {
        HStack(spacing: 18) {
            VStack(alignment: .leading, spacing: 6) {
                Label("Control Studio", systemImage: "square.grid.2x2.fill")
                    .font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                if let snapshot = entry.snapshot {
                    Text("\(snapshot.value)").font(.system(size: 46, weight: .bold, design: .rounded))
                        .minimumScaleFactor(0.4).lineLimit(1).accessibilityLabel("Counter \(snapshot.value)")
                    Text("Last Updated").font(.caption2).foregroundStyle(.secondary)
                    if let milliseconds = snapshot.updatedAtUnixMilliseconds {
                        Text(Date(timeIntervalSince1970: Double(milliseconds) / 1000), style: .time)
                            .font(.caption).lineLimit(1)
                    } else { Text("Not yet").font(.caption) }
                } else {
                    Text("Open Control Studio").font(.headline)
                    Text("Shared counter unavailable. Check App Group signing.").font(.caption)
                }
            }
            if family == .systemMedium {
                Spacer(minLength: 0)
                Image(systemName: "square.grid.2x2.fill").font(.system(size: 42)).foregroundStyle(.blue)
                    .accessibilityHidden(true)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .containerBackground(.background, for: .widget)
    }
}

@main
struct ControlStudioWidgets: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: StudioConstants.widgetKind, provider: CounterProvider()) { entry in
            CounterWidgetView(entry: entry)
        }
        .configurationDisplayName("Control Studio Counter")
        .description("Your locally shared counter and its last update.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}
