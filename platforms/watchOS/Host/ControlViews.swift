import SwiftUI

struct ListNodeView: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        // Blank text between the rows' markup would otherwise become empty rows.
        List(node.children.filter { !$0.isBlankText }) { row in
            NodeView(node: row, store: store)
        }
    }
}

struct ProgressViewNode: View {
    let node: UiNode

    var body: some View {
        switch node.attribute("Style") {
        case "Linear":
            progress.progressViewStyle(.linear)
        case "Circular":
            progress.progressViewStyle(.circular)
        case "Automatic", nil:
            progress
        case let style?:
            progress
                .onAppear {
                    reportUnknownValue(style, of: "Style", on: node.type)
                }
        }
    }

    @ViewBuilder
    private var progress: some View {
        let text = node.attribute("Text") ?? ""
        if let value = node.attribute("Value").flatMap(Double.init) {
            if text.isEmpty {
                ProgressView(value: value)
            } else {
                ProgressView(text, value: value)
            }
        } else if text.isEmpty {
            ProgressView()
        } else {
            ProgressView(text)
        }
    }
}

struct PickerNode: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        let items = self.items
        Picker(node.attribute("Text") ?? "", selection: selection(count: items.count)) {
            ForEach(items.indices, id: \.self) { index in
                Text(items[index]).tag(index)
            }
        }
    }

    private var items: [String] {
        guard let items = node.attribute("Items"), !items.isEmpty else {
            return []
        }
        return items.components(separatedBy: "\n")
    }

    private func selection(count: Int) -> Binding<Int> {
        Binding(
            get: { min(max(Int(node.attribute("SelectedIndex") ?? "") ?? 0, 0), max(count - 1, 0)) },
            set: { index in
                guard let eventHandlerId = node.eventHandlerId("selected") else {
                    return
                }
                store.dispatch(eventHandlerId, "selected", value: String(index))
            }
        )
    }
}

struct TextFieldNode: View {
    let node: UiNode
    let store: WatchiousStore

    var body: some View {
        TextField(node.attribute("Placeholder") ?? "", text: text)
    }

    private var text: Binding<String> {
        Binding(
            get: { node.attribute("Text") ?? "" },
            set: { text in
                guard let eventHandlerId = node.eventHandlerId("changed") else {
                    return
                }
                store.dispatch(eventHandlerId, "changed", value: text)
            }
        )
    }
}

// An app image, which Watchious.Sdk copies into the app bundle under its file name.
struct ImageNode: View {
    let node: UiNode

    var body: some View {
        // UIImage(named:) finds a PNG by its name alone; a JPEG needs its extension.
        if let source = node.attribute("Source"), let image = UIImage(named: source) ?? UIImage(named: source + ".jpg") {
            Image(uiImage: image)
                .resizable()
                .scaledToFit()
                .frame(width: dimension("Width"), height: dimension("Height"))
                .accessibilityLabel(node.attribute("Description") ?? "")
        }
    }

    private func dimension(_ name: String) -> CGFloat? {
        node.attribute(name).flatMap(Double.init).map { CGFloat($0) }
    }
}
