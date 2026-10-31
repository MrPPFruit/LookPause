using PausePal.Windows;

internal static class ShortcutTests
{
    public static Dictionary<string, Action> Checks() => new()
    {
        ["兼容仅精确自身Space与原Left豁免"] = () => {foreach(var key in new uint[]{0x20,0x25}){if(!InputWatch.IsOwnMediaKey(key,0x10,InputWatch.OwnRewindTag))throw new Exception("自身键未豁免");if(InputWatch.IsOwnMediaKey(key,0,InputWatch.OwnRewindTag)||InputWatch.IsOwnMediaKey(key,0x10,0))throw new Exception("真实输入被豁免");}if(InputWatch.IsOwnMediaKey(0x41,0x10,InputWatch.OwnRewindTag))throw new Exception("任意键被豁免");},
        ["兼容开始不发键，离开回看各一次且重复不重发"] = () => { var(e,s)=Setup(); s.Start(); Eq(0,e.Sends); Away(e,s); Away(e,s); Eq(1,e.Sends); Back(e,s); Back(e,s); Eq(2,e.Sends); Away(e,s);Back(e,s);Eq(4,e.Sends); },
        ["兼容UNKNOWN不发键且不丢有效pending"] = () => { var(e,s)=Setup();s.Start();Away(e,s);s.Receive(WatchingIntent.UNKNOWN,e.Epoch);Eq(1,e.Sends);Back(e,s);Eq(2,e.Sends); },
        ["兼容无pending回看不发键"] = () => { var(e,s)=Setup();s.Start();Back(e,s);Eq(0,e.Sends); },
        ["兼容接管解除整次，后来离开回看也不重授"] = () => { var(e,s)=Setup();s.Start();Away(e,s);e.Revoke();s.Tick();Back(e,s);Away(e,s);Back(e,s);Eq(1,e.Sends);s.Start();Away(e,s);Eq(2,e.Sends); },
        ["兼容停止后迟到意图不发键"] = () => { var(e,s)=Setup();s.Start();Away(e,s);s.Stop();Back(e,s);Away(e,s);Eq(1,e.Sends); },
        ["兼容焦点文档或进程变化解除整次"] = () => { foreach(var kind in new[]{0,1,2}) {var(e,s)=Setup();s.Start();Away(e,s);e.Frame=kind switch {0=>e.Frame with {Window=2},1=>e.Frame with {Context="B"},_=>e.Frame with {Process=2}};s.Tick();Back(e,s);Away(e,s);Eq(1,e.Sends);} },
        ["兼容失去输入健康后不能自动续控"] = () => {var(e,s)=Setup();s.Start();Away(e,s);e.Healthy=false;s.Tick();e.Healthy=true;Back(e,s);Away(e,s);Eq(1,e.Sends);},
        ["兼容执行失败不重试不保留回看"] = () => {var(e,s)=Setup();e.Apply=false;s.Start();Away(e,s);Back(e,s);Away(e,s);Eq(1,e.Sends);},
        ["兼容开始前旧代次意图拒绝"] = () => {var(e,s)=Setup();s.Start();e.Revoke();s.Receive(WatchingIntent.AWAY,0);Eq(0,e.Sends);},
        ["兼容边界读取中撤销不提交"] = () => {var(e,s)=Setup();s.Start();e.OnRead=e.Revoke;Away(e,s);Eq(0,e.Sends);},
        ["兼容过期或未来采样不提交"] = () => {foreach(var stamp in new long[]{-1600,100}) {var(e,s)=Setup();e.Frame=e.Frame with {ObservedAt=stamp};s.Start();Away(e,s);Eq(0,e.Sends);}},
        ["兼容等待期间长时间无观察禁止补发"] = () => {var(e,s)=Setup();s.Start();Away(e,s);e.Now+=2100;Back(e,s);Eq(1,e.Sends);},
        ["兼容边界读取异常解除整次"] = () => {var(e,s)=Setup();s.Start();e.OnRead=()=>throw new InvalidOperationException();Away(e,s);e.OnRead=null;Away(e,s);Back(e,s);Eq(0,e.Sends);},
        ["兼容不宣称已确认暂停恢复"] = () => {var(e,s)=Setup();var text="";s.Changed+=v=>text=v;s.Start();Away(e,s);if(!text.Contains("未确认"))throw new Exception("状态文案错误");Back(e,s);if(!text.Contains("未确认"))throw new Exception("状态文案错误");},
        ["兼容重启不继承旧pending"] = () => {var(e,s)=Setup();s.Start();Away(e,s);s.Start();Back(e,s);Eq(1,e.Sends);}
    };
    static (Fake,ShortcutSession) Setup() {var e=new Fake();return(e,new ShortcutSession(e));}
    static void Away(Fake e,ShortcutSession s)=>s.Receive(WatchingIntent.AWAY,e.Epoch);
    static void Back(Fake e,ShortcutSession s)=>s.Receive(WatchingIntent.ATTENDING,e.Epoch);
    static void Eq(int expected,int actual) {if(expected!=actual)throw new Exception($"期望{expected}，实际{actual}");}
    sealed class Fake : IShortcutEnvironment
    {
        public long Now {get;set;}
        public bool Healthy {get;set;}=true;
        public long Epoch {get;private set;}
        public void Revoke()=>Epoch++;
        public ShortcutBoundary Frame=new(1,1,"A",0);
        public Action? OnRead;
        public bool Apply=true;
        public int Sends;
        public ShortcutBoundary Observe(){OnRead?.Invoke();return Frame;}
        public bool SendSpace(ShortcutBoundary boundary,long epoch){Sends++;return Apply;}
    }
}
