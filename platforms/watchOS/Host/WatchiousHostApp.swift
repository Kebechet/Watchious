import SwiftUI

@main
struct WatchiousHostApp: App {
    private let store = WatchiousStore.shared

    init() {
        watchious_start_runtime()
        let result = watchious_start(watchiousPatchesCallback, watchiousErrorCallback)
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
    let store: WatchiousStore

    var body: some View {
        Group {
            if let lastError = store.lastError {
                ScrollView {
                    Text(lastError)
                        .font(.caption2)
                        .foregroundStyle(.red)
                }
            } else {
                ForEach(store.tree.root.children) { node in
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
// --watchious-autotap clicks the first Button, --watchious-autotoggle turns on the first Toggle, and each
// --watchious-click=<text> clicks the Button with that text, one second apart (--watchious-click=<back> pops the
// navigation stack the way the back button does).
@MainActor
enum DebugAutomation {
    static func runIfRequested(store: WatchiousStore) async {
        let arguments = ProcessInfo.processInfo.arguments
        let tap = arguments.contains("--watchious-autotap")
        let toggle = arguments.contains("--watchious-autotoggle")
        let clickPrefix = "--watchious-click="
        // Percent-encoded, because launch arguments are split at spaces: --watchious-click=Send%20hello.
        let clicks = arguments.filter { $0.hasPrefix(clickPrefix) }.map { argument in
            let text = String(argument.dropFirst(clickPrefix.count))
            return text.removingPercentEncoding ?? text
        }
        guard tap || toggle || !clicks.isEmpty else {
            return
        }

        try? await Task.sleep(for: .seconds(1.5))
        if tap, let button = store.firstNode(ofType: "Button"), let id = button.eventHandlerId("click") {
            NSLog("[Watchious] autotap click %llu", id)
            store.dispatch(id, "click")
        }

        try? await Task.sleep(for: .seconds(0.5))
        if toggle, let toggleNode = store.firstNode(ofType: "Toggle"), let id = toggleNode.eventHandlerId("toggled") {
            NSLog("[Watchious] autotoggle %llu", id)
            store.dispatch(id, "toggled", value: "true")
        }

        for text in clicks {
            try? await Task.sleep(for: .seconds(1))
            if text == "<back>" {
                NSLog("[Watchious] back")
                store.goBackLikeTheBackButton()
                continue
            }
            let button = store.tree.root.first { $0.type == "Button" && $0.attribute("Text") == text }
            if let button, let id = button.eventHandlerId("click") {
                NSLog("[Watchious] click %@ %llu", text, id)
                store.dispatch(id, "click")
            } else {
                NSLog("[Watchious] click %@: no such button", text)
            }
        }
    }
}
