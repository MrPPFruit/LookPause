namespace PausePal.Windows;

internal sealed record ShortcutBoundary(nint Window, uint Process, string Context, long ObservedAt);
internal interface IShortcutEnvironment : IRevocation
{
    long Now { get; }
    bool Healthy { get; }
    ShortcutBoundary Observe();
    bool SendSpace(ShortcutBoundary boundary, long epoch);
}

internal sealed class ShortcutSession(IShortcutEnvironment environment)
{
    public event Action<string>? Changed;
    private bool active, pending;
    private ShortcutBoundary? target;
    private long epoch, lastObservation;
    public void Start()
    {
        target=null; pending=false; active=true;
        Changed?.Invoke("快捷键兼容：请确保视频播放且播放器有焦点，效果不验证");
    }
    public void Stop() { active=false; pending=false; target=null; }
    private void Disarm()
    {
        Stop(); Changed?.Invoke("快捷键兼容已解除，请确认播放与焦点后重新开始观影");
    }
    private bool Valid(long expected) => active && environment.Healthy && environment.Epoch==expected;
    private bool Fresh(ShortcutBoundary b) => environment.Now>=b.ObservedAt && environment.Now-b.ObservedAt<=1500;
    private static bool Same(ShortcutBoundary a,ShortcutBoundary b) => a.Window==b.Window && a.Process==b.Process && a.Context==b.Context;
    private bool Check()
    {
        if(target==null)return false;
        if(!Valid(epoch) || environment.Now-lastObservation>2000) return false;
        var current=environment.Observe();
        if(!Valid(epoch) || !Fresh(current) || environment.Now-lastObservation>2000 || !Same(target,current))return false;
        lastObservation=environment.Now; target=current; return true;
    }
    public void Receive(WatchingIntent intent,long receivedEpoch)
    {
        if(!active)return;
        try
        {
            if(target!=null && !Check()){Disarm();return;}
            if(!Valid(receivedEpoch))return;
            if(intent==WatchingIntent.UNKNOWN)return;
            if(intent is WatchingIntent.AWAY or WatchingIntent.NO_FACE)
            {
                if(pending)return;
                if(target==null)
                {
                    epoch=receivedEpoch;
                    var observed=environment.Observe();
                    if(!Valid(epoch)||!Fresh(observed)){Disarm();return;}
                    target=observed;lastObservation=environment.Now;
                }
                if(!environment.SendSpace(target,epoch)){Disarm();return;}
                pending=true;
                Changed?.Invoke("快捷键兼容：离开空格已发送，效果未确认");
            }
            else if(intent==WatchingIntent.ATTENDING && pending)
            {
                pending=false; // 先消费，再提交；失败也不能补发。
                if(!environment.SendSpace(target!,epoch)){Disarm();return;}
                Changed?.Invoke("快捷键兼容：回看空格已发送，效果未确认；不回退");
            }
        }
        catch { Disarm(); }
    }
    public void Tick()
    {
        if(!active||target==null)return;
        try {if(!Check())Disarm();}catch{Disarm();}
    }
}
