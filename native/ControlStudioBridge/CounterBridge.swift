import Foundation
import WidgetKit
import OSLog

private let bridgeLog = Logger(subsystem: "com.faikalizham.controlstudio", category: "CounterBridge")

private func response(increment: Bool) -> UnsafeMutablePointer<CChar>? {
    do {
        let store = try CounterStore.forHost()
        var snapshot = try increment ? store.increment() : store.read()
        if increment && snapshot.storage == "shared" {
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
public func counterRead() -> UnsafeMutablePointer<CChar>? { response(increment: false) }

@_cdecl("cs_counter_increment")
public func counterIncrement() -> UnsafeMutablePointer<CChar>? { response(increment: true) }

@_cdecl("cs_counter_free")
public func counterFree(_ pointer: UnsafeMutablePointer<CChar>?) { free(pointer) }
