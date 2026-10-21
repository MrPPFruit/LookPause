using PausePal.Windows;
using System.IO;

if (args is ["--native-discovery"]) { TestModes.NativeDiscovery().GetAwaiter().GetResult(); return; }
if (args is ["--ui-preview", var directory]) { TestModes.UiPreview(directory); return; }

var checks = new Dictionary<string, Action>
{
    ["开始不播放"] = () => { var (e,s)=Setup(); s.Start(); s.Receive(WatchingIntent.ATTENDING); Equal(0,e.Actions.Count); },
    ["初始手动暂停不恢复"] = () => { var (e,s)=Setup(Playback.Paused); s.Start(); s.Receive(WatchingIntent.AWAY); s.Receive(WatchingIntent.ATTENDING); Equal(0,e.Actions.Count); },
    ["明确播放状态无需等待进度增长"] = () => { var(e,s)=Setup();e.FreezeProgress=true;s.Start();s.Receive(WatchingIntent.AWAY);Equal(1,e.Actions.Count);e.Step(s,200);Equal(MediaPhase.Owned,s.Phase);s.Receive(WatchingIntent.ATTENDING);e.Step(s,200);Equal(2,e.Actions.Count);Equal(MediaPhase.Idle,s.Phase); },
    ["未知媒体状态不盲目操作"] = () => { var(e,s)=Setup(Playback.Unknown);s.Start();s.Receive(WatchingIntent.AWAY);s.Receive(WatchingIntent.ATTENDING);Equal(0,e.Actions.Count);True(s.Status.Contains("不明确")); },
    ["未知期间保留归属并回看恢复"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.UNKNOWN);e.Step(s,500);Equal(MediaPhase.Owned,s.Phase);s.Receive(WatchingIntent.ATTENDING);e.Step(s,500);Equal(2,e.Actions.Count);Equal(Playback.Playing,e.Frame.State); },
    ["未知不延长暂停确认"] = () => { var(e,s)=Setup();e.Apply=false;s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,1000);s.Receive(WatchingIntent.UNKNOWN);e.Step(s,1200);e.Step(s,1200);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count);Equal(MediaPhase.Idle,s.Phase); },
    ["暂停待确认期间未知不播放且继续确认"] = () => { var(e,s)=Setup();s.Start();s.Receive(WatchingIntent.AWAY);s.Receive(WatchingIntent.UNKNOWN);Equal(1,e.Actions.Count);e.Step(s,500);Equal(MediaPhase.Owned,s.Phase);Equal(1,e.Actions.Count);s.Receive(WatchingIntent.ATTENDING);Equal(2,e.Actions.Count); },
    ["键鼠接管后回看不播放"] = () => { var(e,s)=Owned();e.Epoch++;e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["切前台后回看不播放"] = () => { var(e,s)=Owned();e.Foreground=2;e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["换媒体后回看不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Identity="B"};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["改暂停位置后不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Position=40};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["改媒体范围后不播放"] = () => { var(e,s)=Owned();e.Frame=e.Frame with {Maximum=700};e.Step(s,300);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["停止后迟到回看不播放"] = () => { var(e,s)=Owned();s.Stop();s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["重连开始不继承归属"] = () => { var(e,s)=Owned();s.Stop();s.Start();s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["输入监听失效禁止恢复"] = () => { var(e,s)=Owned();e.Healthy=false;s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["媒体不可识别不误报输入监听故障"] = () => { var(e,s)=Setup();e.OnRead=()=>throw new MediaObservationException("未找到可识别的播放控件");s.Start();s.Receive(WatchingIntent.AWAY);True(s.Status.Contains("未找到可识别的播放控件"));True(!s.Status.Contains("输入"));Equal(0,e.Actions.Count); },
    ["输入监听失效给出明确处理提示"] = () => { var(e,s)=Setup();e.Healthy=false;s.Start();s.Receive(WatchingIntent.AWAY);True(s.Status.Contains("输入监听已失效"));Equal(0,e.Actions.Count); },
    ["状态冻结未确认暂停不得获得恢复资格"] = () => { var(e,s)=Setup();e.Apply=false;e.FreezeProgress=true;s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,500);Equal(MediaPhase.Pausing,s.Phase);e.Step(s,1600);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count);Equal(MediaPhase.Idle,s.Phase); },
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
    ["恢复中再次侧看重新确认暂停"] = () => { var(e,s)=Owned();s.Receive(WatchingIntent.ATTENDING);s.Receive(WatchingIntent.AWAY);e.Step(s,500);Equal(MediaPhase.Pausing,s.Phase);e.Step(s,200);Equal(3,e.Actions.Count);Equal(MediaPhase.Owned,s.Phase); },
    ["普通页面公共祖先不是媒体区域"] = () => { True(!UiMedia.HasMediaSemantics("Page content group"));True(UiMedia.HasMediaSemantics("Video Player"));True(!UiMedia.HasMediaSemantics("Play controls")); },
    ["时间与媒体语义解析"] = () => { True(MediaClock.TryParse("0 Minutes 37 Seconds of 10 Minutes 34 Seconds",out var p,out var d));Equal(37d,p);Equal(634d,d);True(!MediaClock.TryParse("1:70 / 10:00",out _,out _));Equal(Playback.Unknown,UiMedia.Semantics("PausePal"));Equal(Playback.Paused,UiMedia.Semantics("Play keyboard shortcut k")); }
};
foreach (var check in MigrationChecks()) checks.Add(check.Key, check.Value);
foreach (var check in ClusterChecks()) checks.Add(check.Key, check.Value);
foreach (var check in MediaPolicyTests.Checks()) checks.Add(check.Key, check.Value);
foreach (var check in ForegroundChecks()) checks.Add(check.Key, check.Value);
foreach (var check in RewindFocusChecks()) checks.Add(check.Key, check.Value);
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

static Dictionary<string, Action> RewindFocusChecks() => new()
{
    ["回退焦点已就绪不增加固定等待"] = () =>
    { True(MediaRewind.WaitUntilReady(()=>true,()=>true,()=>throw new Exception("不应等待"))); },
    ["回退等待异步焦点条件而非固定睡眠"] = () =>
    { var turn=0;True(MediaRewind.WaitUntilReady(()=>turn<5,()=>turn==2,()=>turn++));Equal(2,turn); },
    ["回退资格初始无效不读取或等待焦点"] = () =>
    { True(!MediaRewind.WaitUntilReady(()=>false,()=>throw new Exception("不应读取"),()=>throw new Exception("不应等待"))); },
    ["回退等待时撤销即使焦点随后到达仍拒绝"] = () =>
    { var valid=true;var ready=false;True(!MediaRewind.WaitUntilReady(()=>valid,()=>ready,()=>{valid=false;ready=true;})); },
    ["回退焦点读取中资格失效不得因ready为真而通过"] = () =>
    { var valid=true;True(!MediaRewind.WaitUntilReady(()=>valid,()=>{valid=false;return true;},()=>throw new Exception("不应等待"))); },
    ["回退焦点读取耗尽原采样预算时拒绝"] = () =>
    { var age=1400;True(!MediaRewind.WaitUntilReady(()=>age<=1500,()=>{age=1600;return true;},()=>throw new Exception("不应等待"))); },
    ["回退焦点不达成时沿原预算结束"] = () =>
    { var age=1400;True(!MediaRewind.WaitUntilReady(()=>age<=1500,()=>false,()=>age+=20));Equal(1520,age); },
    ["回退焦点读取异常立即终止不继续等待"] = () =>
    { var yielded=false;try{MediaRewind.WaitUntilReady(()=>true,()=>throw new InvalidOperationException(),()=>yielded=true);}catch(InvalidOperationException){True(!yielded);return;}throw new Exception("异常未向执行边界传递"); }
};

static Dictionary<string, Action> MigrationChecks() => new()
{
    ["调音量接管仅取消本轮并可建立新一轮"] = () =>
    {
        var(e,s)=Owned(); e.Epoch++; e.Step(s,200); True(s.Watching); Equal(MediaPhase.Idle,s.Phase);
        s.Receive(WatchingIntent.ATTENDING); Equal(1,e.Actions.Count);
        e.Frame=e.Frame with {State=Playback.Playing}; s.Receive(WatchingIntent.AWAY); e.Step(s,200);
        Equal(MediaPhase.Owned,s.Phase); s.Receive(WatchingIntent.ATTENDING); e.Step(s,200); Equal(3,e.Actions.Count);
    },
    ["播放期间调整进度音量后下一轮正常控制"] = () =>
    {
        var(e,s)=Setup();s.Start();s.Receive(WatchingIntent.ATTENDING);e.Epoch++;e.Frame=e.Frame with {Position=50};
        s.Receive(WatchingIntent.AWAY);e.Step(s,200);Equal(MediaPhase.Owned,s.Phase);
        s.Receive(WatchingIntent.ATTENDING);e.Step(s,200);Equal(2,e.Actions.Count);True(s.Watching);
    },
    ["换片取消旧轮但后续无需重新开始观影"] = () =>
    {
        var(e,s)=Owned();e.Frame=e.Frame with {Identity="B",State=Playback.Playing,Maximum=800,Position=10};
        e.Step(s,200);True(s.Watching);Equal(MediaPhase.Idle,s.Phase);s.Receive(WatchingIntent.ATTENDING);
        Equal(1,e.Actions.Count);s.Receive(WatchingIntent.AWAY);e.Step(s,200);Equal(MediaPhase.Owned,s.Phase);
        s.Receive(WatchingIntent.ATTENDING);e.Step(s,200);Equal(3,e.Actions.Count);
    },
    ["接管后仍手动暂停则后续回看也不播放"] = () =>
    {
        var(e,s)=Owned();e.Epoch++;e.Step(s,200);s.Receive(WatchingIntent.ATTENDING);
        s.Receive(WatchingIntent.AWAY);e.Step(s,200);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count);True(s.Watching);
    },
    ["缺少时间线可暂停但不授予恢复资格"] = () =>
    {
        var(e,s)=Setup();e.Frame=e.Frame with {Source=TimelineSource.None,Position=double.NaN,Minimum=double.NaN,Maximum=double.NaN};
        s.Start();s.Receive(WatchingIntent.AWAY);Equal(1,e.Actions.Count);e.Step(s,200);
        Equal(MediaPhase.Idle,s.Phase);True(s.Status.Contains("不可核验"));s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count);
    },
    ["暂停后首次取得时间线不混淆媒体身份"] = () =>
    {
        var(e,s)=Setup();var valid=e.Frame;e.Frame=valid with {Source=TimelineSource.None,Position=double.NaN,Minimum=double.NaN,Maximum=double.NaN};
        s.Start();s.Receive(WatchingIntent.AWAY);e.Frame=valid with {State=Playback.Paused};e.Step(s,200);Equal(MediaPhase.Owned,s.Phase);
    },
    ["已证明结束的媒体暂停前零操作"] = () => { var(e,s)=Setup();e.Frame=e.Frame with {Position=634};s.Start();s.Receive(WatchingIntent.AWAY);Equal(0,e.Actions.Count); },
    ["暂停锚点丢失或格式变化不继承资格"] = () =>
    {
        foreach(var source in new[]{TimelineSource.None,TimelineSource.Text,TimelineSource.SeekText})
        { var(e,s)=Owned();e.Frame=e.Frame with {Source=source};e.Step(s,200);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); }
    },
    ["恢复命令无状态变化不确认成功不回退"] = () =>
    { var(e,s)=Owned();e.Apply=false;s.Receive(WatchingIntent.ATTENDING);e.Step(s,1000);e.Step(s,1100);Equal(2,e.Actions.Count);Equal(0,e.Rewinds);Equal(MediaPhase.Idle,s.Phase); },
    ["暂停前采样过期不能提交操作"] = () => { var(e,s)=Setup();e.OnRead=()=>e.Now+=1600;s.Start();s.Receive(WatchingIntent.AWAY);Equal(0,e.Actions.Count); },
    ["文字秒锚点不接受位置漂移"] = () =>
    { var(e,s)=Setup();e.Frame=e.Frame with {Source=TimelineSource.Text};s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,200);e.Frame=e.Frame with {Position=21};s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["回退重读的时间范围或来源改变必须拒绝"] = () =>
    {
        var(e,_)=Setup();var target=e.Frame;
        True(MediaRewind.MatchesPlayingTarget(target,target with {Position=21}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Maximum=800}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Source=TimelineSource.Text}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Source=TimelineSource.None}));
    },
    ["回退重读换目标暂停或结束必须拒绝"] = () =>
    {
        var(e,_)=Setup();var target=e.Frame;
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Identity="B"}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Window=2}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {State=Playback.Paused}));
        True(!MediaRewind.MatchesPlayingTarget(target,target with {Position=634}));
    },
    ["同一组合时间与子文字仅计一次"] = () =>
    { var clock=MediaClock.Unique([new("group",[],20,60),new("text",["group"],20,60)]);Equal("group",clock!.Id); },
    ["独立相同数值时钟仍有歧义"] = () => RejectClocks([new("a",[],20,60),new("b",[],20,60)]),
    ["父子时间冲突不能去重隐藏"] = () => RejectClocks([new("group",[],20,60),new("text",["group"],21,60)]),
    ["时间线缺失与结束分开"] = () =>
    { var(e,_)=Setup();True(MediaClock.Unique([])==null);True(!(e.Frame with {Position=634}).BeforeEnd);True((e.Frame with {Position=634}).HasTimeline);True(!(e.Frame with {Source=TimelineSource.None}).HasTimeline); },
    ["原生当前时间与总时长分开描述可解析"] = () =>
    { True(MediaClock.TrySplitDescription("已播放时间：2:23","总时间：28:54",out var p,out var d));Equal(143d,p);Equal(1734d,d);True(MediaClock.TrySplitDescription("elapsed time: 0:02","total time: 0:05",out _,out _)); },
    ["剩余时间或普通说明不能当作总时长"] = () =>
    { True(!MediaClock.TrySplitDescription("elapsed time: 0:02","remaining time: 0:05",out _,out _));True(!MediaClock.TrySplitDescription("2","5",out _,out _));True(!MediaClock.TrySplitDescription("elapsed time: 0:02","video duration 0:05",out _,out _)); },
    ["分开时间缺失异常直播及越界不授予资格"] = () =>
    { foreach(var text in new[]{"","total time: live","total time: 0:00","total time: 1:70","total time: 0:01"})True(!MediaClock.TrySplitDescription("elapsed time: 0:02",text,out _,out _)); },
    ["分开时间来源变换仍撤销原资格"] = () =>
    { var(e,s)=Owned();e.Frame=e.Frame with {Source=TimelineSource.SeekDescription};e.Step(s,200);s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["原生描述总时长变化撤销暂停归属"] = () =>
    { var(e,s)=Setup();e.Frame=e.Frame with {Source=TimelineSource.SeekDescription};s.Start();s.Receive(WatchingIntent.AWAY);e.Step(s,200);e.Frame=e.Frame with {Maximum=900};s.Receive(WatchingIntent.ATTENDING);Equal(1,e.Actions.Count); },
    ["零窗口不进入原生描述读取"] = () => True(NativeUiaDescription.Read(null!,0,[])==null)
};
static void RejectClocks(ClockReading[] clocks)
{
    try { MediaClock.Unique(clocks); }
    catch(MediaObservationException) { return; }
    throw new Exception("歧义时间来源未拒绝");
}

static Dictionary<string, Action> ClusterChecks() => new()
{
    ["原生视频时间进度标签与普通滑块区分"] = () =>
    { True(MediaCluster.IsSeek("视频时间进度条"));True(MediaCluster.IsSeek("video time scrubber"));True(!MediaCluster.IsSeek("音量"));True(!MediaCluster.IsSeek("进度设置")); },
    ["媒体音量与动作名称完整匹配"] = () =>
    { True(MediaCluster.IsVolume("volume"));True(MediaCluster.IsAction("进入全屏模式"));True(!MediaCluster.IsAction("进入全屏模式教程"));True(!MediaCluster.IsVolume("调整音量的说明")); },
    ["无名控件组不吞入普通正文或网页时间"] = () =>
    { True(MediaCluster.IsTimeText("02:34"));True(MediaCluster.IsTimeText("正在缓冲"));True(!MediaCluster.IsTimeText("文章发布日期")); },
    ["文档移到另一顶层或仅有顶层不继承资格"] = () =>
    { True(!MediaCluster.SameDocumentBranch(["frame","page-a"],["frame","page-b"]));True(!MediaCluster.SameDocumentBranch(["page"],["frame","page"]));True(!MediaCluster.SameDocumentBranch([],[])); }
};

static Dictionary<string, Action> ForegroundChecks() => new()
{
    ["同一顶层下的子窗口通知不误报切走"] = () => { var f=new ForegroundIdentity(1);True(!f.Observe(1,1));True(!f.Observe(1,1)); },
    ["切走又切回仍撤销原资格"] = () => { var f=new ForegroundIdentity(1);True(f.Observe(2,2));True(f.Observe(1,1)); },
    ["同进程的独立弹窗仍按不同顶层撤销"] = () => { var f=new ForegroundIdentity(1);True(f.Observe(3,3)); },
    ["未知事件或未知实际前台不得保留资格"] = () => { var f=new ForegroundIdentity(1);True(f.Observe(0,1));True(f.Observe(1,0));True(f.Observe(0,0)); },
    ["迟到事件冲突仍撤销且不污染下一次前台基线"] = () => { var f=new ForegroundIdentity(1);True(f.Observe(2,1));True(!f.Observe(1,1)); },
    ["事件目标相同但实际已切走仍撤销"] = () => { var f=new ForegroundIdentity(1);True(f.Observe(1,2));True(!f.Observe(2,2)); }
};

sealed class FakeEnvironment:IMediaEnvironment
{
    public long Now{get;set;}
    public long Epoch{get;set;}
    public bool Healthy{get;set;}=true;
    public nint Foreground{get;set;}=1;
    public required UiMedia Frame{get;set;}
    public bool Apply{get;set;}=true;
    public bool FreezeProgress{get;set;}
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
        if(Frame.State==Playback.Playing&&!FreezeProgress)Frame=Frame with {Position=Frame.Position+milliseconds/1000d};
        session.Tick();
    }
}
