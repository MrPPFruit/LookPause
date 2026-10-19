using System.Runtime.InteropServices;

namespace PausePal.Windows;

// 由 Windows DNS-SD 发布现有 Android 可识别的服务，不自行实现 mDNS。
internal sealed class DiscoveryRegistration : IAsyncDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Completion(uint status, nint context, nint instance);
    [StructLayout(LayoutKind.Sequential)] private struct Request
    {
        public uint Version, InterfaceIndex;
        public nint Instance, Callback, Context, Credentials;
        public int Unicast;
    }
    private static readonly Completion Callback = Completed;
    private const uint Pending = 9506;
    private TaskCompletionSource<uint> operation = NewCompletion();
    private readonly TaskCompletionSource stop = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly nint request, cancel;
    private nint instance;
    private readonly GCHandle root;
    private readonly Task lifecycle;
    private readonly Action<string> status;
    public Task<uint> Registered { get; private set; } = Task.FromResult(uint.MaxValue);
    public Task CompletionTask => lifecycle;
    private static TaskCompletionSource<uint> NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DiscoveryRegistration(int port, string serverId, string fingerprint, Action<string> status)
    {
        this.status = status;
        var host = new string(Environment.MachineName.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(40).ToArray());
        if (host.Length == 0) host = "Windows";
        instance = DnsServiceConstructInstance($"PausePal-{host}._lookpause._tcp.local", host + ".local", 0, 0,
            checked((ushort)port), 0, 0, 3, ["v", "id", "fp"], ["1", serverId, fingerprint]);
        if (instance == 0) throw new InvalidOperationException("无法创建局域网发现记录");
        request = Marshal.AllocHGlobal(Marshal.SizeOf<Request>());
        cancel = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(cancel, 0);
        root = GCHandle.Alloc(this);
        Marshal.StructureToPtr(new Request { Version = 1, Instance = instance,
            Callback = Marshal.GetFunctionPointerForDelegate(Callback), Context = GCHandle.ToIntPtr(root) }, request, false);
        lifecycle = RunAsync();
    }
    private async Task RunAsync()
    {
        try
        {
            var result = DnsServiceRegister(request, cancel);
            if (result != Pending) operation.TrySetResult(result);
            Registered = operation.Task;
            result = await operation.Task;
            status(result == 0 ? "局域网服务已注册，等待手机连接" : $"局域网发现注册失败（{result}）");
            if (result != 0) return;
            await stop.Task;
            operation = NewCompletion();
            result = DnsServiceDeRegister(request, 0);
            if (result != Pending) operation.TrySetResult(result);
            await operation.Task;
        }
        finally
        {
            DnsServiceFreeInstance(instance); instance = 0;
            Marshal.FreeHGlobal(request); Marshal.FreeHGlobal(cancel); root.Free();
        }
    }
    private static void Completed(uint result, nint context, nint returnedInstance)
    {
        var owner = (DiscoveryRegistration)GCHandle.FromIntPtr(context).Target!;
        // API 返回的实例拷贝归调用方；原请求实例保留到反注册结束。
        if (returnedInstance != 0) DnsServiceFreeInstance(returnedInstance);
        owner.operation.TrySetResult(result);
    }
    public async ValueTask DisposeAsync()
    {
        stop.TrySetResult();
        // 不在异步回调释放 cancel 的同时调用取消；完成后会立即反注册。
        try { await lifecycle.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { /* 参数继续由 GCHandle 保活；回调到达才释放，进程结束系统撤销。 */ }
    }
    [DllImport("dnsapi.dll", CharSet = CharSet.Unicode)] private static extern nint DnsServiceConstructInstance(
        string name, string host, nint ip4, nint ip6, ushort port, ushort priority, ushort weight, uint count,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 7)] string[] keys,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 7)] string[] values);
    [DllImport("dnsapi.dll")] private static extern uint DnsServiceRegister(nint request, nint cancel);
    [DllImport("dnsapi.dll")] private static extern uint DnsServiceDeRegister(nint request, nint cancel);
    [DllImport("dnsapi.dll")] private static extern void DnsServiceFreeInstance(nint instance);
}
