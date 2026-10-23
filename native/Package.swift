// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "ControlStudioStorage",
    platforms: [.macOS(.v13)],
    products: [.library(name: "ControlStudioStorage", targets: ["ControlStudioStorage"])],
    targets: [
        .target(name: "ControlStudioStorage", path: "Shared", linkerSettings: [.linkedLibrary("sqlite3")]),
        .target(name: "ControlStudioWidgetRuntime", path: "WidgetRuntime", publicHeadersPath: "include",
            cSettings: [.unsafeFlags(["-fobjc-arc"])], linkerSettings: [.linkedFramework("Foundation")]),
        .testTarget(name: "ControlStudioStorageTests", dependencies: ["ControlStudioStorage"], path: "Tests"),
        .testTarget(name: "ControlStudioWidgetRuntimeTests", dependencies: ["ControlStudioWidgetRuntime"], path: "WidgetRuntimeTests")
    ]
)
