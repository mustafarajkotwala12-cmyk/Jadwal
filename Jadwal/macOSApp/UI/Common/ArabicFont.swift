import SwiftUI
import CoreText
import AppKit

@MainActor
public enum FontManager {
    private static var hasRegistered = false

    public static func registerFonts() {
        guard !hasRegistered else { return }
        hasRegistered = true

        let candidates: [URL?] = [
            Bundle.module.url(forResource: "KanzalLulu-Regular", withExtension: "ttf"),
            Bundle.main.url(forResource: "KanzalLulu-Regular", withExtension: "ttf"),
            Bundle.main.resourceURL?.appendingPathComponent("KanzalLulu-Regular.ttf"),
            URL(fileURLWithPath: "assets/KanzalLulu-Regular.ttf"),
            URL(fileURLWithPath: "../assets/KanzalLulu-Regular.ttf")
        ]

        for case let url? in candidates {
            if FileManager.default.fileExists(atPath: url.path) {
                var error: Unmanaged<CFError>?
                if CTFontManagerRegisterFontsForURL(url as CFURL, .process, &error) {
                    return
                }
            }
        }
    }
}

public extension String {
    var containsArabic: Bool {
        unicodeScalars.contains { scalar in
            let v = scalar.value
            return (0x0600...0x06FF).contains(v) ||
                   (0x0750...0x077F).contains(v) ||
                   (0x08A0...0x08FF).contains(v) ||
                   (0xFB50...0xFDFF).contains(v) ||
                   (0xFE70...0xFEFF).contains(v)
        }
    }
}

public extension Font {
    static func kanzalLulu(size: CGFloat) -> Font {
        Font.custom("Kanz-al-Lulu", size: size)
    }

    static func arabicAdaptive(
        for text: String,
        size: CGFloat,
        latinWeight: Font.Weight = .regular,
        latinDesign: Font.Design = .default
    ) -> Font {
        if text.containsArabic {
            return Font.custom("Kanz-al-Lulu", size: size + 2)
        } else {
            return .system(size: size, weight: latinWeight, design: latinDesign)
        }
    }
}

public struct ArabicAwareFontModifier: ViewModifier {
    public let text: String
    public let size: CGFloat
    public let latinWeight: Font.Weight
    public let latinDesign: Font.Design

    public func body(content: Content) -> some View {
        content.font(
            .arabicAdaptive(
                for: text,
                size: size,
                latinWeight: latinWeight,
                latinDesign: latinDesign
            )
        )
    }
}

public extension View {
    func arabicAwareFont(
        for text: String,
        size: CGFloat,
        weight: Font.Weight = .regular,
        design: Font.Design = .default
    ) -> some View {
        modifier(ArabicAwareFontModifier(text: text, size: size, latinWeight: weight, latinDesign: design))
    }
}
