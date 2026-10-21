using System.Text.RegularExpressions;

namespace PausePal.Windows;

internal enum MediaMember { Container, Time, Playback, Seek, Volume, Action, Foreign }
internal readonly record struct MediaClusterMember(MediaMember Kind, string Parent);

// 名称用于识别控件能力，不把容器显示名称当作媒体身份。
internal static class MediaCluster
{
    internal static bool IsClosed(string scope, IReadOnlyList<MediaClusterMember> members) =>
        !members.Any(x => x.Kind == MediaMember.Foreign) && members.Count(x => x.Kind == MediaMember.Playback) == 1 &&
        members.Count(x => x.Kind == MediaMember.Seek) == 1 && members.Count(x => x.Kind == MediaMember.Volume) == 1 &&
        members.Any(x => x.Kind == MediaMember.Action) &&
        members.Where(x => x.Kind is MediaMember.Playback or MediaMember.Seek or MediaMember.Volume)
            .All(x => x.Parent == scope);

    internal static bool IsSeek(string name) => Regex.IsMatch(name.Trim(),
        @"^(seek(?: slider)?|position|timeline|定位|播放进度|进度|时间线|视频时间进度条|音频时间进度条|video time (?:scrubber|slider)|audio time (?:scrubber|slider))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsVolume(string name) => Regex.IsMatch(name.Trim(),
        @"^(volume(?: slider)?|音量(?:滑块|滑动条)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsAction(string name) => Regex.IsMatch(name.Trim(),
        @"^(mute|unmute|静音|取消静音|fullscreen|full screen|exit full screen|进入全屏模式|退出全屏模式|全屏|退出全屏|显示更多媒体控件|show more media controls)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsTimeText(string text) => string.IsNullOrWhiteSpace(text) ||
        MediaClock.TryParse(text, out _, out _) || Regex.IsMatch(text.Trim(),
            @"^(?:\d{1,3}:\d{2}(?::\d{2})?|/|正在缓冲|正在加载|加载中\s*\.*|buffering|loading\s*\.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool SameDocumentBranch(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count > 0 && left.SequenceEqual(right);
}
