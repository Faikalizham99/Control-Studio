import XCTest
@testable import ControlStudioStorage

final class WidgetTapLinkTests: XCTestCase {
    func testLinksAgreeWithManagedRoutingContractAcrossSizesAndSlotBoundaries() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let data = try Data(contentsOf: root.appendingPathComponent("contracts/widget-tap-links-v1.example.json"))
        let fixture = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(WidgetTapLinks.counter.absoluteString, fixture["counter"] as? String)
        let slots = try XCTUnwrap(fixture["slots"] as? [[String: Any]])
        XCTAssertEqual(slots.count, 8)
        for slot in slots {
            let size = try XCTUnwrap(WidgetDesignSize(rawValue: try XCTUnwrap(slot["size"] as? String)))
            let number = try XCTUnwrap(slot["number"] as? Int)
            XCTAssertEqual(WidgetTapLinks.slot(size: size, number: number)?.absoluteString, slot["url"] as? String)
        }
        for size in [WidgetDesignSize.small, .medium, .large, .extraLargePortrait] {
            for number in 1...20 {
                XCTAssertEqual(WidgetTapLinks.slot(size: size, number: number)?.absoluteString,
                    "controlstudio://widget/\(size.rawValue)/\(number)")
            }
            for number in [-1, 0, 21] { XCTAssertNil(WidgetTapLinks.slot(size: size, number: number)) }
        }
        XCTAssertNil(WidgetTapLinks.slot(size: .extraLarge, number: 1))
    }
}
