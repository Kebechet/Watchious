import SwiftUI

struct NodeView: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        switch node.type {
        case "NavigationStack":
            NavigationStackView(node: node, store: store)
        case "ScrollView":
            ScrollView {
                children
                    .scenePadding(.horizontal)
            }
        case "VStack":
            VStack(alignment: horizontalAlignment, spacing: spacing) {
                children
            }
        case "HStack":
            HStack(alignment: verticalAlignment, spacing: spacing) {
                children
            }
        case "Label":
            Text(node.joinedText)
                .font(font(for: node.attribute("Style")))
        case "Button":
            Button(node.attribute("Text") ?? "") {
                dispatch("click")
            }
            .disabled(node.attribute("IsEnabled") == "false")
        case "List":
            ListNodeView(node: node, store: store)
        case "Image":
            ImageNode(node: node)
        case "ProgressView":
            ProgressViewNode(node: node)
        case "Picker":
            PickerNode(node: node, store: store)
        case "TextField":
            TextFieldNode(node: node, store: store)
        case "Toggle":
            if let text = node.attribute("Text"), !text.isEmpty {
                Toggle(text, isOn: isOnBinding)
            } else {
                Toggle(isOn: isOnBinding) {
                    EmptyView()
                }
                .labelsHidden()
            }
        case UiNode.textType:
            // Blank text is the whitespace between controls in the Razor markup.
            if !node.isBlankText {
                Text(node.text ?? "")
            }
        default:
            EmptyView()
        }
    }

    private var children: some View {
        // The node id keeps a view's identity (state, animations) when siblings are inserted, removed or moved.
        ForEach(node.children) { child in
            NodeView(node: child, store: store)
        }
    }

    // Nil leaves the spacing to SwiftUI, like a VStack without spacing.
    private var spacing: CGFloat? {
        node.attribute("Spacing").flatMap(Double.init).map { CGFloat($0) }
    }

    private var horizontalAlignment: HorizontalAlignment {
        switch node.attribute("Alignment") {
        case "Leading":
            return .leading
        case "Trailing":
            return .trailing
        default:
            return .center
        }
    }

    private var verticalAlignment: VerticalAlignment {
        switch node.attribute("Alignment") {
        case "Top":
            return .top
        case "Bottom":
            return .bottom
        default:
            return .center
        }
    }

    private var isOnBinding: Binding<Bool> {
        Binding(
            get: { node.attribute("IsOn") == "true" },
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
        case "Headline":
            return .headline
        case "Caption":
            return .caption
        default:
            return .body
        }
    }
}
