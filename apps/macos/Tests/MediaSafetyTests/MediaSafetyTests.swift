import XCTest
import ApplicationServices
@testable import LookPauseMediaProbe

final class MediaSafetyTests: XCTestCase {
    @MainActor func testReplacedControlCannotInheritPauseOwnership() {
        let window = AXUIElementCreateSystemWide()
        func media(_ control: AXUIElement) -> FrontMedia {
            FrontMedia(pid: 123, bundle: "fixture", window: window, context: "same-page-same-title",
                       hint: .paused, control: control, outputActive: nil, controls: 1, evidence: "fixture")
        }
        let old = media(AXUIElementCreateApplication(123))
        let replacement = media(AXUIElementCreateApplication(124))
        XCTAssertFalse(old.matches(replacement), "同页换控件不能继承旧暂停资格")
    }
}

extension MediaSafetyTests {
    @MainActor private func paused(position: Double = 10, duration: Double = 60, rounded: Bool = false,
                                   timelineID: pid_t = 456) -> FrontMedia {
        FrontMedia(pid: 123, bundle: "fixture", window: AXUIElementCreateSystemWide(), context: "same-page-same-title",
                   hint: .paused, control: AXUIElementCreateApplication(123), outputActive: nil, controls: 1, evidence: "fixture", seekControl: AXUIElementCreateApplication(timelineID),
                   timeline: MediaTimeline(position: position, duration: duration, rounded: rounded),
                   timelineControl: AXUIElementCreateApplication(timelineID))
    }

    @MainActor func testSameControlsCannotInheritChangedDurationOrTimeline() {
        XCTAssertFalse(paused().matches(paused(duration: 90)))
        XCTAssertFalse(paused().matches(paused(timelineID: 457)))
        XCTAssertTrue(paused().canResume(paused()))
    }

    @MainActor func testPausePositionAndEndMustRemainVerifiable() {
        XCTAssertFalse(paused().canResume(paused(position: 0)))
        XCTAssertFalse(paused().canResume(paused(position: 11)))
        XCTAssertFalse(paused(position: 60).canResume(paused(position: 60)))
        XCTAssertFalse(paused(position: .nan).canResume(paused(position: .nan)))
        XCTAssertTrue(paused().canResume(paused(position: 10.02)))
        XCTAssertFalse(paused().canResume(paused(position: 10.1)))
        XCTAssertFalse(paused(rounded: true).canResume(paused(position: 11, rounded: true)))
        var missing = paused(); missing.timeline = nil
        XCTAssertFalse(paused().canResume(missing))
    }

    @MainActor func testTimeTextRerenderIsNotMediaIdentity() {
        let old = paused()
        var refreshed = paused()
        refreshed.timelineControl = AXUIElementCreateApplication(789)
        XCTAssertTrue(old.matches(refreshed))
        XCTAssertTrue(old.canResume(refreshed))
    }

    func testTerminalAndConflictingStateOverridesPlayButton() {
        XCTAssertEqual(resolvedMediaHint(label: "Play", checkValue: nil, classes: ["paused", "player-state-ended"]), .unknown)
        XCTAssertEqual(resolvedMediaHint(label: "播放/暂停", checkValue: 0, classes: ["loading"]), .unknown)
        XCTAssertEqual(resolvedMediaHint(label: "Pause", checkValue: nil, classes: ["paused"]), .unknown)
        XCTAssertEqual(resolvedMediaHint(label: "Play", checkValue: nil, classes: ["paused"]), .paused)
    }

    func testTimelineRetainsEndInsteadOfDroppingIt() {
        XCTAssertEqual(MediaTimeline.parse("00:10 / 01:00")?.position, 10)
        XCTAssertEqual(MediaTimeline.parse("1 Minutes 0 Seconds of 2 Minutes 0 Seconds")?.duration, 120)
        XCTAssertEqual(MediaTimeline.parse("01:00 / 01:00")?.beforeEnd, false)
        XCTAssertNil(MediaTimeline.parse("00:99 / 01:00"))
        XCTAssertNil(MediaTimeline.parse("NaN / 01:00"))
        XCTAssertNil(MediaTimeline.parse("直播"))
    }

    func testOnlyThisRoundsOwnFixedKeysBypassInputRevocation() throws {
        let event = try XCTUnwrap(CGEvent(keyboardEventSource: nil, virtualKey: 123, keyDown: true))
        event.setIntegerValueField(.eventSourceUnixProcessID, value: Int64(getpid()))
        event.setIntegerValueField(.eventSourceUserData, value: 12345)
        XCTAssertTrue(isOwnMediaKey(event, type: .keyDown, tag: 12345))
        event.setIntegerValueField(.keyboardEventKeycode, value: 49)
        XCTAssertTrue(isOwnMediaKey(event, type: .keyDown, tag: 12345))
        XCTAssertFalse(isOwnMediaKey(event, type: .keyDown, tag: 67890), "真实或其他轮次输入仍撤销")
        XCTAssertFalse(isOwnMediaKey(event, type: .flagsChanged, tag: 12345))
        event.setIntegerValueField(.eventSourceUnixProcessID, value: Int64(getpid()) + 1)
        XCTAssertFalse(isOwnMediaKey(event, type: .keyDown, tag: 12345))
        event.setIntegerValueField(.eventSourceUnixProcessID, value: Int64(getpid()))
        event.setIntegerValueField(.keyboardEventKeycode, value: 124)
        XCTAssertFalse(isOwnMediaKey(event, type: .keyDown, tag: 12345), "不能扩大为任意按键豁免")
    }
}
