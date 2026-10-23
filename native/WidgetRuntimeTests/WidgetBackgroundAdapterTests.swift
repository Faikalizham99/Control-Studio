import Foundation
import XCTest
import ControlStudioWidgetRuntime

@objc(CSBackgroundTestDescriptor)
class BackgroundTestDescriptor: NSObject, NSSecureCoding, NSMutableCopying {
    static var supportsSecureCoding: Bool { true }
    @objc dynamic var kind = ""
    @objc dynamic var backgroundRemovable = false
    @objc dynamic var transparent = false
    @objc dynamic var preferredBackgroundStyle: UInt = 0
    required override init() { super.init() }
    required init?(coder: NSCoder) {
        kind = coder.decodeObject(of: NSString.self, forKey: "kind") as String? ?? ""
        backgroundRemovable = coder.decodeBool(forKey: "removable")
        transparent = coder.decodeBool(forKey: "transparent")
        preferredBackgroundStyle = UInt(coder.decodeInteger(forKey: "style"))
        super.init()
    }
    func encode(with coder: NSCoder) {
        coder.encode(kind as NSString, forKey: "kind")
        coder.encode(backgroundRemovable, forKey: "removable")
        coder.encode(transparent, forKey: "transparent")
        coder.encode(Int(preferredBackgroundStyle), forKey: "style")
    }
    func mutableCopy(with zone: NSZone? = nil) -> Any {
        let copy = type(of: self).init()
        copy.kind = kind; copy.backgroundRemovable = backgroundRemovable
        copy.transparent = transparent; copy.preferredBackgroundStyle = preferredBackgroundStyle
        return copy
    }
}

@objc(CSBackgroundMissingSetter)
final class BackgroundMissingSetter: BackgroundTestDescriptor {
    override func responds(to selector: Selector!) -> Bool {
        selector == NSSelectorFromString("setTransparent:") ? false : super.responds(to: selector)
    }
}

@objc(CSBackgroundSelfCopy)
final class BackgroundSelfCopy: BackgroundTestDescriptor {
    override func mutableCopy(with zone: NSZone? = nil) -> Any { self }
}

@objc(CSBackgroundWrongABI)
final class BackgroundWrongABI: NSObject, NSSecureCoding, NSMutableCopying {
    static var supportsSecureCoding: Bool { true }
    @objc dynamic var kind = "ControlStudio.Slot.1"
    @objc dynamic var backgroundRemovable = false
    @objc dynamic var transparent = false
    // An incompatible future setter must be rejected before dispatch.
    @objc dynamic var preferredBackgroundStyle = "default"
    override init() { super.init() }
    required init?(coder: NSCoder) { super.init() }
    func encode(with coder: NSCoder) { }
    func mutableCopy(with zone: NSZone? = nil) -> Any { BackgroundWrongABI() }
}

@objc(CSBackgroundTestResult)
final class BackgroundTestResult: NSObject, NSSecureCoding {
    static var supportsSecureCoding: Bool { true }
    let widgets: [NSObject]
    let controls: [String]
    let activities: [String]
    let futureMetadata: [String: String]
    let rejectDecode: Bool
    init(widgets: [NSObject], rejectDecode: Bool = false) {
        self.widgets = widgets; self.rejectDecode = rejectDecode
        controls = ["control metadata"]; activities = ["activity metadata"]
        futureMetadata = ["new-runtime-field": "must survive"]
        super.init()
    }
    required init?(coder: NSCoder) {
        if coder.decodeBool(forKey: "rejectDecode") {
            coder.failWithError(NSError(domain: "BackgroundFixture", code: 1)); return nil
        }
        widgets = coder.decodeObject(of: [NSArray.self, BackgroundTestDescriptor.self, BackgroundWrongABI.self], forKey: "widgets") as? [NSObject] ?? []
        controls = coder.decodeObject(of: [NSArray.self, NSString.self], forKey: "controls") as? [String] ?? []
        activities = coder.decodeObject(of: [NSArray.self, NSString.self], forKey: "activities") as? [String] ?? []
        futureMetadata = coder.decodeObject(of: [NSDictionary.self, NSString.self], forKey: "futureMetadata") as? [String: String] ?? [:]
        rejectDecode = false; super.init()
    }
    func encode(with coder: NSCoder) {
        coder.encode(widgets as NSArray, forKey: "widgets"); coder.encode(controls as NSArray, forKey: "controls")
        coder.encode(activities as NSArray, forKey: "activities"); coder.encode(futureMetadata as NSDictionary, forKey: "futureMetadata")
        coder.encode(rejectDecode, forKey: "rejectDecode")
    }
}

final class WidgetBackgroundAdapterTests: XCTestCase {
    private func descriptor(_ kind: String) -> BackgroundTestDescriptor {
        let value = BackgroundTestDescriptor(); value.kind = kind; return value
    }
    func testOnlyExactNumberedStudioKindsAreAllowed() {
        for number in 1...10 { XCTAssertTrue(CSIsStudioSlotKind("ControlStudio.Slot.\(number)")) }
        for value in ["ControlStudio.Counter", "ControlStudio.Slot.0", "ControlStudio.Slot.11", "ControlStudio.Slot.01", "ControlStudio.Slot.1.extra", "Other.Slot.1", ""] {
            XCTAssertFalse(CSIsStudioSlotKind(value))
        }
        XCTAssertFalse(CSIsStudioSlotKind(nil))
    }
    func testAllTenSlotsArePatchedWithoutMutatingCounterOriginalOrFutureMetadata() throws {
        let slots = (1...10).map { descriptor("ControlStudio.Slot.\($0)") }
        let counter = descriptor("ControlStudio.Counter")
        let original = BackgroundTestResult(widgets: slots + [counter])
        let rewritten = try XCTUnwrap(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? BackgroundTestResult)
        XCTAssertFalse(rewritten === original); XCTAssertEqual(rewritten.widgets.count, 11)
        for index in 0..<10 {
            let slot = try XCTUnwrap(rewritten.widgets[index] as? BackgroundTestDescriptor)
            XCTAssertEqual(slot.kind, slots[index].kind); XCTAssertTrue(slot.backgroundRemovable)
            XCTAssertTrue(slot.transparent); XCTAssertEqual(slot.preferredBackgroundStyle, 1)
            XCTAssertFalse(slots[index].transparent); XCTAssertEqual(slots[index].preferredBackgroundStyle, 0)
        }
        let untouched = try XCTUnwrap(rewritten.widgets[10] as? BackgroundTestDescriptor)
        XCTAssertEqual(untouched.kind, counter.kind); XCTAssertFalse(untouched.transparent)
        XCTAssertFalse(untouched.backgroundRemovable); XCTAssertEqual(untouched.preferredBackgroundStyle, 0)
        XCTAssertEqual(rewritten.controls, original.controls); XCTAssertEqual(rewritten.activities, original.activities)
        XCTAssertEqual(rewritten.futureMetadata, original.futureMetadata)
    }
    func testUnrelatedResultsAndMissingRuntimeClassReturnOriginalIdentity() {
        let original = BackgroundTestResult(widgets: [descriptor("ControlStudio.Counter")])
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? NSObject === original)
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: nil) as? NSObject === original)
        let unencoded = NSObject()
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(unencoded, descriptorClass: BackgroundTestDescriptor.self) as? NSObject === unencoded)
        XCTAssertNil(CSWidgetBackgroundRewriteResult(nil, descriptorClass: BackgroundTestDescriptor.self))
    }
    func testMissingSetterDiscardsWholeRewriteAndPreservesOriginal() {
        let broken = BackgroundMissingSetter(); broken.kind = "ControlStudio.Slot.2"
        let valid = descriptor("ControlStudio.Slot.1")
        let original = BackgroundTestResult(widgets: [valid, broken])
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? NSObject === original)
        XCTAssertFalse(valid.transparent); XCTAssertFalse(broken.transparent)
    }
    func testIncompatibleSetterABIReturnsOriginal() {
        let original = BackgroundTestResult(widgets: [BackgroundWrongABI()])
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundWrongABI.self) as? NSObject === original)
    }
    func testMutableCopyReturningSelfCannotModifyTheOriginal() {
        let value = BackgroundSelfCopy(); value.kind = "ControlStudio.Slot.1"
        let original = BackgroundTestResult(widgets: [value])
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? NSObject === original)
        XCTAssertFalse(value.transparent)
    }
    func testSecureDecodeFailureReturnsOriginal() {
        let original = BackgroundTestResult(widgets: [descriptor("ControlStudio.Slot.1")], rejectDecode: true)
        XCTAssertTrue(CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? NSObject === original)
    }
    func testConcurrentRequestsDoNotShareMutableDecoderState() {
        let original = BackgroundTestResult(widgets: [descriptor("ControlStudio.Slot.1")])
        DispatchQueue.concurrentPerform(iterations: 8) { _ in
            let result = CSWidgetBackgroundRewriteResult(original, descriptorClass: BackgroundTestDescriptor.self) as? BackgroundTestResult
            XCTAssertTrue((result?.widgets.first as? BackgroundTestDescriptor)?.transparent == true)
        }
        XCTAssertFalse((original.widgets.first as? BackgroundTestDescriptor)?.transparent ?? true)
    }
    func testInstallerIsHarmlessOnTheMacOSTestHost() {
        CSInstallWidgetBackgroundAdapter(); CSInstallWidgetBackgroundAdapter()
    }
}
