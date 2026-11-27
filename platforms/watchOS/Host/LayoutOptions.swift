import SwiftUI

// What a control's HorizontalOptions and VerticalOptions (MAUI's LayoutOptions, see LayoutOptions.cs) place it in. A
// stack ignores its children's options along its own direction, as MAUI does.
enum LayoutParent {
    // A VStack, ScrollView, List row or DigitalCrownRotation: places a child by its HorizontalOptions.
    case column
    // An HStack: places a child by its VerticalOptions, within the row's height.
    case row
    // A page without a List or ScrollView: places a child by both, each with VerticalOptions in an equal share of the
    // height the others leave.
    case page
}

private struct LayoutParentKey: EnvironmentKey {
    static let defaultValue = LayoutParent.column
}

extension EnvironmentValues {
    var watchiousLayoutParent: LayoutParent {
        get { self[LayoutParentKey.self] }
        set { self[LayoutParentKey.self] = newValue }
    }
}

extension UiNode {
    var hasVerticalOptions: Bool {
        attribute("VerticalOptions") != nil
    }

    var scrolls: Bool {
        type == "List" || type == "ScrollView" || children.contains { $0.scrolls }
    }
}

// Start, Center and End keep the control's own size and put it in the space it is given; Fill takes the whole space,
// with the control centered in it unless it stretches by itself (as a watchOS button does). Without options the frames
// take the control's own size, so it stays where its parent puts it.
struct LayoutOptionsModifier: ViewModifier {
    let node: UiNode
    @Environment(\.watchiousLayoutParent) private var parent

    // The controls that stretch across the width by themselves; Start, Center and End size them to their content, as
    // the Wear OS host does.
    private static let stretchingTypes: Set<String> = ["Button", "Toggle", "TextField", "ProgressView"]

    func body(content: Content) -> some View {
        let horizontal = horizontalOptions
        let vertical = verticalOptions
        let keepsOwnWidth = horizontal != nil && node.attribute("HorizontalOptions") != "Fill"
            && Self.stretchingTypes.contains(node.type)
        content
            .fixedSize(horizontal: keepsOwnWidth, vertical: false)
            .frame(maxWidth: horizontal == nil ? nil : .infinity, alignment: Alignment(horizontal: horizontal ?? .center, vertical: .center))
            .frame(maxHeight: vertical == nil ? nil : .infinity, alignment: Alignment(horizontal: .center, vertical: vertical ?? .center))
    }

    private var horizontalOptions: HorizontalAlignment? {
        guard parent != .row else {
            return nil
        }
        switch node.attribute("HorizontalOptions") {
        case nil:
            return nil
        case "Start":
            return .leading
        case "End":
            return .trailing
        case "Center", "Fill":
            return .center
        case let options?:
            reportUnknownValue(options, of: "HorizontalOptions", on: node.type)
            return nil
        }
    }

    private var verticalOptions: VerticalAlignment? {
        guard parent != .column else {
            return nil
        }
        switch node.attribute("VerticalOptions") {
        case nil:
            return nil
        case "Start":
            return .top
        case "End":
            return .bottom
        case "Center", "Fill":
            return .center
        case let options?:
            reportUnknownValue(options, of: "VerticalOptions", on: node.type)
            return nil
        }
    }
}
