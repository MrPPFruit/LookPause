using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace PausePal.Windows;

internal static class MediaRewind
{
    // 不提供网络参数。只允许已确认自动恢复后的同一目标调用一次。
    public static RewindResult Try(UiMedia target, InputWatch input, long epoch)
    {
        try
        {
            if (!Valid(target, input, epoch)) return RewindResult.Skipped;
            var fresh = UiMedia.Read();
            if (!target.SameTarget(fresh) || fresh.State != Playback.Playing || !fresh.BeforeEnd || !Valid(fresh, input, epoch)) return RewindResult.Skipped;
            var range = (RangeValuePattern)fresh.Timeline.GetCurrentPattern(RangeValuePattern.Pattern);
            // 网页 ARIA 可写不证明实现了 seek；有 Document 祖先时只走已批准的固定 Left。
            if (NativeTimeline(fresh.Timeline) && !range.Current.IsReadOnly)
            {
                var value = range.Current;
                if (!double.IsFinite(value.Value) || value.Value <= value.Minimum || value.Minimum != fresh.Minimum || value.Maximum != fresh.Maximum)
                    return RewindResult.Skipped;
                if (!Valid(fresh, input, epoch)) return RewindResult.Skipped;
                // 原生写入一旦提交，异常也不补 Left。
                try { range.SetValue(Math.Max(value.Minimum, value.Value - 5)); } catch { }
                return RewindResult.Attempted;
            }
            if (!NoModifiers() || !SafeFocus(fresh.Control, fresh.Window)) return RewindResult.Skipped;
            if (!Automation.Compare(AutomationElement.FocusedElement, fresh.Control))
            {
                if (!Valid(fresh, input, epoch) || !NoModifiers()) return RewindResult.Skipped;
                fresh.Control.SetFocus();
                if (!Automation.Compare(AutomationElement.FocusedElement, fresh.Control)) return RewindResult.Skipped;
            }
            // 准备完成后再做最终边界核验，不能将焦点检查耗时从采样年龄中扣除。
            var events = new[] { Key(0), Key(2) };
            if (!Automation.Compare(AutomationElement.FocusedElement, fresh.Control)) return RewindResult.Skipped;
            if (!NoModifiers() || !Valid(fresh, input, epoch)) return RewindResult.Skipped;
            var inserted = SendInput(2, events, Marshal.SizeOf<NativeInput>());
            // 只做按键释放清理，不重试按下或回退操作。
            if (inserted == 1) SendInput(1, [Key(2)], Marshal.SizeOf<NativeInput>());
            return RewindResult.Attempted;
        }
        catch { return RewindResult.Skipped; }
    }
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
        return focusedDoc != null && controlDoc != null && Automation.Compare(focusedDoc, controlDoc);
    }
    private static AutomationElement? Document(AutomationElement element, nint window, bool rejectEditing)
    {
        AutomationElement? document = null;
        for (var i = 0; i < 35; i++)
        {
            var current = element.Current;
            if (rejectEditing && (current.ControlType == ControlType.Edit || current.ControlType == ControlType.ComboBox ||
                current.IsPassword || element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && !((ValuePattern)value).Current.IsReadOnly)) return null;
            if (current.ControlType == ControlType.Document) document = element;
            if (current.NativeWindowHandle == window) return document;
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
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
