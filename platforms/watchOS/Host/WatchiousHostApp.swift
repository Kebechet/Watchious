import SwiftUI

@main
struct WatchiousHostApp: App {
    private let store = WatchiousStore.shared

    init() {
        DebugAutomation.clearResultsIfRequested()
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
// navigation stack the way the back button does). Then each --watchious-expect=<text> logs whether a Label shows that
// text, which is how the canary CI job sees that a click went through C# and back. Each step is also written to
// tmp/watchious-automation.log in the app's container: on a fresh CI simulator, `log show` prints the lines as
// <compose failure>.
@MainActor
enum DebugAutomation {
    private static let resultsFile = URL(fileURLWithPath: NSTemporaryDirectory())
        .appendingPathComponent("watchious-automation.log")

    // Before .NET starts, so that a run that hangs at start-up can't leave an earlier run's results behind.
    static func clearResultsIfRequested() {
        let prefixes = ["--watchious-autotap", "--watchious-autotoggle", "--watchious-click=", "--watchious-expect="]
        let isRequested = ProcessInfo.processInfo.arguments.contains { argument in
            prefixes.contains { argument.hasPrefix($0) }
        }
        if isRequested {
            try? Data().write(to: resultsFile)
        }
    }

    private static func report(_ message: String) {
        NSLog("[Watchious] %@", message)
        guard let line = "[Watchious] \(message)\n".data(using: .utf8),
              let handle = try? FileHandle(forWritingTo: resultsFile) else {
            return
        }
        handle.seekToEndOfFile()
        handle.write(line)
        try? handle.close()
    }

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
        let expectPrefix = "--watchious-expect="
        let expectations = arguments.filter { $0.hasPrefix(expectPrefix) }.map { argument in
            let text = String(argument.dropFirst(expectPrefix.count))
            return text.removingPercentEncoding ?? text
        }
        guard tap || toggle || !clicks.isEmpty || !expectations.isEmpty else {
            return
        }

        try? await Task.sleep(for: .seconds(1.5))
        if tap, let button = store.firstNode(ofType: "Button"), let id = button.eventHandlerId("click") {
            report("autotap click \(id)")
            store.dispatch(id, "click")
        }

        try? await Task.sleep(for: .seconds(0.5))
        if toggle, let toggleNode = store.firstNode(ofType: "Toggle"), let id = toggleNode.eventHandlerId("toggled") {
            report("autotoggle \(id)")
            store.dispatch(id, "toggled", value: "true")
        }

        for text in clicks {
            try? await Task.sleep(for: .seconds(1))
            if text == "<back>" {
                report("back")
                store.goBackLikeTheBackButton()
                continue
            }
            let button = store.tree.root.first { $0.type == "Button" && $0.attribute("Text") == text }
            if let button, let id = button.eventHandlerId("click") {
                report("click \(text) \(id)")
                store.dispatch(id, "click")
            } else {
                report("click \(text): no such button")
            }
        }

        if !expectations.isEmpty {
            try? await Task.sleep(for: .seconds(1))
        }
        for text in expectations {
            let label = store.tree.root.first { $0.type == "Label" && $0.joinedText == text }
            report("expect \(text): \(label == nil ? "missing" : "found")")
        }
    }
}
