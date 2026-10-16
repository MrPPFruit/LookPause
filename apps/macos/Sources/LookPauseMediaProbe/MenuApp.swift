import AppKit
@preconcurrency import ApplicationServices
import ServiceManagement
import OSLog

@MainActor
@available(macOS 15, *)
final class MenuAppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var item: NSStatusItem!
    private var receiver: Receiver?
    private let connectionRow = NSMenuItem(title: "正在启动", action: nil, keyEquivalent: "")
    private let mediaRow = NSMenuItem(title: "尚未开始观影", action: nil, keyEquivalent: "")
    private let permissionRow = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    private let loginRow = NSMenuItem(title: "登录后启动", action: #selector(toggleLogin), keyEquivalent: "")
    private var startupError: String?

    func applicationDidFinishLaunching(_ notification: Notification) {
        Logger(subsystem: "com.lookpause.pilot", category: "permissions").notice("普通入口权限：AX=\(AXIsProcessTrusted(), privacy: .public) 输入监听=\(CGPreflightListenEventAccess(), privacy: .public)")
        NSApp.setActivationPolicy(.accessory)
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.image = NSImage(systemSymbolName: "eye", accessibilityDescription: "LookPause")
        item.button?.setAccessibilityLabel("LookPause")
        let menu = NSMenu()
        menu.delegate = self
        menu.addItem(connectionRow); menu.addItem(mediaRow); menu.addItem(permissionRow)
        menu.addItem(.separator()); loginRow.target = self; menu.addItem(loginRow)
        menu.addItem(.separator())
        let quit = NSMenuItem(title: "退出 LookPause", action: #selector(quitApp), keyEquivalent: "q")
        quit.target = self; menu.addItem(quit)
        item.menu = menu
        do {
            let receiver = try Receiver()
            self.receiver = receiver
            receiver.onStatus = { [weak self] in self?.refresh() }
            receiver.approve = { name, completion in
                let alert = NSAlert()
                alert.messageText = "允许这台手机连接 LookPause？"
                alert.informativeText = name + " 请求在当前局域网配对。允许后将记住这台设备，仅接收观看方向意图。"
                alert.addButton(withTitle: "允许")
                alert.addButton(withTitle: "拒绝")
                NSApp.activate(ignoringOtherApps: true)
                completion(alert.runModal() == .alertFirstButtonReturn)
            }
            try receiver.start()
        } catch { startupError = "接收端启动失败，请退出后重试" }
        if UserDefaults.standard.object(forKey: "LookPause.login") == nil {
            UserDefaults.standard.set(true, forKey: "LookPause.login")
        }
        Logger(subsystem: "com.lookpause.pilot", category: "login").notice("登记前登录状态=\(SMAppService.mainApp.status.rawValue) 意愿=\(UserDefaults.standard.bool(forKey: "LookPause.login"))")
        if UserDefaults.standard.bool(forKey: "LookPause.login"), [.notRegistered, .notFound].contains(SMAppService.mainApp.status) {
            do { try SMAppService.mainApp.register() } catch {
                let error = error as NSError
                Logger(subsystem: "com.lookpause.pilot", category: "login").error("登录登记失败 domain=\(error.domain, privacy: .public) code=\(error.code)")
                startupError = "登录启动未能登记"
            }
        }
        Logger(subsystem: "com.lookpause.pilot", category: "login").notice("登记后登录状态=\(SMAppService.mainApp.status.rawValue)")
        if !AXIsProcessTrusted() {
            let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true]
            _ = AXIsProcessTrustedWithOptions(options as CFDictionary)
        }
        refresh()
    }

    func menuWillOpen(_ menu: NSMenu) { refresh() }
    private func refresh() {
        connectionRow.title = receiver?.status ?? startupError ?? "接收端不可用"
        mediaRow.title = receiver?.media.status ?? "尚未开始观影"
        var missingPermissions: [String] = []
        if !AXIsProcessTrusted() { missingPermissions.append("辅助功能") }
        if !CGPreflightListenEventAccess() { missingPermissions.append("输入监控") }
        permissionRow.isHidden = missingPermissions.isEmpty && startupError == nil
        permissionRow.title = startupError ?? "需要在系统设置允许" + missingPermissions.joined(separator: "、")
        loginRow.state = SMAppService.mainApp.status == .enabled ? .on :
                         (SMAppService.mainApp.status == .requiresApproval ? .mixed : .off)
        loginRow.title = SMAppService.mainApp.status == .requiresApproval ? "登录后启动（待系统批准）" : "登录后启动"
        item.button?.toolTip = "LookPause · " + connectionRow.title + " · " + mediaRow.title
    }

    @objc private func toggleLogin() {
        do {
            if SMAppService.mainApp.status == .enabled || SMAppService.mainApp.status == .requiresApproval {
                try SMAppService.mainApp.unregister()
                UserDefaults.standard.set(false, forKey: "LookPause.login")
            } else {
                try SMAppService.mainApp.register()
                UserDefaults.standard.set(true, forKey: "LookPause.login")
            }
            startupError = nil
        } catch { startupError = "登录启动设置未能保存" }
        refresh()
    }
    @objc private func quitApp() { receiver?.stop(); NSApp.terminate(nil) }
    func applicationWillTerminate(_ notification: Notification) { receiver?.stop() }
}

@MainActor
@available(macOS 15, *)
func runMenuApp() {
    let app = NSApplication.shared
    let delegate = MenuAppDelegate()
    app.delegate = delegate
    withExtendedLifetime(delegate) { app.run() }
}

@MainActor
@available(macOS 15, *)
func runReceiverTest() throws {
    // 资格入口强制只监听 loopback，不发布 Bonjour；测试凭据与正式手机凭据分离。
    let receiver = try Receiver(loopbackOnly: true)
    receiver.approve = { _, done in done(true) }
    receiver.onStatus = {
        emit(["scope": "loopback-receiver-test", "port": receiver.port ?? 0,
              "fingerprint": receiver.fingerprint, "status": receiver.status, "media": receiver.media.status])
    }
    try receiver.start()
    withExtendedLifetime(receiver) { RunLoop.current.run() }
}
