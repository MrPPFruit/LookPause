using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace PausePal.Windows;

internal enum Playback { Playing, Paused, Unknown }

internal sealed record UiMedia(nint Window, uint ProcessId, string Identity, AutomationElement Control,
    AutomationElement Timeline, Playback State, double Position, double Minimum, double Maximum, long ObservedAt = 0)
{
    public bool BeforeEnd => double.IsFinite(Position) && double.IsFinite(Minimum) && double.IsFinite(Maximum) &&
        Maximum > Minimum && Position >= Minimum && Position < Maximum - Math.Max(0.01, (Maximum - Minimum) * 0.001);

    public bool SameTarget(UiMedia other) => Window == other.Window && ProcessId == other.ProcessId &&
        Identity == other.Identity && Minimum == other.Minimum && Maximum == other.Maximum;

    public bool HoldsPause(UiMedia other) => SameTarget(other) && BeforeEnd && other.BeforeEnd &&
        other.State == Playback.Paused && Math.Abs(Position - other.Position) < 0.01;

    public static Playback Semantics(string name)
    {
        var match = Regex.Match(name.Trim(), @"^(play|pause|播放|暂停)(?:\s*\([a-z ]+\)| keyboard shortcut [a-z])?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return Playback.Unknown;
        return match.Groups[1].Value.ToLowerInvariant() is "pause" or "暂停" ? Playback.Playing : Playback.Paused;
    }

    private sealed record Item(AutomationElement Element, string Id, string? Document,
        string[] Parents, ControlType Type, string Name, bool Visible, bool Enabled);

    // 完整扫描才可证明当前观察面中的唯一性；任何截断、失效或前台变化都拒绝。
    public static UiMedia Read()
    {
        var window = Program.GetForegroundWindow();
        if (window == 0) throw new InvalidOperationException("没有可核验前台");
        Program.GetWindowThreadProcessId(window, out var processId);
        var observedAt = Environment.TickCount64;
        var timer = Stopwatch.StartNew();
        var cache = new CacheRequest { TreeScope = TreeScope.Element };
        foreach (var property in new[] { AutomationElement.NameProperty, AutomationElement.ControlTypeProperty,
                     AutomationElement.IsOffscreenProperty, AutomationElement.IsEnabledProperty,
                     AutomationElement.BoundingRectangleProperty, AutomationElement.RuntimeIdProperty,
                     AutomationElement.HelpTextProperty }) cache.Add(property);
        var root = AutomationElement.FromHandle(window).GetUpdatedCache(cache);
        var rootId = string.Join(".", (int[])root.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
        var items = new List<Item>();
        var regions = new Dictionary<string, string>();
        var walker = TreeWalker.ControlViewWalker;
        AutomationElementCollection relevant;
        using (cache.Activate()) relevant = root.FindAll(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document)));
        if (relevant.Count > 500 || timer.ElapsedMilliseconds > 1500) throw new InvalidOperationException("媒体控件查询不完整或超时");
        foreach (AutomationElement node in relevant)
        {
            if (timer.ElapsedMilliseconds > 1500) throw new InvalidOperationException("媒体控件读取超时");
            var current = node.Cached;
            if (current.IsOffscreen || !current.IsEnabled || current.BoundingRectangle.IsEmpty) continue;
            if (current.ControlType == ControlType.Button && Semantics(current.Name) == Playback.Unknown &&
                !MediaClock.TryParse(current.Name, out _, out _)) continue;
            if (current.ControlType == ControlType.Slider && !IsSeek(current.Name)) continue;
            var id = string.Join(".", (int[])node.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
            string? document = current.ControlType == ControlType.Document ? id : null;
            var parents = new List<string>();
            var ancestor = walker.GetParent(node, cache);
            while (ancestor != null)
            {
                if (parents.Count >= 35 || timer.ElapsedMilliseconds > 1500) throw new InvalidOperationException("媒体祖先观察不完整");
                var ancestorId = string.Join(".", (int[])ancestor.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
                parents.Add(ancestorId);
                if (ancestor.Cached.ControlType == ControlType.Group || ancestor.Cached.ControlType == ControlType.Pane)
                    regions.TryAdd(ancestorId, ancestor.Cached.Name + " " + ancestor.Cached.HelpText);
                if (document == null && ancestor.Cached.ControlType == ControlType.Document) document = ancestorId;
                if (ancestorId == rootId) break;
                ancestor = walker.GetParent(ancestor, cache);
            }
            if (parents.LastOrDefault() != rootId) throw new InvalidOperationException("媒体不再属于当前前台");
            parents.Reverse();
            items.Add(new Item(node, id, document, [.. parents], current.ControlType, current.Name, true, true));
        }
        if (timer.ElapsedMilliseconds > 1500 || Program.GetForegroundWindow() != window)
            throw new InvalidOperationException("前台观察已过期");
        var documents = items.Where(x => x.Visible && x.Type == ControlType.Document).ToArray();
        if (documents.Length > 1) throw new InvalidOperationException("前台内容有歧义");
        var controls = items.Where(x => x.Visible && x.Enabled && x.Type == ControlType.Button &&
            Semantics(x.Name) != Playback.Unknown && x.Element.TryGetCurrentPattern(InvokePattern.Pattern, out _)).ToArray();
        if (controls.Length != 1) throw new InvalidOperationException("媒体控制不唯一或不可识别");
        var control = controls[0];
        var scope = control.Parents.Reverse().FirstOrDefault(id => id != control.Document && id != rootId &&
            regions.TryGetValue(id, out var label) && HasMediaSemantics(label));
        if (scope == null) throw new InvalidOperationException("缺少明确的媒体语义区域");
        var seekers = items.Where(x => x.Visible && x.Enabled && x.Document == control.Document && x.Type == ControlType.Slider &&
            x.Parents.Contains(scope) &&
            IsSeek(x.Name) &&
            x.Element.TryGetCurrentPattern(RangeValuePattern.Pattern, out _)).ToArray();
        if (seekers.Length != 1) throw new InvalidOperationException("没有唯一且范围明确的媒体时间线");
        var seek = seekers[0];
        // 当前没有原生播放器的稳定播放项身份时，不用窗口标题替代它。
        if (control.Document == null) throw new InvalidOperationException("原生媒体播放项身份尚不可核验");
        var context = documents.Single(x => x.Id == control.Document);
        var contextValue = context.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? ((ValuePattern)value).Current.Value : "";
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{context.Id}\n{context.Name}\n{contextValue}\n{scope}\n{control.Id}\n{seek.Id}")));
        var range = ((RangeValuePattern)seek.Element.GetCurrentPattern(RangeValuePattern.Pattern)).Current;
        var clocks = items.Where(x => x.Type == ControlType.Button && x.Parents.Contains(scope) &&
            MediaClock.TryParse(x.Name, out _, out _)).ToArray();
        if (clocks.Length != 1 || !MediaClock.TryParse(clocks[0].Name, out var clockPosition, out var clockDuration) ||
            Math.Abs(range.Maximum - clockDuration) > 1 || Math.Abs(range.Minimum) > 0.001 ||
            Math.Abs(range.Value - clockPosition) > 1)
            throw new InvalidOperationException("时间线秒单位或范围未得到独立语义核验");
        var result = new UiMedia(window, processId, identity, control.Element, seek.Element, Semantics(control.Name),
            range.Value, range.Minimum, range.Maximum, observedAt);
        if (!result.BeforeEnd) throw new InvalidOperationException("时间线无效或媒体已经结束");
        Program.GetWindowThreadProcessId(Program.GetForegroundWindow(), out var finalProcess);
        if (timer.ElapsedMilliseconds > 1500 || Program.GetForegroundWindow() != window || finalProcess != processId)
            throw new InvalidOperationException("完整媒体采样已过期");
        return result;
    }

    private static bool IsSeek(string name) => Regex.IsMatch(name.Trim(),
        @"^(seek(?: slider)?|position|timeline|定位|播放进度|进度|时间线)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool HasMediaSemantics(string label) => Regex.IsMatch(label,
        @"\b(?:video|audio|media)\s+player\b|视频播放器|媒体播放器|音频播放器|视频播放区域", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Invoke(Playback expected, InputWatch input, long epoch)
    {
        // 调用没有重试：provider 一旦接受动作，超时也不能假定已撤回。
        var current = Control.Current;
        var invoke = (InvokePattern)Control.GetCurrentPattern(InvokePattern.Pattern);
        if (!current.IsEnabled || current.IsOffscreen || Semantics(current.Name) != expected ||
            !input.Healthy || input.Epoch != epoch || Program.GetForegroundWindow() != Window ||
            Environment.TickCount64 - ObservedAt > 1500)
            throw new InvalidOperationException("执行前资格已改变");
        invoke.Invoke();
    }
}
