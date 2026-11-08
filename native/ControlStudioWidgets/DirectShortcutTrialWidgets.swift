import SwiftUI
import WidgetKit
import AppIntents

// Separate kinds, entries and providers: these trials do not read or change
// designs/slots and never route through the host or a URL scheme.
@available(iOS 27.0, *)
struct DirectShortcutStaticEntry: TimelineEntry {
    let date: Date
}

@available(iOS 27.0, *)
struct DirectShortcutStaticProvider: TimelineProvider {
    func placeholder(in context: Context) -> DirectShortcutStaticEntry { .init(date: .now) }
    func getSnapshot(in context: Context, completion: @escaping (DirectShortcutStaticEntry) -> Void) {
        completion(.init(date: .now))
    }
    func getTimeline(in context: Context, completion: @escaping (Timeline<DirectShortcutStaticEntry>) -> Void) {
        completion(Timeline(entries: [.init(date: .now)], policy: .never))
    }
}

@available(iOS 27.0, *)
struct DirectShortcutPickerConfiguration: WidgetConfigurationIntent {
    static var title: LocalizedStringResource { "Direct Shortcut Test" }
    static var description: IntentDescription { "Choose the action to run from this test widget." }
    static var isDiscoverable: Bool { false }

    @Parameter(title: "Action")
    var action: SystemShortcut?
}

@available(iOS 27.0, *)
struct DirectShortcutPickerEntry: TimelineEntry {
    let date: Date
    let configuration: DirectShortcutPickerConfiguration
}

@available(iOS 27.0, *)
struct DirectShortcutPickerProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> DirectShortcutPickerEntry {
        .init(date: .now, configuration: DirectShortcutPickerConfiguration())
    }
    func snapshot(for configuration: DirectShortcutPickerConfiguration, in context: Context) async -> DirectShortcutPickerEntry {
        .init(date: .now, configuration: configuration)
    }
    func timeline(for configuration: DirectShortcutPickerConfiguration, in context: Context) async -> Timeline<DirectShortcutPickerEntry> {
        Timeline(entries: [.init(date: .now, configuration: configuration)], policy: .never)
    }
}

@available(iOS 27.0, *)
private struct DirectShortcutTrialFace: View {
    let mode: String
    let title: LocalizedStringResource
    let detail: LocalizedStringKey
    let configured: Bool
    @Environment(\.widgetFamily) private var family
    private var compact: Bool { family == .systemSmall || family == .systemMedium }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label(mode, systemImage: "bolt.circle")
                .font(.caption.weight(.medium)).foregroundStyle(.secondary).lineLimit(1)
            if !compact { Spacer(minLength: 0) }
            Label {
                Text(title).font(.headline).lineLimit(2).minimumScaleFactor(0.8)
            } icon: {
                Image(systemName: configured ? "play.circle.fill" : "slider.horizontal.3")
                    .font(.title2).foregroundStyle(.tint)
            }
            Text(detail)
                .font(.caption2).foregroundStyle(.secondary).lineLimit(compact ? 2 : 3)
            if !compact {
                Text("A Shortcut that asks for input may open Shortcuts. This test does not use your Studio slots.")
                    .font(.caption).foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if !compact { Spacer(minLength: 0) }
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .contentShape(Rectangle())
    }
}

@available(iOS 27.0, *)
struct DirectShortcutStaticTrialView: View {
    var body: some View {
        // Parameterless resolution is an experiment, not an assumed replacement
        // for Apple's explicitly configured SystemShortcut path.
        Button(intent: RunSystemShortcutIntent()) {
            DirectShortcutTrialFace(mode: "Static Shortcut test", title: "Run Shortcut",
                                    detail: "Try Edit Widget to choose an action.", configured: true)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Run system-selected Shortcut. Static test.")
        .accessibilityHint("Choose an action in the widget's system configuration if iOS offers it. Shortcuts may request input.")
        .containerBackground(for: .widget) { Color.primary.opacity(0.07) }
    }
}

@available(iOS 27.0, *)
struct DirectShortcutPickerTrialView: View {
    let entry: DirectShortcutPickerEntry
    var body: some View {
        Group {
            if let action = entry.configuration.action {
                Button(intent: RunSystemShortcutIntent(shortcut: action)) {
                    DirectShortcutTrialFace(mode: "Picker Shortcut test", title: action.displayRepresentation.title,
                                            detail: "iOS runs the chosen action.", configured: true)
                }
                .accessibilityLabel(Text(action.displayRepresentation.title))
                .accessibilityHint("Runs the action chosen in Edit Widget. Shortcuts may request input.")
            } else {
                // No chosen action: an inactive button, not a fabricated value
                // or automatic launch. Configure Action before testing a tap.
                Button(intent: RunSystemShortcutIntent()) {
                    DirectShortcutTrialFace(mode: "Picker Shortcut test", title: "Choose an action",
                                            detail: "Hold widget → Edit Widget → Action", configured: false)
                }
                .disabled(true)
                .accessibilityLabel("No action chosen. Hold the widget and choose Edit Widget, then Action.")
            }
        }
        .buttonStyle(.plain)
        .containerBackground(for: .widget) { Color.primary.opacity(0.07) }
    }
}

@available(iOS 27.0, *)
struct DirectShortcutStaticTrialWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: "ControlStudio.Trial.Shortcut.Static", provider: DirectShortcutStaticProvider()) { _ in
            DirectShortcutStaticTrialView()
        }
        .configurationDisplayName("Shortcut Test - Static")
        .description("Tests direct system Shortcut execution without changing your Studio slots. Configure the action if iOS offers it.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge, .systemExtraLargePortrait])
        .contentMarginsDisabled()
    }
}

@available(iOS 27.0, *)
struct DirectShortcutPickerTrialWidget: Widget {
    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: "ControlStudio.Trial.Shortcut.Picker", intent: DirectShortcutPickerConfiguration.self,
                               provider: DirectShortcutPickerProvider()) { entry in
            DirectShortcutPickerTrialView(entry: entry)
        }
        .configurationDisplayName("Shortcut Test - Picker")
        .description("Choose a system action in Edit Widget to test direct Shortcut execution without changing your Studio slots.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge, .systemExtraLargePortrait])
        .contentMarginsDisabled()
    }
}

@available(iOS 27.0, *)
struct DirectShortcutTrialWidgets: WidgetBundle {
    var body: some Widget {
        DirectShortcutStaticTrialWidget()
        DirectShortcutPickerTrialWidget()
    }
}
