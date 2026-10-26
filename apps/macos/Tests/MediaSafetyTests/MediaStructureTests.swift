import XCTest
import ApplicationServices
@testable import LookPauseMediaProbe

final class MediaStructureTests: XCTestCase {
    func testActiveDocumentRequiresUniqueVisibleRootAndCurrentEvidence() {
        XCTAssertEqual(activeMediaDocument(roots: [(1,"current")], windowDocument: "current", focusedRoot: nil), 1)
        XCTAssertEqual(activeMediaDocument(roots: [(1,"current")], windowDocument: "stale", focusedRoot: 1), 1)
        XCTAssertNil(activeMediaDocument(roots: [(1,"current")], windowDocument: "stale", focusedRoot: nil))
        XCTAssertNil(activeMediaDocument(roots: [(1,"current"),(2,"other")], windowDocument: "current", focusedRoot: 1))
    }
    func testSharedPolicyCases() throws {
        struct Fixture: Decodable {
            struct Case: Decodable {
                struct Member: Decodable { let kind: MediaMember; let parent: String }
                let id: String; let kind: String; let scope: String?; let members: [Member]?
                let left: [String]?; let right: [String]?; let expected: Bool
            }
            let cases: [Case]
        }
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let fixture = try JSONDecoder().decode(Fixture.self, from: Data(contentsOf: root.appendingPathComponent("tests/media-policy/cases.json")))
        for item in fixture.cases {
            let result: Bool
            if item.kind == "cluster" {
                let names = Array(Set((item.members ?? []).map(\.parent) + [item.scope!])).sorted()
                result = closedMediaCluster(item.members!.map { ($0.kind, names.firstIndex(of: $0.parent)!) }, scope: names.firstIndex(of: item.scope!)!)
            } else { result = item.left == item.right }
            XCTAssertEqual(result, item.expected, item.id)
        }
    }

    private func nativeTree(extra: MediaMember? = nil, siblingDocument: Bool = false, duplicatePlay: Bool = false, volumeVisible: Bool = true) -> [MediaAXNode] {
        let structure: [(Int?, MediaMember)] = [(nil,.container),(0,.container),(1,.playback),(1,.container),(3,.volume),(3,.action),(1,.action),(0,.seek)] +
            (extra.map { [(0,$0)] } ?? []) + (duplicatePlay ? [(1,.playback)] : [])
        return structure.enumerated().map { index, item in
            MediaAXNode(element: AXUIElementCreateApplication(pid_t(index + 100)), parent: item.0,
                        role: item.1 == .container ? kAXGroupRole : "fixture", semantics: "", member: item.1,
                        documents: siblingDocument && item.1 == .seek ? [2,1] : [1], namedScope: nil, visible: item.1 == .volume ? volumeVisible : true)
        }
    }

    func testMacNativeClosedWrappersAndForeignRejection() {
        XCTAssertTrue(closedMediaScope(0, nodes: nativeTree()))
        XCTAssertFalse(closedMediaScope(0, nodes: nativeTree(extra: .foreign)))
        XCTAssertFalse(closedMediaScope(0, nodes: nativeTree(siblingDocument: true)))
        XCTAssertFalse(closedMediaScope(0, nodes: nativeTree(duplicatePlay: true)))
        XCTAssertFalse(closedMediaScope(0, nodes: nativeTree(extra: .seek)))
        XCTAssertFalse(closedMediaScope(0, nodes: nativeTree(volumeVisible: false)), "收起尺寸由AX适配器处理，隐藏或禁用音量仍拒绝")
    }

    func testNativeRoleAdapterUsesActualControlLabels() {
        XCTAssertEqual(mediaMember(role: kAXButtonRole, label: "暂停", semantics: "暂停 按钮"), .playback)
        XCTAssertEqual(mediaMember(role: kAXSliderRole, label: "volume", semantics: "volume 滑块"), .volume)
        XCTAssertEqual(mediaMember(role: kAXSliderRole, label: "视频时间进度条", semantics: "视频时间进度条 滑块"), .seek)
        XCTAssertEqual(mediaMember(role: kAXButtonRole, label: "进入全屏模式", semantics: ""), .action)
        XCTAssertEqual(mediaMember(role: kAXPopUpButtonRole, label: "显示更多媒体控件", semantics: ""), .action)
        XCTAssertEqual(mediaMember(role: kAXButtonRole, label: "删除视频", semantics: ""), .foreign)
    }

    func testSplitTimeDescriptionsRequireUnitsRangeAndMeaning() {
        XCTAssertEqual(MediaTimeline.described(elapsed: "已播放时间：2:23", total: "总时间：28:54"), MediaTimeline(position: 143, duration: 1734, rounded: true))
        XCTAssertEqual(MediaTimeline.described(elapsed: "elapsed time: 0:02", total: "total time: 0:05")?.position, 2)
        XCTAssertEqual(MediaTimeline.described(elapsed: "elapsed time: 5 Seconds", total: "total time: 5 Seconds")?.beforeEnd, false)
        for (current,total) in [("剩余时间：0:02","总时间：0:05"),("已播放时间：2","总时间：5"),("已播放时间：0:06","总时间：0:05"),("已播放时间：0:99","总时间：1:00"),("elapsed time: 2 Seconds junk","total time: 5 Seconds"),("已播放时间：0:02","直播")] {
            XCTAssertNil(MediaTimeline.described(elapsed: current, total: total), current)
        }
        XCTAssertNil(MediaTimeline.parse("00:12 / 00:10"))
        XCTAssertNil(coherentMediaTimeline([MediaTimeline(position: 9, duration: 10, rounded: true),MediaTimeline(position: 10, duration: 10, rounded: true)]))
    }

    @MainActor func testScopeAndEveryDocumentAncestorPinOwnership() {
        let frame = AXUIElementCreateApplication(123)
        let page = AXUIElementCreateApplication(124)
        let window = AXUIElementCreateSystemWide()
        let original = FrontMedia(pid: 123, bundle: "fixture", window: window, context: "fixture", hint: .paused,
                                  control: frame, outputActive: nil, controls: 1, evidence: "fixture",
                                  mediaScope: page, documents: [MediaDocument(element: frame,address: "inner"),MediaDocument(element: page,address: "outer")])
        XCTAssertTrue(original.matches(original))
        var changed = original
        changed.documents[1] = MediaDocument(element: page,address: "other")
        XCTAssertFalse(original.matches(changed))
        changed = original; changed.mediaScope = frame
        XCTAssertFalse(original.matches(changed))
        changed = original; changed.documents.removeLast()
        XCTAssertFalse(original.matches(changed))
    }
}
