using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace PausePal.Windows;

internal static class MediaRewind
{
    // 不提供网络参数。只允许已确认自动恢复后的同一目标调用一次。
    public static RewindResult Try(UiMedia target, InputWatch input, long epoch)
    {
        try
        {
            if (!Valid(target, input, epoch)) return Skip("initial-boundary");
            var fresh = UiMedia.Read();
            if (!MatchesPlayingTarget(target, fresh) || !Valid(fresh, input, epoch)) return Skip("fresh-target");
            // 网页 ARIA 可写不证明实现了 seek；有 Document 祖先时只走已批准的固定 Left。
            if (fresh.Timeline != null && NativeTimeline(fresh.Timeline) &&
                fresh.Timeline.TryGetCurrentPattern(RangeValuePattern.Pattern, out var pattern) && !((RangeValuePattern)pattern).Current.IsReadOnly)
            {
                var range = (RangeValuePattern)pattern;
                var value = range.Current;
                if (!double.IsFinite(value.Value) || value.Value <= value.Minimum || value.Minimum != fresh.Minimum || value.Maximum != fresh.Maximum ||
                    Math.Abs(value.Value - fresh.Position) > (fresh.Rounded ? 1 : 0.05))
                    return Skip("native-range");
                if (!Valid(fresh, input, epoch)) return Skip("native-boundary");
                // 原生写入一旦提交，异常也不补 Left。
                try
                {
                    range.SetValue(Math.Max(value.Minimum, value.Value - 5));
                    Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "native-submitted" }));
                }
                catch (Exception error)
                { Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "native-error", error = error.GetType().Name })); }
                return RewindResult.Attempted;
            }
            // 使用已验证媒体区域的键盘承载面，不把焦点留在有独立键盘语义的滑块上。
            var keyTarget = fresh.KeyboardSurface;
            if (keyTarget == null || !keyTarget.Current.IsKeyboardFocusable || !keyTarget.Current.IsEnabled ||
                keyTarget.Current.IsOffscreen || keyTarget.Current.BoundingRectangle.IsEmpty)
                return Skip("media-focus-unavailable");
            if (!NoModifiers() || !SafeFocus(keyTarget, fresh.Window)) return Skip("focus-or-modifiers");
            var changedFocus = !Automation.Compare(AutomationElement.FocusedElement, keyTarget);
            if (changedFocus)
            {
                if (!Valid(fresh, input, epoch) || !NoModifiers()) return Skip("before-focus");
                keyTarget.SetFocus();
            }
            // UIA聚焦请求可异步完成；等待确切焦点，沿用原采样预算，不补发请求。
            if (!WaitUntilReady(() => Valid(fresh, input, epoch) && NoModifiers(),
                () => Automation.Compare(AutomationElement.FocusedElement, keyTarget), () => Thread.Sleep(20)))
                return Skip("focus-not-ready");
            var afterFocus = UiMedia.Read();
            if (!MatchesPlayingTarget(fresh, afterFocus) || afterFocus.KeyboardSurface == null ||
                !Automation.Compare(afterFocus.KeyboardSurface, keyTarget) || !Valid(fresh, input, epoch))
                return Skip("after-focus-target");
            var focusState = keyTarget.Current;
            if (!focusState.IsKeyboardFocusable || !focusState.IsEnabled || focusState.IsOffscreen || focusState.BoundingRectangle.IsEmpty)
                return Skip("after-focus-unavailable");
            // 准备完成后再做最终边界核验，不能将焦点检查耗时从采样年龄中扣除。
            var events = new[] { Key(0), Key(2) };
            if (!Automation.Compare(AutomationElement.FocusedElement, keyTarget)) return Skip("focus-changed");
            if (!NoModifiers() || !Valid(fresh, input, epoch)) return Skip("final-boundary");
            var inserted = SendInput(2, events, Marshal.SizeOf<NativeInput>());
            var sendError = inserted < 2 ? Marshal.GetLastWin32Error() : 0;
            uint? released = null;
            var releaseError = 0;
            // 只做按键释放清理，不重试按下或回退操作。
            if (inserted == 1)
            {
                released = SendInput(1, [Key(2)], Marshal.SizeOf<NativeInput>());
                releaseError = released < 1 ? Marshal.GetLastWin32Error() : 0;
            }
            // 诊断在释放清理之后，避免日志异常跳过清理。
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "keyboard-submitted", inserted, error = sendError,
                inputSize = Marshal.SizeOf<NativeInput>(), key = 0x25, scan = events[0].Data.Keyboard.Scan,
                downFlags = events[0].Data.Keyboard.Flags, upFlags = events[1].Data.Keyboard.Flags, changedFocus }));
            if (released != null)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "release-cleanup", inserted = released, error = releaseError }));
            }
            return RewindResult.Attempted;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "error", error = error.GetType().Name }));
            return RewindResult.Skipped;
        }
    }
    internal static bool WaitUntilReady(Func<bool> valid, Func<bool> ready, Action yield)
    {
        while (valid())
        {
            var observedReady = ready();
            if (!valid()) return false;
            if (observedReady) return true;
            yield();
        }
        return false;
    }
    private static RewindResult Skip(string reason)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "rewind", stage = "skipped", reason }));
        return RewindResult.Skipped;
    }
    internal static bool MatchesPlayingTarget(UiMedia target, UiMedia fresh) => target.SameTarget(fresh) &&
        target.SameTimeline(fresh) && fresh.State == Playback.Playing && fresh.BeforeEnd;

    private static bool Valid(UiMedia media, InputWatch input, long epoch) => Program.GetForegroundWindow() == media.Window &&
        Environment.TickCount64 - media.ObservedAt <= 1500 && input.Healthy && input.Epoch == epoch;
    private static bool NativeTimeline(AutomationElement element)
    {
        for (var i = 0; i < 35; i++)
        {
            var type = element.Current.ControlType;
            if (type == ControlType.Document) return false;
            if (type == ControlType.Window) return true;
            element = TreeWalker.ControlViewWalker.GetParent(element);
            if (element == null) return false;
        }
        return false;
    }
    private static bool SafeFocus(AutomationElement control, nint window)
    {
        var focused = AutomationElement.FocusedElement;
        if (focused == null) return false;
        // 不从搜索框、地址栏、编辑框抢焦点；焦点必须属于同一前台文档。
        var focusedDoc = Document(focused, window, rejectEditing: true);
        var controlDoc = Document(control, window, rejectEditing: false);
        return focusedDoc != null && controlDoc != null && MediaCluster.SameDocumentBranch(focusedDoc, controlDoc);
    }
    private static string[]? Document(AutomationElement element, nint window, bool rejectEditing)
    {
        var documents = new List<string>();
        for (var i = 0; i < 35; i++)
        {
            var current = element.Current;
            if (rejectEditing && (current.ControlType == ControlType.Edit || current.ControlType == ControlType.ComboBox ||
                current.IsPassword || element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && !((ValuePattern)value).Current.IsReadOnly)) return null;
            if (current.ControlType == ControlType.Document) documents.Add(string.Join(".", element.GetRuntimeId()));
            if (current.NativeWindowHandle == window) return documents.Count > 0 ? [.. documents] : null;
            element = TreeWalker.ControlViewWalker.GetParent(element);
            if (element == null) return null;
        }
        return null;
    }
    private static bool NoModifiers() => new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.All(k => (GetAsyncKeyState(k) & 0x8000) == 0);
    private static NativeInput Key(uint flags) => new() { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = 0x25, Flags = flags, Extra = InputWatch.OwnRewindTag } } };
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
