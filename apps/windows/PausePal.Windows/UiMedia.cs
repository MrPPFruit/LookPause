using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace PausePal.Windows;

internal enum Playback { Playing, Paused, Unknown }
internal enum TimelineSource { None, Text, SeekText, SeekDescription, Numeric }

internal sealed record UiMedia(nint Window, uint ProcessId, string Identity, AutomationElement Control,
    AutomationElement? Timeline, Playback State, double Position, double Minimum, double Maximum, long ObservedAt = 0)
{
    public AutomationElement? KeyboardSurface { get; init; }
    public TimelineSource Source { get; init; } = TimelineSource.Numeric;
    public bool Rounded => Source is TimelineSource.Text or TimelineSource.SeekText or TimelineSource.SeekDescription;
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
            if (current.ControlType == ControlType.Slider && !MediaCluster.IsSeek(current.Name)) continue;
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
        if (control.Document == null) throw new MediaObservationException("原生媒体播放项身份尚不可核验");
        var localAncestors = control.Parents.Reverse().TakeWhile(id => id != control.Document).ToArray();
        var scope = localAncestors.FirstOrDefault(id => id != rootId &&
            regions.TryGetValue(id, out var region) && HasMediaSemantics(region.Label));
        if (scope == null)
        {
            foreach (var id in localAncestors)
            {
                if (regions.TryGetValue(id, out var region) && ClosedCluster(region.Element, control.Document, timer, cache))
                { scope = id; break; }
            }
        }
        if (scope == null) throw new MediaObservationException("未找到可核验的局部媒体控件组");
        var seekers = items.Where(x => x.Visible && x.Enabled && x.Document == control.Document && x.Type == ControlType.Slider &&
            x.Parents.Contains(scope) &&
            MediaCluster.IsSeek(x.Name)).ToArray();
        if (seekers.Length > 1) throw new MediaObservationException("存在多个媒体时间线，无法确定目标");
        var seek = seekers.SingleOrDefault();
        // 当前没有原生播放器的稳定播放项身份时，不用窗口标题替代它。
        var context = documents.Single(x => x.Id == control.Document);
        var contextValue = context.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? ((ValuePattern)value).Current.Value : "";
        var documentBranch = string.Join("\n", control.Parents.Where(id => documents.Any(d => d.Id == id)));
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{documentBranch}\n{context.Id}\n{context.Name}\n{contextValue}\n{scope}\n{control.Id}\n{seek?.Id}")));
        var clock = ReadClock(regions[scope].Element, scope, control.Document, timer, cache);
        var source = clock == null ? TimelineSource.None : TimelineSource.Text;
        if (clock == null && seek != null && seek.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var description))
        {
            var text = ((ValuePattern)description).Current.Value;
            if (MediaClock.TryParse(text, out var position, out var duration))
            { clock = new ClockReading(seek.Id, [], position, duration); source = TimelineSource.SeekText; }
            else
            {
                var full = NativeUiaDescription.Read(seek.Element, window,
                    (int[])seek.Element.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
                if (full != null && MediaClock.TrySplitDescription(text, full, out position, out duration))
                { clock = new ClockReading(seek.Id, [], position, duration); source = TimelineSource.SeekDescription; }
            }
        }
        // 无单位 RangeValue 不当作秒；时间缺失与结束分开，缺失不能获得恢复资格。
        var result = new UiMedia(window, processId, identity, control.Element, seek?.Element, Semantics(control.Name),
            clock?.Position ?? double.NaN, clock == null ? double.NaN : 0, clock?.Duration ?? double.NaN, observedAt)
            { Source = source, KeyboardSurface = regions[scope].Element };
        if (result.HasTimeline && !result.BeforeEnd) throw new MediaObservationException("媒体已经结束，未操作");
        Program.GetWindowThreadProcessId(Program.GetForegroundWindow(), out var finalProcess);
        if (timer.ElapsedMilliseconds > 1500 || Program.GetForegroundWindow() != window || finalProcess != processId)
            throw new MediaObservationException("完整媒体采样已过期");
        return result;
    }

    private static bool ClosedCluster(AutomationElement scope, string document, Stopwatch timer, CacheRequest cache)
    {
        if (scope.Cached.IsOffscreen || scope.Cached.BoundingRectangle.IsEmpty) return false;
        AutomationElementCollection nodes;
        using (cache.Activate()) nodes = scope.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        if (timer.ElapsedMilliseconds > 1500 || nodes.Count > 300) throw new MediaObservationException("媒体控件组读取不完整或超时");
        var members = new List<MediaClusterMember>();
        var scopeId = string.Join(".", (int[])scope.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
        foreach (AutomationElement node in nodes)
        {
            if (timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体控件组读取超时");
            var current = node.Cached;
            var type = current.ControlType;
            if (current.IsOffscreen) continue;
            // 收起的音量条可无显示区域，仅用作结构证据；不向它提交动作。
            var auxiliaryVolume = type == ControlType.Slider && current.IsEnabled && MediaCluster.IsVolume(current.Name);
            if (current.BoundingRectangle.IsEmpty && !auxiliaryVolume) continue;
            if (type == ControlType.Document) return false;
            MediaMember member;
            if (type == ControlType.Group || type == ControlType.Pane) member = MediaMember.Container;
            else if (type == ControlType.Text) member = MediaCluster.IsTimeText(current.Name) ? MediaMember.Time : MediaMember.Foreign;
            else if (type == ControlType.Button && current.IsEnabled && Semantics(current.Name) != Playback.Unknown && node.TryGetCurrentPattern(InvokePattern.Pattern, out _)) member = MediaMember.Playback;
            else if (type == ControlType.Slider && current.IsEnabled && MediaCluster.IsSeek(current.Name)) member = MediaMember.Seek;
            else if (type == ControlType.Slider && current.IsEnabled && MediaCluster.IsVolume(current.Name)) member = MediaMember.Volume;
            else if (type == ControlType.Button && current.IsEnabled && MediaCluster.IsAction(current.Name)) member = MediaMember.Action;
            else return false;
            var parent = TreeWalker.ControlViewWalker.GetParent(node, cache);
            if (parent == null) throw new MediaObservationException("媒体控件组已改变");
            members.Add(new(member, string.Join(".", (int[])parent.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty))));
        }
        return MediaCluster.IsClosed(scopeId, members) && NearestDocument(scope, cache, timer) == document;
    }

    private static string? NearestDocument(AutomationElement node, CacheRequest cache, Stopwatch timer)
    {
        for (var depth = 0; depth < 35; depth++)
        {
            if (timer.ElapsedMilliseconds > 1500) throw new MediaObservationException("媒体文档读取超时");
            if (node.Cached.ControlType == ControlType.Document)
                return string.Join(".", (int[])node.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty));
            node = TreeWalker.ControlViewWalker.GetParent(node, cache);
            if (node == null) return null;
        }
        throw new MediaObservationException("媒体文档观察不完整");
    }

    private static ClockReading? ReadClock(AutomationElement scope, string scopeId, string document, Stopwatch timer, CacheRequest cache)
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
            if (NearestDocument(node, cache, timer) != document) return;
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
