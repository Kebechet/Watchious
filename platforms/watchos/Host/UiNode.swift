import Foundation

struct UiTree: Decodable {
    let version: Int
    let children: [UiNode]
}

struct UiNode: Decodable {
    let type: String
    let attributes: [String: String?]?
    let events: [String: UInt64]?
    let children: [UiNode]?
    let text: String?

    func attribute(_ name: String) -> String? {
        attributes?[name] ?? nil
    }

    func eventHandlerId(_ name: String) -> UInt64? {
        events?[name]
    }

    var childNodes: [UiNode] {
        children ?? []
    }

    func first(ofType type: String) -> UiNode? {
        for child in childNodes {
            if child.type == type {
                return child
            }
            if let match = child.first(ofType: type) {
                return match
            }
        }
        return nil
    }
}
