import SwiftUI

@main
struct WatchiousHostApp: App {
    @StateObject private var store = WatchiousStore.shared

    init() {
        let result = watchious_start(watchiousRenderCallback, watchiousErrorCallback)
        if result != 0 {
            NSLog("[Watchious] watchious_start returned %d", result)
        }
    }

    var body: some Scene {
        WindowGroup {
            RootView(store: store)
        }
    }
}

struct RootView: View {
    @ObservedObject var store: WatchiousStore

    var body: some View {
        Group {
            if let lastError = store.lastError {
                ScrollView {
                    Text(lastError)
                        .font(.caption2)
                        .foregroundStyle(.red)
                }
            } else {
                ForEach(Array(store.nodes.enumerated()), id: \.offset) { _, node in
                    NodeView(node: node, store: store)
                }
            }
        }
        .task {
            await DebugAutomation.runIfRequested(store: store)
        }
    }
}

// The simulator CLI cannot tap, so these launch arguments drive the UI for automated checks:
// --watchious-autotap clicks the first Button, --watchious-autotoggle turns on the first Switch.
@MainActor
enum DebugAutomation {
    static func runIfRequested(store: WatchiousStore) async {
        let arguments = ProcessInfo.processInfo.arguments
        let tap = arguments.contains("--watchious-autotap")
        let toggle = arguments.contains("--watchious-autotoggle")
        guard tap || toggle else {
            return
        }

        try? await Task.sleep(for: .seconds(1.5))
        if tap, let button = store.firstNode(ofType: "Button"), let id = button.eventHandlerId("click") {
            NSLog("[Watchious] autotap click %llu", id)
            store.dispatch(id, "click")
        }

        try? await Task.sleep(for: .seconds(0.5))
        if toggle, let toggleSwitch = store.firstNode(ofType: "Switch"), let id = toggleSwitch.eventHandlerId("toggled") {
            NSLog("[Watchious] autotoggle %llu", id)
            store.dispatch(id, "toggled", value: "true")
        }
    }
}
