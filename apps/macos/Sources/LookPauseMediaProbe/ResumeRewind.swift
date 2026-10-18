import AppKit
import ApplicationServices
import OSLog

// 仅由已确认的自动恢复调用，一轮只调用一次；LAN不接收按键或回退参数。
struct RewindAttempt {
    let from: Double?
}

@MainActor
func rewindMedia(_ target: FrontMedia, watch: InputWatch) throws -> RewindAttempt? {
    guard watch.denied == nil, stillFrontmost(target) else { return nil }
    let fresh = try frontMedia(includeAudio: false)
    guard target.matches(fresh), fresh.hint == .playing, watch.denied == nil else { return nil }
    if let seek = fresh.seekControl, nativeTimeline(seek), let seconds = numericTimelineSeconds(seek),
       let minimum = (axAttribute(seek, kAXMinValueAttribute) as? NSNumber)?.doubleValue,
       let maximum = (axAttribute(seek, kAXMaxValueAttribute) as? NSNumber)?.doubleValue {
        guard let destination = rewindDestination(current: seconds, minimum: minimum, maximum: maximum) else { return nil }
        var settable = DarwinBoolean(false)
        if AXUIElementIsAttributeSettable(seek, kAXValueAttribute as CFString, &settable) == .success, settable.boolValue {
            // 已提交过原生写入就不再补Left，避免错误返回下重复回退。
            guard stillFrontmost(fresh), watch.denied == nil,
                  AXUIElementSetAttributeValue(seek, kAXValueAttribute as CFString, NSNumber(value: destination)) == .success else { return nil }
            return RewindAttempt(from: seconds)
        }
    }
    // shortcut: Left语义由播放器决定；仅在恢复后尝试一次，结果不明时不补发。
    guard try sendMediaKey(.left, target: fresh, watch: watch) else { return nil }
    return RewindAttempt(from: fresh.progress)
}

func rewindDestination(current: Double, minimum: Double, maximum: Double) -> Double? {
    guard current.isFinite, minimum.isFinite, maximum.isFinite,
          minimum >= 0, maximum > minimum, current > minimum, current <= maximum else { return nil }
    return max(minimum, current - 5)
}

@MainActor
func sendMediaKey(_ key: MediaKey, target: FrontMedia, watch: InputWatch) throws -> Bool {
    guard key != .left || target.hint == .playing else { return false }
    let app = AXUIElementCreateApplication(target.pid)
    let focusedElement = axElement(app, kAXFocusedUIElementAttribute)
    let editing = focusedElement.map(editableFocus) ?? true
    let modifiers = CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand])
    guard let control = target.control, let focus = focusedElement, !editing,
          CGPreflightPostEventAccess(), watch.denied == nil, stillFrontmost(target),
          modifiers.isEmpty else {
        Logger(subsystem: "com.lookpause.pilot", category: "media").notice("固定媒体键前置检查拒绝 post=\(CGPreflightPostEventAccess()) input=\(watch.denied != nil) front=\(stillFrontmost(target)) focus=\(focusedElement != nil) editing=\(editing) modifiers=\(!modifiers.isEmpty)")
        return false
    }
    if !CFEqual(focus, control) {
        guard AXUIElementSetAttributeValue(control, kAXFocusedAttribute as CFString, kCFBooleanTrue) == .success else { return false }
    }
    guard let focused = axElement(app, kAXFocusedUIElementAttribute), CFEqual(focused, control),
          stillFrontmost(target), watch.denied == nil else {
        Logger(subsystem: "com.lookpause.pilot", category: "media").notice("固定媒体键焦点校验拒绝")
        return false
    }
    let source = CGEventSource(stateID: .combinedSessionState)
    guard let down = CGEvent(keyboardEventSource: source, virtualKey: key.rawValue, keyDown: true),
          let up = CGEvent(keyboardEventSource: source, virtualKey: key.rawValue, keyDown: false) else { return false }
    for event in [down, up] {
        event.flags = []
        event.setIntegerValueField(.eventSourceUserData, value: watch.ownEventTag)
        event.postToPid(target.pid)
    }
    Logger(subsystem: "com.lookpause.pilot", category: "media").notice("固定媒体键已提交 key=\(key.rawValue, privacy: .public)")
    return true
}

func rewindSelfTest() {
    precondition(rewindDestination(current: 27.19, minimum: 0, maximum: 52.209) == 22.19)
    precondition(rewindDestination(current: 3, minimum: 0, maximum: 52) == 0)
    precondition(rewindDestination(current: 0, minimum: 0, maximum: 52) == nil)
    precondition(rewindDestination(current: 53, minimum: 0, maximum: 52) == nil)
    precondition(rewindDestination(current: 20, minimum: 30, maximum: 52) == nil)
    precondition(rewindDestination(current: 20, minimum: 0, maximum: 0) == nil)
    precondition(rewindDestination(current: .nan, minimum: 0, maximum: 52) == nil)
    precondition(rewindDestination(current: 20, minimum: 0, maximum: .infinity) == nil)
}

func numericTimelineSeconds(_ slider: AXUIElement) -> Double? {
    guard let value = (axAttribute(slider, kAXValueAttribute) as? NSNumber)?.doubleValue, value.isFinite,
          let units = axAttribute(slider, kAXValueDescriptionAttribute) as? String,
          units.range(of: "秒|seconds?", options: [.regularExpression, .caseInsensitive]) != nil else { return nil }
    return value
}

// 网页ARIA数值不等于原生可写时间线；网页即使AXSetValue返回成功也可能不执行seek。
func nativeTimeline(_ slider: AXUIElement) -> Bool {
    var node: AXUIElement? = slider
    for _ in 0..<16 {
        guard let current = node else { return false }
        let role = axAttribute(current, kAXRoleAttribute) as? String ?? ""
        if role == "AXWebArea" { return false }
        if role == kAXWindowRole { return true }
        node = axElement(current, kAXParentAttribute)
    }
    return false
}

func editableFocus(_ focus: AXUIElement) -> Bool {
    var current: AXUIElement? = focus
    // 真实网页播放控件到WebArea超过16层；完整核查有限祖先链，超界仍拒绝。
    for _ in 0..<64 {
        guard let node = current else { return false }
        let role = axAttribute(node, kAXRoleAttribute) as? String ?? ""
        if [kAXTextFieldRole, kAXTextAreaRole, kAXComboBoxRole].contains(role) { return true }
        if role == kAXWindowRole || role == "AXWebArea" { return false }
        current = axElement(node, kAXParentAttribute)
    }
    return true
}
