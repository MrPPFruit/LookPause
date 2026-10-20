using System.Runtime.InteropServices;

namespace PausePal.Windows;

// 钩子只递增代次，不保存按键、鼠标坐标或窗口标题。UI 消息线程不得执行 UIA。
internal sealed class InputWatch : IDisposable, IRevocation
{
    internal static readonly nuint OwnRewindTag = (nuint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardEvent { public uint Key, Scan, Flags, Time; public nuint Extra; }
    private delegate nint Hook(int code, nint message, nint data);
    private delegate void WindowEvent(nint hook, uint kind, nint window, int objectId, int childId, uint thread, uint time);
    private readonly Hook keyboard;
    private readonly Hook mouse;
    private readonly WindowEvent foreground;
    private readonly nint keyboardHook;
    private readonly nint mouseHook;
    private readonly nint windowHook;
    private readonly System.Windows.Forms.Timer pump;
    private long epoch;
    private long keyboardEvents, mouseEvents, foregroundEvents, externalRevocations, heartbeatFailures;
    private long sameForegroundEvents, differentForegroundEvents, injectedKeyboardEvents, injectedMouseEvents;
    private nint lastForeground = Program.GetForegroundWindow();
    private int disposed;
    private readonly InputHeartbeat heartbeat;
    public long Epoch => Interlocked.Read(ref epoch);
    public bool Healthy => Volatile.Read(ref disposed) == 0 && keyboardHook != 0 && mouseHook != 0 && windowHook != 0 &&
                           heartbeat.Healthy(Environment.TickCount64);
    public void Revoke() { Interlocked.Increment(ref externalRevocations); Interlocked.Increment(ref epoch); }
    internal object Diagnostics => new { keyboard = Interlocked.Read(ref keyboardEvents), mouse = Interlocked.Read(ref mouseEvents),
        foreground = Interlocked.Read(ref foregroundEvents), external = Interlocked.Read(ref externalRevocations),
        sameForeground = Interlocked.Read(ref sameForegroundEvents), differentForeground = Interlocked.Read(ref differentForegroundEvents),
        injectedKeyboard = Interlocked.Read(ref injectedKeyboardEvents), injectedMouse = Interlocked.Read(ref injectedMouseEvents),
        heartbeatFailures = Interlocked.Read(ref heartbeatFailures), heartbeatAgeMs = Environment.TickCount64 - heartbeat.LastPulse,
        keyboardInstalled = keyboardHook != 0, mouseInstalled = mouseHook != 0, foregroundInstalled = windowHook != 0 };

    public InputWatch()
    {
        heartbeat = new InputHeartbeat(Environment.TickCount64, () => { Interlocked.Increment(ref heartbeatFailures); Interlocked.Increment(ref epoch); });
        keyboard = (code, message, data) =>
        {
            if (code >= 0)
            {
                var item = Marshal.PtrToStructure<KeyboardEvent>(data);
                // 只排除本进程带随机标记的一次 Left；不忽略其他注入事件。
                if (!(item.Key == 0x25 && (item.Flags & 0x10) != 0 && item.Extra == OwnRewindTag))
                { if ((item.Flags & 0x10) != 0) Interlocked.Increment(ref injectedKeyboardEvents);
                    Interlocked.Increment(ref keyboardEvents); Interlocked.Increment(ref epoch); }
            }
            return CallNextHookEx(0, code, message, data);
        };
        mouse = (code, message, data) =>
        {
            // 纯移动不撤销；所有来源的点击、滚轮和按键都撤销。
            if (code >= 0 && message != 0x0200)
            { if ((Marshal.ReadInt32(data, 12) & 1) != 0) Interlocked.Increment(ref injectedMouseEvents);
                Interlocked.Increment(ref mouseEvents); Interlocked.Increment(ref epoch); }
            return CallNextHookEx(0, code, message, data);
        };
        foreground = (hook, kind, window, obj, child, thread, time) =>
        {
            if (Interlocked.Exchange(ref lastForeground, window) == window) Interlocked.Increment(ref sameForegroundEvents);
            else Interlocked.Increment(ref differentForegroundEvents);
            Interlocked.Increment(ref foregroundEvents); Interlocked.Increment(ref epoch);
        };
        keyboardHook = SetWindowsHookEx(13, keyboard, GetModuleHandle(null), 0);
        mouseHook = SetWindowsHookEx(14, mouse, GetModuleHandle(null), 0);
        windowHook = SetWinEventHook(3, 3, 0, foreground, 0, 0, 0);
        pump = new System.Windows.Forms.Timer { Interval = 100 };
        pump.Tick += (_, _) => heartbeat.Pulse(Environment.TickCount64);
        pump.Start();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Revoke();
        pump.Dispose();
        if (keyboardHook != 0) UnhookWindowsHookEx(keyboardHook);
        if (mouseHook != 0) UnhookWindowsHookEx(mouseHook);
        if (windowHook != 0) UnhookWinEvent(windowHook);
        GC.KeepAlive(keyboard);
        GC.KeepAlive(mouse);
        GC.KeepAlive(foreground);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, Hook proc, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WindowEvent proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
}
