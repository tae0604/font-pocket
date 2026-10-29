// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "FontPocketMac",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "FontPocket", targets: ["FontPocketMac"])],
    targets: [
        .target(name: "FontPocketCore"),
        .executableTarget(name: "FontPocketMac", dependencies: ["FontPocketCore"], resources: [.process("Resources")]),
        .testTarget(name: "FontPocketCoreTests", dependencies: ["FontPocketCore"])
    ]
)
