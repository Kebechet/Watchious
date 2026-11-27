import SwiftUI

// SwiftUI's digitalCrownRotation on the content, which takes the crown once it is focused (touched, on a screen that
// scrolls). While the crown turns, C#'s answers to earlier steps arrive after newer steps and would set the value back:
// the view keeps its own value until the crown has rested for a moment, then follows C# again.
struct DigitalCrownRotationNode: View {
    let node: UiNode
    let store: WatchiousStore

    @State private var value = 0.0
    // The value C# knows, from it or from the last change sent to it; a change of value to anything else is the crown's.
    @State private var knownValue = 0.0
    @State private var lastTurn = Date.distantPast
    @Environment(\.watchiousLayoutParent) private var parent

    var body: some View {
        StackLayout(spacing: nil, fillsWidth: parent != .row && !node.keepsOwnWidth) {
            ForEach(node.placedChildren) { child in
                NodeView(node: child, store: store)
            }
        }
        .environment(\.watchiousLayoutParent, .stack)
        .focusable()
        .digitalCrownRotation($value, from: from, through: through, by: by)
        .onChange(of: value) { _, newValue in
            let turned = Self.rounded(newValue)
            guard turned != knownValue, let eventHandlerId = node.eventHandlerId("changed") else {
                return
            }
            knownValue = turned
            lastTurn = .now
            store.dispatch(eventHandlerId, "changed", value: String(turned))
        }
        .task(id: Sync(sentValue: sentValue, lastTurn: lastTurn)) {
            let rest = lastTurn.addingTimeInterval(0.5).timeIntervalSinceNow
            if rest > 0 {
                do {
                    try await Task.sleep(for: .seconds(rest))
                } catch {
                    return
                }
            }
            // Rounded as a turn is, so that C#'s value itself (0.1 + 0.2, say) doesn't count as a turn.
            knownValue = Self.rounded(sentValue)
            value = sentValue
        }
    }

    private static func rounded(_ value: Double) -> Double {
        (value * 1e9).rounded() / 1e9
    }

    private struct Sync: Equatable {
        let sentValue: Double
        let lastTurn: Date
    }

    private var from: Double {
        node.attribute("From").flatMap(Double.init) ?? 0
    }

    private var through: Double {
        max(node.attribute("Through").flatMap(Double.init) ?? from, from)
    }

    private var by: Double {
        node.attribute("By").flatMap(Double.init).flatMap { $0 > 0 ? $0 : nil } ?? 1
    }

    private var sentValue: Double {
        node.attribute("Value").flatMap(Double.init) ?? from
    }
}
