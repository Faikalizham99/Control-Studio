// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "ControlStudioStorage",
    platforms: [.macOS(.v13)],
    products: [.library(name: "ControlStudioStorage", targets: ["ControlStudioStorage"])],
    targets: [
        .systemLibrary(name: "SQLite3", path: "SQLite3"),
        .target(name: "ControlStudioStorage", dependencies: ["SQLite3"], path: "Shared"),
        .testTarget(name: "ControlStudioStorageTests", dependencies: ["ControlStudioStorage", "SQLite3"], path: "Tests")
    ]
)
