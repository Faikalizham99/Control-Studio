import AppIntents
import WidgetKit
import OSLog

struct IncrementControlStudioCounter: AppIntent {
    static let title: LocalizedStringResource = "Increment Control Studio Counter"
    static let description = IntentDescription("Increment the shared Control Studio counter and request a widget refresh.")
    static let openAppWhenRun = false
    static let isDiscoverable = true

    func perform() async throws -> some IntentResult & ReturnsValue<Int> & ProvidesDialog {
        do {
            let snapshot = try CounterStore.shared().increment()
            WidgetCenter.shared.reloadTimelines(ofKind: StudioConstants.widgetKind)
            return .result(value: Int(snapshot.value), dialog: "Control Studio counter is now \(snapshot.value). Widget refresh requested.")
        } catch {
            Logger(subsystem: "com.faikalizham.controlstudio", category: "AppIntent")
                .error("Increment failed. Code=\((error as? CounterError)?.rawValue ?? "unknown", privacy: .public)")
            throw error
        }
    }
}

struct ControlStudioShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(intent: IncrementControlStudioCounter(),
                    phrases: ["Increment my counter in \(.applicationName)"],
                    shortTitle: "Increment Counter", systemImageName: "plus.circle.fill")
    }
}

@main
struct ControlStudioIntents: AppIntentsExtension { }
