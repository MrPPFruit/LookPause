import AppKit
import ApplicationServices
import OSLog

// 仅由已确认的自动恢复调用，一轮只调用一次；LAN不接收按键或回退参数。
@MainActor
func rewindMedia(_ target: FrontMedia, watch: InputWatch) throws -> Bool {
    guard watch.denied == nil, stillFrontmost(target) else { return false }
    let fresh = try frontMedia(includeAudio: false)
    guard target.matches(fresh), fresh.hint == .playing, watch.denied == nil else { return false }
    if let seek = fresh.seekControl, nativeTimeline(seek), let seconds = numericTimelineSeconds(seek) {
        var settable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(seek, kAXValueAttribute as CFString, &settable) == .success, settable.boolValue,
              stillFrontmost(fresh), watch.denied == nil else { return false }
        let minimum = (axAttribute(seek, kAXMinValueAttribute) as? NSNumber)?.doubleValue ?? 0
        return AXUIElementSetAttributeValue(seek, kAXValueAttribute as CFString, NSNumber(value: max(minimum, seconds - 5))) == .success
    }
    // 非秒单位时间线的AXDecrement可能成功返回但没有副作用；使用已聚焦播放控件的固定Left。
    return try sendMediaKey(.back, target: fresh, watch: watch, focusControl: false)
}

enum FixedMediaKey: CGKeyCode { case toggle = 49, back = 123 }

@MainActor
func sendMediaKey(_ key: FixedMediaKey, target: FrontMedia, watch: InputWatch, focusControl: Bool) throws -> Bool {
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
        guard focusControl else { return false }
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
        // 方向键本身带numericPad/function语义；清空这些标记会改变Cocoa对Left的分发。
        event.flags = key == .back ? [.maskNumericPad, .maskSecondaryFn] : []
        event.setIntegerValueField(.eventSourceUserData, value: watch.ownEventTag)
        event.postToPid(target.pid)
    }
    return true
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
