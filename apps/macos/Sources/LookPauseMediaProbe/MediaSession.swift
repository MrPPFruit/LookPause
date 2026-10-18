import Foundation
import OSLog
import ApplicationServices

private let mediaSessionLog = Logger(subsystem: "com.lookpause.pilot", category: "media")

enum WatchingIntent: String, Codable, CaseIterable, Sendable {
    case attending = "ATTENDING", away = "AWAY", noFace = "NO_FACE", unknown = "UNKNOWN"
}

// 所有播放器副作用与暂停资格仅在此 MainActor owner 内；网络不直接调用控件。
@MainActor
final class MediaSession {
    private enum Phase { case checking, pausing, paused, resuming, rewinding }
    private var phase: Phase?
    private var target: FrontMedia?
    private var watch: InputWatch?
    private var timer: Timer?
    private var deadline = 0.0
    private var lastTick = 0.0
    private var rewindFrom: Double?
    private var lastIntent: WatchingIntent = .unknown
    private(set) var isWatching = false
    private(set) var status = "尚未开始观影" { didSet {
        mediaSessionLog.notice("裁决：\(self.status, privacy: .public)")
        onStatus?(status)
    } }
    var onStatus: ((String) -> Void)?

    func start() {
        clearOwnership()
        lastIntent = .unknown
        isWatching = true
        status = "观影中，等待观看方向"
    }

    func stop(_ reason: String = "已停止观影") {
        isWatching = false
        lastIntent = .unknown
        clearOwnership()
        status = reason
    }

    func receive(_ intent: WatchingIntent) {
        guard isWatching else { return }
        let previous = lastIntent
        lastIntent = intent
        switch intent {
        case .unknown:
            cancel("识别状态未知，恢复资格已取消")
        case .away, .noFace:
            guard previous != .away && previous != .noFace, phase == nil else { return }
            pause()
        case .attending:
            if phase == .checking { cancel("看向屏幕，取消待定暂停") }
            else if phase == .paused { resume() }
            else if phase == nil { status = "看向屏幕" }
        }
    }

    private func pause() {
        mediaSessionLog.notice("暂停前权限：AX=\(AXIsProcessTrusted(), privacy: .public) 输入监听=\(CGPreflightListenEventAccess(), privacy: .public)")
        do {
            let current = try frontMedia(includeAudio: false)
            if current.hint == .unknown, let control = current.control, current.progress != nil,
               axAttribute(control, kAXRoleAttribute) as? String == kAXButtonRole, controlClassHint(control) == nil {
                target = current; watch = try InputWatch(); phase = .checking
                deadline = ProcessInfo.processInfo.systemUptime + 2
                status = "正在核验播放进度"
                beginTimer()
                return
            }
            guard current.hint == .playing else {
                status = current.hint == .paused ? "视频已暂停，交由你控制" : "当前媒体不可识别，未操作"
                return
            }
            let input = try InputWatch()
            target = current; watch = input; phase = .pausing
            deadline = ProcessInfo.processInfo.systemUptime + 2
            try pressMedia(current, expected: .playing, watch: input)
            status = "正在确认暂停"
            beginTimer()
        } catch {
            mediaSessionLog.error("暂停失败：\(String(describing: error), privacy: .public)")
            cancel("无法确认暂停，未取得恢复资格")
        }
    }

    private func resume() {
        guard let target, let watch, watch.denied == nil, stillFrontmost(target), target.pauseStillHeld else {
            cancel("前台或手动操作已改变，恢复取消"); return
        }
        do {
            phase = .resuming
            deadline = ProcessInfo.processInfo.systemUptime + 2
            try pressMedia(target, expected: .paused, watch: watch)
            status = "正在确认恢复"
        } catch { cancel("未能恢复，交由你控制") }
    }

    private func beginTimer() {
        timer?.invalidate()
        lastTick = ProcessInfo.processInfo.systemUptime
        timer = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
    }

    private func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        guard now - lastTick < 2 else { cancel("观察已中断，恢复取消"); return }
        lastTick = now
        guard isWatching, let target, let watch, let phase else { clearOwnership(); return }
        guard watch.denied == nil else { cancel("你已接管控制，恢复取消"); return }
        guard stillFrontmost(target) else { cancel("前台已改变，恢复取消"); return }
        switch phase {
        case .checking:
            guard let observed = try? frontMedia(includeAudio: false), target.matches(observed) else {
                cancel("媒体目标已改变，未操作"); return
            }
            if observed.hint == .playing {
                do {
                    self.target = observed
                    self.phase = .pausing
                    deadline = now + 2
                    try pressMedia(observed, expected: .playing, watch: watch)
                    status = "正在确认暂停"
                } catch { cancel("无法确认播放状态，未操作") }
            } else if observed.hint == .paused || now >= deadline { cancel("未确认正在播放，未操作") }
        case .pausing:
            if let observed = try? frontMedia(includeAudio: false), target.matches(observed), observed.hint == .paused {
                guard observed.timeline?.beforeEnd == true else {
                    cancel("无法确认暂停，未取得恢复资格"); return
                }
                self.target = observed
                self.phase = .paused
                status = "已自动暂停，等待回看"
                if lastIntent == .attending { resume() }
            } else if now >= deadline { cancel("暂停结果不可确认，恢复取消") }
        case .paused:
            // 持有期间只读上下文和既有控件，避免高频扫描整棵树或枚举音频进程。
            if !target.pauseStillHeld { cancel("媒体状态已改变，恢复取消") }
        case .resuming:
            if let observed = try? frontMedia(includeAudio: false), target.matches(observed), observed.hint == .playing {
                if lastIntent == .away || lastIntent == .noFace {
                    clearOwnership(); pause(); return
                }
                if let position = try? rewindMedia(observed, watch: watch) {
                    rewindFrom = position
                    self.phase = .rewinding
                    deadline = now + 2
                    status = "已恢复播放，正在确认回退"
                } else { cancel("已恢复播放，未能安全回退") }
            } else if now >= deadline { cancel("恢复结果不可确认，交由你控制") }
        case .rewinding:
            if lastIntent == .away || lastIntent == .noFace {
                clearOwnership(); pause(); return
            }
            if let observed = try? frontMedia(includeAudio: false), target.matches(observed), observed.hint == .playing,
               let from = rewindFrom, let position = observed.progress,
               position < from - min(2, from * 0.5) {
                cancel("已恢复播放并回退一次")
            } else if now >= deadline { cancel("已恢复播放，回退结果未确认") }
        }
    }

    private func cancel(_ reason: String) { clearOwnership(); status = reason }
    private func clearOwnership() {
        timer?.invalidate(); timer = nil
        watch?.close(); watch = nil
        target = nil; phase = nil; rewindFrom = nil
    }
}
