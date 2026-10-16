// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LookPauseMediaProbe",
    platforms: [.macOS(.v15)],
    products: [.executable(name: "LookPauseMediaProbe", targets: ["LookPauseMediaProbe"])],
    targets: [.executableTarget(name: "LookPauseMediaProbe")]
)
