using PausePal.Windows;
using System.IO;

if (args is ["--native-discovery"]) { TestModes.NativeDiscovery().GetAwaiter().GetResult(); return; }
if (args is ["--ui-preview", var directory]) { TestModes.UiPreview(directory); return; }

var checks = new Dictionary<string, Action>
{
    ["开始不播放"] = () => { var (e,s)=Setup(); s.Start(); s.Receive(WatchingIntent.ATTENDING); Equal(0,e.Actions.Count); },
    ["初始手动暂停不恢复"] = () => { var (e,s)=Setup(Playback.Paused); s.Start(); s.Receive(WatchingIntent.AWAY); s.Receive(WatchingIntent.ATTENDING); Equal(0,e.Actions.Count); },
    ["标签播放但进度不动不操作"] = () => { var(e,s)=Setup(); s.Start(); s.Receive(WatchingIntent.AWAY); e.Now=1500; s.Tick(); e.Now=3100;s.Tick();Equal(0,e.Actions.Count); },
    ["未解释进度跳变不操作"] = () => { var(e,s)=Setup();s.Start();s.Receive(WatchingIntent.AWAY);e.Frame=e.Frame with {Position=70};e.Now=200;s.Tick();Equal(0,e.Actions.Count); },
    ["未知期间保留归属并回看恢复"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.UNKNOWN);e.Step(s,500);Equal(MediaPhase.Owned,s.Phase);s.Receive(WatchingIntent.ATTENDING);e.Step(s,500);Equal(2,e.Actions.Count);Equal(Playback.Playing,e.Frame.State); },
    ["未知不延长暂停确认"] = () => { var(e,s)=Setup();e.Apply=false;s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,1000);s.Receive(WatchingIntent.UNKNOWN);e.Step(s,1200);e.Step(s,1200);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count);Equal(MediaPhase.Idle,s.Phase); },
    ["待核验期间未知不授予资格"] = () => { var(e,s)=Setup();s.Start();s.Receive(WatchingIntent.AWAY);s.Receive(WatchingIntent.UNKNOWN);e.Step(s,500);s.Receive(WatchingIntent.ATTENDING);Equal(0,e.Actions.Count); },
    ["键鼠接管后回看不播放"] = () => { var(e,s)=Owned();e.Epoch++;e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["切前台后回看不播放"] = () => { var(e,s)=Owned();e.Foreground=2;e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["换媒体后回看不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Identity="B"};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["改暂停位置后不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Position=40};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["改媒体范围后不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Maximum=700};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["停止后迟到回看不播放"] = () => { var(e,s)=Owned();s.Stop();s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["重连开始不继承归属"] = () => { var(e,s)=Owned();s.Stop();s.Start();s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["输入监听失效禁止恢复"] = () => { var(e,s)=Owned();e.Healthy=false;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["恢复前采样中接管禁止动作"] = () => { var(e,s)=Owned();e.OnRead=()=>e.Epoch++;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["已结束禁止恢复"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Position=634};s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["重复回看不重复播放"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.ATTENDING);e.Step(s,500);s.Receive(WatchingIntent.ATTENDING);Equal(2,e.Actions.Count); },
    ["确认恢复后回退一次且重复回看不补发"] = () => { var(e,s)=Owned();Equal(0,e.Rewinds);s.Receive(WatchingIntent.ATTENDING);Equal(0,e.Rewinds);e.Step(s,500);Equal(1,e.Rewinds);s.Receive(WatchingIntent.ATTENDING);e.Step(s,500);Equal(1,e.Rewinds); },
    ["恢复中停止不回退"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.ATTENDING);s.Stop();e.Step(s,500);Equal(0,e.Rewinds); },
    ["停止入口立即撤销进行中的观察"] = () => { var(e,s)=Owned();var inbox=new IntentInbox(e);e.OnRead=()=>inbox.Post("STOP");s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["出队后收到停止不重新授予旧意图"] = () => { var(e,s)=Setup();var inbox=new IntentInbox(e);s.Start();inbox.Post("AWAY");True(inbox.TryRead(out var pending));inbox.Post("STOP");s.Receive(WatchingIntent.AWAY,pending.Epoch);e.Step(s,1000);Equal(0,e.Actions.Count); },
    ["断线入口撤销进行中的观察"] = () => { var(e,s)=Owned();var inbox=new IntentInbox(e);e.OnRead=()=>inbox.Post("DISCONNECT");s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["输入流结束立即撤销"] = () => { var(e,s)=Owned();var inbox=new IntentInbox(e);e.OnRead=inbox.Complete;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["队列溢出拒绝旧命令"] = () => { var(e,s)=Setup();var inbox=new IntentInbox(e);for(var i=0;i<17;i++)inbox.Post("ATTENDING");True(inbox.Closed);True(!inbox.TryRead(out _)); },
    ["恢复观察返回过晚不操作"] = () => { var(e,s)=Owned();e.OnRead=()=>e.Now+=1600;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["长期未观察直接回看不操作"] = () => { var(e,s)=Owned();e.Now+=2500;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["最终执行准备期间撤销不操作"] = () => { var(e,s)=Owned();e.OnInvokePreparation=()=>e.Epoch++;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["监听心跳中断后不能自行恢复"] = () => { var invalidations=0;var heartbeat=new InputHeartbeat(0,()=>invalidations++);True(heartbeat.Healthy(100));heartbeat.Pulse(1200);True(!heartbeat.Healthy(1200));heartbeat.Pulse(1300);True(!heartbeat.Healthy(1300));Equal(1,invalidations); },
    ["监听失效先于新心跳时间发布"] = () => { InputHeartbeat? heartbeat=null;heartbeat=new InputHeartbeat(0,()=>{Equal(0L,heartbeat!.LastPulse);True(!heartbeat.Healthy(1200));});heartbeat.Pulse(1200);Equal(1200L,heartbeat.LastPulse); },
    ["恢复中再次侧看重新确认暂停"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.ATTENDING);s.Receive(WatchingIntent.AWAY);e.Step(s,500);Equal(MediaPhase.Checking,s.Phase);e.Step(s,1000);e.Step(s,200);Equal(3,e.Actions.Count);Equal(MediaPhase.Owned,s.Phase); },
    ["普通页面公共祖先不是媒体区域"] = () => { True(!UiMedia.HasMediaSemantics("Page content group"));True(UiMedia.HasMediaSemantics("Video Player"));True(!UiMedia.HasMediaSemantics("Play controls")); },
    ["时间与媒体语义解析"] = () => { True(MediaClock.TryParse("0 Minutes 37 Seconds of 10 Minutes 34 Seconds",out var p,out var d));Equal(37d,p);Equal(634d,d);True(!MediaClock.TryParse("1:70 / 10:00",out _,out _));Equal(Playback.Unknown,UiMedia.Semantics("PausePal"));Equal(Playback.Paused,UiMedia.Semantics("Play keyboard shortcut k")); }
};
foreach (var check in ProtocolTests.Checks()) checks.Add(check.Key, check.Value);
var passed=0;
foreach(var(name,run) in checks)
{
    var output = Console.Out;
    try { Console.SetOut(TextWriter.Null); run();Console.SetOut(output);Console.WriteLine($"通过：{name}");passed++; }
    catch(Exception error) { Console.Error.WriteLine($"失败：{name}；{error.Message}");Environment.ExitCode=1; }
    finally { Console.SetOut(output); }
}
Console.WriteLine($"安全回归：{passed}/{checks.Count} 通过；不代表真实播放器或手机通过。");

static (FakeEnvironment,MediaSession) Setup(Playback state=Playback.Playing)
{
    var env=new FakeEnvironment {Frame=new UiMedia(1,1,"A",null!,null!,state,20,0,634)};
    return(env,new MediaSession(env));
}
static (FakeEnvironment,MediaSession) Owned()
{
    var(e,s)=Setup();s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,1000);e.Step(s,200);Equal(MediaPhase.Owned,s.Phase);return(e,s);
}
static void True(bool value) { if(!value)throw new Exception("断言不成立"); }
static void Equal<T>(T expected,T actual) { if(!Equals(expected,actual))throw new Exception($"期望{expected}，实际{actual}"); }

sealed class FakeEnvironment:IMediaEnvironment
{
    public long Now{get;set;}
    public long Epoch{get;set;}
    public bool Healthy{get;set;}=true;
    public nint Foreground{get;set;}=1;
    public required UiMedia Frame{get;set;}
    public bool Apply{get;set;}=true;
    public Action? OnRead{get;set;}
    public Action? OnInvokePreparation{get;set;}
    public List<Playback> Actions{get;}=[];
    public int Rewinds { get; private set; }
    public RewindResult Rewind(UiMedia media,long epoch) { if (Healthy && Epoch == epoch) { Rewinds++; return RewindResult.Attempted; } return RewindResult.Skipped; }
    public void Revoke()=>Epoch++;
    public UiMedia Observe(){var started=Now;OnRead?.Invoke();return Frame with {ObservedAt=started};}
    public void Invoke(UiMedia media,Playback expected,long epoch)
    {
        OnInvokePreparation?.Invoke();
        if(!Healthy||Epoch!=epoch||Frame.State!=expected)throw new Exception("执行边界失效");
        Actions.Add(expected);
        if(Apply)Frame=Frame with {State=expected==Playback.Playing?Playback.Paused:Playback.Playing};
    }
    public void Step(MediaSession session,int milliseconds)
    {
        Now+=milliseconds;
        if(Frame.State==Playback.Playing)Frame=Frame with {Position=Frame.Position+milliseconds/1000d};
        session.Tick();
    }
}
