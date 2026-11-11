import AppKit
import SwiftUI

/// Keep native segment labels at their measured widths. NSTabView on macOS 26
/// can compress its automatic tab strip even when the surrounding sheet is wide.
struct SectionOption: Identifiable {
    let id: String
    let label: String
}

struct SectionSwitcher: View {
    let options: [SectionOption]
    @Binding var selection: String

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            NativeSectionControl(options: options, selection: $selection)
                .frame(width: NativeSectionControl.width(for: options), height: 30)
                .padding(.horizontal, 3)
        }
        .frame(height: 36)
        .accessibilityLabel("Sections")
    }
}

private struct NativeSectionControl: NSViewRepresentable {
    let options: [SectionOption]
    @Binding var selection: String

    static func segmentWidth(_ label: String) -> CGFloat {
        ceil((label as NSString).size(withAttributes: [.font: NSFont.systemFont(ofSize: NSFont.systemFontSize)]).width) + 28
    }

    static func width(for options: [SectionOption]) -> CGFloat {
        options.reduce(CGFloat(8)) { $0 + segmentWidth($1.label) }
    }

    func makeCoordinator() -> Coordinator { Coordinator(parent: self) }

    func makeNSView(context: Context) -> NSSegmentedControl {
        let control = NSSegmentedControl()
        control.trackingMode = .selectOne
        control.segmentDistribution = .fit
        control.font = .systemFont(ofSize: NSFont.systemFontSize)
        control.target = context.coordinator
        control.action = #selector(Coordinator.changed(_:))
        control.setAccessibilityIdentifier("font-pocket-sections")
        control.identifier = NSUserInterfaceItemIdentifier("font-pocket-sections")
        updateNSView(control, context: context)
        return control
    }

    func updateNSView(_ control: NSSegmentedControl, context: Context) {
        context.coordinator.parent = self
        control.segmentCount = options.count
        for (index, option) in options.enumerated() {
            control.setLabel(option.label, forSegment: index)
            control.setWidth(Self.segmentWidth(option.label), forSegment: index)
        }
        control.selectedSegment = options.firstIndex { $0.id == selection } ?? 0
    }

    final class Coordinator: NSObject {
        var parent: NativeSectionControl
        init(parent: NativeSectionControl) { self.parent = parent }
        @objc func changed(_ sender: NSSegmentedControl) {
            guard parent.options.indices.contains(sender.selectedSegment) else { return }
            parent.selection = parent.options[sender.selectedSegment].id
        }
    }
}
