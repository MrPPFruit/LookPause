using System.Globalization;
using System.Text.RegularExpressions;

namespace PausePal.Windows;

internal sealed record ClockReading(string Id, string[] Ancestors, double Position, double Duration);

internal static class MediaClock
{
    public static bool TrySplitDescription(string positionText, string durationText, out double position, out double duration)
    {
        position = duration = 0;
        var current = Regex.Match(positionText.Trim(), @"^(?:elapsed time|已播放时间)\s*[:：]\s*(?<time>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var total = Regex.Match(durationText.Trim(), @"^(?:total time|总时间|总时长)\s*[:：]\s*(?<time>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return current.Success && total.Success && Time(current.Groups["time"].Value, out position) &&
            Time(total.Groups["time"].Value, out duration) && duration > 0 && position >= 0 && position <= duration;
    }

    public static ClockReading? Unique(IReadOnlyList<ClockReading> readings)
    {
        // 同一组合控件与其子文字可能重复暴露相同时间；独立时钟不能只因数值相等就合并。
        var distinct = readings.Where(child => !readings.Any(parent => parent.Id != child.Id &&
            child.Ancestors.Contains(parent.Id) && parent.Position == child.Position && parent.Duration == child.Duration)).ToArray();
        if (distinct.Length > 1) throw new MediaObservationException("媒体时间来源有歧义，不能确认暂停归属");
        return distinct.SingleOrDefault();
    }

    public static bool TryParse(string text, out double position, out double duration)
    {
        position = duration = 0;
        var parts = text.Contains(" of ", StringComparison.OrdinalIgnoreCase)
            ? Regex.Split(text, " of ", RegexOptions.IgnoreCase) : text.Split('/');
        return parts.Length == 2 && Time(parts[0], out position) && Time(parts[1], out duration) &&
            duration > 0 && position >= 0 && position <= duration;
    }

    private static bool Time(string text, out double seconds)
    {
        seconds = 0;
        text = text.Trim();
        if (Regex.IsMatch(text, @"^\d{1,3}:\d{2}(?::\d{2})?$"))
        {
            var parts = text.Split(':').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (parts.Skip(1).Any(x => x >= 60)) return false;
            seconds = parts.Aggregate(0d, (sum, x) => sum * 60 + x);
            return true;
        }
        const string pattern = @"(\d+(?:\.\d+)?)\s*(hours?|minutes?|seconds?|小时|分钟?|秒)";
        if (Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase).Trim().Length != 0) return false;
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
        if (matches.Count == 0) return false;
        foreach (Match match in matches)
        {
            var amount = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var unit = match.Groups[2].Value.ToLowerInvariant();
            seconds += amount * (unit.StartsWith('h') || unit == "小时" ? 3600 : unit.StartsWith('m') || unit.StartsWith('分') ? 60 : 1);
        }
        return double.IsFinite(seconds);
    }
}
