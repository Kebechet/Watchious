import Foundation
import Observation

@MainActor
@Observable
final class WatchiousStore {
    static let shared = WatchiousStore()

    let tree = UiTree()
    private(set) var lastError: String?
    // DebugAutomation's stand-in for the back button, which the simulator CLI can't tap.
    private(set) var automatedBackCount = 0

    func apply(_ patches: Data) {
        do {
            try tree.apply(patches)
        } catch {
            report("Invalid Watchious patches: \(error)")
        }
    }

    func goBackLikeTheBackButton() {
        automatedBackCount += 1
    }

    func report(_ error: String) {
        lastError = error
        NSLog("[Watchious] %@", error)
    }

    func dispatch(_ eventHandlerId: UInt64, _ eventName: String, value: String? = nil) {
        let result: Int32
        if let value {
            result = watchious_dispatch(eventHandlerId, eventName, value)
        } else {
            result = watchious_dispatch(eventHandlerId, eventName, nil)
        }
        if result != 0 {
            report("watchious_dispatch(\(eventName)) returned \(result)")
        }
    }

    func firstNode(ofType type: String) -> UiNode? {
        tree.root.first(ofType: type)
    }
}

private func copyText(_ bytes: UnsafePointer<UInt8>?, _ length: Int32) -> Data? {
    guard let bytes else {
        return nil
    }
    return Data(bytes: bytes, count: Int(length))
}

// Called on the thread .NET rendered on. The main queue keeps the batches in order.
let watchiousPatchesCallback: watchious_patches_callback = { bytes, length in
    guard let data = copyText(bytes, length) else {
        return
    }
    DispatchQueue.main.async {
        WatchiousStore.shared.apply(data)
    }
}

let watchiousErrorCallback: watchious_text_callback = { utf8, length in
    guard let data = copyText(utf8, length) else {
        return
    }
    let message = String(decoding: data, as: UTF8.self)
    DispatchQueue.main.async {
        WatchiousStore.shared.report(message)
    }
}
