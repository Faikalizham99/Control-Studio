"""Generate a minimal native project using only Python's standard library.

No XcodeGen/CocoaPods/SPM dependencies. Stable IDs make the generated project reviewable.
"""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]


def generate():
    objects = {}

    def add(key, isa, **values):
        identifier = hashlib.sha256(key.encode()).hexdigest()[:24].upper()
        objects[identifier] = dict(isa=isa, **values)
        return identifier

    source = add("shared-source", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                 path="Shared/CounterStore.swift", sourceTree="<group>")
    group_source = add("app-group-source", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                       path="Shared/AppGroupConfiguration.swift", sourceTree="<group>")
    counter_appearance = add("counter-appearance-source", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                             path="Shared/CounterAppearance.swift", sourceTree="<group>")
    widget_store = add("widget-design-store", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                       path="Shared/WidgetDesignStore.swift", sourceTree="<group>")
    privacy = add("privacy-manifest", "PBXFileReference", lastKnownFileType="text.xml",
                  path="PrivacyInfo.xcprivacy", sourceTree="<group>")
    targets, products, children = [], [], [source, group_source, counter_appearance, widget_store, privacy]
    for name, suffix, kind in [
        ("ControlStudioWidgets", "widgets", "app-extension"),
        ("ControlStudioIntents", "intents", "extensionkit-extension"),
    ]:
        file = add(name + "-source", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                   path=f"{name}/{name}.swift", sourceTree="<group>")
        children.append(file)
        product = add(name + "-product", "PBXFileReference", explicitFileType=f"wrapper.{kind}",
                      path=name + ".appex", sourceTree="BUILT_PRODUCTS_DIR", includeInIndex=0)
        products.append(product)
        build_files = [add(name + "-" + ref, "PBXBuildFile", fileRef=ref) for ref in [source, group_source, widget_store, file]]
        if name == "ControlStudioWidgets":
            build_files.append(add("counter-appearance-build", "PBXBuildFile", fileRef=counter_appearance))
            design_view = add("widget-slots-view", "PBXFileReference", lastKnownFileType="sourcecode.swift",
                              path="ControlStudioWidgets/StudioSlotWidget.swift", sourceTree="<group>")
            children.append(design_view)
            build_files.append(add("widget-slots-view-build", "PBXBuildFile", fileRef=design_view))
            runtime = add("widget-background-adapter", "PBXFileReference", lastKnownFileType="sourcecode.c.objc",
                          path="WidgetRuntime/CSWidgetBackgroundAdapter.m", sourceTree="<group>")
            children.append(runtime)
            build_files.append(add("widget-background-adapter-build", "PBXBuildFile", fileRef=runtime))
            diagnostics = add("widget-background-diagnostics", "PBXFileReference", lastKnownFileType="sourcecode.c.objc",
                              path="WidgetRuntime/CSWidgetBackgroundDiagnostics.m", sourceTree="<group>")
            children.append(diagnostics)
            build_files.append(add("widget-background-diagnostics-build", "PBXBuildFile", fileRef=diagnostics))
        sources = add(name + "-sources", "PBXSourcesBuildPhase", buildActionMask=2147483647,
                      files=build_files, runOnlyForDeploymentPostprocessing=0)
        frameworks = add(name + "-frameworks", "PBXFrameworksBuildPhase", buildActionMask=2147483647,
                         files=[], runOnlyForDeploymentPostprocessing=0)
        privacy_build = add(name + "-privacy-build", "PBXBuildFile", fileRef=privacy)
        resources = add(name + "-resources", "PBXResourcesBuildPhase", buildActionMask=2147483647,
                        files=[privacy_build], runOnlyForDeploymentPostprocessing=0)
        settings = {
            "PRODUCT_NAME": name, "PRODUCT_BUNDLE_IDENTIFIER": f"com.faikalizham.controlstudio.{suffix}",
            "INFOPLIST_FILE": f"{name}/Info.plist", "GENERATE_INFOPLIST_FILE": "YES",
            "CODE_SIGN_ENTITLEMENTS": f"{name}/Entitlements.plist",
            "CODE_SIGNING_ALLOWED": "NO", "SKIP_INSTALL": "YES",
            "APPLICATION_EXTENSION_API_ONLY": "YES", "IPHONEOS_DEPLOYMENT_TARGET": "17.0",
            "SWIFT_VERSION": "5.0", "SWIFT_OPTIMIZATION_LEVEL": "-O",
            "SWIFT_EMIT_LOC_STRINGS": "YES", "TARGETED_DEVICE_FAMILY": "1,2",
            "CURRENT_PROJECT_VERSION": "26", "MARKETING_VERSION": "0.2.24",
            "LD_RUNPATH_SEARCH_PATHS": ["$(inherited)", "@executable_path/Frameworks", "@executable_path/../../Frameworks"],
            "OTHER_LDFLAGS": ["$(inherited)", "-lsqlite3"],
        }
        if name == "ControlStudioWidgets":
            settings["SWIFT_OBJC_BRIDGING_HEADER"] = "ControlStudioWidgets/ControlStudioWidgets-Bridging-Header.h"
        config = add(name + "-release", "XCBuildConfiguration", name="Release", buildSettings=settings)
        configs = add(name + "-configs", "XCConfigurationList", buildConfigurations=[config],
                      defaultConfigurationIsVisible=0, defaultConfigurationName="Release")
        targets.append(add(name + "-target", "PBXNativeTarget", name=name, productName=name,
                           productReference=product, productType="com.apple.product-type." + kind,
                           buildConfigurationList=configs, buildPhases=[sources, frameworks, resources],
                           buildRules=[], dependencies=[]))
    products_group = add("products", "PBXGroup", name="Products", children=products, sourceTree="<group>")
    main = add("main", "PBXGroup", children=children + [products_group], sourceTree="<group>")
    config = add("project-release", "XCBuildConfiguration", name="Release", buildSettings={
        "SDKROOT": "iphoneos", "CLANG_ENABLE_MODULES": "YES", "ENABLE_STRICT_OBJC_MSGSEND": "YES",
        "CLANG_ENABLE_OBJC_ARC": "YES", "SWIFT_VERSION": "5.0", "DEBUG_INFORMATION_FORMAT": "dwarf-with-dsym",
    })
    configs = add("project-configs", "XCConfigurationList", buildConfigurations=[config],
                  defaultConfigurationIsVisible=0, defaultConfigurationName="Release")
    project = add("project", "PBXProject", archiveVersion=1, attributes={"LastUpgradeCheck": "2660"},
                  buildConfigurationList=configs, compatibilityVersion="Xcode 14.0",
                  developmentRegion="en", knownRegions=["en", "Base"], mainGroup=main,
                  productRefGroup=products_group, projectDirPath="", projectRoot="", targets=targets)

    def value(item, depth=0):
        indent = "\t" * depth
        nested = "\t" * (depth + 1)
        if isinstance(item, dict):
            return "{\n" + "\n".join(f"{nested}{json.dumps(k)} = {value(v, depth + 1)};" for k, v in item.items()) + "\n" + indent + "}"
        if isinstance(item, list):
            return "(\n" + "\n".join(nested + value(v, depth + 1) + "," for v in item) + "\n" + indent + ")"
        if isinstance(item, int):
            return str(item)
        return json.dumps(item)

    model = {"archiveVersion": 1, "classes": {}, "objectVersion": 56, "objects": objects, "rootObject": project}
    contents = "// !$*UTF8*$!\n" + value(model) + "\n"
    output = ROOT / "native/ControlStudioNative.xcodeproj/project.pbxproj"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(contents, encoding="utf-8", newline="\n")
    return model


if __name__ == "__main__":
    generate()
