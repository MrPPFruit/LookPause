import XCTest
@testable import LookPauseMediaProbe

final class ShortcutModeTests: XCTestCase {
    @MainActor func testSelectionOnlyChangesNextStart() {
        let owner = MediaController()
        XCTAssertFalse(owner.nextShortcutMode)
        owner.start()
        XCTAssertFalse(owner.status.contains("快捷键兼容"))
        owner.nextShortcutMode = true
        XCTAssertFalse(owner.status.contains("快捷键兼容"))
        owner.start()
        XCTAssertTrue(owner.status.contains("快捷键兼容"))
        owner.nextShortcutMode = false
        XCTAssertTrue(owner.status.contains("快捷键兼容"))
        owner.stop()
        owner.receive(.away) // STOP后的迟到意图不得创建输入观察或发键。
        XCTAssertFalse(owner.isWatching)
        XCTAssertEqual(owner.lastIntent, .unknown)
        owner.start()
        XCTAssertFalse(owner.status.contains("快捷键兼容"))
        owner.stop()
    }
    @MainActor func testNewControllerDoesNotRememberCompatibilitySelection() {
        let first = MediaController(); first.nextShortcutMode = true
        let second = MediaController()
        XCTAssertFalse(second.nextShortcutMode)
    }
}
