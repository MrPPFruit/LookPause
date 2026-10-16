import AppKit
import ApplicationServices
import CoreAudio
import CryptoKit
import Foundation

// 本机固定意图 Pilot；无应用/站点白名单，无网络或任意输入接口。
enum MediaHint: String { case playing = "PLAYING", paused = "PAUSED", unknown = "UNKNOWN" }

func mediaHint(label: String, checkValue: Int? = nil) -> MediaHint? {
    var name = label.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    if let suffix = name.range(of: " keyboard shortcut ") { name = String(name[..<suffix.lowerBound]) }
    if let suffix = name.range(of: " (") { name = String(name[..<suffix.lowerBound]) }
    switch name {
    case "play", "播放":
        if let checkValue { return checkValue == 1 ? .playing : checkValue == 0 ? .paused : .unknown }
        return .paused
    case "pause", "暂停", "暫停": return .playing
    case "play/pause", "播放/暂停", "播放／暂停": return .unknown
    default: return nil
    }
}

@MainActor
struct FrontMedia {
    let pid: pid_t
    let bundle: String
    let window: AXUIElement
    let context: String
    let hint: MediaHint
    let control: AXUIElement?
    let outputActive: Bool?
    let controls: Int
    let evidence: String

    func matches(_ other: FrontMedia) -> Bool {
        pid == other.pid && context == other.context && CFEqual(window, other.window)
    }

    var report: [String: Any] {
        ["frontmostBundle": bundle, "contextHash": context, "hint": hint.rawValue,
         "controls": controls, "audioOutputActive": outputActive.map { $0 as Any } ?? NSNull(),
         "evidence": evidence, "scope": "local-pilot-only", "experimental": true]
    }
}

// 只读输出流活动元数据；活动不等于声音，也不等于当前窗口视频播放。
func audioOutputActive(bundle: String) -> Bool? {
    var address = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyProcessObjectList,
                                             mScope: kAudioObjectPropertyScopeGlobal,
                                             mElement: kAudioObjectPropertyElementMain)
    var bytes: UInt32 = 0
    let system = AudioObjectID(kAudioObjectSystemObject)
    guard AudioObjectGetPropertyDataSize(system, &address, 0, nil, &bytes) == noErr, bytes > 0 else { return nil }
    var objects = [AudioObjectID](repeating: 0, count: Int(bytes) / MemoryLayout<AudioObjectID>.size)
    let status = objects.withUnsafeMutableBytes { buffer in
        AudioObjectGetPropertyData(system, &address, 0, nil, &bytes, buffer.baseAddress!)
    }
    guard status == noErr else { return nil }
    var matched = false
    var incomplete = false
    for object in objects {
        address.mSelector = kAudioProcessPropertyBundleID
        var rawBundle: Unmanaged<CFString>?
        bytes = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        guard AudioObjectGetPropertyData(object, &address, 0, nil, &bytes, &rawBundle) == noErr,
              let rawBundle else { incomplete = true; continue }
        let owner = rawBundle.takeRetainedValue() as String
        guard owner == bundle || owner.hasPrefix(bundle + ".") else { continue }
        matched = true
        address.mSelector = kAudioProcessPropertyIsRunningOutput
        var active: UInt32 = 0
        bytes = UInt32(MemoryLayout<UInt32>.size)
        guard AudioObjectGetPropertyData(object, &address, 0, nil, &bytes, &active) == noErr else {
            incomplete = true; continue
        }
        if active == 1 { return true }
    }
    return matched && !incomplete ? false : nil
}

func axElement(_ element: AXUIElement, _ attribute: String) -> AXUIElement? {
    guard let value = axAttribute(element, attribute), CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
    return unsafeDowncast(value, to: AXUIElement.self)
}

func axAddress(_ element: AXUIElement, _ attribute: String) -> String {
    let raw = axAttribute(element, attribute)
    return (raw as? URL)?.absoluteString ?? (raw as? String ?? "")
}

@MainActor
func frontMedia() throws -> FrontMedia {
    guard AXIsProcessTrusted(), let front = NSWorkspace.shared.frontmostApplication,
          let bundle = front.bundleIdentifier else { throw ProbeError.noFrontmostTarget }
    let app = AXUIElementCreateApplication(front.processIdentifier)
    guard let window = axElement(app, kAXFocusedWindowAttribute) else { throw ProbeError.noFrontmostTarget }
    let document = axAddress(window, kAXDocumentAttribute)
    let title = axAttribute(window, kAXTitleAttribute) as? String ?? ""
    // 浏览器标题会随播放音频、通知数变化；有文档标识时不把标题当成媒体身份。
    let context = SHA256.hash(data: Data((document.isEmpty ? title : document).utf8)).map { String(format: "%02x", $0) }.joined()
    let fileMedia = URL(string: document).map { $0.isFileURL && ["mp4", "mov", "m4v", "webm", "mkv"].contains($0.pathExtension.lowercased()) } ?? false
    var stack = [(window, true, fileMedia)]
    var budget = 6000
    var candidates: [(AXUIElement, MediaHint)] = []
    while let (element, inheritedScope, inheritedMedia) = stack.popLast(), budget > 0 {
        budget -= 1
        let role = axAttribute(element, kAXRoleAttribute) as? String ?? ""
        var inScope = inheritedScope
        let semantics = [kAXDescriptionAttribute, kAXRoleDescriptionAttribute].compactMap { axAttribute(element, $0) as? String }.joined(separator: " ").lowercased()
        let mediaScope = inheritedMedia || semantics.range(of: "video|movie|media player|播放器|视频|影片", options: .regularExpression) != nil
        if role == "AXWebArea", !document.isEmpty {
            inScope = axAddress(element, "AXURL") == document
        }
        if inScope && mediaScope && [kAXButtonRole, kAXCheckBoxRole].contains(role),
           (axAttribute(element, kAXEnabledAttribute) as? NSNumber)?.boolValue != false {
            let description = axAttribute(element, kAXDescriptionAttribute) as? String ?? ""
            let label = description.isEmpty ? (axAttribute(element, kAXTitleAttribute) as? String ?? "") : description
            let checked = role == kAXCheckBoxRole ? (axAttribute(element, kAXValueAttribute) as? NSNumber)?.intValue : nil
            if let hint = mediaHint(label: label, checkValue: checked) { candidates.append((element, hint)) }
        }
        stack.append(contentsOf: (axAttribute(element, kAXChildrenAttribute) as? [AXUIElement] ?? []).map { ($0, inScope, mediaScope) })
    }
    let unique = budget > 0 && candidates.count == 1
    let audio = audioOutputActive(bundle: bundle)
    return FrontMedia(pid: front.processIdentifier, bundle: bundle, window: window, context: context,
                      hint: unique ? candidates[0].1 : .unknown, control: unique ? candidates[0].0 : nil,
                      outputActive: audio, controls: candidates.count,
                      evidence: unique && candidates[0].1 != .unknown ? "AX_EXPLICIT" : "UNKNOWN")
}

// AXPress 自身不产生键鼠事件；观察到任何真实按键/点击/滚轮均保守撤销本轮。
final class InputWatch: @unchecked Sendable {
    private let lock = NSLock()
    private var reason: String?
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    var denied: String? { lock.withLock { reason } }
    func revoke(_ value: String) { lock.withLock { if reason == nil { reason = value } } }

    init() throws {
        let events: [CGEventType] = [.keyDown, .flagsChanged, .leftMouseDown, .rightMouseDown, .otherMouseDown, .scrollWheel]
        let mask = events.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly,
                               eventsOfInterest: mask, callback: { _, type, event, info in
            if let info {
                let watch = Unmanaged<InputWatch>.fromOpaque(info).takeUnretainedValue()
                watch.revoke(type == .tapDisabledByTimeout || type == .tapDisabledByUserInput ? "INPUT_MONITOR_LOST" : "USER_INPUT")
            }
            return Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque())
        guard let tap, let source = CFMachPortCreateRunLoopSource(nil, tap, 0) else { throw ProbeError.resultNotObserved }
        self.source = source
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        guard CGEvent.tapIsEnabled(tap: tap) else { close(); throw ProbeError.resultNotObserved }
    }

    func close() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false); CFMachPortInvalidate(tap) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        source = nil; tap = nil
    }
    deinit { close() }
}

@MainActor
func pressMedia(_ target: FrontMedia, expected: MediaHint) throws {
    let fresh = try frontMedia()
    guard target.matches(fresh) else { throw ProbeError.targetChanged }
    guard fresh.hint == expected, let control = fresh.control else { throw ProbeError.resultNotObserved }
    guard NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid else { throw ProbeError.targetChanged }
    guard AXUIElementPerformAction(control, kAXPressAction as CFString) == .success else { throw ProbeError.resultNotObserved }
}

@MainActor
func observeHint(_ expected: MediaHint, target: FrontMedia, watch: InputWatch) throws -> FrontMedia {
    let deadline = Date().addingTimeInterval(2)
    repeat {
        if let denial = watch.denied { emit(["resumeCancelled": denial]); throw ProbeError.resultNotObserved }
        let fresh = try frontMedia()
        guard target.matches(fresh) else { throw ProbeError.targetChanged }
        if fresh.hint == expected { return fresh }
        RunLoop.current.run(until: Date().addingTimeInterval(0.1))
    } while Date() < deadline
    throw ProbeError.resultNotObserved
}

@MainActor
func runPilotCycle() throws {
    emit(["pilot": "WAITING", "setupSeconds": 3, "ordinaryProductAvailable": false])
    RunLoop.current.run(until: Date().addingTimeInterval(3))
    let initial = try frontMedia()
    emit(initial.report)
    guard initial.hint == .playing else {
        emit(["pilot": "NO_ACTION", "reason": initial.hint == .paused ? "INITIALLY_PAUSED" : "STATE_UNKNOWN", "playerCommands": 0])
        return
    }
    let watch = try InputWatch()
    defer { watch.close() }
    try pressMedia(initial, expected: .playing)
    let paused = try observeHint(.paused, target: initial, watch: watch)
    emit(["pilot": "PAUSE_OBSERVED", "playerCommands": 1, "returnAfterSeconds": 20])
    // shortcut: 上下文摘要不能证明应用内部的全部媒体换片；当前仅作为可见 Pilot，不授予正式产品资格。
    let deadline = Date().addingTimeInterval(20)
    var lastCheck = Date()
    while Date() < deadline {
        RunLoop.current.run(until: Date().addingTimeInterval(0.15))
        if Date().timeIntervalSince(lastCheck) > 1 { watch.revoke("OBSERVATION_INTERRUPTED") }
        lastCheck = Date()
        if let denial = watch.denied {
            emit(["pilot": "RESUME_CANCELLED", "reason": denial, "playerCommands": 1]); return
        }
        let fresh = try frontMedia()
        guard paused.matches(fresh), fresh.hint == .paused else {
            emit(["pilot": "RESUME_CANCELLED", "reason": "CONTEXT_OR_STATE_CHANGED", "playerCommands": 1]); return
        }
    }
    // 最后一次读状态与调用之间仍有平台竞态，实测范围不能推广为绝对保证。
    if let denial = watch.denied { emit(["pilot": "RESUME_CANCELLED", "reason": denial]); return }
    try pressMedia(paused, expected: .paused)
    _ = try observeHint(.playing, target: paused, watch: watch)
    emit(["pilot": "RESUME_OBSERVED", "playerCommands": 2, "ordinaryProductAvailable": false])
}

func genericSelfTest() {
    precondition(mediaHint(label: "Pause (k)") == .playing)
    precondition(mediaHint(label: "Play keyboard shortcut k") == .paused)
    precondition(mediaHint(label: "播放", checkValue: 0) == .paused)
    precondition(mediaHint(label: "播放", checkValue: 1) == .playing)
    precondition(mediaHint(label: "播放/暂停") == .unknown)
    for label in ["Autoplay is on", "Save to playlist", "LookPause", "播放下一个"] {
        precondition(mediaHint(label: label) == nil)
    }
}

@MainActor
func inputWatchTest() throws {
    let modifiers: CGEventFlags = [.maskShift, .maskControl, .maskAlternate, .maskCommand]
    guard CGPreflightPostEventAccess(), CGEventSource.flagsState(.combinedSessionState).intersection(modifiers).isEmpty else {
        throw ProbeError.resultNotObserved
    }
    let watch = try InputWatch()
    defer { watch.close() }
    // 固定 Shift 按下/松开验证系统事件链；不输入字符，不操作播放器。
    for flags: NSEvent.ModifierFlags in [.shift, []] {
        guard let event = NSEvent.keyEvent(with: .flagsChanged, location: .zero, modifierFlags: flags,
                                         timestamp: ProcessInfo.processInfo.systemUptime, windowNumber: 0,
                                         context: nil, characters: "", charactersIgnoringModifiers: "",
                                         isARepeat: false, keyCode: 56)?.cgEvent else { throw ProbeError.resultNotObserved }
        event.post(tap: .cgSessionEventTap)
    }
    RunLoop.current.run(until: Date().addingTimeInterval(0.5))
    guard watch.denied == "USER_INPUT" else { throw ProbeError.resultNotObserved }
    emit(["inputWatchTest": "PASS", "reason": watch.denied!, "playerCommands": 0])
}
