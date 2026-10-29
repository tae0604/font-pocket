import SwiftUI

private struct PocketTransparencyOverride: EnvironmentKey { static let defaultValue: Bool? = nil }
extension EnvironmentValues {
    var pocketTransparencyOverride: Bool? {
        get { self[PocketTransparencyOverride.self] }
        set { self[PocketTransparencyOverride.self] = newValue }
    }
}

// Real system Liquid Glass on macOS Tahoe. The compiler gate also permits
// building the native-material fallback with older Xcode / SDK installations.
private struct PocketPrimary: ViewModifier {
    @Environment(\.accessibilityReduceTransparency) private var systemReduced
    @Environment(\.pocketTransparencyOverride) private var testReduced
    @ViewBuilder func body(content: Content) -> some View {
        if testReduced ?? systemReduced { content.buttonStyle(.borderedProminent) }
        else {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.buttonStyle(.glassProminent) }
        else { content.buttonStyle(.borderedProminent) }
        #else
        content.buttonStyle(.borderedProminent)
        #endif
        }
    }
}
private struct PocketGlassButton: ViewModifier {
    @Environment(\.accessibilityReduceTransparency) private var systemReduced
    @Environment(\.pocketTransparencyOverride) private var testReduced
    @ViewBuilder func body(content: Content) -> some View {
        if testReduced ?? systemReduced { content.buttonStyle(.bordered) }
        else {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.buttonStyle(.glass) }
        else { content.buttonStyle(.bordered) }
        #else
        content.buttonStyle(.bordered)
        #endif
        }
    }
}
private struct PocketFloatingSurface: ViewModifier {
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency
    @Environment(\.pocketTransparencyOverride) private var testReduced
    @ViewBuilder func body(content: Content) -> some View {
        if testReduced ?? reduceTransparency {
            content.background(Color(nsColor: .controlBackgroundColor), in: Capsule())
        } else {
        #if compiler(>=6.2)
        if #available(macOS 26.0, *) { content.glassEffect(.regular, in: Capsule()) }
        else { content.background(.regularMaterial, in: Capsule()) }
        #else
        content.background(.regularMaterial, in: Capsule())
        #endif
        }
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
