import SwiftUI

// What a control's HorizontalOptions and VerticalOptions (MAUI's LayoutOptions, see LayoutOptions.cs) place it in. A
// stack ignores its children's options along its own direction, as MAUI does.
enum LayoutParent {
    // A VStack or DigitalCrownRotation (StackLayout): places a child by its HorizontalOptions, within its own width.
    case stack
    // A ScrollView or List row, which spans the screen's width: places a child by its HorizontalOptions.
    case column
    // An HStack: places a child by its VerticalOptions, within the row's height.
    case row
    // A page without a List or ScrollView: places a child by both, each with VerticalOptions in an equal share of the
    // height the others leave.
    case page
}

enum LayoutOption {
    case start, center, end, fill
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

    // Blank text between the markup's tags and Wear OS's own controls draw nothing here, and must not take a place (or
    // a stack's spacing) either.
    var placedChildren: [UiNode] {
        children.filter { !$0.isBlankText && !$0.type.hasPrefix("Wear.") }
    }

    // Start, Center and End keep a stack, and a control that stretches by itself, at its own width.
    var keepsOwnWidth: Bool {
        ["Start", "Center", "End"].contains(attribute("HorizontalOptions"))
    }

    func layoutOption(_ attribute: String) -> LayoutOption? {
        switch self.attribute(attribute) {
        case nil:
            return nil
        case "Start":
            return .start
        case "Center":
            return .center
        case "End":
            return .end
        case "Fill":
            return .fill
        case let options?:
            reportUnknownValue(options, of: attribute, on: type)
            return nil
        }
    }
}

// The controls that stretch across the width by themselves, as Wear OS's do (a linear ProgressView stretches on both
// whatever its options).
private let stretchingTypes: Set<String> = ["Button", "Toggle", "TextField"]

private struct HorizontalOptionKey: LayoutValueKey {
    static let defaultValue: LayoutOption? = nil
}

private struct KeepsIdealWidthKey: LayoutValueKey {
    static let defaultValue = false
}

struct LayoutOptionsModifier: ViewModifier {
    let node: UiNode
    @Environment(\.watchiousLayoutParent) private var parent

    @ViewBuilder
    func body(content: Content) -> some View {
        let keepsIdealWidth = node.keepsOwnWidth && stretchingTypes.contains(node.type)
        switch parent {
        case .stack:
            content
                .layoutValue(key: HorizontalOptionKey.self, value: node.layoutOption("HorizontalOptions"))
                .layoutValue(key: KeepsIdealWidthKey.self, value: keepsIdealWidth)
        case .row:
            // Fill centers too: no control stretches vertically.
            let vertical = node.layoutOption("VerticalOptions")
            content
                .frame(maxHeight: vertical == nil ? nil : .infinity, alignment: Alignment(horizontal: .center, vertical: vertical.vertical))
        case .column, .page:
            let horizontal = node.layoutOption("HorizontalOptions")
            let vertical = parent == .page ? node.layoutOption("VerticalOptions") : nil
            IdealWidthLayout(isEnabled: keepsIdealWidth) {
                content
            }
            .frame(maxWidth: horizontal == nil ? nil : .infinity, alignment: Alignment(horizontal: horizontal.horizontal, vertical: .center))
            .frame(maxHeight: vertical == nil ? nil : .infinity, alignment: Alignment(horizontal: .center, vertical: vertical.vertical))
        }
    }
}

extension Optional where Wrapped == LayoutOption {
    var horizontal: HorizontalAlignment {
        switch self {
        case .start:
            return .leading
        case .end:
            return .trailing
        case .center, .fill, nil:
            return .center
        }
    }

    var vertical: VerticalAlignment {
        switch self {
        case .start:
            return .top
        case .end:
            return .bottom
        case .center, .fill, nil:
            return .center
        }
    }
}

// A control that stretches by itself, at its own width but never wider than the space: a fixed size would let a long
// text run off the screen instead of wrapping.
private func idealWidthProposal(_ subview: LayoutSubview, width: CGFloat?) -> ProposedViewSize {
    let ideal = subview.sizeThatFits(.unspecified).width
    return ProposedViewSize(width: min(ideal, width ?? ideal), height: nil)
}

struct IdealWidthLayout: Layout {
    let isEnabled: Bool

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        guard let subview = subviews.first else {
            return .zero
        }
        return subview.sizeThatFits(isEnabled ? idealWidthProposal(subview, width: proposal.width) : proposal)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        subviews.first?.place(at: bounds.origin, proposal: ProposedViewSize(bounds.size))
    }
}

// A VStack whose children each sit where their HorizontalOptions put them, within the stack's own width. SwiftUI's
// VStack aligns all its children alike, and a child's frame with an unlimited width would widen the stack (and push an
// HStack's other children aside) instead. The stack spans the width it is offered, as MAUI's default Fill does, unless
// it is in a row or its own options keep its width; then it is as wide as its widest child.
struct StackLayout: Layout {
    let spacing: CGFloat?
    let fillsWidth: Bool

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let sizes = subviews.map { size(of: $0, width: proposal.width) }
        let widest = sizes.map(\.width).max() ?? 0
        let width = fillsWidth ? (proposal.width ?? widest) : widest
        return CGSize(width: width, height: sizes.map(\.height).reduce(0, +) + gaps(subviews).reduce(0, +))
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let gaps = gaps(subviews)
        var y = bounds.minY
        for (index, subview) in subviews.enumerated() {
            let size = size(of: subview, width: bounds.width)
            let x: CGFloat
            switch subview[HorizontalOptionKey.self] {
            case .start:
                x = bounds.minX
            case .end:
                x = bounds.maxX - size.width
            case .center, .fill, nil:
                x = bounds.minX + (bounds.width - size.width) / 2
            }
            subview.place(at: CGPoint(x: x, y: y), proposal: ProposedViewSize(size))
            y += size.height + (index < gaps.count ? gaps[index] : 0)
        }
    }

    private func size(of subview: LayoutSubview, width: CGFloat?) -> CGSize {
        let proposal = subview[KeepsIdealWidthKey.self]
            ? idealWidthProposal(subview, width: width)
            : ProposedViewSize(width: width, height: nil)
        return subview.sizeThatFits(proposal)
    }

    // Without Spacing, SwiftUI's own spacing between each pair of controls, as a VStack has.
    private func gaps(_ subviews: Subviews) -> [CGFloat] {
        guard subviews.count > 1 else {
            return []
        }
        return (0..<subviews.count - 1).map { index in
            spacing ?? subviews[index].spacing.distance(to: subviews[index + 1].spacing, along: .vertical)
        }
    }
}
