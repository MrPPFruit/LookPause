using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace PausePal.Windows;

internal enum Playback { Playing, Paused, Unknown }
internal enum TimelineSource { None, Text, SeekText, Numeric }

internal sealed record UiMedia(nint Window, uint ProcessId, string Identity, AutomationElement Control,
    AutomationElement? Timeline, Playback State, double Position, double Minimum, double Maximum, long ObservedAt = 0)
{
    public TimelineSource Source { get; init; } = TimelineSource.Numeric;
    public bool Rounded => Source is TimelineSource.Text or TimelineSource.SeekText;
    public bool HasTimeline => Source != TimelineSource.None && double.IsFinite(Position) && double.IsFinite(Minimum) &&
        double.IsFinite(Maximum) && Minimum >= 0 && Maximum > Minimum && Position >= Minimum && Position <= Maximum;
    public bool BeforeEnd => HasTimeline && Position < Maximum - 0.05;

    public bool SameTarget(UiMedia other) => Window == other.Window && ProcessId == other.ProcessId &&
        Identity == other.Identity;

    public bool SameTimeline(UiMedia other) => HasTimeline && other.HasTimeline && Source == other.Source &&
        Minimum == other.Minimum && Maximum == other.Maximum;

    public bool HoldsPause(UiMedia other) => SameTarget(other) && SameTimeline(other) && BeforeEnd && other.BeforeEnd &&
        other.State == Playback.Paused && Math.Abs(Position - other.Position) <= (Rounded ? 0 : 0.05);

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
        if (window == 0) throw new MediaObservationException("没有可核验前台");
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
        var regions = new Dictionary<string, (AutomationElement Element, string Label)>();
        var walker = TreeWalker.ControlViewWalker;
        AutomationElementCollection relevant;
        using (cache.Activate()) relevant = root.FindAll(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document)));
        if (relevant.Count > 500 || timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体控件查询不完整或超时");
        foreach (AutomationElement node in relevant)
        {
            if (timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体控件读取超时");
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
                if (parents.Count >= 35 || timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体祖先观察不完整");
                var ancestorId = string.Join(".", (int[])ancestor.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
                parents.Add(ancestorId);
                if (ancestor.Cached.ControlType == ControlType.Group || ancestor.Cached.ControlType == ControlType.Pane)
                    regions.TryAdd(ancestorId, (ancestor, ancestor.Cached.Name + " " + ancestor.Cached.HelpText));
                if (document == null && ancestor.Cached.ControlType == ControlType.Document) document = ancestorId;
                if (ancestorId == rootId) break;
                ancestor = walker.GetParent(ancestor, cache);
            }
            if (parents.LastOrDefault() != rootId) throw new MediaObservationException("媒体不再属于当前前台");
            parents.Reverse();
            items.Add(new Item(node, id, document, [.. parents], current.ControlType, current.Name, true, true));
        }
        if (timer.ElapsedMilliseconds > 1500 || Program.GetForegroundWindow() != window)
            throw new MediaObservationException("前台观察已过期");
        var documents = items.Where(x => x.Visible && x.Type == ControlType.Document).ToArray();
        if (documents.Length > 1) throw new MediaObservationException("前台内容有歧义");
        var controls = items.Where(x => x.Visible && x.Enabled && x.Type == ControlType.Button &&
            Semantics(x.Name) != Playback.Unknown && x.Element.TryGetCurrentPattern(InvokePattern.Pattern, out _)).ToArray();
        if (controls.Length != 1)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { kind = "media-candidates", documents = documents.Length,
                matchingButtons = items.Count(x => x.Type == ControlType.Button && Semantics(x.Name) != Playback.Unknown),
                invokableButtons = controls.Length, relevantNodes = relevant.Count, elapsedMs = timer.ElapsedMilliseconds }));
            throw new MediaObservationException(controls.Length == 0 ? "未找到可识别的播放控件" : "存在多个播放控件，无法确定目标");
        }
        var control = controls[0];
        var scope = control.Parents.Reverse().FirstOrDefault(id => id != control.Document && id != rootId &&
            regions.TryGetValue(id, out var region) && HasMediaSemantics(region.Label));
        if (scope == null) throw new MediaObservationException("缺少明确的媒体语义区域");
        var seekers = items.Where(x => x.Visible && x.Enabled && x.Document == control.Document && x.Type == ControlType.Slider &&
            x.Parents.Contains(scope) &&
            IsSeek(x.Name)).ToArray();
        if (seekers.Length > 1) throw new MediaObservationException("存在多个媒体时间线，无法确定目标");
        var seek = seekers.SingleOrDefault();
        // 当前没有原生播放器的稳定播放项身份时，不用窗口标题替代它。
        if (control.Document == null) throw new MediaObservationException("原生媒体播放项身份尚不可核验");
        var context = documents.Single(x => x.Id == control.Document);
        var contextValue = context.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? ((ValuePattern)value).Current.Value : "";
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{context.Id}\n{context.Name}\n{contextValue}\n{scope}\n{control.Id}\n{seek?.Id}")));
        var clock = ReadClock(regions[scope].Element, scope, timer, cache);
        var source = clock == null ? TimelineSource.None : TimelineSource.Text;
        if (clock == null && seek != null && seek.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var description) &&
            MediaClock.TryParse(((ValuePattern)description).Current.Value, out var position, out var duration))
        { clock = new ClockReading(seek.Id, [], position, duration); source = TimelineSource.SeekText; }
        // 无单位 RangeValue 不当作秒；时间缺失与结束分开，缺失不能获得恢复资格。
        var result = new UiMedia(window, processId, identity, control.Element, seek?.Element, Semantics(control.Name),
            clock?.Position ?? double.NaN, clock == null ? double.NaN : 0, clock?.Duration ?? double.NaN, observedAt) { Source = source };
        if (result.HasTimeline && !result.BeforeEnd) throw new MediaObservationException("媒体已经结束，未操作");
        Program.GetWindowThreadProcessId(Program.GetForegroundWindow(), out var finalProcess);
        if (timer.ElapsedMilliseconds > 1500 || Program.GetForegroundWindow() != window || finalProcess != processId)
            throw new MediaObservationException("完整媒体采样已过期");
        return result;
    }

    private static ClockReading? ReadClock(AutomationElement scope, string scopeId, Stopwatch timer, CacheRequest cache)
    {
        void Check() { if (timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体时间读取超时"); }
        var walker = TreeWalker.ControlViewWalker;
        AutomationElementCollection nodes;
        using (cache.Activate()) nodes = scope.FindAll(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
        Check();
        if (nodes.Count > 300) throw new MediaObservationException("媒体时间查询不完整");
        var readings = new List<ClockReading>();
        var groups = new Dictionary<string, AutomationElement>();
        bool Visible(AutomationElement node) => !node.Cached.IsOffscreen && !node.Cached.BoundingRectangle.IsEmpty;
        string Id(AutomationElement node) => string.Join(".", (int[])node.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
        void Add(AutomationElement node, string text)
        {
            if (text.Length > 512 || !MediaClock.TryParse(text, out var position, out var duration)) return;
            var ancestors = new List<string>();
            var parent = walker.GetParent(node, cache);
            while (parent != null && ancestors.Count < 35)
            {
                Check(); var id = Id(parent); ancestors.Add(id);
                if (id == scopeId) break;
                parent = walker.GetParent(parent, cache);
            }
            if (ancestors.LastOrDefault() != scopeId) throw new MediaObservationException("媒体时间不再属于目标区域");
            readings.Add(new ClockReading(Id(node), [.. ancestors], position, duration));
        }
        foreach (AutomationElement node in nodes)
        {
            Check();
            if (!Visible(node)) continue;
            Add(node, node.Cached.Name);
            if (node.Cached.ControlType != ControlType.Text) continue;
            var parent = walker.GetParent(node, cache);
            if (parent != null && (parent.Cached.ControlType == ControlType.Group || parent.Cached.ControlType == ControlType.Pane))
                groups.TryAdd(Id(parent), parent);
        }
        foreach (var (id, group) in groups)
        {
            Check();
            if (id == scopeId || !Visible(group)) continue;
            var values = new List<string>();
            var child = walker.GetFirstChild(group, cache);
            while (child != null && values.Count < 9)
            {
                Check();
                if (child.Cached.ControlType != ControlType.Text || !Visible(child)) break;
                values.Add(child.Cached.Name); child = walker.GetNextSibling(child, cache);
            }
            if (child == null && values.Count is >= 2 and <= 8) Add(group, string.Concat(values));
        }
        Check();
        return MediaClock.Unique(readings);
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
