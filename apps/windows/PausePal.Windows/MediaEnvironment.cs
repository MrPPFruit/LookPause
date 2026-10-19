namespace PausePal.Windows;

// 用同一边界测试撤销与迟到观察；测试替身不能触达系统控件。
internal interface IMediaEnvironment : IRevocation
{
    long Now { get; }
    bool Healthy { get; }
    nint Foreground { get; }
    UiMedia Observe();
    void Invoke(UiMedia media, Playback expected, long epoch);
    RewindResult Rewind(UiMedia media, long epoch);
}

internal enum RewindResult { Skipped, Attempted }

internal sealed class WindowsMediaEnvironment(InputWatch input) : IMediaEnvironment
{
    public long Now => Environment.TickCount64;
    public long Epoch => input.Epoch;
    public void Revoke() => input.Revoke();
    public bool Healthy => input.Healthy;
    public nint Foreground => Program.GetForegroundWindow();
    public UiMedia Observe() => UiMedia.Read();
    public void Invoke(UiMedia media, Playback expected, long epoch) => media.Invoke(expected, input, epoch);
    public RewindResult Rewind(UiMedia media, long epoch) => MediaRewind.Try(media, input, epoch);
}
