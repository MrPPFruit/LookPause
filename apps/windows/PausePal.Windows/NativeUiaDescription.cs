using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace PausePal.Windows;

// .NET托管UIA未暴露FullDescription；只读标准COM属性，按同一RuntimeId关联。
internal static class NativeUiaDescription
{
    internal static string? Read(AutomationElement element, nint window, int[] runtimeId)
    {
        nint client = 0, root = 0, condition = 0, target = 0;
        var initialized = false;
        try
        {
            if (window == 0 || Program.GetForegroundWindow() != window) return null;
            if (runtimeId.Length == 0 || !runtimeId.SequenceEqual(element.GetRuntimeId())) return null;
            var init = CoInitializeEx(0, 0);
            initialized = init >= 0;
            if (init < 0 && init != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(init);
            var clsid = new Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E");
            var iid = new Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, 0, 1, ref iid, out client));
            if (client == 0) return null;
            // 槽位来自Windows SDK UIAutomationClient.idl，包含IUnknown的前三槽。
            Marshal.ThrowExceptionForHR(Method<FromHandle>(client, 6)(client, window, out root));
            if (root == 0) return null;
            // int[]按VT_ARRAY|VT_I4传入；不按名称或坐标寻找替代节点。
            Marshal.ThrowExceptionForHR(Method<PropertyCondition>(client, 23)(client, 30000, runtimeId, out condition));
            if (condition == 0) return null;
            Marshal.ThrowExceptionForHR(Method<FindFirst>(root, 5)(root, 4, condition, out target));
            if (target == 0) return null;
            var read = Method<PropertyValue>(target, 10);
            Marshal.ThrowExceptionForHR(read(target, 30000, out var identity));
            if (identity is not int[] actualId || !runtimeId.SequenceEqual(actualId)) return null;
            Marshal.ThrowExceptionForHR(read(target, 30159, out var description));
            if (Program.GetForegroundWindow() != window || !runtimeId.SequenceEqual(element.GetRuntimeId())) return null;
            return description is string text && text.Length <= 512 ? text : null;
        }
        catch { return null; }
        finally
        {
            // 只释放本次取得的COM引用；VARIANT/SAFEARRAY由CLR互操作封送释放。
            foreach (var pointer in new[] { target, condition, root, client }) if (pointer != 0) Marshal.Release(pointer);
            if (initialized) CoUninitialize();
        }
    }

    private static T Method<T>(nint pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * nint.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FromHandle(nint self, nint window, out nint element);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PropertyCondition(nint self, int property,
        [MarshalAs(UnmanagedType.Struct)] object value, out nint condition);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FindFirst(nint self, int scope, nint condition, out nint element);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PropertyValue(nint self, int property,
        [MarshalAs(UnmanagedType.Struct)] out object value);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, out nint result);
}
