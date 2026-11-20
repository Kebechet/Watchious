import Foundation
import SwiftUI

@MainActor
final class WatchiousStore: ObservableObject {
    static let shared = WatchiousStore()

    @Published private(set) var nodes: [UiNode] = []
    @Published private(set) var lastError: String?

    func apply(_ json: Data) {
        do {
            nodes = try JSONDecoder().decode(UiTree.self, from: json).children
        } catch {
            lastError = "Invalid UI tree: \(error)"
        }
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
        for node in nodes {
            if node.type == type {
                return node
            }
            if let match = node.first(ofType: type) {
                return match
            }
        }
        return nil
    }
}

private func copyText(_ utf8: UnsafePointer<UInt8>?, _ length: Int32) -> Data? {
    guard let utf8 else {
        return nil
    }
    return Data(bytes: utf8, count: Int(length))
}

let watchiousRenderCallback: watchious_text_callback = { utf8, length in
    guard let data = copyText(utf8, length) else {
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
