namespace PausePal.Windows;

// 事件可能来自顶层窗口内部的子 HWND；所有调用方先统一为 GA_ROOT。
internal sealed class ForegroundIdentity(long currentRoot)
{
    private long lastRoot = currentRoot;

    internal bool Observe(long eventRoot, long actualRoot)
    {
        var unchanged = eventRoot != 0 && eventRoot == actualRoot && actualRoot == lastRoot;
        // 异步迟到事件仍撤销，但不能把旧事件窗口变成下一次比较的实际前台。
        lastRoot = actualRoot;
        return !unchanged;
    }
}
