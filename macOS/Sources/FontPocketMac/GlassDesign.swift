import SwiftUI

// Real system Liquid Glass on macOS Tahoe. The compiler gate also permits
// building the native-material fallback with older Xcode / SDK installations.
private struct PocketPrimary: ViewModifier {
    @ViewBuilder func body(content: Content) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.buttonStyle(.glassProminent) }
        else { content.buttonStyle(.borderedProminent) }
        #else
        content.buttonStyle(.borderedProminent)
        #endif
    }
}
private struct PocketGlassButton: ViewModifier {
    @ViewBuilder func body(content: Content) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.buttonStyle(.glass) }
        else { content.buttonStyle(.bordered) }
        #else
        content.buttonStyle(.bordered)
        #endif
    }
}
private struct PocketFloatingSurface: ViewModifier {
    @ViewBuilder func body(content: Content) -> some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.glassEffect(.regular, in: Capsule()) }
        else { content.background(.regularMaterial, in: Capsule()) }
        #else
        content.background(.regularMaterial, in: Capsule())
        #endif
    }
}
extension View {
    func pocketPrimary() -> some View { modifier(PocketPrimary()) }
    func pocketGlassButton() -> some View { modifier(PocketGlassButton()) }
    func pocketFloatingSurface() -> some View { modifier(PocketFloatingSurface()) }
}

struct GlassActions<Content: View>: View {
    @ViewBuilder let content: () -> Content
    @ViewBuilder var body: some View {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { GlassEffectContainer(spacing: 12) { content() } }
        else { content() }
        #else
        content()
        #endif
    }
}
