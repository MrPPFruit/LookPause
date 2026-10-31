import AppKit
import ApplicationServices

// 仅用户明确选择的兼容会话使用；与严格暂停归属完全隔离。
@MainActor
final class ShortcutSession {
    private struct Boundary {
        let pid: pid_t
        let window: AXUIElement
        let context: String
        let chain: [AXUIElement]
        let documents: [MediaDocument]
        let observedAt: Double
        func matches(_ other: Boundary) -> Bool {
            pid == other.pid && CFEqual(window, other.window) && context == other.context &&
            chain.count == other.chain.count && zip(chain, other.chain).allSatisfy { CFEqual($0, $1) } &&
            sameMediaDocuments(documents, other.documents)
        }
    }
    private var armed = false
    private var pending = false
    private var target: Boundary?
    private var watch: InputWatch?
    private var timer: Timer?
    private var lastObservation = 0.0
    private(set) var isWatching = false
    private(set) var lastIntent: WatchingIntent = .unknown
    private(set) var status = "尚未开始观影" { didSet { onStatus?(status) } }
    var onStatus: ((String) -> Void)?
    private var now: Double { ProcessInfo.processInfo.systemUptime }

    func start() {
        stop()
        isWatching = true; armed = true
        status = "快捷键兼容：请先播放并聚焦播放器，效果不验证"
    }
    func stop(_ reason: String = "已停止观影") {
        release(); isWatching = false; lastIntent = .unknown; status = reason
    }
    private func release() {
        armed = false; pending = false; target = nil
        timer?.invalidate(); timer = nil; watch?.close(); watch = nil
    }
    private func disarm() {
        release()
        status = "快捷键兼容已解除，请确认播放与焦点后重新开始观影"
    }
    func receive(_ intent: WatchingIntent) {
        guard isWatching else { return }
        lastIntent = intent
        guard armed else { return }
        do {
            if target != nil { try check() }
            switch intent {
            case .unknown: return
            case .away, .noFace:
                guard !pending else { return }
                if target == nil {
                    watch = try InputWatch()
                    target = try observe()
                    lastObservation = now
                    timer = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in
                        MainActor.assumeIsolated {
                            guard let self else { return }
                            do { try self.check() } catch { self.disarm() }
                        }
                    }
                }
                pending = true // 提交前消费，失败也不补发。
                try sendSpace()
                status = "快捷键兼容：离开空格已提交，效果未确认"
            case .attending:
                guard pending else { return }
                pending = false
                try sendSpace()
                status = "快捷键兼容：回看空格已提交，效果未确认；不回退"
            }
        } catch { disarm() }
    }
    private func check() throws {
        guard let target, now >= lastObservation, now - lastObservation <= 2 else { throw ProbeError.targetChanged }
        let fresh = try observe()
        guard target.matches(fresh), now - lastObservation <= 2 else { throw ProbeError.targetChanged }
        self.target = fresh; lastObservation = now
    }
    private func observe() throws -> Boundary {
        let started = now
        guard watch?.healthy == true, AXIsProcessTrusted(), noModifiers,
              let front = NSWorkspace.shared.frontmostApplication else { throw ProbeError.targetChanged }
        let app = AXUIElementCreateApplication(front.processIdentifier)
        guard let window = axElement(app, kAXFocusedWindowAttribute),
              let focus = axElement(app, kAXFocusedUIElementAttribute),
              mediaVisible(focus), !editableFocus(focus), let documents = mediaDocuments(focus) else { throw ProbeError.targetChanged }
        let document = axAddress(window, kAXDocumentAttribute)
        guard !documents.isEmpty || !document.isEmpty else { throw ProbeError.targetChanged }
        var chain: [AXUIElement] = []
        var node: AXUIElement? = focus
        for _ in 0..<64 {
            guard let current = node, now - started <= 1.5 else { throw ProbeError.targetChanged }
            // 自绘可编辑区域也不允许，仅检查文本值可写，不把slider数值当编辑框。
            var writable = DarwinBoolean(false)
            if axAttribute(current, kAXValueAttribute) is String,
               AXUIElementIsAttributeSettable(current, kAXValueAttribute as CFString, &writable) == .success,
               writable.boolValue { throw ProbeError.targetChanged }
            chain.append(current)
            if CFEqual(current, window) { break }
            node = axElement(current, kAXParentAttribute)
        }
        guard let last = chain.last, CFEqual(last, window),
              let finalFocus = axElement(app, kAXFocusedUIElementAttribute), CFEqual(focus, finalFocus),
              let finalWindow = axElement(app, kAXFocusedWindowAttribute), CFEqual(window, finalWindow),
              NSWorkspace.shared.frontmostApplication?.processIdentifier == front.processIdentifier,
              watch?.healthy == true, noModifiers, now - started <= 1.5 else { throw ProbeError.targetChanged }
        return Boundary(pid: front.processIdentifier, window: window,
                        context: foregroundContext(document: document, title: axAttribute(window, kAXTitleAttribute) as? String ?? ""),
                        chain: chain, documents: documents, observedAt: started)
    }
    private var noModifiers: Bool {
        CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand]).isEmpty
    }
    private func sendSpace() throws {
        guard let target, let watch, CGPreflightPostEventAccess() else { throw ProbeError.targetChanged }
        let fresh = try observe()
        let app = AXUIElementCreateApplication(target.pid)
        guard target.matches(fresh), now >= target.observedAt, now - target.observedAt <= 1.5,
              watch.healthy, noModifiers,
              let focus = axElement(app, kAXFocusedUIElementAttribute), let expected = target.chain.first, CFEqual(focus, expected),
              !editableFocus(focus), NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid,
              let window = axElement(app, kAXFocusedWindowAttribute), CFEqual(window, target.window),
              now - fresh.observedAt <= 1.5 else { throw ProbeError.targetChanged }
        let source = CGEventSource(stateID: .combinedSessionState)
        guard let down = CGEvent(keyboardEventSource: source, virtualKey: MediaKey.space.rawValue, keyDown: true),
              let up = CGEvent(keyboardEventSource: source, virtualKey: MediaKey.space.rawValue, keyDown: false) else { throw ProbeError.resultNotObserved }
        for event in [down, up] {
            event.flags = []
            event.setIntegerValueField(.eventSourceUserData, value: watch.ownEventTag)
            event.postToPid(target.pid)
        }
        // postToPid没有播放器执行回执；绝不把提交当成暂停或播放成功。
        guard watch.healthy else { throw ProbeError.resultNotObserved }
    }
}

// 仅在START切换owner，网络、状态图标和严格模式沿用现有入口。
@MainActor
final class MediaController {
    private let normal = MediaSession()
    private let shortcut = ShortcutSession()
    var nextShortcutMode = false
    private var shortcutMode = false
    var onStatus: ((String) -> Void)?
    var status: String { shortcutMode ? shortcut.status : normal.status }
    var isWatching: Bool { shortcutMode ? shortcut.isWatching : normal.isWatching }
    var lastIntent: WatchingIntent { shortcutMode ? shortcut.lastIntent : normal.lastIntent }
    init() {
        normal.onStatus = { [weak self] text in guard let self, !self.shortcutMode else { return }; self.onStatus?(text) }
        shortcut.onStatus = { [weak self] text in guard let self, self.shortcutMode else { return }; self.onStatus?(text) }
    }
    func start() {
        normal.stop(); shortcut.stop()
        shortcutMode = nextShortcutMode
        if shortcutMode { shortcut.start() } else { normal.start() }
    }
    func stop(_ reason: String = "已停止观影") { normal.stop(reason); shortcut.stop(reason) }
    func receive(_ intent: WatchingIntent) {
        if shortcutMode { shortcut.receive(intent) } else { normal.receive(intent) }
    }
}
