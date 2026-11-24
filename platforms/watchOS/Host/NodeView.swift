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
            UnknownNodeView(node: node, store: store)
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
        case "Center", nil:
            return .center
        case let alignment?:
            reportUnknownValue(alignment, of: "Alignment", on: node.type)
            return .center
        }
    }

    private var verticalAlignment: VerticalAlignment {
        switch node.attribute("Alignment") {
        case "Top":
            return .top
        case "Bottom":
            return .bottom
        case "Center", nil:
            return .center
        case let alignment?:
            reportUnknownValue(alignment, of: "Alignment", on: node.type)
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
        case "Body", nil:
            return .body
        case let style?:
            reportUnknownValue(style, of: "Style", on: node.type)
            return .body
        }
    }
}

// A name or value this host doesn't know means it and the C# controls don't match
// (https://github.com/Kebechet/Watchious/issues/3). An unknown value is only logged, and the control uses its default;
// an unknown control is shown as a warning in a debug build (UnknownNodeView).
func reportUnknownValue(_ value: String, of attribute: String, on type: String) {
    NSLog("[Watchious] unknown %@ value %@ on %@", attribute, value, type)
}

// A control this host has no view for: a misspelled element in the Razor markup, or a control that C# has and this host
// doesn't yet. Its children are still shown; a debug build also shows a warning in its place.
struct UnknownNodeView: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        VStack {
            #if DEBUG
            Text("⚠️ \(node.type)")
                .font(.caption)
                .foregroundStyle(.orange)
            #endif
            ForEach(node.children) { child in
                NodeView(node: child, store: store)
            }
        }
        .onAppear {
            NSLog("[Watchious] unknown control %@: this host has no view for it", node.type)
        }
    }
}
