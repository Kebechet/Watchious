import SwiftUI

struct NodeView: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        control
            .modifier(LayoutOptionsModifier(node: node))
    }

    @ViewBuilder
    private var control: some View {
        switch node.type {
        case "NavigationStack":
            NavigationStackView(node: node, store: store)
        case "ScrollView":
            ScrollView {
                children
                    .scenePadding(.horizontal)
            }
            .environment(\.watchiousLayoutParent, .column)
        case "VStack":
            VStack(spacing: spacing) {
                children
            }
            .environment(\.watchiousLayoutParent, .column)
        case "HStack":
            // A child's VerticalOptions places it within the row's height: its frame takes the height, and the row
            // keeps the height of its tallest child.
            HStack(spacing: spacing) {
                children
            }
            .fixedSize(horizontal: false, vertical: node.children.contains { $0.hasVerticalOptions })
            .environment(\.watchiousLayoutParent, .row)
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
        case "DigitalCrownRotation":
            DigitalCrownRotationNode(node: node, store: store)
        case "Apple.Gauge":
            GaugeNode(node: node)
        // Wear OS's own controls (the Wear namespace in C#) have no place on watchOS.
        case let type where type.hasPrefix("Wear."):
            EmptyView()
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
        .environment(\.watchiousLayoutParent, .column)
        .onAppear {
            NSLog("[Watchious] unknown control %@: this host has no view for it", node.type)
        }
    }
}
