using PausePal.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Forms = System.Windows.Forms;

internal static class TestModes
{
    public static async Task NativeDiscovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PausePal-discovery-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var identity = IdentityStore.Load(directory);
            var core = new ReceiverCore(identity, new IntentInbox(new Revocation()));
            using var receiver = new LanReceiver(identity, core);
            var discovery = new DiscoveryRegistration(receiver.Port, identity.ServerId, identity.Fingerprint, Console.WriteLine);
            try
            {
                var result = await discovery.Registered.WaitAsync(TimeSpan.FromSeconds(10));
                if (result != 0) throw new Exception("原生发现注册失败：" + result);
            }
            finally { await discovery.DisposeAsync(); }
            await discovery.CompletionTask.WaitAsync(TimeSpan.FromSeconds(5));
            Console.WriteLine("原生 DNS-SD 注册回调及反注册完成；不代表 S25 已发现。");
            var earlyExit = new DiscoveryRegistration(receiver.Port, identity.ServerId, identity.Fingerprint, Console.WriteLine);
            await earlyExit.DisposeAsync();
            await earlyExit.CompletionTask.WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine("注册尚在进行时退出完成，无悬空原生参数。");
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    public static void UiPreview(string directory)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(directory);
                Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
                Forms.Application.EnableVisualStyles();
                using var dialog = new PreviewPairingDialog(new string('测', 64));
                dialog.ShowInTaskbar = false; dialog.StartPosition = Forms.FormStartPosition.Manual;
                dialog.Location = new Point(-20000, -20000); dialog.Show(); dialog.PerformLayout(); dialog.Update();
                using var screenshot = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(directory, "pairing.png"), ImageFormat.Png);
                dialog.Close();
                foreach (var theme in new[] { "light", "dark" })
                {
                    using var canvas = new Bitmap(1000, 530);
                    using var graphics = Graphics.FromImage(canvas);
                    graphics.Clear(theme == "light" ? Color.White : Color.FromArgb(32, 32, 32));
                    using var font = new Font("Microsoft YaHei UI", 10);
                    using var brush = new SolidBrush(theme == "light" ? Color.Black : Color.White);
                    var row = 0;
                    foreach (var state in Enum.GetValues<DesktopStatus>())
                    {
                        using var icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icons", theme, state + ".ico"), 32, 32);
                        graphics.DrawIcon(icon, new Rectangle(24, 20 + row * 50, 32, 32));
                        graphics.DrawString(state.ToString(), font, brush, 76, 25 + row * 50); row++;
                    }
                    using var menu = new Forms.ContextMenuStrip();
                    TrayApp.PopulateMenu(menu,
                        new Forms.ToolStripMenuItem("手机已连接") { Enabled = false },
                        new Forms.ToolStripMenuItem("暂停位置或结束状态不可核验，不能自动恢复，本轮自动控制已取消") { Enabled = false },
                        new Forms.ToolStripMenuItem("局域网服务已注册，等待手机连接") { Enabled = false },
                        new Forms.ToolStripMenuItem("下次观影用快捷键兼容模式") { Checked = theme=="dark" },
                        new Forms.ToolStripMenuItem("登录后启动") { Checked = true }, (_, _) => { });
                    menu.PerformLayout(); menu.CreateControl();
                    using var menuBitmap = new Bitmap(menu.Width, menu.Height);
                    menu.DrawToBitmap(menuBitmap, new Rectangle(Point.Empty, menuBitmap.Size));
                    graphics.DrawImageUnscaled(menuBitmap, 320, 24);
                    graphics.DrawString("实际 WinForms 控件离屏渲染；系统托盘、主题和 DPI 仍待用户实测", font, brush, 24, 460);
                    canvas.Save(Path.Combine(directory, "tray-" + theme + ".png"), ImageFormat.Png);
                }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
        Console.WriteLine("配对窗口、菜单与图标的离屏预览已生成，非系统托盘实测。");
    }
    private sealed class Revocation : IRevocation { public long Epoch { get; private set; } public void Revoke() => Epoch++; }
    private sealed class PreviewPairingDialog(string name) : PairingDialog(name)
    { protected override bool ShowWithoutActivation => true; }
}
