import Accessibility
import AppKit
import ApplicationServices
import CoreAudio
import CryptoKit
import Foundation
import OSLog

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
    case "play/pause", "播放/暂停", "播放／暂停":
        if let checkValue { return checkValue == 1 ? .playing : checkValue == 0 ? .paused : .unknown }
        return .unknown
    default: return nil
    }
}

@MainActor
struct FrontMedia {
    let pid: pid_t
    let bundle: String
    let window: AXUIElement
    let context: String
    var hint: MediaHint
    let control: AXUIElement?
    let outputActive: Bool?
    let controls: Int
    let evidence: String
    var seekControl: AXUIElement? = nil
    var progress: Double? = nil
    var timeline: MediaTimeline? = nil
    var timelineControl: AXUIElement? = nil

    var mediaScope: AXUIElement? = nil
    var documents: [MediaDocument] = []

    // shortcut: AX相同观察值不是播放项版本；取得稳定播放项ID后再扩大换片保证。
    func matches(_ other: FrontMedia) -> Bool {
        guard pid == other.pid && context == other.context && CFEqual(window, other.window) else { return false }
        guard sameMediaDocuments(documents, other.documents) else { return false }
        for (left, right) in [(control, other.control), (seekControl, other.seekControl), (mediaScope, other.mediaScope)] {
            switch (left, right) {
            case let (a?, b?): if !CFEqual(a, b) { return false }
            case (nil, nil): break
            default: return false
            }
        }
        switch (timeline, other.timeline) {
        case let (a?, b?): return a.sameDuration(b)
        case (nil, nil): return true
        default: return false
        }
    }

    func canResume(_ fresh: FrontMedia) -> Bool {
        guard matches(fresh), hint == .paused, fresh.hint == .paused,
              let timeline, let latest = fresh.timeline else { return false }
        return timeline.holdsPause(latest)
    }

    var pauseStillHeld: Bool {
        guard targetSourcesValid, controlHint(control) == .paused, let timeline, let source = timelineControl,
              let current = readTimeline(source) else { return false }
        return timeline.holdsPause(current)
    }

    var targetSourcesValid: Bool {
        guard let control else { return false }
        for source in [control, seekControl, timelineControl].compactMap({ $0 }) {
            guard let current = mediaDocuments(source), sameMediaDocuments(documents, current),
                  mediaScope.map({ insideMediaScope(source, scope: $0) }) != false else { return false }
        }
        return true
    }

    var report: [String: Any] {
        ["frontmostBundle": bundle, "contextHash": context, "hint": hint.rawValue,
         "controls": controls, "timelineAvailable": timeline != nil, "durationSeconds": timeline.map { $0.duration as Any } ?? NSNull(), "progressSeconds": progress.map { $0 as Any } ?? NSNull(), "audioOutputActive": outputActive.map { $0 as Any } ?? NSNull(),
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
func frontMedia(includeAudio: Bool = true) throws -> FrontMedia {
    guard AXIsProcessTrusted(), let front = NSWorkspace.shared.frontmostApplication,
          let bundle = front.bundleIdentifier else { throw ProbeError.noFrontmostTarget }
    let app = AXUIElementCreateApplication(front.processIdentifier)
    guard let window = axElement(app, kAXFocusedWindowAttribute) else { throw ProbeError.noFrontmostTarget }
    let document = axAddress(window, kAXDocumentAttribute)
    let title = axAttribute(window, kAXTitleAttribute) as? String ?? ""
    // 浏览器标题会随播放音频、通知数变化；归一化这些装饰后与文档标识共同关联目标。
    let context = foregroundContext(document: document, title: title)
    let fileMedia = URL(string: document).map { $0.isFileURL && ["mp4", "mov", "m4v", "webm", "mkv"].contains($0.pathExtension.lowercased()) } ?? false
    var stack: [(AXUIElement, Int?, [Int], Int?)] = [(window, nil, [], nil)]
    var nodes: [MediaAXNode] = []
    var budget = 6000
    while let (element, parent, inheritedDocuments, inheritedNamed) = stack.popLast(), budget > 0 {
        budget -= 1
        let role = axAttribute(element, kAXRoleAttribute) as? String ?? ""
        let index = nodes.count
        let documents = role == "AXWebArea" ? [index] + inheritedDocuments : inheritedDocuments
        let semantics = [kAXDescriptionAttribute, kAXRoleDescriptionAttribute].compactMap { axAttribute(element, $0) as? String }.joined(separator: " ").lowercased()
        let description = axAttribute(element, kAXDescriptionAttribute) as? String ?? ""
        let label = description.isEmpty ? (axAttribute(element, kAXTitleAttribute) as? String ?? "") : description
        let named = (role == kAXWindowRole && fileMedia) ||
            (role == kAXGroupRole && semantics.range(of: "video|movie|media player|播放器|视频|影片", options: .regularExpression) != nil)
        // 文档边界停止继承外层名称资格；同一播放器嵌套框架须有自己的控件证据。
        let namedScope = named ? index : (role == "AXWebArea" ? nil : inheritedNamed)
        let member = mediaMember(role: role, label: label, semantics: semantics)
        nodes.append(MediaAXNode(element: element, parent: parent, role: role, semantics: semantics,
                                 member: member, documents: documents, namedScope: namedScope,
                                 visible: mediaVisible(element, allowCollapsedVolume: member == .volume)))
        stack.append(contentsOf: (axAttribute(element, kAXChildrenAttribute) as? [AXUIElement] ?? []).reversed().map { ($0, index, documents, namedScope) })
    }
    let roots = nodes.indices.filter { nodes[$0].role == "AXWebArea" && nodes[$0].documents.count == 1 && nodes[$0].visible }
    let focusedRoot = axElement(app, kAXFocusedUIElementAttribute).flatMap(mediaDocuments)?.last?.element
    let activeRoot = activeMediaDocument(roots: roots.map { ($0, axAddress(nodes[$0].element, "AXURL")) },
                                        windowDocument: document,
                                        focusedRoot: roots.first { index in focusedRoot.map { CFEqual($0, nodes[index].element) } ?? false })
    var candidates: [(Int, Int)] = [] // 播放控件、局部 scope。
    if budget > 0 {
        for index in nodes.indices where nodes[index].member == .playback && nodes[index].visible {
            guard (roots.isEmpty && nodes[index].documents.isEmpty) ||
                    (!nodes[index].documents.isEmpty && nodes[index].documents.last == activeRoot) else { continue }
            if let scope = nodes[index].namedScope { candidates.append((index, scope)); continue }
            var parent = nodes[index].parent
            for _ in 0..<8 {
                guard let scope = parent, nodes[scope].documents == nodes[index].documents else { break }
                if closedMediaScope(scope, nodes: nodes) { candidates.append((index, scope)); break }
                if nodes[scope].role == "AXWebArea" || nodes[scope].role == kAXWindowRole { break }
                parent = nodes[scope].parent
            }
        }
    }
    let unique = candidates.count == 1
    let selected = unique ? candidates[0] : nil
    let local = selected.map { choice in mediaDescendants(choice.1, nodes: nodes).filter {
        nodes[$0].documents == nodes[choice.0].documents
    }} ?? []
    let seekControls = local.filter { nodes[$0].member == .seek && nodes[$0].visible }.map { nodes[$0].element }
    // 优先唯一 seek 的完整时间。文字备选仍须同组同文档；重建文字节点不改变媒体身份。
    let positions = local.filter { [kAXGroupRole, kAXButtonRole, kAXStaticTextRole].contains(nodes[$0].role) }
        .filter { readTimeline(nodes[$0].element) != nil }
    let independentPositions = positions.filter { child in !positions.contains { parent in
        parent != child && insideMediaScope(nodes[child].element, scope: nodes[parent].element)
    }}
    let source = seekControls.count == 1 && readTimeline(seekControls[0]) != nil ? seekControls[0] :
        (independentPositions.count == 1 ? nodes[independentPositions[0]].element : nil)
    let sources = (source.map { [$0] } ?? []) + independentPositions.map { nodes[$0].element }
    let timeline = independentPositions.count <= 1 ? coherentMediaTimeline(sources.compactMap(readTimeline)) : nil
    let audio = includeAudio ? audioOutputActive(bundle: bundle) : nil
    let control = selected.map { nodes[$0.0].element }
    var media = FrontMedia(pid: front.processIdentifier, bundle: bundle, window: window, context: context,
                           hint: control.map(controlHint) ?? .unknown, control: control,
                           outputActive: audio, controls: candidates.count,
                           evidence: unique ? "AX_MEDIA_STATE" : "UNKNOWN",
                           seekControl: seekControls.count == 1 ? seekControls[0] : nil,
                           progress: timeline?.beforeEnd == true ? timeline?.position : nil,
                           timeline: timeline, timelineControl: source,
                           mediaScope: selected.map { nodes[$0.1].element },
                           documents: control.flatMap(mediaDocuments) ?? [])
    let canInferProgress = unique && axAttribute(control!, kAXRoleAttribute) as? String == kAXButtonRole && controlClassHint(control!) == nil
    let now = ProcessInfo.processInfo.systemUptime
    var playingAt: Double?
    if canInferProgress && media.hint == .unknown, let progress = media.progress, let previous = lastProgressSample,
       previous.media.matches(media), let previousPosition = previous.media.progress, now - previous.time <= 2,
       progress > previousPosition, progress - previousPosition <= 8 { media.hint = .playing; playingAt = now }
    if canInferProgress && media.hint == .unknown, let previous = lastProgressSample,
       previous.media.matches(media), media.progress != nil, media.progress == previous.media.progress,
       let confirmedAt = previous.playingAt, now - confirmedAt < 0.3 {
        media.hint = .playing; playingAt = confirmedAt
    }
    if (source != nil && timeline == nil) || (timeline.map { !$0.beforeEnd } ?? false) { media.hint = .unknown }
    lastProgressSample = (media, now, playingAt)
    return media
}

func mediaContextHash(_ value: String) -> String {
    SHA256.hash(data: Data(value.utf8)).map { String(format: "%02x", $0) }.joined()
}

@MainActor
func stillFrontmost(_ target: FrontMedia) -> Bool {
    guard NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid else { return false }
    let app = AXUIElementCreateApplication(target.pid)
    guard let window = axElement(app, kAXFocusedWindowAttribute), CFEqual(window, target.window) else { return false }
    let document = axAddress(window, kAXDocumentAttribute)
    let title = axAttribute(window, kAXTitleAttribute) as? String ?? ""
    return foregroundContext(document: document, title: title) == target.context
}

func controlHint(_ control: AXUIElement?) -> MediaHint {
    guard let control, (axAttribute(control, kAXEnabledAttribute) as? NSNumber)?.boolValue != false else { return .unknown }
    let description = axAttribute(control, kAXDescriptionAttribute) as? String ?? ""
    let label = description.isEmpty ? (axAttribute(control, kAXTitleAttribute) as? String ?? "") : description
    let checked = (axAttribute(control, kAXRoleAttribute) as? String) == kAXCheckBoxRole ? (axAttribute(control, kAXValueAttribute) as? NSNumber)?.intValue : nil
    return resolvedMediaHint(label: label, checkValue: checked, classes: controlClasses(control))
}

func resolvedMediaHint(label: String, checkValue: Int?, classes: [String]) -> MediaHint {
    let explicit = mediaHint(label: label, checkValue: checkValue) ?? .unknown
    let inherited = mediaClassHint(classes)
    if inherited == .unknown { return .unknown }
    if explicit != .unknown, let inherited, inherited != explicit { return .unknown }
    return explicit != .unknown ? explicit : (inherited ?? .unknown)
}

func controlClassHint(_ control: AXUIElement) -> MediaHint? { mediaClassHint(controlClasses(control)) }

private func controlClasses(_ control: AXUIElement) -> [String] {
    var node: AXUIElement? = control
    var classes: [String] = []
    for _ in 0..<8 {
        guard let current = node else { break }
        let role = axAttribute(current, kAXRoleAttribute) as? String ?? ""
        if role == kAXWindowRole || role == "AXWebArea" { break }
        classes += axAttribute(current, "AXDOMClassList") as? [String] ?? []
        node = axElement(current, kAXParentAttribute)
    }
    return classes
}

@MainActor private var lastProgressSample: (media: FrontMedia, time: Double, playingAt: Double?)?

func readTimeline(_ element: AXUIElement) -> MediaTimeline? {
    let role = axAttribute(element, kAXRoleAttribute) as? String ?? ""
    if role == kAXButtonRole {
        let descriptions = [kAXDescriptionAttribute, kAXTitleAttribute].compactMap { axAttribute(element, $0) as? String }.compactMap(MediaTimeline.parse)
        if !descriptions.isEmpty { return coherentMediaTimeline(descriptions) }
    }
    if role == kAXSliderRole {
        var descriptions: [MediaTimeline] = []
        for attribute in [kAXValueAttribute, kAXValueDescriptionAttribute] {
            if let value = axAttribute(element, attribute) as? String, let timeline = MediaTimeline.parse(value) { descriptions.append(timeline) }
        }
        if let elapsed = axAttribute(element, kAXValueDescriptionAttribute) as? String,
           let total = customMediaDescription(element),
           let timeline = MediaTimeline.described(elapsed: elapsed, total: total) { descriptions.append(timeline) }
        if !descriptions.isEmpty { return coherentMediaTimeline(descriptions) }
        // 网页百分比数值不当作秒；只接受明确秒单位的原生范围。
        if nativeTimeline(element), let current = numericTimelineSeconds(element),
           let end = (axAttribute(element, kAXMaxValueAttribute) as? NSNumber)?.doubleValue, end.isFinite, end > 0 {
            return MediaTimeline(position: current, duration: end, rounded: false)
        }
        return nil
    }
    if role == kAXGroupRole || role == kAXButtonRole {
        let children = axAttribute(element, kAXChildrenAttribute) as? [AXUIElement] ?? []
        guard (2...8).contains(children.count), children.allSatisfy({ axAttribute($0, kAXRoleAttribute) as? String == kAXStaticTextRole }) else { return nil }
        return MediaTimeline.parse(children.compactMap { axAttribute($0, kAXValueAttribute) as? String }.joined())
    }
    return (axAttribute(element, kAXValueAttribute) as? String).flatMap(MediaTimeline.parse)
}

func foregroundContext(document: String, title: String) -> String {
    let stableTitle = title.replacingOccurrences(of: #"^\(\d+\)\s*"#, with: "", options: .regularExpression)
    let mediaTitle = stableTitle.replacingOccurrences(of: #" - (?:正在播放音频|播放音频|Playing audio|Audio playing)"#, with: "", options: .regularExpression)
    return mediaContextHash(document + "\n" + mediaTitle)
}

func mediaProgress(_ value: String) -> Double? {
    guard let timeline = MediaTimeline.parse(value), timeline.beforeEnd else { return nil }
    return timeline.position
}

func mediaClassHint(_ classes: [String]) -> MediaHint? {
    func has(_ state: String) -> Bool {
        classes.contains { $0.range(of: "(^|[-_])state[-_]" + state + "$|^" + state + "$|--" + state + "$", options: .regularExpression) != nil }
    }
    if has("ended") || has("loading") || (has("paused") && has("playing")) { return .unknown }
    if has("paused") { return .paused }
    if has("playing") { return .playing }
    return nil
}

// AXPress 自身不产生键鼠事件；观察到任何真实按键/点击/滚轮均保守撤销本轮。
enum MediaKey: CGKeyCode { case space = 49, left = 123 }

func isOwnMediaKey(_ event: CGEvent, type: CGEventType, tag: Int64) -> Bool {
    type == .keyDown && [MediaKey.space, .left].contains(where: { Int64($0.rawValue) == event.getIntegerValueField(.keyboardEventKeycode) }) &&
    event.getIntegerValueField(.eventSourceUserData) == tag &&
    event.getIntegerValueField(.eventSourceUnixProcessID) == Int64(getpid())
}

final class InputWatch: @unchecked Sendable {
    private let lock = NSLock()
    private var reason: String?
    let ownEventTag = Int64.random(in: 1...Int64.max)
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    var denied: String? { lock.withLock { reason } }
    var healthy: Bool { denied == nil && tap.map { CGEvent.tapIsEnabled(tap: $0) } == true && CGPreflightListenEventAccess() }
    func revoke(_ value: String) { lock.withLock { if reason == nil { reason = value } } }

    init() throws {
        let events: [CGEventType] = [.keyDown, .flagsChanged, .leftMouseDown, .rightMouseDown, .otherMouseDown, .scrollWheel]
        let mask = events.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly,
                               eventsOfInterest: mask, callback: { _, type, event, info in
            if let info {
                let watch = Unmanaged<InputWatch>.fromOpaque(info).takeUnretainedValue()
                if isOwnMediaKey(event, type: type, tag: watch.ownEventTag) { return Unmanaged.passUnretained(event) }
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
func pressMedia(_ target: FrontMedia, expected: MediaHint, watch: InputWatch? = nil) throws {
    let fresh = try frontMedia(includeAudio: false)
    guard target.matches(fresh) else { throw ProbeError.targetChanged }
    guard fresh.hint == expected else {
        Logger(subsystem: "com.lookpause.pilot", category: "media").notice("执行前判态不一致 expected=\(expected.rawValue, privacy: .public) actual=\(fresh.hint.rawValue, privacy: .public)")
        throw ProbeError.resultNotObserved
    }
    guard expected != .paused || target.canResume(fresh) else { throw ProbeError.targetChanged }
    guard let control = fresh.control else { throw ProbeError.resultNotObserved }
    guard fresh.targetSourcesValid, stillFrontmost(fresh), watch?.denied == nil else { throw ProbeError.targetChanged }
    let label = axAttribute(control, kAXDescriptionAttribute) as? String ?? ""
    if mediaHint(label: label) == .unknown && axAttribute(control, kAXRoleAttribute) as? String != kAXCheckBoxRole {
        guard let watch, try sendMediaKey(.space, target: fresh, watch: watch) else { throw ProbeError.resultNotObserved }
    } else if AXUIElementPerformAction(control, kAXPressAction as CFString) != .success { throw ProbeError.resultNotObserved }
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
    try pressMedia(initial, expected: .playing, watch: watch)
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
    try pressMedia(paused, expected: .paused, watch: watch)
    _ = try observeHint(.playing, target: paused, watch: watch)
    emit(["pilot": "RESUME_OBSERVED", "playerCommands": 2, "ordinaryProductAvailable": false])
}

func genericSelfTest() {
    precondition(mediaHint(label: "Pause (k)") == .playing)
    precondition(mediaHint(label: "Play keyboard shortcut k") == .paused)
    precondition(mediaHint(label: "播放", checkValue: 0) == .paused)
    precondition(mediaHint(label: "播放", checkValue: 1) == .playing)
    precondition(mediaHint(label: "播放/暂停") == .unknown)
    precondition(mediaHint(label: "播放/暂停", checkValue: 1) == .playing)
    precondition(mediaHint(label: "播放/暂停", checkValue: 0) == .paused)
    precondition(mediaHint(label: "播放/暂停", checkValue: 2) == .unknown)
    precondition(mediaClassHint(["player-state-paused"]) == .paused)
    precondition(mediaClassHint(["player-state-paused", "player-state-ended"]) == .unknown)
    precondition(mediaClassHint(["ctrl-play"]) == nil)
    precondition(mediaProgress("00:23 / 14:50") == 23)
    precondition(mediaProgress("14:50 / 14:50") == nil)
    precondition(mediaProgress("not a timeline") == nil)
    precondition(foregroundContext(document: "a", title: "Film - 正在播放音频 - Browser") == foregroundContext(document: "a", title: "Film - Browser"))
    precondition(foregroundContext(document: "a", title: "(3) Film") == foregroundContext(document: "a", title: "(4) Film"))
    precondition(foregroundContext(document: "a", title: "Film") != foregroundContext(document: "a", title: "Other"))
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

// 系统提供的 AXCustomContent 为安全归档；不启用任意 NSObject 解码。
func customMediaDescription(_ element: AXUIElement) -> String? {
    guard let data = axAttribute(element, "AXCustomContent") as? Data, data.count <= 65536,
          let decoder = try? NSKeyedUnarchiver(forReadingFrom: data) else { return nil }
    decoder.requiresSecureCoding = true
    decoder.decodingFailurePolicy = .setErrorAndReturn
    defer { decoder.finishDecoding() }
    guard let contents = decoder.decodeObject(of: [NSArray.self, AXCustomContent.self], forKey: NSKeyedArchiveRootObjectKey) as? [AXCustomContent],
          decoder.error == nil, contents.count <= 16 else { return nil }
    let values = contents.map(\.value).filter { $0.count <= 512 && MediaTimeline.totalDescription($0) != nil }
    return values.count == 1 ? values[0] : nil
}
