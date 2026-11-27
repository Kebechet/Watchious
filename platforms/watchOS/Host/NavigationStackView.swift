import SwiftUI

// The pages of a Watchious NavigationStack element in SwiftUI's NavigationStack: the root page first, the others
// pushed. The back button and edge swipe pop natively; the host then tells C# how many pages are left, and C#
// removes the rest.
struct NavigationStackView: View {
    let node: UiNode
    let store: WatchiousStore
    @State private var path: [UInt32] = []

    var body: some View {
        NavigationStack(path: $path) {
            if let root = pages.first {
                PageView(page: root, store: store)
                    .navigationDestination(for: UInt32.self) { id in
                        if let page = pages.first(where: { $0.id == id }) {
                            PageView(page: page, store: store)
                        }
                    }
            }
        }
        .onAppear {
            path = pushedPageIds
        }
        .onChange(of: pushedPageIds) { _, ids in
            path = ids
        }
        .onChange(of: store.automatedBackCount) {
            if !path.isEmpty {
                path.removeLast()
            }
        }
        .onChange(of: path) { _, newPath in
            let pushed = pushedPageIds
            guard newPath.count < pushed.count, newPath == Array(pushed.prefix(newPath.count)),
                  let eventHandlerId = node.eventHandlerId("back") else {
                return
            }
            store.dispatch(eventHandlerId, "back", value: String(newPath.count + 1))
        }
    }

    private var pages: [UiNode] {
        node.children.filter { $0.type == "Page" }
    }

    private var pushedPageIds: [UInt32] {
        pages.dropFirst().map(\.id)
    }
}

struct PageView: View {
    let page: UiNode
    let store: WatchiousStore

    // The page's content in the middle of the screen, unless its controls' options put it elsewhere. A List or ScrollView
    // takes the whole page and keeps its content off the screen's sides itself; other content gets the same margin.
    var body: some View {
        VStack(spacing: 0) {
            ForEach(page.children) { child in
                NodeView(node: child, store: store)
            }
        }
        .environment(\.watchiousLayoutParent, .page)
        .scenePadding(page.scrolls ? [] : .horizontal)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
