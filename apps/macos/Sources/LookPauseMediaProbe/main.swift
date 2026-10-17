import AppKit
import ApplicationServices
import Carbon
import CryptoKit
import Foundation

// 仅供已授权的本机播放器资格/Pilot 测试；没有手机入口或网络接口。
enum ProbeError: Error {
    case invalidCommand
    case noFrontmostTarget
    case targetChanged
    case malformedReply
    case appleEvent(Int)
    case resultNotObserved
}

struct Snapshot: Codable, Equatable {
    let windowID: Int
    let mediaFingerprint: String
    let playing: Bool
    let currentTime: Double
    let duration: Double
    let rate: Double

    func matches(_ other: Snapshot) -> Bool {
        windowID == other.windowID && mediaFingerprint == other.mediaFingerprint
    }
}

// 原始文件标识仅在进程内使用，不进入 JSON 证据或日志。
var currentFileIdentity = ""

func emit(_ value: [String: Any]) {
    if let data = try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys]),
       let text = String(data: data, encoding: .utf8) {
        print(text)
        fflush(nil)
    }
}

func jsonSnapshot(_ value: Snapshot) throws -> [String: Any] {
    try JSONSerialization.jsonObject(with: JSONEncoder().encode(value)) as! [String: Any]
}

@MainActor
func requireQuickTimeFrontmost() throws {
    guard NSWorkspace.shared.frontmostApplication?.bundleIdentifier == "com.apple.QuickTimePlayerX" else {
        throw ProbeError.noFrontmostTarget
    }
}

func execute(_ source: String, arguments: NSAppleEventDescriptor? = nil) throws -> NSAppleEventDescriptor {
    var error: NSDictionary?
    guard let script = NSAppleScript(source: source) else { throw ProbeError.malformedReply }
    let result: NSAppleEventDescriptor
    if let arguments {
        let event = NSAppleEventDescriptor(eventClass: AEEventClass(kASAppleScriptSuite),
                                          eventID: AEEventID(kASSubroutineEvent), targetDescriptor: nil,
                                          returnID: AEReturnID(kAutoGenerateReturnID),
                                          transactionID: AETransactionID(kAnyTransactionID))
        event.setParam(NSAppleEventDescriptor(string: "applyrate"), forKeyword: AEKeyword(keyASSubroutineName))
        event.setParam(arguments, forKeyword: AEKeyword(keyDirectObject))
        result = script.executeAppleEvent(event, error: &error)
    } else {
        result = script.executeAndReturnError(&error)
    }
    if let error {
        throw ProbeError.appleEvent((error[NSAppleScript.errorNumber] as? Int) ?? 0)
    }
    return result
}

@MainActor
func snapshot() throws -> Snapshot {
    try requireQuickTimeFrontmost()
    let reply = try execute("""
    tell application id "com.apple.QuickTimePlayerX"
        if not frontmost then error "TARGET_NOT_FRONTMOST" number -2700
        if (count of documents) is 0 then error "NO_MEDIA" number -2701
        set w to front window
        set d to document of w
        return {id of w, (file of d as text), playing of d, current time of d, duration of d, rate of d}
    end tell
    """)
    guard reply.numberOfItems == 6,
          let id = reply.atIndex(1), let file = reply.atIndex(2)?.stringValue,
          let playing = reply.atIndex(3), let time = reply.atIndex(4),
          let duration = reply.atIndex(5), let rate = reply.atIndex(6) else {
        throw ProbeError.malformedReply
    }
    let value = Snapshot(windowID: Int(id.int32Value),
                         mediaFingerprint: SHA256.hash(data: Data(file.utf8)).map { String(format: "%02x", $0) }.joined(),
                         playing: playing.booleanValue, currentTime: time.doubleValue,
                         duration: duration.doubleValue, rate: rate.doubleValue)
    guard value.currentTime.isFinite, value.duration.isFinite, value.rate.isFinite else {
        throw ProbeError.malformedReply
    }
    currentFileIdentity = file
    return value
}

@MainActor
func setRate(_ rate: Int, target: Snapshot) throws -> Snapshot {
    // 再读目标以防读取和操作之间切换；脚本执行内也核对窗口和播放项。
    let latest = try snapshot()
    guard target.matches(latest) else { throw ProbeError.targetChanged }
    try requireQuickTimeFrontmost()
    let arguments = NSAppleEventDescriptor.list()
    arguments.insert(NSAppleEventDescriptor(int32: Int32(target.windowID)), at: 1)
    arguments.insert(NSAppleEventDescriptor(string: currentFileIdentity), at: 2)
    arguments.insert(NSAppleEventDescriptor(int32: Int32(rate)), at: 3)
    _ = try execute("""
    on applyrate(expectedWindow, expectedFile, requestedRate)
        tell application id "com.apple.QuickTimePlayerX"
            if not frontmost then error "TARGET_NOT_FRONTMOST" number -2700
            if id of front window is not expectedWindow then error "TARGET_CHANGED" number -2702
            set d to document of front window
            if (file of d as text) is not expectedFile then error "TARGET_CHANGED" number -2702
            set rate of d to requestedRate
        end tell
    end applyrate
    """, arguments: arguments)
    let observed = try snapshot()
    guard target.matches(observed) else { throw ProbeError.targetChanged }
    guard observed.playing == (rate != 0) else { throw ProbeError.resultNotObserved }
    return observed
}

func selfTest() {
    wireSelfTest()
    genericSelfTest()
    rewindSelfTest()
    let original = Snapshot(windowID: 1, mediaFingerprint: "a", playing: true, currentTime: 3, duration: 50, rate: 1)
    let same = Snapshot(windowID: 1, mediaFingerprint: "a", playing: false, currentTime: 4, duration: 50, rate: 0)
    let otherWindow = Snapshot(windowID: 2, mediaFingerprint: "a", playing: false, currentTime: 4, duration: 50, rate: 0)
    let otherMedia = Snapshot(windowID: 1, mediaFingerprint: "b", playing: false, currentTime: 4, duration: 50, rate: 0)
    precondition(original.matches(same))
    precondition(!original.matches(otherWindow))
    precondition(!original.matches(otherMedia))
    precondition(playbackState(label: "Pause (k)") == "PLAYING")
    precondition(playbackState(label: "Play (k)") == "PAUSED")
    precondition(playbackState(label: "Play keyboard shortcut k") == "PAUSED")
    precondition(playbackState(label: "Pause keyboard shortcut k") == "PLAYING")
    precondition(playbackState(label: "播放/暂停") == "UNKNOWN")
    for label in ["Autoplay is on", "Save to playlist", "LookPause", "播放下一个"] {
        precondition(playbackState(label: label) == nil)
    }
    precondition(resumeDenial(state: "PAUSED", owned: false, sameTarget: true, qualified: true) == "NO_PAUSE_OWNERSHIP")
    precondition(resumeDenial(state: "PAUSED", owned: true, sameTarget: false, qualified: true) == "TARGET_CHANGED")
    precondition(resumeDenial(state: "PAUSED", owned: true, sameTarget: true, qualified: false) == "ATTRIBUTION_UNQUALIFIED")
    precondition(resumeDenial(state: "PAUSED", owned: true, sameTarget: true, qualified: true) == nil)
    emit(["selfTest": "PASS", "automaticResumeEnabled": false])
}

func playbackState(label: String) -> String? {
    switch label {
    case "Pause (k)", "Pause keyboard shortcut k", "暂停 (k)", "暫停 (k)": return "PLAYING"
    case "Play (k)", "Play keyboard shortcut k", "播放 (k)": return "PAUSED"
    case "播放/暂停": return "UNKNOWN"
    default: return nil
    }
}

func resumeDenial(state: String, owned: Bool, sameTarget: Bool, qualified: Bool) -> String? {
    guard owned else { return "NO_PAUSE_OWNERSHIP" }
    guard sameTarget else { return "TARGET_CHANGED" }
    guard qualified else { return "ATTRIBUTION_UNQUALIFIED" }
    guard state == "PAUSED" else { return "PLAYBACK_STATE_NOT_PAUSED" }
    return nil
}

func axAttribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
    return value
}

func inspectChromeAccessibility(checkInitialHold: Bool = false) {
    guard AXIsProcessTrusted() else {
        emit(["error": "ACCESSIBILITY_PERMISSION_REQUIRED", "automaticResumeEnabled": false])
        return
    }
    guard let chrome = NSRunningApplication.runningApplications(withBundleIdentifier: "com.google.Chrome").first else {
        emit(["error": "CHROME_NOT_RUNNING"])
        return
    }
    let app = AXUIElementCreateApplication(chrome.processIdentifier)
    if checkInitialHold && NSWorkspace.shared.frontmostApplication?.processIdentifier != chrome.processIdentifier {
        emit(["error": "TARGET_NOT_FRONTMOST", "playerCommands": 0])
        return
    }
    var windowsValue: CFTypeRef?
    let windowsStatus = AXUIElementCopyAttributeValue(app, kAXWindowsAttribute as CFString, &windowsValue)
    guard windowsStatus == .success, let windows = windowsValue as? [AXUIElement] else {
        emit(["error": "CHROME_AX_WINDOWS_UNAVAILABLE", "axStatus": windowsStatus.rawValue])
        return
    }
    let inspectedWindows: [AXUIElement]
    if checkInitialHold {
        guard let focused = axAttribute(app, kAXFocusedWindowAttribute),
              CFGetTypeID(focused) == AXUIElementGetTypeID() else {
            emit(["error": "FOCUSED_WINDOW_UNAVAILABLE", "playerCommands": 0])
            return
        }
        inspectedWindows = windows.filter { CFEqual($0, focused) }
        guard inspectedWindows.count == 1 else {
            emit(["error": "FOCUSED_WINDOW_UNAVAILABLE", "playerCommands": 0])
            return
        }
    } else {
        inspectedWindows = windows
    }
    var reports: [[String: Any]] = []
    for window in inspectedWindows {
        let document = axAttribute(window, kAXDocumentAttribute)
        let address = (document as? String) ?? (document as? URL)?.absoluteString ?? ""
        guard let host = URL(string: address)?.host,
              ["www.bilibili.com", "www.youtube.com"].contains(host) else { continue }
        var stack = [(window, false)]
        var remaining = 6000
        var controls: [[String: String]] = []
        while let (element, inheritedPageScope) = stack.popLast(), remaining > 0 {
            remaining -= 1
            let role = axAttribute(element, kAXRoleAttribute) as? String ?? ""
            var pageScope = inheritedPageScope
            if role == "AXWebArea" {
                let rawURL = axAttribute(element, "AXURL")
                let webAddress = (rawURL as? URL)?.absoluteString ?? (rawURL as? String ?? "")
                pageScope = webAddress == address
            }
            if pageScope && role == kAXButtonRole {
                let label = axAttribute(element, kAXDescriptionAttribute) as? String ?? ""
                if let state = playbackState(label: label) {
                    controls.append(["role": role, "label": label, "state": state])
                }
            }
            stack.append(contentsOf: (axAttribute(element, kAXChildrenAttribute) as? [AXUIElement] ?? []).map { ($0, pageScope) })
        }
        let state = remaining > 0 && controls.count == 1 ? controls[0]["state"]! : "UNKNOWN"
        var report: [String: Any] = ["host": host, "mediaControls": controls, "state": state, "truncated": remaining == 0]
        if checkInitialHold {
            report["resumeDenied"] = resumeDenial(state: state, owned: false, sameTarget: true, qualified: false)
            report["playerCommands"] = 0
            report["result"] = state == "PAUSED" ? "HELD_UNOWNED_PAUSE" : "HELD_UNQUALIFIED"
        }
        reports.append(report)
    }
    emit(["scope": "read-only-accessibility-qualification", "windows": reports,
          "windowCount": windows.count,
          "focusedWindowOnly": checkInitialHold,
          "automaticResumeEnabled": false])
}

do {
    let args = Array(CommandLine.arguments.dropFirst())
    if args.isEmpty { runMenuApp(); exit(0) }
    guard args.count == 1 else { throw ProbeError.invalidCommand }
    switch args[0] {
    case "self-test": selfTest()
    case "receiver-test": try runReceiverTest()
    case "generic-state": emit(try frontMedia().report)
    case "input-watch-test": try inputWatchTest()
    case "pilot-cycle": try runPilotCycle()
    case "front":
        let front = NSWorkspace.shared.frontmostApplication
        emit(["frontmostBundle": front?.bundleIdentifier ?? "UNKNOWN", "automaticResumeEnabled": false])
    case "chrome-ax-state": inspectChromeAccessibility()
    case "chrome-manual-hold-check": inspectChromeAccessibility(checkInitialHold: true)
    case "manual-hold-check":
        let before = try snapshot()
        guard !before.playing else { throw ProbeError.resultNotObserved }
        // START 清空所有权；首次回看必须在任何播放器操作之前被拒绝。
        let denial = resumeDenial(state: "PAUSED", owned: false, sameTarget: true, qualified: false)
        precondition(denial == "NO_PAUSE_OWNERSHIP")
        RunLoop.current.run(until: Date().addingTimeInterval(0.5))
        let after = try snapshot()
        guard before.matches(after), !after.playing, before.currentTime == after.currentTime else {
            throw ProbeError.resultNotObserved
        }
        emit(["manualHoldCheck": "PASS", "resumeDenied": denial!, "playerCommands": 0,
              "before": try jsonSnapshot(before), "after": try jsonSnapshot(after),
              "automaticResumeEnabled": false, "scope": "initial-manual-pause-only"])
    case "quicktime-state", "pause-test", "play-test":
        let before = try snapshot()
        let after: Snapshot
        if args[0] == "pause-test" { after = try setRate(0, target: before) }
        else if args[0] == "play-test" { after = try setRate(1, target: before) }
        else { after = before }
        emit(["command": args[0], "before": try jsonSnapshot(before), "after": try jsonSnapshot(after),
              "automaticResumeEnabled": false, "scope": "technical-probe-only"])
    default: throw ProbeError.invalidCommand
    }
} catch {
    let code: String
    switch error {
    case ProbeError.invalidCommand: code = "INVALID_COMMAND"
    case ProbeError.noFrontmostTarget: code = "TARGET_NOT_FRONTMOST"
    case ProbeError.targetChanged: code = "TARGET_CHANGED"
    case ProbeError.malformedReply: code = "PLAYBACK_STATE_UNKNOWN"
    case ProbeError.resultNotObserved: code = "COMMAND_RESULT_NOT_OBSERVED"
    case ProbeError.appleEvent(let number): code = "APPLE_EVENT_\(number)"
    default: code = "PROBE_FAILED"
    }
    emit(["error": code, "automaticResumeEnabled": false])
    exit(1)
}
