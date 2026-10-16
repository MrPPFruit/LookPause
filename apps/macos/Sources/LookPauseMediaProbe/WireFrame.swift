import Foundation

struct WireFrame: Codable, Sendable {
    enum Kind: String, Codable, Sendable { case hello, ready, start, stop, intent, ping, ack, status, error }
    var v = 1
    var kind: Kind
    var deviceId: String?
    var name: String?
    var token: String?
    var session: String?
    var seq: Int64?
    var intent: WatchingIntent?
    var status: String?
    var ok: Bool?
    var error: String?
}

// 每个 TLS 连接只允许一个会话；重连重新 START，旧意图绝不补发。
struct SequenceGate {
    private(set) var latest: Int64 = 0
    mutating func accept(_ seq: Int64) -> Bool {
        guard seq > latest else { return false }
        latest = seq
        return true
    }
}

func wireSelfTest() {
    var sequence = SequenceGate()
    precondition(sequence.accept(1))
    precondition(!sequence.accept(1))
    precondition(!sequence.accept(0))
    precondition(sequence.accept(3))
    precondition(!sequence.accept(2))
    precondition(SecureStore.equalTokens(Data(repeating: 7, count: 32).base64EncodedString(), Data(repeating: 7, count: 32).base64EncodedString()))
    precondition(!SecureStore.equalTokens("bad", "bad"))
    let unknown = Data("{\"v\":1,\"kind\":\"shell\"}".utf8)
    precondition((try? JSONDecoder().decode(WireFrame.self, from: unknown)) == nil)
}
