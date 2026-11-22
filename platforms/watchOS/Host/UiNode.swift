import Foundation
import Observation

// Observable, so a patch re-renders only the views that read what it changed.
@Observable
final class UiNode: Identifiable {
    static let textType = "#text"

    let id: UInt32
    let type: String
    var attributes: [String: String] = [:]
    var events: [String: UInt64] = [:]
    var children: [UiNode] = []
    var text: String?

    init(id: UInt32, type: String) {
        self.id = id
        self.type = type
    }

    var isBlankText: Bool {
        type == Self.textType && (text ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func attribute(_ name: String) -> String? {
        attributes[name]
    }

    func eventHandlerId(_ name: String) -> UInt64? {
        events[name]
    }

    func first(ofType type: String) -> UiNode? {
        first { $0.type == type }
    }

    func first(where predicate: (UiNode) -> Bool) -> UiNode? {
        for child in children {
            if predicate(child) {
                return child
            }
            if let match = child.first(where: predicate) {
                return match
            }
        }
        return nil
    }
}
