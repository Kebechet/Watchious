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

// A system icon, or an app image, which Watchious.Sdk copies into the app bundle under its file name.
struct ImageNode: View {
    let node: UiNode

    var body: some View {
        if let symbolName {
            SymbolImage(name: symbolName, node: node)
        // UIImage(named:) finds a PNG by its name alone; a JPEG needs its extension.
        } else if let source = node.attribute("Source"), let image = UIImage(named: source) ?? UIImage(named: source + ".jpg") {
            Image(uiImage: image)
                .resizable()
                .scaledToFit()
                .frame(width: dimension("Width"), height: dimension("Height"))
                .accessibilityLabel(node.attribute("Description") ?? "")
        }
    }

    // AppleSymbol names any SF Symbol and wins over Icon, the shared set.
    private var symbolName: String? {
        if let name = node.attribute("AppleSymbol"), !name.isEmpty {
            return name
        }
        guard let icon = node.attribute("Icon"), !icon.isEmpty else {
            return nil
        }
        guard let name = watchiousSymbols[icon] else {
            reportUnknownValue(icon, of: "Icon", on: node.type)
            return nil
        }
        return name
    }

    private func dimension(_ name: String) -> CGFloat? {
        node.attribute(name).flatMap(Double.init).map { CGFloat($0) }
    }
}

// An SF Symbol, sized to the text around it, or to Width and Height. A name watchOS doesn't have is logged, and shows
// as a warning in a debug build.
private struct SymbolImage: View {
    let name: String
    let node: UiNode

    var body: some View {
        if UIImage(systemName: name) != nil {
            symbol
                .accessibilityLabel(node.attribute("Description") ?? "")
        } else {
            missing
        }
    }

    @ViewBuilder
    private var symbol: some View {
        let width = dimension("Width")
        let height = dimension("Height")
        if width != nil || height != nil {
            Image(systemName: name)
                .resizable()
                .scaledToFit()
                .frame(width: width, height: height)
        } else {
            Image(systemName: name)
        }
    }

    @ViewBuilder
    private var missing: some View {
        let _ = NSLog("[Watchious] Unknown SF Symbol %@", name)
        #if DEBUG
        Text("⚠️ \(name)")
            .font(.caption2)
            .foregroundStyle(.red)
        #endif
    }

    private func dimension(_ name: String) -> CGFloat? {
        node.attribute(name).flatMap(Double.init).map { CGFloat($0) }
    }
}

// Watchious's shared icons (Watchious.Controls.Icon) as SF Symbols.
private let watchiousSymbols: [String: String] = [
    "Heart": "heart.fill",
    "Star": "star.fill",
    "Bell": "bell.fill",
    "Home": "house.fill",
    "Person": "person.fill",
    "Settings": "gearshape.fill",
    "Info": "info.circle.fill",
    "Warning": "exclamationmark.triangle.fill",
    "Search": "magnifyingglass",
    "Lock": "lock.fill",
    "Plus": "plus",
    "Minus": "minus",
    "Checkmark": "checkmark",
    "Close": "xmark",
    "Edit": "pencil",
    "Delete": "trash.fill",
    "Refresh": "arrow.clockwise",
    "Share": "square.and.arrow.up",
    "ChevronLeft": "chevron.left",
    "ChevronRight": "chevron.right",
    "ChevronUp": "chevron.up",
    "ChevronDown": "chevron.down",
    "Play": "play.fill",
    "Pause": "pause.fill",
    "Stop": "stop.fill",
    "Microphone": "mic.fill",
    "Speaker": "speaker.wave.2.fill",
    "Music": "music.note",
    "Phone": "phone.fill",
    "Message": "message.fill",
    "Location": "mappin.and.ellipse",
    "Calendar": "calendar",
    "Clock": "clock.fill",
    "Timer": "timer",
    "Alarm": "alarm.fill",
    "Run": "figure.run",
    "Walk": "figure.walk",
    "Bike": "figure.outdoor.cycle",
    "Swim": "figure.pool.swim",
    "Dumbbell": "dumbbell.fill",
    "Flame": "flame.fill",
    "Bolt": "bolt.fill",
    "Drop": "drop.fill",
    "Moon": "moon.fill",
    "Sun": "sun.max.fill",
    "Battery": "battery.100",
    "Wifi": "wifi",
    "List": "list.bullet",
]
