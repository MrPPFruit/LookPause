import AppKit
import Network
import Security
import OSLog

private let receiverLog = Logger(subsystem: "com.lookpause.pilot", category: "receiver")

// I/O 仅负责有界帧传递；所有认证/会话/播放器决策回到 MainActor。
final class Peer: @unchecked Sendable {
    let id = UUID()
    let connection: NWConnection
    private var buffer = Data()
    private let queue = DispatchQueue(label: "LookPause.peer")
    var onFrame: (@Sendable (Data, @escaping @Sendable () -> Void) -> Void)?
    var onClose: (@Sendable () -> Void)?
    init(_ connection: NWConnection) { self.connection = connection }
    func start() {
        connection.stateUpdateHandler = { [weak self] state in
            switch state {
            case .ready: self?.receive()
            case .failed, .cancelled: self?.onClose?()
            default: break
            }
        }
        connection.start(queue: queue)
    }
    func send(_ frame: WireFrame) {
        guard var data = try? JSONEncoder().encode(frame), data.count < 4096 else { close(); return }
        data.append(10)
        connection.send(content: data, completion: .contentProcessed { [weak self] error in if error != nil { self?.close() } })
    }
    func close() { connection.cancel() }
    private func receive() {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { [weak self] data, _, complete, error in
            guard let self else { return }
            if let data { self.buffer.append(data) }
            if complete || error != nil { self.close(); return }
            self.drain()
        }
    }
    private func drain() {
        if let end = buffer.firstIndex(of: 10) {
            guard end > 0, end < 4096, let onFrame else { close(); return }
            let frame = Data(buffer.prefix(end))
            buffer.removeSubrange(...end)
            // 消费完一帧再读下一帧，防止网络输入堆积为无界 MainActor 任务。
            onFrame(frame) { [weak self] in
                guard let self else { return }
                self.queue.async { self.drain() }
            }
        } else if buffer.count >= 4096 { close() }
        else { receive() }
    }
}

@MainActor
@available(macOS 15, *)
final class Receiver {
    private struct Client {
        let peer: Peer
        var lastSeen = ProcessInfo.processInfo.systemUptime
        var deviceId: String?
        var name = ""
        var session: String?
        var gate = SequenceGate()
        var rateWindow = ProcessInfo.processInfo.systemUptime
        var frames = 0
    }
    let media = MediaSession()
    let serverId: String
    private let identity: PairingIdentity
    private let loopbackOnly: Bool
    private var listener: NWListener?
    private var clients: [UUID: Client] = [:]
    private var active: UUID?
    private var pending: UUID?
    private var pairAttempts: [Double] = []
    private var timer: Timer?
    private(set) var status = "正在启动接收端" { didSet { onStatus?() } }
    var onStatus: (() -> Void)?
    var approve: ((String, @escaping (Bool) -> Void) -> Void)?
    var port: UInt16? { listener?.port?.rawValue }
    var fingerprint: String { identity.fingerprint }

    init(loopbackOnly: Bool = false) throws {
        self.loopbackOnly = loopbackOnly
        identity = try PairingIdentity.load()
        serverId = UserDefaults.standard.string(forKey: "LookPause.serverId") ?? UUID().uuidString.lowercased()
        UserDefaults.standard.set(serverId, forKey: "LookPause.serverId")
        media.onStatus = { [weak self] text in self?.publishStatus(text) }
    }

    func start() throws {
        let tls = NWProtocolTLS.Options()
        guard let secIdentity = sec_identity_create(identity.identity) else { throw IdentityError.certificate }
        sec_protocol_options_set_local_identity(tls.securityProtocolOptions, secIdentity)
        sec_protocol_options_set_min_tls_protocol_version(tls.securityProtocolOptions, .TLSv12)
        let parameters = NWParameters(tls: tls, tcp: NWProtocolTCP.Options())
        parameters.includePeerToPeer = false
        if loopbackOnly { parameters.requiredLocalEndpoint = .hostPort(host: .ipv4(.loopback), port: .any) }
        let listener = try NWListener(using: parameters, on: .any)
        if !loopbackOnly {
            listener.service = NWListener.Service(name: "PausePal · " + (Host.current().localizedName ?? "Mac"),
                type: "_lookpause._tcp", txtRecord: NWTXTRecord(["v": "1", "id": serverId, "fp": identity.fingerprint]))
        }
        listener.stateUpdateHandler = { [weak self] state in
            Task { @MainActor in
                switch state {
                case .ready:
                    self?.status = "等待手机连接"
                    receiverLog.notice("接收端已就绪")
                case .failed: self?.stop("接收端不可用")
                default: break
                }
            }
        }
        listener.newConnectionHandler = { [weak self] connection in Task { @MainActor in self?.accept(connection) } }
        self.listener = listener
        listener.start(queue: DispatchQueue(label: "LookPause.listener"))
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.expire() }
        }
    }

    func stop(_ reason: String = "接收端已停止") {
        timer?.invalidate(); timer = nil
        listener?.cancel(); listener = nil
        media.stop(reason)
        for client in clients.values { client.peer.close() }
        clients.removeAll(); active = nil; pending = nil
        status = reason
    }

    private func accept(_ connection: NWConnection) {
        guard clients.count < 4 else { connection.cancel(); return }
        let peer = Peer(connection)
        clients[peer.id] = Client(peer: peer)
        peer.onFrame = { [weak self, weak peer] data, consumed in
            Task { @MainActor in
                if let peer { self?.receive(data, peer: peer) }
                consumed()
            }
        }
        peer.onClose = { [weak self, weak peer] in
            Task { @MainActor in if let peer { self?.closed(peer.id) } }
        }
        peer.start()
    }

    private func receive(_ data: Data, peer: Peer) {
        guard var client = clients[peer.id] else { return }
        let now = ProcessInfo.processInfo.systemUptime
        if now - client.rateWindow >= 1 { client.rateWindow = now; client.frames = 0 }
        client.frames += 1
        guard client.frames <= 20 else { reject(peer, "RATE_LIMIT"); return }
        clients[peer.id] = client
        guard let frame = try? JSONDecoder().decode(WireFrame.self, from: data), frame.v == 1 else {
            reject(peer, "INVALID_FRAME"); return
        }
        if client.session == nil {
            guard frame.kind == .hello, pending != peer.id else { reject(peer, "UNAUTHORIZED"); return }
            hello(frame, peer: peer); return
        }
        guard frame.session == client.session, let seq = frame.seq, client.gate.accept(seq) else {
            peer.send(WireFrame(kind: .error, seq: frame.seq, error: "STALE_SESSION_OR_SEQUENCE")); return
        }
        guard active == peer.id else { reject(peer, "INACTIVE_SESSION"); return }
        client.lastSeen = ProcessInfo.processInfo.systemUptime
        clients[peer.id] = client
        switch frame.kind {
        case .start: media.start()
        case .stop: media.stop()
        case .intent:
            guard let intent = frame.intent, media.isWatching else {
                peer.send(WireFrame(kind: .error, seq: seq, error: "NOT_WATCHING_OR_INVALID_INTENT")); return
            }
            receiverLog.notice("已接收认证意图：\(intent.rawValue, privacy: .public)")
            media.receive(intent)
        case .ping: break
        default: reject(peer, "UNSUPPORTED_OPERATION"); return
        }
        peer.send(WireFrame(kind: .ack, session: client.session, seq: seq, status: media.status, ok: true))
    }

    private func hello(_ frame: WireFrame, peer: Peer) {
        guard let rawId = frame.deviceId, let id = UUID(uuidString: rawId)?.uuidString.lowercased(),
              let rawName = frame.name, !rawName.isEmpty, rawName.utf8.count <= 128 else { reject(peer, "INVALID_HELLO"); return }
        let name = String(rawName.unicodeScalars.filter { !CharacterSet.controlCharacters.contains($0) }).prefix(64)
        do {
            let account = (loopbackOnly ? "local-test-phone-" : "phone-") + id
            if let known = try SecureStore.read(account) {
                guard let saved = String(data: known, encoding: .utf8), let token = frame.token,
                      SecureStore.equalTokens(saved, token) else { reject(peer, "INVALID_CREDENTIAL"); return }
                authorize(peer, deviceId: id, name: String(name), token: nil)
            } else {
                let now = ProcessInfo.processInfo.systemUptime
                pairAttempts.removeAll { now - $0 > 60 }
                guard pending == nil, active == nil, pairAttempts.count < 3, frame.token == nil else { reject(peer, "PAIRING_UNAVAILABLE"); return }
                pairAttempts.append(now); pending = peer.id
                clients[peer.id]?.name = String(name)
                status = "等待首次配对确认"
                guard let approve else { reject(peer, "PAIRING_NOT_AVAILABLE"); return }
                approve(String(name)) { [weak self, weak peer] allowed in
                    guard let self, let peer, self.pending == peer.id, self.clients[peer.id] != nil else { return }
                    self.pending = nil
                    guard allowed else { self.reject(peer, "PAIRING_REJECTED"); return }
                    do {
                        let token = try SecureStore.randomToken()
                        try SecureStore.write(account, data: Data(token.utf8))
                        self.authorize(peer, deviceId: id, name: String(name), token: token)
                    } catch { self.reject(peer, "CREDENTIAL_STORE_FAILED") }
                }
            }
        } catch { reject(peer, "CREDENTIAL_STORE_FAILED") }
    }

    private func authorize(_ peer: Peer, deviceId: String, name: String, token: String?) {
        if let active, active != peer.id {
            guard clients[active]?.deviceId == deviceId else { reject(peer, "BUSY"); return }
            clients[active]?.peer.close()
        }
        media.stop("已连接，等待手机开始")
        active = peer.id
        clients[peer.id]?.deviceId = deviceId
        clients[peer.id]?.name = name
        clients[peer.id]?.session = UUID().uuidString.lowercased()
        clients[peer.id]?.lastSeen = ProcessInfo.processInfo.systemUptime
        status = "已连接：" + name
        receiverLog.notice("设备认证已通过；新会话尚未开始")
        peer.send(WireFrame(kind: .ready, token: token, session: clients[peer.id]?.session, status: "已连接，等待开始", ok: true))
    }

    private func reject(_ peer: Peer, _ reason: String) {
        peer.send(WireFrame(kind: .error, error: reason))
        // 延后一轮仅供错误帧写出；无重试或操作补发。
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) { peer.close() }
    }

    private func closed(_ id: UUID) {
        clients.removeValue(forKey: id)
        if pending == id { pending = nil; if active == nil { status = "等待手机连接" } }
        if active == id { active = nil; media.stop("手机已断开，恢复取消"); status = "等待手机连接" }
    }

    private func expire() {
        let now = ProcessInfo.processInfo.systemUptime
        for client in Array(clients.values) {
            let limit = client.session == nil ? (pending == client.peer.id ? 60.0 : 10.0) : 6.0
            if now - client.lastSeen > limit { client.peer.close(); closed(client.peer.id) }
        }
    }

    private func publishStatus(_ text: String) {
        if let active, let client = clients[active] { client.peer.send(WireFrame(kind: .status, session: client.session, status: text)) }
        onStatus?()
    }
}
