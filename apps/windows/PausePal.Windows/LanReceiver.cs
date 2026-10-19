using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Channels;

namespace PausePal.Windows;

internal sealed class LanReceiver : IDisposable
{
    private readonly TcpListener listener;
    private readonly IdentityStore identity;
    private readonly ReceiverCore core;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim slots = new(4);
    private readonly System.Threading.Timer timer;
    private readonly bool loopback;
    private readonly Action<Exception>? diagnostic;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public LanReceiver(IdentityStore identity, ReceiverCore core, bool loopback = false, Action<Exception>? diagnostic = null)
    {
        this.identity = identity; this.core = core; this.loopback = loopback;
        this.diagnostic = diagnostic;
        listener = new TcpListener(loopback ? IPAddress.Loopback : IPAddress.IPv6Any, 0);
        if (!loopback) listener.Server.DualMode = true;
        listener.Start(4);
        timer = new System.Threading.Timer(_ => core.Expire(), null, 1000, 1000);
        // 不继承托盘 UI 的 SynchronizationContext，真实断线必须直接进入 Core。
        _ = Task.Run(AcceptAsync);
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var tcp = await listener.AcceptTcpClientAsync(lifetime.Token).ConfigureAwait(false);
                var remote = ((IPEndPoint)tcp.Client.RemoteEndPoint!).Address;
                if (!(loopback ? IPAddress.IsLoopback(remote) : IsLocal(remote)) || !slots.Wait(0)) { tcp.Dispose(); continue; }
                _ = RunAsync(tcp);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { if (!lifetime.IsCancellationRequested) core.Stop(); }
    }
    internal static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 ? bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 169 && bytes[1] == 254 :
            address.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
    }
    private async Task RunAsync(TcpClient tcp)
    {
        using (tcp)
        using (var peer = new PhonePeer(tcp, core, lifetime.Token))
        {
            try
            {
                // 名额从接受 TCP 起占用；慢 TLS 握手不能无限占位。
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(peer.Cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(8));
                await peer.Stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = identity.Certificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false, AllowRenegotiation = false,
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, deadline.Token).ConfigureAwait(false);
                if (!core.Add(peer)) return;
                await peer.RunAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or AuthenticationException or OperationCanceledException or SocketException or System.Text.Json.JsonException or InvalidOperationException or System.Text.DecoderFallbackException or KeyNotFoundException or FormatException)
            {
                // 日志属于外部副作用，磁盘或诊断回调阻塞也不能延迟断线撤销。
                core.Closed(peer.Id);
                diagnostic?.Invoke(error);
            }
            finally { core.Closed(peer.Id); slots.Release(); }
        }
    }
    public void Dispose()
    {
        core.Stop(); timer.Dispose(); lifetime.Cancel(); listener.Stop();
        // 在途任务仍持有取消令牌，不能在其退出前释放 CTS/Semaphore。
    }

    private sealed class PhonePeer : IPhonePeer, IDisposable
    {
        private sealed record Output(byte[] Bytes, bool CloseAfter);
        private readonly TcpClient tcp;
        private readonly ReceiverCore core;
        private readonly CancellationTokenSource cancellation;
        private readonly Channel<Output> output = Channel.CreateBounded<Output>(new BoundedChannelOptions(16)
        { SingleReader = true, AllowSynchronousContinuations = false, FullMode = BoundedChannelFullMode.Wait });
        private int closed;
        public Guid Id { get; } = Guid.NewGuid();
        public SslStream Stream { get; }
        public CancellationToken Cancellation => cancellation.Token;
        public PhonePeer(TcpClient tcp, ReceiverCore core, CancellationToken parent)
        {
            this.tcp = tcp; this.core = core;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(parent);
            Stream = new SslStream(tcp.GetStream(), false);
        }
        public bool Send(WireFrame frame, bool closeAfter = false)
        {
            if (Volatile.Read(ref closed) != 0) return false;
            if (output.Writer.TryWrite(new Output(frame.Encode(), closeAfter))) return true;
            Close(); return false;
        }
        public async Task RunAsync()
        {
            var writer = WriteAsync();
            try
            {
                var reader = new FrameReader(Stream);
                while (!Cancellation.IsCancellationRequested)
                {
                    var bytes = await reader.ReadAsync(Cancellation).ConfigureAwait(false);
                    core.Handle(Id, WireFrame.Parse(bytes));
                }
            }
            finally { Close(); core.Closed(Id); await writer.ConfigureAwait(false); }
        }
        private async Task WriteAsync()
        {
            try
            {
                await foreach (var item in output.Reader.ReadAllAsync(Cancellation).ConfigureAwait(false))
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(2));
                    await Stream.WriteAsync(item.Bytes, deadline.Token).ConfigureAwait(false);
                    if (item.CloseAfter) break;
                }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { }
            finally { Close(); }
        }
        public void Close()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            cancellation.Cancel(); output.Writer.TryComplete(); tcp.Dispose();
            // 不在 Core 锁内同步回调；RunAsync/finally 会通知，入口原子撤销无需 UI。
        }
        public void Dispose() { Close(); Stream.Dispose(); cancellation.Dispose(); }
    }
}
