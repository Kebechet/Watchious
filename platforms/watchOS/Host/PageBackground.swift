import SwiftUI

// A page's background as C# resolved it (PageBackground.cs): a color as #RRGGBBAA, or "linear x1 y1 x2 y2" or
// "radial cx cy r" followed by each stop's color and offset. Points are relative to the page, and a radial radius is a
// fraction of the page's longer side, as in MAUI.
enum PageBackgroundStyle: Equatable {
    case color(Color)
    case linear(stops: [Gradient.Stop], start: UnitPoint, end: UnitPoint)
    case radial(stops: [Gradient.Stop], center: UnitPoint, radius: CGFloat)

    init?(_ value: String?) {
        guard let value, !value.isEmpty else {
            return nil
        }
        guard let style = Self.parse(value.split(separator: " ")) else {
            reportUnknownValue(value, of: "Background", on: "Page")
            return nil
        }
        self = style
    }

    private static func parse(_ parts: [Substring]) -> PageBackgroundStyle? {
        if parts.count == 1 {
            return color(parts[0]).map { .color($0) }
        }
        switch parts.first {
        case "linear":
            guard parts.count >= 5, let numbers = numbers(parts[1...4]), let stops = stops(parts.dropFirst(5)) else {
                return nil
            }
            return .linear(
                stops: stops,
                start: UnitPoint(x: numbers[0], y: numbers[1]),
                end: UnitPoint(x: numbers[2], y: numbers[3]))
        case "radial":
            guard parts.count >= 4, let numbers = numbers(parts[1...3]), let stops = stops(parts.dropFirst(4)) else {
                return nil
            }
            return .radial(stops: stops, center: UnitPoint(x: numbers[0], y: numbers[1]), radius: numbers[2])
        default:
            return nil
        }
    }

    private static func numbers(_ parts: ArraySlice<Substring>) -> [Double]? {
        let numbers = parts.compactMap { Double($0) }
        return numbers.count == parts.count ? numbers : nil
    }

    private static func stops(_ parts: ArraySlice<Substring>) -> [Gradient.Stop]? {
        // At least two stops: C# sends a gradient with one stop as its color.
        guard parts.count >= 4, parts.count % 2 == 0 else {
            return nil
        }
        var stops: [Gradient.Stop] = []
        var index = parts.startIndex
        while index < parts.endIndex {
            guard let color = color(parts[index]), let offset = Double(parts[index + 1]) else {
                return nil
            }
            stops.append(Gradient.Stop(color: color, location: offset))
            index += 2
        }
        return stops
    }

    private static func color(_ hex: Substring) -> Color? {
        guard hex.hasPrefix("#"), hex.count == 9, let value = UInt32(hex.dropFirst(), radix: 16) else {
            return nil
        }
        return Color(
            .sRGB,
            red: Double((value >> 24) & 0xFF) / 255,
            green: Double((value >> 16) & 0xFF) / 255,
            blue: Double((value >> 8) & 0xFF) / 255,
            opacity: Double(value & 0xFF) / 255)
    }
}

// The page's background behind its navigation container, and black while the watch shows the dimmed always-on screen
// (as Wear OS requires there, and so both watches behave alike).
struct PageBackgroundView: View {
    let style: PageBackgroundStyle?
    @Environment(\.isLuminanceReduced) private var isLuminanceReduced

    var body: some View {
        if isLuminanceReduced {
            Color.black
        } else {
            switch style {
            case .color(let color):
                color
            case .linear(let stops, let start, let end):
                LinearGradient(stops: stops, startPoint: start, endPoint: end)
            case .radial(let stops, let center, let radius):
                GeometryReader { proxy in
                    RadialGradient(
                        stops: stops,
                        center: center,
                        startRadius: 0,
                        endRadius: max(radius * max(proxy.size.width, proxy.size.height), 0.5))
                }
            case nil:
                Color.black
            }
        }
    }
}
