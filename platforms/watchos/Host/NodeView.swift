import SwiftUI

struct NodeView: View {
    let node: UiNode
    @ObservedObject var store: WatchiousStore

    var body: some View {
        switch node.type {
        case "ScrollView":
            ScrollView {
                children
            }
        case "VerticalStackLayout":
            VStack(alignment: .leading, spacing: spacing) {
                children
            }
        case "HorizontalStackLayout":
            HStack(spacing: spacing) {
                children
            }
        case "Label":
            Text(node.attribute("Text") ?? "")
                .font(font(for: node.attribute("Style")))
        case "Button":
            Button(node.attribute("Text") ?? "") {
                dispatch("click")
            }
            .disabled(node.attribute("IsEnabled") == "false")
        case "Switch":
            Toggle(isOn: toggledBinding) {
                EmptyView()
            }
            .labelsHidden()
        case "#text":
            Text(node.text ?? "")
        default:
            EmptyView()
        }
    }

    private var children: some View {
        ForEach(Array(node.childNodes.enumerated()), id: \.offset) { _, child in
            NodeView(node: child, store: store)
        }
    }

    private var spacing: CGFloat {
        CGFloat(Double(node.attribute("Spacing") ?? "") ?? 0)
    }

    private var toggledBinding: Binding<Bool> {
        Binding(
            get: { node.attribute("IsToggled") == "true" },
            set: { isOn in dispatch("toggled", value: isOn ? "true" : "false") }
        )
    }

    private func dispatch(_ eventName: String, value: String? = nil) {
        guard let eventHandlerId = node.eventHandlerId(eventName) else {
            return
        }
        store.dispatch(eventHandlerId, eventName, value: value)
    }

    private func font(for style: String?) -> Font {
        switch style {
        case "Title":
            return .title3
        case "Caption":
            return .caption
        default:
            return .body
        }
    }
}
