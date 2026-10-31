using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Automation;

namespace PausePal.Windows;

// 不读取媒体控件或播放状态，只核验输入能否留在用户已经聚焦的非编辑文档。
internal sealed class ShortcutEnvironment(InputWatch input) : IShortcutEnvironment
{
    public long Now=>Environment.TickCount64;
    public long Epoch=>input.Epoch;
    public bool Healthy=>input.Healthy;
    public void Revoke()=>input.Revoke();
    private static bool NoModifiers()=>new[]{0x10,0x11,0x12,0x5b,0x5c}.All(k=>(GetAsyncKeyState(k)&0x8000)==0);
    public ShortcutBoundary Observe()
    {
        var started=Now;
        var window=Program.GetForegroundWindow();
        if(window==0||!Healthy||!NoModifiers())throw new InvalidOperationException();
        Program.GetWindowThreadProcessId(window,out var pid);
        var focus=AutomationElement.FocusedElement??throw new InvalidOperationException();
        var node=focus;var ids=new List<string>();var documents=0;var reached=false;
        for(var depth=0;depth<35;depth++)
        {
            if(Now-started>1500)throw new InvalidOperationException();
            var c=node.Current;
            if(c.ControlType==ControlType.Edit||c.ControlType==ControlType.ComboBox||c.IsPassword ||
                node.TryGetCurrentPattern(ValuePattern.Pattern,out var value)&&!((ValuePattern)value).Current.IsReadOnly)
                throw new InvalidOperationException();
            if(depth==0&&(!c.IsEnabled||c.IsOffscreen||c.BoundingRectangle.IsEmpty))throw new InvalidOperationException();
            ids.Add(string.Join(".",node.GetRuntimeId()));
            if(c.ControlType==ControlType.Document)documents++;
            if(c.NativeWindowHandle==window){reached=true;break;}
            node=TreeWalker.ControlViewWalker.GetParent(node)??throw new InvalidOperationException();
        }
        Program.GetWindowThreadProcessId(Program.GetForegroundWindow(),out var finalPid);
        if(!reached||documents==0||pid==0||pid!=finalPid||Program.GetForegroundWindow()!=window||
            !Automation.Compare(focus,AutomationElement.FocusedElement)||!Healthy||!NoModifiers()||Now-started>1500)
            throw new InvalidOperationException();
        return new(window,pid,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("/",ids)))),started);
    }
    public bool SendSpace(ShortcutBoundary boundary,long epoch)
    {
        var current=Observe();
        bool Valid()=>Healthy&&Epoch==epoch&&NoModifiers()&&Program.GetForegroundWindow()==boundary.Window&&
            Now>=boundary.ObservedAt&&Now-boundary.ObservedAt<=1500;
        if(!Valid()||boundary.Window!=current.Window||boundary.Process!=current.Process||boundary.Context!=current.Context)return false;
        var events=new[]{Key(0),Key(2)};
        if(!Valid())return false;
        var inserted=SendInput(2,events,Marshal.SizeOf<NativeInput>());
        var error=inserted==2?0:Marshal.GetLastWin32Error();
        if(inserted==1)SendInput(1,[Key(2)],Marshal.SizeOf<NativeInput>());
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{kind="shortcut-submitted",inserted,error,effect="unconfirmed"}));
        return inserted==2 && Healthy && Epoch==epoch;
    }
    private static NativeInput Key(uint flags)=>new(){Type=1,Data=new(){Keyboard=new(){Key=0x20,Flags=flags,Extra=InputWatch.OwnRewindTag}}};
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput{public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    { [FieldOffset(0)]public KeyboardInput Keyboard;[FieldOffset(0)]public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput{public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,NativeInput[] inputs,int size);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
}
