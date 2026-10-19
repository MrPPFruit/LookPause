using PausePal.Windows;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

internal static class ProtocolTests
{
    private const string Phone = "12345678-1234-1234-1234-123456789012";
    private static readonly string Token = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
    public static Dictionary<string, Action> Checks() => new()
    {
        ["协议拒绝重复字段和扩展命令"] = () =>
        {
            Rejects(() => WireFrame.Parse(Encoding.UTF8.GetBytes("{\"v\":1,\"v\":1,\"kind\":\"ping\"}")));
            Rejects(() => WireFrame.Parse(Encoding.UTF8.GetBytes("{\"v\":1,\"kind\":\"shell\"}")));
            Rejects(() => WireFrame.Parse(Encoding.UTF8.GetBytes("{\"v\":1,\"kind\":\"intent\",\"key\":\"SPACE\"}")));
            Rejects(() => WireFrame.Parse([0xff]));
        },
        ["线帧边界与粘包"] = () =>
        {
            var input = new MemoryStream([.. Enumerable.Repeat((byte)'x', 4095), 10, (byte)'a', 10]);
            var reader = new FrameReader(input);
            Equal(4095, reader.ReadAsync(default).GetAwaiter().GetResult().Length);
            Equal("a", Encoding.UTF8.GetString(reader.ReadAsync(default).GetAwaiter().GetResult()));
            Rejects(() => new FrameReader(new MemoryStream(new byte[4096])).ReadAsync(default).GetAwaiter().GetResult());
            Rejects(() => new FrameReader(new MemoryStream([10])).ReadAsync(default).GetAwaiter().GetResult());
        },
        ["令牌长度和固定时间比较"] = () =>
        {
            True(IdentityStore.EqualTokens(Token, Token)); True(!IdentityStore.EqualTokens("bad", "bad"));
            True(!IdentityStore.EqualTokens(Token, Convert.ToBase64String(new byte[32])));
        },
        ["未允许设备不能启动"] = () =>
        {
            var t = new Fixture(false); t.Core.Handle(t.Peer.Id, new WireFrame { kind = "start", seq = 1 });
            Equal("UNAUTHORIZED", t.Peer.Frames.Last().error); True(!t.Inbox.TryRead(out _));
        },
        ["首次批准持久化后才返回令牌"] = () =>
        {
            var t = new Fixture(false); t.Hello(); Equal(0, t.Peer.Frames.Count);
            t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult();
            var ready = t.Peer.Frames.Single(); Equal("ready", ready.kind); True(IdentityStore.ValidToken(ready.token));
            Equal(t.Store.ReadToken(Phone), ready.token); True(Guid.TryParse(ready.session, out _));
            Equal(DesktopStatus.waiting, t.Core.Snapshot.Icon);
        },
        ["配对写盘失败不认证"] = () =>
        {
            var t = new Fixture(false); t.Store.Fail = true; t.Hello();
            t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult();
            Equal("CREDENTIAL_STORE_FAILED", t.Peer.Frames.Last().error); Equal(DesktopStatus.disconnected, t.Core.Snapshot.Icon);
        },
        ["已配对手机缺令牌不重新弹窗"] = () =>
        {
            var t = new Fixture(); t.Hello(false); Equal("INVALID_CREDENTIAL", t.Peer.Frames.Last().error); True(!t.Core.IsPending(t.Peer.Id));
        },
        ["重放旧会话无副作用且认证错误带会话"] = () =>
        {
            var t = new Fixture(); t.Hello(); t.Drain(); t.Send("start", 1); t.Drain();
            var epoch = t.Revocation.Epoch;
            t.Send("stop", 1); Equal(epoch, t.Revocation.Epoch); True(!t.Inbox.TryRead(out _));
            Equal(t.Session, t.Peer.Frames.Last().session); Equal(1L, t.Peer.Frames.Last().seq!.Value);
            t.Core.Handle(t.Peer.Id, new WireFrame { kind = "stop", session = Guid.NewGuid().ToString(), seq = 2 });
            Equal(epoch, t.Revocation.Epoch);
        },
        ["开始停止方向四态生命周期"] = () =>
        {
            var t = new Fixture(); t.Hello(); t.Send("start", 1); Equal(DesktopStatus.waiting, t.Core.Snapshot.Icon);
            t.Send("intent", 2, "ATTENDING"); Equal(DesktopStatus.attending, t.Core.Snapshot.Icon);
            t.Send("intent", 3, "AWAY"); Equal(DesktopStatus.away, t.Core.Snapshot.Icon);
            t.Send("intent", 4, "UNKNOWN"); Equal(DesktopStatus.waiting, t.Core.Snapshot.Icon);
            t.Send("stop", 5); Equal(DesktopStatus.waiting, t.Core.Snapshot.Icon);
            t.Core.Closed(t.Peer.Id); Equal(DesktopStatus.disconnected, t.Core.Snapshot.Icon);
        },
        ["六秒超时立即撤销且无重放续命"] = () =>
        {
            var t = new Fixture(); t.Hello(); t.Send("start", 1); t.Drain(); var epoch = t.Revocation.Epoch;
            t.Now = 5000; t.Send("ping", 1); t.Now = 6001; t.Core.Expire();
            True(t.Revocation.Epoch > epoch); Equal(DesktopStatus.disconnected, t.Core.Snapshot.Icon); True(t.Peer.Closed);
        },
        ["旧连接关闭不能撤销新连接"] = () =>
        {
            var t = new Fixture(); t.Hello(); var old = t.Peer; var next = new Peer(); True(t.Core.Add(next));
            t.Core.Handle(next.Id, new WireFrame { kind = "hello", deviceId = Phone, name = "S25", token = Token });
            var epoch = t.Revocation.Epoch;
            t.Core.Closed(old.Id); Equal(epoch, t.Revocation.Epoch); Equal(DesktopStatus.waiting, t.Core.Snapshot.Icon);
            True(next.Frames.Single().session != old.Frames.Single().session);
        },
        ["旧媒体状态不能跨观影轮次"] = () =>
        {
            var t = new Fixture(); t.Hello(); t.Send("start", 1); var old = t.Core.Generation;
            t.Send("stop", 2); t.Send("start", 3); t.Core.MediaChanged(old, "旧状态");
            True(t.Core.Snapshot.Media != "旧状态");
        },
        ["无效业务消息消耗序号但不能续命"] = () =>
        {
            var t = new Fixture(); t.Hello(); t.Send("start", 1); t.Drain();
            t.Now = 5000; t.Send("intent", 100, "INVALID"); t.Send("ping", 99);
            Equal("STALE_SESSION_OR_SEQUENCE", t.Peer.Frames.Last().error);
            t.Now = 6001; t.Core.Expire(); Equal(DesktopStatus.disconnected, t.Core.Snapshot.Icon);
        },
        ["首配ready丢失允许重新明确批准"] = () =>
        {
            var t = new Fixture(false); t.Hello(); t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult();
            True(!t.Store.IsConfirmed(Phone)); t.Core.Closed(t.Peer.Id);
            t.Peer = new Peer(); t.Core.Add(t.Peer); t.Hello(false);
            True(t.Core.IsPending(t.Peer.Id)); Equal(0, t.Peer.Frames.Count);
            t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult(); Equal("ready", t.Peer.Frames.Single().kind);
        },
        ["迟到配对不能覆盖已确认凭据"] = () =>
        {
            var t = new Fixture(false); t.Store.SaveToken(Phone, Token, false); t.Hello(false);
            t.Store.ConfirmToken(Phone, Token);
            t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult();
            Equal(Token, t.Store.ReadToken(Phone)); Equal("CREDENTIAL_CHANGED", t.Peer.Frames.Last().error);
        },
        ["断线后的迟到允许无效"] = () =>
        {
            var t = new Fixture(false); t.Hello(); t.Core.Closed(t.Peer.Id);
            t.Core.ApproveAsync(t.Peer.Id, true).GetAwaiter().GetResult(); Equal(0, t.Peer.Frames.Count); True(t.Store.ReadToken(Phone) == null);
        },
        ["配对弹窗频率限制"] = () =>
        {
            var t = new Fixture(false);
            for (var i = 0; i < 3; i++)
            {
                t.Hello(); t.Core.ApproveAsync(t.Peer.Id, false).GetAwaiter().GetResult();
                t.Peer = new Peer(); True(t.Core.Add(t.Peer));
            }
            t.Hello(); Equal("PAIRING_UNAVAILABLE", t.Peer.Frames.Last().error);
        },
        ["帧速率与连接数有界"] = () =>
        {
            var t = new Fixture(); t.Hello();
            for (var i = 1; i <= 20; i++) t.Send("ping", i);
            Equal("RATE_LIMIT", t.Peer.Frames.Last().error);
            var fresh = new Fixture(); for (var i = 0; i < 3; i++) True(fresh.Core.Add(new Peer()));
            True(!fresh.Core.Add(new Peer()));
        },
        ["公网和回环不进入普通LAN接收端"] = () =>
        {
            True(LanReceiver.IsLocal(IPAddress.Parse("192.168.1.1"))); True(LanReceiver.IsLocal(IPAddress.Parse("fd00::1")));
            True(!LanReceiver.IsLocal(IPAddress.Parse("8.8.8.8"))); True(!LanReceiver.IsLocal(IPAddress.Loopback));
            True(!LanReceiver.IsLocal(IPAddress.IPv6Loopback));
        },
        ["真实DPAPI身份重启保持且损坏不重建"] = () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "PausePal-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                string id, fp;
                using (var first = IdentityStore.Load(directory)) { id = first.ServerId; fp = first.Fingerprint; first.SaveToken(Phone, Token); }
                using (var second = IdentityStore.Load(directory)) { Equal(id, second.ServerId); Equal(fp, second.Fingerprint); Equal(Token, second.ReadToken(Phone)); }
                var file = Path.Combine(directory, "identity.dpapi"); File.WriteAllBytes(file, [1, 2, 3]);
                Rejects(() => IdentityStore.Load(directory)); Equal(3L, new FileInfo(file).Length);
            }
            finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        },
        ["真实TLS与Android格式帧端到端且断线独立于UI消息泵"] = () => TlsRoundTrip().GetAwaiter().GetResult()
    };

    private static async Task TlsRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PausePal-tls-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var identity = IdentityStore.Load(directory);
            identity.SaveToken(Phone, Token);
            var revocation = new Revocation(); var inbox = new IntentInbox(revocation);
            var core = new ReceiverCore(identity, inbox);
            Exception? serverError = null;
            var diagnosticEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseDiagnostic = new ManualResetEventSlim();
            var blockDiagnostic = 0;
            var context = new NonPumpingContext();
            var previousContext = SynchronizationContext.Current;
            LanReceiver server;
            SynchronizationContext.SetSynchronizationContext(context);
            try { server = new LanReceiver(identity, core, loopback: true, diagnostic: error =>
            {
                serverError = error;
                if (Volatile.Read(ref blockDiagnostic) != 0)
                {
                    diagnosticEntered.TrySetResult(); releaseDiagnostic.Wait(TimeSpan.FromSeconds(3));
                }
            }); }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            using var serverLifetime = server;
            using var tcp = new TcpClient(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await tcp.ConnectAsync(IPAddress.Loopback, server.Port, deadline.Token);
            using var tls = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) => certificate != null &&
                Convert.ToHexStringLower(SHA256.HashData(certificate.GetRawCertData())) == identity.Fingerprint);
            try { await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "PausePal", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, deadline.Token); }
            catch { throw new Exception("服务端TLS：" + serverError?.Message + "；内部：" + serverError?.InnerException?.Message); }
            await tls.WriteAsync(new WireFrame { kind = "hello", deviceId = Phone, name = "S25", token = Token }.Encode(), deadline.Token);
            var reader = new FrameReader(tls);
            using var ready = JsonDocument.Parse(await reader.ReadAsync(deadline.Token));
            Equal("ready", ready.RootElement.GetProperty("kind").GetString());
            var session = ready.RootElement.GetProperty("session").GetString();
            await tls.WriteAsync(new WireFrame { kind = "start", session = session, seq = 1 }.Encode(), deadline.Token);
            using var ack = JsonDocument.Parse(await reader.ReadAsync(deadline.Token));
            Equal(session, ack.RootElement.GetProperty("session").GetString()); Equal("ack", ack.RootElement.GetProperty("kind").GetString());
            var before = revocation.Epoch;
            await tls.WriteAsync(new WireFrame { kind = "stop", session = session, seq = 2 }.Encode(), deadline.Token);
            await reader.ReadAsync(deadline.Token); True(revocation.Epoch > before);
            before = revocation.Epoch; Volatile.Write(ref blockDiagnostic, 1); tcp.Close();
            try
            {
                await diagnosticEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
                True(revocation.Epoch > before); Equal(0, context.Posts);
            }
            finally { releaseDiagnostic.Set(); }
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    private sealed class NonPumpingContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback callback, object? state) => Interlocked.Increment(ref Posts);
    }
    private sealed class Fixture
    {
        public Revocation Revocation = new(); public Store Store = new(); public Peer Peer = new();
        public IntentInbox Inbox; public ReceiverCore Core; public long Now;
        public string Session => Peer.Frames.First(x => x.kind == "ready").session!;
        public Fixture(bool known = true)
        {
            if (known) Store.Tokens[Phone] = Token;
            Inbox = new IntentInbox(Revocation); Core = new ReceiverCore(Store, Inbox, () => Now); True(Core.Add(Peer));
        }
        public void Hello(bool token = true) => Core.Handle(Peer.Id, new WireFrame { kind = "hello", deviceId = Phone, name = "S25", token = token && Store.ReadToken(Phone) != null ? Token : null });
        public void Send(string kind, long seq, string? intent = null) => Core.Handle(Peer.Id, new WireFrame { kind = kind, seq = seq, session = Session, intent = intent });
        public void Drain() { while (Inbox.TryRead(out _)) { } }
    }
    private sealed class Revocation : IRevocation { private long epoch; public long Epoch => Interlocked.Read(ref epoch); public void Revoke() => Interlocked.Increment(ref epoch); }
    private sealed class Store : IPhoneCredentials
    {
        public Dictionary<string, string> Tokens = []; public bool Fail;
        private readonly HashSet<string> unconfirmed = [];
        public string? ReadToken(string deviceId) => Tokens.GetValueOrDefault(deviceId);
        public bool IsConfirmed(string deviceId) => !unconfirmed.Contains(deviceId);
        public void SaveToken(string deviceId, string token, bool confirmed = true) { if (Fail) throw new IOException(); Tokens[deviceId] = token; if (confirmed) unconfirmed.Remove(deviceId); else unconfirmed.Add(deviceId); }
        public void ConfirmToken(string deviceId, string token) { if (Tokens.GetValueOrDefault(deviceId) == token) unconfirmed.Remove(deviceId); }
        public bool TryStageToken(string deviceId, string token, string? expectedToken)
        {
            if (Tokens.GetValueOrDefault(deviceId) != expectedToken || expectedToken != null && !unconfirmed.Contains(deviceId)) return false;
            SaveToken(deviceId, token, false); return true;
        }
    }
    private sealed class Peer : IPhonePeer
    {
        public Guid Id { get; } = Guid.NewGuid(); public List<WireFrame> Frames = []; public bool Closed;
        public bool Send(WireFrame frame, bool closeAfter = false) { Frames.Add(frame); if (closeAfter) Closed = true; return true; }
        public void Close() => Closed = true;
    }
    private static void True(bool value) { if (!value) throw new Exception("断言不成立"); }
    private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"期望{expected}，实际{actual}"); }
    private static void Rejects(Action action) { try { action(); } catch { return; } throw new Exception("应拒绝无效输入"); }
}
