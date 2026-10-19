using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PausePal.Windows;

internal interface IPhoneCredentials
{
    string? ReadToken(string deviceId);
    bool IsConfirmed(string deviceId);
    void SaveToken(string deviceId, string token, bool confirmed = true);
    void ConfirmToken(string deviceId, string token);
    bool TryStageToken(string deviceId, string token, string? expectedToken);
}

// 只保存当前用户 DPAPI 密文；身份损坏时停止启动，不能悄悄换电脑身份。
internal sealed class IdentityStore : IPhoneCredentials, IDisposable
{
    private sealed record Data(string ServerId, byte[] Certificate, Dictionary<string, string> Phones)
    {
        public HashSet<string> Unconfirmed { get; init; } = [];
    }
    private readonly string path;
    private Data data;
    public string ServerId => data.ServerId;
    public X509Certificate2 Certificate { get; }
    public string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(Certificate.RawData));
    private IdentityStore(string path, Data data)
    {
        this.path = path; this.data = data;
        if (!Guid.TryParse(data.ServerId, out _) || data.Phones.Count > 32 ||
            data.Phones.Any(x => !Guid.TryParse(x.Key, out _) || !ValidToken(x.Value)))
            throw new InvalidDataException("保存的电脑身份无效");
        // Windows Schannel 的真实握手拒绝 EphemeralKeySet。用户级临时容器随证书释放，
        // 不设 PersistKeySet，不装受信根；持久秘密来源仍是此 DPAPI 文件。
        Certificate = X509CertificateLoader.LoadPkcs12(data.Certificate, null, X509KeyStorageFlags.UserKeySet);
        if (!Certificate.HasPrivateKey || Certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow || Certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        { Certificate.Dispose(); throw new InvalidDataException("电脑证书不可用"); }
    }

    public static IdentityStore Load(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PausePal");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "identity.dpapi");
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("电脑身份文件无效");
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try { return new IdentityStore(path, JsonSerializer.Deserialize<Data>(plain) ?? throw new InvalidDataException("电脑身份无效")); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=PausePal", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(10));
        var result = new IdentityStore(path, new Data(Guid.NewGuid().ToString(), cert.Export(X509ContentType.Pfx), []));
        try { result.Persist(result.data); return result; }
        catch { result.Dispose(); throw; }
    }

    public string? ReadToken(string deviceId) => Volatile.Read(ref data).Phones.GetValueOrDefault(deviceId);
    public bool IsConfirmed(string deviceId) => !Volatile.Read(ref data).Unconfirmed.Contains(deviceId);
    public void SaveToken(string deviceId, string token, bool confirmed = true)
    {
        lock (this)
        {
            if (!Guid.TryParse(deviceId, out _) || !ValidToken(token) || data.Phones.Count >= 32 && !data.Phones.ContainsKey(deviceId))
                throw new InvalidDataException("无法保存配对");
            var phones = new Dictionary<string, string>(data.Phones) { [deviceId] = token };
            var unconfirmed = new HashSet<string>(data.Unconfirmed);
            if (confirmed) unconfirmed.Remove(deviceId); else unconfirmed.Add(deviceId);
            var next = data with { Phones = phones, Unconfirmed = unconfirmed };
            Persist(next); Volatile.Write(ref data, next);
        }
    }
    public void ConfirmToken(string deviceId, string token)
    {
        lock (this)
            if (data.Unconfirmed.Contains(deviceId) && EqualTokens(data.Phones.GetValueOrDefault(deviceId) ?? "", token))
                SaveToken(deviceId, token, confirmed: true);
    }
    public bool TryStageToken(string deviceId, string token, string? expectedToken)
    {
        lock (this)
        {
            var current = data.Phones.GetValueOrDefault(deviceId);
            if (current != expectedToken || current != null && !data.Unconfirmed.Contains(deviceId)) return false;
            SaveToken(deviceId, token, confirmed: false); return true;
        }
    }
    private void Persist(Data next)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(next);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, encrypted); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static bool ValidToken(string? value) => value is { Length: 44 } && Convert.TryFromBase64String(value, new byte[32], out var count) && count == 32;
    public static bool EqualTokens(string saved, string? supplied)
    {
        if (!ValidToken(saved) || !ValidToken(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(saved), Convert.FromBase64String(supplied!));
    }
    public void Dispose() => Certificate.Dispose();
}
