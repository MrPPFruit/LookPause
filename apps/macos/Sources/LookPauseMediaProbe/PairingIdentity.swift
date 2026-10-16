import CryptoKit
import Foundation
import Security

enum IdentityError: Error { case keychain(OSStatus), certificate, process, randomness }

enum SecureStore {
    // 产品与资格探针分开；稳定签名后，同一应用的新构建不需要反复迁移凭据。
    static var service: String { "LookPause.Pilot.v1." + (Bundle.main.bundleIdentifier ?? "command-line") }
    static func read(_ account: String) throws -> Data? {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service, kSecAttrAccount as String: account,
            kSecReturnData as String: true, kSecMatchLimit as String: kSecMatchLimitOne]
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecItemNotFound { return nil }
        guard status == errSecSuccess, let data = result as? Data else { throw IdentityError.keychain(status) }
        return data
    }
    static func write(_ account: String, data: Data) throws {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service, kSecAttrAccount as String: account]
        let status = SecItemUpdate(query as CFDictionary, [kSecValueData as String: data] as CFDictionary)
        if status == errSecItemNotFound {
            var item = query
            item[kSecValueData as String] = data
            item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            let inserted = SecItemAdd(item as CFDictionary, nil)
            guard inserted == errSecSuccess else { throw IdentityError.keychain(inserted) }
        } else if status != errSecSuccess { throw IdentityError.keychain(status) }
    }
    static func randomToken() throws -> String {
        var bytes = [UInt8](repeating: 0, count: 32)
        guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else { throw IdentityError.randomness }
        return Data(bytes).base64EncodedString()
    }
    static func equalTokens(_ left: String, _ right: String) -> Bool {
        guard let a = Data(base64Encoded: left), let b = Data(base64Encoded: right), a.count == 32, b.count == 32 else { return false }
        return zip(a, b).reduce(UInt8(0)) { $0 | ($1.0 ^ $1.1) } == 0
    }
}

@available(macOS 15, *)
struct PairingIdentity {
    private struct Stored: Codable { let p12: Data; let password: String }
    let identity: SecIdentity
    let fingerprint: String

    static func load() throws -> PairingIdentity {
        let stored: Stored
        if let data = try SecureStore.read("server-identity") {
            stored = try JSONDecoder().decode(Stored.self, from: data)
        } else {
            let directory = FileManager.default.temporaryDirectory.appendingPathComponent("lookpause-" + UUID().uuidString)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: false,
                                                    attributes: [.posixPermissions: 0o700])
            defer { try? FileManager.default.removeItem(at: directory) }
            let key = directory.appendingPathComponent("key.pem")
            let cert = directory.appendingPathComponent("cert.pem")
            let p12 = directory.appendingPathComponent("identity.p12")
            let password = try SecureStore.randomToken()
            try openssl(["req", "-x509", "-newkey", "rsa:2048", "-nodes", "-sha256", "-days", "3650",
                         "-subj", "/CN=LookPause-Pilot", "-keyout", key.path, "-out", cert.path])
            try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: key.path)
            try openssl(["pkcs12", "-export", "-inkey", key.path, "-in", cert.path,
                         "-out", p12.path, "-passout", "stdin"], input: password + "\n")
            stored = Stored(p12: try Data(contentsOf: p12), password: password)
            try SecureStore.write("server-identity", data: JSONEncoder().encode(stored))
        }
        var imported: CFArray?
        let options: [String: Any] = [kSecImportExportPassphrase as String: stored.password,
                                      kSecImportToMemoryOnly as String: true]
        guard SecPKCS12Import(stored.p12 as CFData, options as CFDictionary, &imported) == errSecSuccess,
              let items = imported as? [[String: Any]], let raw = items.first?[kSecImportItemIdentity as String] else {
            throw IdentityError.certificate
        }
        let identity = raw as! SecIdentity
        var certificate: SecCertificate?
        guard SecIdentityCopyCertificate(identity, &certificate) == errSecSuccess, let certificate else { throw IdentityError.certificate }
        let fingerprint = SHA256.hash(data: SecCertificateCopyData(certificate) as Data).map { String(format: "%02x", $0) }.joined()
        return PairingIdentity(identity: identity, fingerprint: fingerprint)
    }

    private static func openssl(_ arguments: [String], input: String? = nil) throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/openssl")
        process.arguments = arguments
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        let pipe = Pipe()
        process.standardInput = pipe
        try process.run()
        if let input { try pipe.fileHandleForWriting.write(contentsOf: Data(input.utf8)) }
        try pipe.fileHandleForWriting.close()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw IdentityError.process }
    }
}
