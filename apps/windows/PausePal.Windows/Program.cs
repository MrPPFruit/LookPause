using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Windows.Media.Control;

namespace PausePal.Windows;

internal static class Program
{
    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")] internal static extern nint NativeForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    internal static nint TopLevelWindow(nint window) => window == 0 ? 0 : GetAncestor(window, 2);
    internal static nint GetForegroundWindow() => TopLevelWindow(NativeForegroundWindow());
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [STAThread]
    static void Main(string[] args)
    {
        if (args is ["--pilot"])
        {
            Pilot.Run();
            return;
        }
        if (args is ["--probe"])
        {
            RunBoundedProbe().GetAwaiter().GetResult();
            return;
        }
        if (args is ["--probe-worker"])
        {
            Probe().GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 0) { TrayApp.Run(); return; }
        Console.WriteLine("PausePal Windows：无参数启动托盘；--probe 为只读探查；--pilot 为本机媒体资格入口。");
    }

    static async Task RunBoundedProbe()
    {
        using var worker = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--probe-worker")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        var output = worker.StandardOutput.ReadToEndAsync();
        var errors = worker.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            await worker.WaitForExitAsync(timeout.Token);
            Console.Write(await output);
            if (worker.ExitCode != 0 || !string.IsNullOrEmpty(await errors))
                Console.WriteLine("{\"kind\":\"probe-error\",\"incomplete\":true}");
        }
        catch (OperationCanceledException)
        {
            worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync();
            Console.WriteLine("{\"kind\":\"probe-timeout\",\"incomplete\":true}");
        }
    }

    static async Task Probe()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "foreground", hwnd = hwnd.ToInt64(), pid }));
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions())
            {
                var info = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "gsmtc", source = session.SourceAppUserModelId,
                    status = info.PlaybackStatus.ToString(), pause = info.Controls.IsPauseEnabled,
                    play = info.Controls.IsPlayEnabled, seek = info.Controls.IsPlaybackPositionEnabled,
                    position = timeline.Position.TotalSeconds, duration = timeline.EndTime.TotalSeconds,
                    updatedAt = timeline.LastUpdatedTime, rate = info.PlaybackRate }));
            }
        }
        catch (Exception error) { Console.WriteLine(JsonSerializer.Serialize(new { kind = "gsmtc-error", error = error.GetType().Name, code = error.HResult })); }
        try
        {
            if (GetForegroundWindow() != hwnd)
            {
                Console.WriteLine("{\"kind\":\"foreground-changed\",\"incomplete\":true}");
                return;
            }
            var root = AutomationElement.FromHandle(hwnd);
            var walker = TreeWalker.ControlViewWalker;
            var pending = new Stack<(AutomationElement Node, string? Document, string Parent, int Depth)>();
            pending.Push((root, null, "", 0));
            var elapsed = Stopwatch.StartNew();
            var count = 0;
            var cutoff = false;
            while (pending.Count > 0 && count < 1200 && elapsed.ElapsedMilliseconds < 1500)
            {
                var (node, document, parent, depth) = pending.Pop();
                count++;
                var c = node.Current;
                var id = string.Join(".", node.GetRuntimeId());
                if (c.ControlType == ControlType.Document) document = id;
                // 语义必须完整匹配，不能把 PausePal 等子串当播放器控件。
                var playback = UiMedia.Semantics(c.Name) != Playback.Unknown;
                var clock = Regex.IsMatch(c.Name, @"^(?:\d{1,2}:)?\d{1,2}:\d{2}(?:\s*/\s*(?:\d{1,2}:)?\d{1,2}:\d{2})?$|^\d+ (?:Minutes?|Hours?) .+ of \d+ (?:Minutes?|Hours?) .+$", RegexOptions.CultureInvariant);
                if (c.ControlType == ControlType.Document || c.ControlType == ControlType.Slider ||
                    c.ControlType == ControlType.Group || c.ControlType == ControlType.Pane ||
                    ((c.ControlType == ControlType.Button || c.ControlType == ControlType.Text) && (playback || clock)))
                {
                    object? value = null;
                    object? clockValue = MediaClock.TryParse(c.Name, out var clockPosition, out var clockDuration)
                        ? new { position = clockPosition, duration = clockDuration } : null;
                    if (node.TryGetCurrentPattern(RangeValuePattern.Pattern, out var range))
                    {
                        var r = ((RangeValuePattern)range).Current;
                        value = new { r.Value, r.Minimum, r.Maximum, r.IsReadOnly };
                    }
                    else if (c.ControlType == ControlType.Slider && node.TryGetCurrentPattern(ValuePattern.Pattern, out var numeric) &&
                        double.TryParse(((ValuePattern)numeric).Current.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var scalar) && double.IsFinite(scalar))
                        value = new { Value = scalar, Unit = "unknown" };
                    Console.WriteLine(JsonSerializer.Serialize(new { kind = "control", type = c.ControlType.ProgrammaticName,
                        semantic = playback ? (UiMedia.Semantics(c.Name) == Playback.Playing ? "PAUSE" : "PLAY") : clock ? "TIME" :
                            c.ControlType == ControlType.Slider && Regex.IsMatch(c.Name, "seek|position|timeline|进度|时间线", RegexOptions.IgnoreCase) ? "SEEK" :
                            Regex.IsMatch(c.Name, "volume|音量", RegexOptions.IgnoreCase) ? "VOLUME" : "OTHER",
                        mediaNamed = UiMedia.HasMediaSemantics(c.Name + " " + c.HelpText),
                        helpClock = MediaClock.TryParse(c.HelpText, out var helpPosition, out var helpDuration)
                            ? new { position = helpPosition, duration = helpDuration } : null,
                        context = c.ControlType == ControlType.Document ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.Name))) : null,
                        id, document, parent, offscreen = c.IsOffscreen, enabled = c.IsEnabled, keyboardFocusable = c.IsKeyboardFocusable,
                        invoke = node.TryGetCurrentPattern(InvokePattern.Pattern, out _), value, clockValue }));
                    if (c.ControlType == ControlType.Slider && MediaCluster.IsSeek(c.Name))
                    {
                        var full = NativeUiaDescription.Read(node, hwnd, node.GetRuntimeId());
                        var currentText = node.TryGetCurrentPattern(ValuePattern.Pattern, out var descriptionPattern) ? ((ValuePattern)descriptionPattern).Current.Value : "";
                        var parsed = full != null && MediaClock.TrySplitDescription(currentText, full, out _, out _);
                        var wrongId = (int[])node.GetRuntimeId().Clone(); wrongId[^1] ^= 1;
                        var mismatchRejected = NativeUiaDescription.Read(node, hwnd, wrongId) == null;
                        Console.WriteLine(JsonSerializer.Serialize(new { kind = "description-probe", hasDescription = full != null, hasSplitTimeline = parsed, mismatchRejected }));
                    }
                }
                // 有界逐节点读取，不先通过 FindAll 拉取整个应用树。
                if (depth < 35)
                {
                    var children = new List<AutomationElement>();
                    var child = walker.GetFirstChild(node);
                    while (child != null && children.Count < 120 && elapsed.ElapsedMilliseconds < 1500)
                    {
                        children.Add(child);
                        child = walker.GetNextSibling(child);
                    }
                    if (child != null) cutoff = true;
                    for (var i = children.Count - 1; i >= 0; i--) pending.Push((children[i], document, id, depth + 1));
                }
                else cutoff = true;
            }
            GetWindowThreadProcessId(GetForegroundWindow(), out var finalPid);
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "uia", count,
                incomplete = pending.Count > 0 || cutoff || GetForegroundWindow() != hwnd || finalPid != pid,
                elapsedMs = elapsed.ElapsedMilliseconds, foregroundUnchanged = GetForegroundWindow() == hwnd && finalPid == pid }));
        }
        catch (Exception error) { Console.WriteLine(JsonSerializer.Serialize(new { kind = "uia-error", incomplete = true, error = error.GetType().Name, code = error.HResult })); }
        try
        {
            if (GetForegroundWindow() != hwnd) return;
            var media = UiMedia.Read();
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "media-read", state = media.State.ToString(), source = media.Source.ToString(),
                hasTimeline = media.HasTimeline, beforeEnd = media.BeforeEnd,
                position = double.IsFinite(media.Position) ? (double?)media.Position : null,
                duration = double.IsFinite(media.Maximum) ? (double?)media.Maximum : null }));
        }
        catch (Exception error) { Console.WriteLine(JsonSerializer.Serialize(new { kind = "media-read-error",
            reason = error is MediaObservationException ? error.Message : error.GetType().Name })); }
    }
}
