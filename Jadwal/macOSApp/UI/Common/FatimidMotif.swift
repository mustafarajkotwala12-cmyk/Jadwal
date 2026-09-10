import SwiftUI

/// Authentic 8-pointed star (Khatam / Star of Al-Quds / Fatimid motif)
/// Formed by two overlapping squares or an 8-pointed polygon.
public struct KhatamEightPointStar: Shape {
    public init() {}

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let center = CGPoint(x: rect.midX, y: rect.midY)
        let outerRadius = min(rect.width, rect.height) / 2.0
        let innerRadius = outerRadius * 0.7071 // cos(45 deg) for sharp geometric 8-point star

        let points = 16
        for i in 0..<points {
            let angle = (Double(i) * Double.pi / 8.0) - (Double.pi / 2.0)
            let radius = (i % 2 == 0) ? outerRadius : innerRadius
            let x = center.x + CGFloat(cos(angle)) * radius
            let y = center.y + CGFloat(sin(angle)) * radius

            if i == 0 {
                path.move(to: CGPoint(x: x, y: y))
            } else {
                path.addLine(to: CGPoint(x: x, y: y))
            }
        }
        path.closeSubpath()
        return path
    }
}

/// Subtle Fatimid pointed keel arch silhouette divider
public struct FatimidArchDivider: Shape {
    public init() {}

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let midX = rect.midX
        let topY = rect.minY
        let bottomY = rect.maxY

        // Base line left
        path.move(to: CGPoint(x: rect.minX, y: bottomY))
        path.addLine(to: CGPoint(x: midX - 30, y: bottomY))

        // Left arch curve up to apex
        path.addQuadCurve(
            to: CGPoint(x: midX, y: topY),
            control: CGPoint(x: midX - 8, y: topY + (bottomY - topY) * 0.4)
        )

        // Right arch curve down from apex
        path.addQuadCurve(
            to: CGPoint(x: midX + 30, y: bottomY),
            control: CGPoint(x: midX + 8, y: topY + (bottomY - topY) * 0.4)
        )

        // Base line right
        path.addLine(to: CGPoint(x: rect.maxX, y: bottomY))

        return path
    }
}

public enum FatimidPalette {
    /// Deep Fatimid emerald green accent
    public static let emerald = Color(red: 0.08, green: 0.48, blue: 0.36)
    public static let emeraldSoft = Color(red: 0.08, green: 0.48, blue: 0.36).opacity(0.15)
    
    /// Imperial gold / warm bronze
    public static let bronze = Color(red: 0.76, green: 0.58, blue: 0.28)
    public static let bronzeSoft = Color(red: 0.76, green: 0.58, blue: 0.28).opacity(0.15)
    
    /// Brilliant gold
    public static let gold = Color(red: 0.88, green: 0.71, blue: 0.25)
    public static let goldSoft = Color(red: 0.88, green: 0.71, blue: 0.25).opacity(0.15)
    
    /// Watermark pattern fill
    public static let watermark = Color.primary.opacity(0.04)
}
