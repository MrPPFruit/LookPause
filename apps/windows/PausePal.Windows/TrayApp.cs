using Microsoft.Win32;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace PausePal.Windows;

internal sealed class TrayApp : Forms.ApplicationContext
{
    private readonly Forms.Control dispatch = new();
    private readonly InputWatch input;
    private readonly IntentInbox inbox;
    private readonly IdentityStore identity;
    private readonly ReceiverCore core;
    private readonly LanReceiver receiver;
    private readonly DiscoveryRegistration discovery;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem connectionRow = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem mediaRow = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem networkRow = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem loginRow = new("登录后启动") { CheckOnClick = false };
    private readonly Forms.ToolStripMenuItem shortcutRow = new("下次观影用快捷键兼容模式") { CheckOnClick = true,
        ToolTipText = "先播放并点击播放器，再在手机开始。空格效果不验证，可能误切换；手动操作后需重新开始。不回退。" };
    private int nextShortcutMode;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Dictionary<string, Icon> icons = [];
    private readonly Task mediaWorker;
    private PairingDialog? pairing;
    private Guid? pairingPeer;
    private long rendered = -1;
    private bool exiting;

    private TrayApp(IdentityStore identity)
    {
        this.identity = identity;
        dispatch.CreateControl();
        input = new InputWatch(); inbox = new IntentInbox(input);
        core = new ReceiverCore(identity, inbox);
        core.Changed += snapshot =>
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { kind = "receiver-state", icon = snapshot.Icon.ToString(), snapshot.Revision }));
            Post(() => Render(snapshot));
        };
        core.PairRequested += (peer, name) => Post(() => ShowPairing(peer, name));
        core.PairEnded += peer => Post(() => { if (pairingPeer == peer) pairing?.Close(); });
        PopulateMenu(menu, connectionRow, mediaRow, networkRow, shortcutRow, loginRow, async (_, _) => await ShutdownAsync());
        shortcutRow.CheckedChanged += (_, _) => Volatile.Write(ref nextShortcutMode,shortcutRow.Checked ? 1 : 0);
        menu.Opening += (_, _) => { Render(core.Snapshot, true); ReadLogin(); };
        loginRow.Click += (_, _) =>
        {
            try { LoginStartup.Set(!LoginStartup.Enabled); ReadLogin(); }
            catch { networkRow.Text = "无法修改登录启动，请检查当前用户设置"; }
        };
        tray = new Forms.NotifyIcon { ContextMenuStrip = menu, Text = "PausePal · 手机未连接" };
        Render(core.Snapshot, true); tray.Visible = true;
        try { LoginStartup.Initialize(); ReadLogin(); }
        catch { networkRow.Text = "登录启动未能配置"; }
        mediaWorker = Task.Run(RunMediaAsync);
        LanReceiver? started = null;
        try
        {
            receiver = started = new LanReceiver(identity, core, diagnostic: error => Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                { kind = "connection-ended", error = error.GetType().Name, code = error.HResult, innerCode = error.InnerException?.HResult })));
            networkRow.Text = "正在注册局域网服务";
            discovery = new DiscoveryRegistration(receiver.Port, identity.ServerId, identity.Fingerprint,
                status => Post(() => networkRow.Text = status));
        }
        catch
        {
            // 构造失败也必须先撤销；不能在错误窗口后留下后台接收和媒体任务。
            exiting = true; core.Stop(); inbox.Complete(); started?.Dispose(); input.Dispose();
            tray.Dispose(); menu.Dispose(); dispatch.Dispose();
            foreach (var icon in icons.Values) icon.Dispose();
            throw;
        }
    }
    internal static void PopulateMenu(Forms.ContextMenuStrip menu, Forms.ToolStripMenuItem connection,
        Forms.ToolStripMenuItem media, Forms.ToolStripMenuItem network, Forms.ToolStripMenuItem shortcut, Forms.ToolStripMenuItem login, EventHandler exit)
    {
        menu.Font = new Font("Microsoft YaHei UI", 9);
        menu.AccessibleName = "PausePal 状态菜单";
        menu.Items.AddRange([connection, media, network, new Forms.ToolStripSeparator(), shortcut, login,
            new Forms.ToolStripMenuItem("退出", null, exit)]);
    }

    public static void Run()
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var single = new Mutex(true, @"Local\PausePal-" + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out var first);
        if (!first) return;
        try
        {
            using var log = new LocalDiagnostics();
            var previous = Console.Out;
            Console.SetOut(log);
            try
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { kind = "startup", version = typeof(Program).Assembly.GetName().Version?.ToString(), windows = Environment.OSVersion.VersionString, runtime = Environment.Version.ToString() }));
                using var identity = IdentityStore.Load();
                using var app = new TrayApp(identity);
                Forms.Application.Run(app);
            }
            finally { Console.SetOut(previous); }
        }
        catch (Exception error)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { kind = "startup-error", error = error.GetType().Name }));
            Forms.MessageBox.Show("PausePal 未能启动。请检查本地身份文件、系统网络及 .NET 环境。已有配对身份已保留。", "PausePal", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
        finally { single.ReleaseMutex(); }
    }
    private async Task RunMediaAsync()
    {
        var session = new MediaSession(new WindowsMediaEnvironment(input));
        var shortcut = new ShortcutSession(new ShortcutEnvironment(input));
        var shortcutMode = false;
        long generation = 0;
        session.Changed += text => { if(!shortcutMode)core.MediaChanged(generation, text); };
        shortcut.Changed += text => { if(shortcutMode)core.MediaChanged(generation, text); };
        try
        {
            while (!inbox.Closed)
            {
                while (inbox.TryRead(out var pending))
                {
                    generation = pending.Generation;
                    switch (pending.Value)
                    {
                        case "START":
                            session.Stop();shortcut.Stop();
                            shortcutMode=Volatile.Read(ref nextShortcutMode)!=0;
                            if(shortcutMode)shortcut.Start();else session.Start();
                            break;
                        case "STOP": session.Stop();shortcut.Stop();break;
                        case "DISCONNECT": session.Stop("手机已断开，恢复资格取消");shortcut.Stop();break;
                        default:
                            if (Enum.TryParse<WatchingIntent>(pending.Value, out var intent))
                            { if(shortcutMode)shortcut.Receive(intent,pending.Epoch);else session.Receive(intent, pending.Epoch); }
                            break;
                    }
                }
                if (!inbox.Closed) { if(shortcutMode)shortcut.Tick();else session.Tick(); }
                await Task.Delay(200);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { kind = "media-worker-ended", error = error.GetType().Name }));
            core.Stop(); inbox.Complete();
        }
        finally { session.Stop();shortcut.Stop(); }
    }
    private void Post(Action action)
    {
        if (exiting || dispatch.IsDisposed) return;
        try { dispatch.BeginInvoke(() => { if (!exiting) action(); }); }
        catch (InvalidOperationException) { }
    }
    private void Render(ReceiverSnapshot snapshot, bool force = false)
    {
        if (!force && snapshot.Revision <= rendered) return;
        rendered = Math.Max(rendered, snapshot.Revision);
        connectionRow.Text = snapshot.Connection; mediaRow.Text = snapshot.Media;
        var dark = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 1) == 0;
        var key = (dark ? "dark/" : "light/") + snapshot.Icon;
        if (!icons.TryGetValue(key, out var icon))
        {
            icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icons", key + ".ico"), SystemInformationIconSize());
            icons.Add(key, icon);
        }
        tray.Icon = icon;
        tray.Text = "PausePal · " + (snapshot.Icon switch
        { DesktopStatus.disconnected => "手机未连接", DesktopStatus.waiting => "已连接，等待观看方向", DesktopStatus.attending => "已连接，正在看屏幕", _ => "已连接，未看屏幕" });
    }
    private static Size SystemInformationIconSize() => Forms.SystemInformation.SmallIconSize;
    private void ReadLogin() => loginRow.Checked = LoginStartup.Enabled;
    private void ShowPairing(Guid peer, string name)
    {
        if (!core.IsPending(peer)) return;
        pairing?.Close(); pairingPeer = peer;
        var dialog = new PairingDialog(name);
        pairing = dialog;
        dialog.FormClosed += async (_, _) =>
        {
            if (pairing == dialog) { pairing = null; pairingPeer = null; }
            await core.ApproveAsync(peer, dialog.Allowed);
            dialog.Dispose();
        };
        dialog.Show();
    }
    private async Task ShutdownAsync()
    {
        if (exiting) return;
        exiting = true; core.Stop(); inbox.Complete();
        pairing?.Close(); tray.Visible = false; receiver.Dispose();
        await discovery.DisposeAsync();
        try { await mediaWorker.WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { }
        input.Dispose(); tray.Dispose(); menu.Dispose();
        foreach (var icon in icons.Values) icon.Dispose();
        dispatch.Dispose(); ExitThread();
    }
}

internal static class LoginStartup
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunPath); return key?.GetValue("PausePal") as string == Command; }
    }
    private static string Command => "\"" + Environment.ProcessPath + "\"";
    public static void Initialize()
    {
        using var preferences = Registry.CurrentUser.CreateSubKey(@"Software\PausePal");
        if (preferences.GetValue("LoginEnabled") is not int enabled || enabled != 0) Set(true);
    }
    public static void Set(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunPath);
        if (enabled) run.SetValue("PausePal", Command, RegistryValueKind.String); else run.DeleteValue("PausePal", false);
        using var preferences = Registry.CurrentUser.CreateSubKey(@"Software\PausePal");
        preferences.SetValue("LoginEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}

internal class PairingDialog : Forms.Form
{
    public bool Allowed { get; private set; }
    public PairingDialog(string name)
    {
        Text = "PausePal · 首次配对"; Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = Forms.AutoScaleMode.Dpi; ClientSize = new Size(440, 270);
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = Forms.FormStartPosition.CenterScreen;
        var layout = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(20), RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        layout.Controls.Add(new Forms.Label { Text = "允许这台手机连接 PausePal？", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Forms.Padding(0, 0, 0, 12) });
        layout.Controls.Add(new Forms.Label { Text = "手机自报名称：" + name + "\n\n仅在可信家庭局域网内允许你正在操作的手机。允许后会记住设备。", Dock = Forms.DockStyle.Fill, AutoSize = true, MaximumSize = new Size(400, 0) });
        var buttons = new Forms.FlowLayoutPanel { AutoSize = true, FlowDirection = Forms.FlowDirection.RightToLeft, Dock = Forms.DockStyle.Fill, WrapContents = false };
        var allow = new Forms.Button { Text = "允许", AutoSize = true, MinimumSize = new Size(88, 34) };
        var deny = new Forms.Button { Text = "拒绝", AutoSize = true, MinimumSize = new Size(88, 34), DialogResult = Forms.DialogResult.Cancel };
        allow.Click += (_, _) => { Allowed = true; Close(); };
        deny.Click += (_, _) => Close();
        buttons.Controls.Add(allow); buttons.Controls.Add(deny); layout.Controls.Add(buttons);
        Controls.Add(layout); CancelButton = deny; AcceptButton = deny;
        ActiveControl = deny;
    }
}
