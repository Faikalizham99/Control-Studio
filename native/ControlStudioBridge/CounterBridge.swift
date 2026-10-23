import Foundation
import WidgetKit
import OSLog

private let bridgeLog = Logger(subsystem: "com.faikalizham.controlstudio", category: "CounterBridge")

private func response(operation: CounterOperation) -> UnsafeMutablePointer<CChar>? {
    do {
        let store = try CounterStore.forHost()
        var snapshot: CounterSnapshot
        switch operation {
        case .read: snapshot = try store.read()
        case .increment: snapshot = try store.increment()
        case .reset: snapshot = try store.reset()
        }
        if operation != .read && snapshot.storage == "shared" {
            WidgetCenter.shared.reloadTimelines(ofKind: StudioConstants.widgetKind)
            snapshot.refreshRequested = true
        }
        let data = try JSONEncoder().encode(snapshot)
        bridgeLog.info("Counter operation succeeded. Shared=\(snapshot.storage == "shared", privacy: .public)")
        return strdup(String(decoding: data, as: UTF8.self))
    } catch {
        let code = (error as? CounterError)?.rawValue ?? "databaseFailure"
        bridgeLog.error("Counter operation failed. Code=\(code, privacy: .public)")
        return strdup("{\"error\":\"\(code)\"}")
    }
}

@_cdecl("cs_counter_read")
public func counterRead() -> UnsafeMutablePointer<CChar>? { response(operation: .read) }

@_cdecl("cs_counter_increment")
public func counterIncrement() -> UnsafeMutablePointer<CChar>? { response(operation: .increment) }

@_cdecl("cs_counter_reset")
public func counterReset() -> UnsafeMutablePointer<CChar>? { response(operation: .reset) }

@_cdecl("cs_counter_free")
public func counterFree(_ pointer: UnsafeMutablePointer<CChar>?) { free(pointer) }

@_cdecl("cs_widgets_reload_slots")
public func reloadWidgetSlots() {
    for number in 1...WidgetDesignStore.slotsPerSize {
        WidgetCenter.shared.reloadTimelines(ofKind: WidgetDesignStore.slotKind(number))
    }
}

@_cdecl("cs_widgets_reload_slot")
public func reloadWidgetSlot(_ number: Int32) {
    guard (1...WidgetDesignStore.slotsPerSize).contains(Int(number)) else { return }
    WidgetCenter.shared.reloadTimelines(ofKind: WidgetDesignStore.slotKind(Int(number)))
}
