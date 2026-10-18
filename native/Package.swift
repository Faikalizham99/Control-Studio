// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "ControlStudioStorage",
    platforms: [.macOS(.v13)],
    products: [.library(name: "ControlStudioStorage", targets: ["ControlStudioStorage"])],
    targets: [
        .target(name: "ControlStudioStorage", path: "Shared", linkerSettings: [.linkedLibrary("sqlite3")]),
        .testTarget(name: "ControlStudioStorageTests", dependencies: ["ControlStudioStorage"], path: "Tests")
    ]
)
