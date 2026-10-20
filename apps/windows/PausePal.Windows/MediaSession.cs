using System.Text.Json;

namespace PausePal.Windows;

internal enum WatchingIntent { ATTENDING, AWAY, NO_FACE, UNKNOWN }
internal enum MediaPhase { Idle, Checking, Pausing, Owned, Resuming }

// 只有此串行 owner 能改变媒体归属并提交固定媒体控件动作。
internal sealed class MediaSession(IMediaEnvironment environment)
{
    public bool Watching { get; private set; }
    public WatchingIntent Intent { get; private set; } = WatchingIntent.UNKNOWN;
    public MediaPhase Phase { get; private set; }
    public string Status { get; private set; } = "尚未开始观影";
    private UiMedia? target;
    private long epoch;
    private long deadline;
    private long lastObservation;
    public event Action<string>? Changed;

    public void Start()
    {
        Clear(); Watching = true; Intent = WatchingIntent.UNKNOWN;
        SetStatus("观影中，等待观看方向");
    }

    public void Stop(string reason = "已停止观影")
    {
        Watching = false; Intent = WatchingIntent.UNKNOWN; Clear(); SetStatus(reason);
    }

    public void Receive(WatchingIntent intent, long? receivedEpoch = null)
    {
        var deliveryEpoch = receivedEpoch ?? environment.Epoch;
        if (!Watching || deliveryEpoch != environment.Epoch) return;
        var previous = Intent;
        Intent = intent;
        if (intent == WatchingIntent.UNKNOWN)
        {
            if (Phase == MediaPhase.Owned) SetStatus("识别暂时未知，视频保持暂停");
            else if (Phase != MediaPhase.Pausing) Cancel("识别未知，未提交新操作");
        }
        else if (intent is WatchingIntent.AWAY or WatchingIntent.NO_FACE && previous is not (WatchingIntent.AWAY or WatchingIntent.NO_FACE) && Phase == MediaPhase.Idle)
            Pause(deliveryEpoch);
        else if (intent == WatchingIntent.ATTENDING && Phase == MediaPhase.Owned)
            Resume();
        else if (intent == WatchingIntent.ATTENDING && Phase == MediaPhase.Checking)
            Cancel("看向屏幕，取消待定暂停");
    }

    private void Pause(long? receivedEpoch = null)
    {
        try
        {
            epoch = receivedEpoch ?? environment.Epoch;
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "qualification-start", epoch, input = environment.InputDiagnostics }));
            if (!environment.Healthy || environment.Epoch != epoch) throw new InvalidOperationException("暂停请求已被撤销");
            var observed = environment.Observe();
            if (observed.State != Playback.Playing) { Cancel("视频已暂停，交由你控制"); return; }
            if (environment.Epoch != epoch || !environment.Healthy || !Watching || environment.Now - observed.ObservedAt > 1500)
                throw new InvalidOperationException("暂停前采样过期或资格已变");
            target = observed; Phase = MediaPhase.Checking;
            deadline = environment.Now + 3000;
            lastObservation = environment.Now;
            SetStatus("正在核验实际播放进度");
        }
        catch (Exception error) { Diagnose("pause", error); Cancel(FailureStatus(error)); }
    }

    private void Resume()
    {
        try
        {
            if (!Valid() || target == null || environment.Now - lastObservation > 2000) throw new InvalidOperationException("恢复前观察不连续");
            var observed = environment.Observe();
            if (!Valid() || !target.HoldsPause(observed) || environment.Now - observed.ObservedAt > 1500 || environment.Now - lastObservation > 2000)
                throw new InvalidOperationException("恢复前采样过期或暂停资格已变");
            Phase = MediaPhase.Resuming; deadline = environment.Now + 2000;
            environment.Invoke(observed, Playback.Paused, epoch);
            SetStatus("正在确认恢复");
        }
        catch (Exception error) { Diagnose("resume", error); Cancel("恢复资格已改变，交由你控制"); }
    }

    public void Tick()
    {
        if (Phase == MediaPhase.Idle) return;
        try
        {
            var now = environment.Now;
            if (!Valid() || now - lastObservation > 2000 || target == null) throw new InvalidOperationException("观察前资格无效");
            var observed = environment.Observe();
            if (!Valid() || !target.SameTarget(observed) || environment.Now - observed.ObservedAt > 1500)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "target-check", same = target.SameTarget(observed),
                    valid = Valid(), previous = target.Identity, current = observed.Identity,
                    previousMin = target.Minimum, previousMax = target.Maximum, observed.Minimum, observed.Maximum, observed.Position, state = observed.State.ToString() }));
                throw new InvalidOperationException("观察后资格或目标已变");
            }
            now = environment.Now;
            if (now - lastObservation > 2000) throw new InvalidOperationException("观察间隔超限");
            lastObservation = now;
            if (Phase == MediaPhase.Checking)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "progress-check", from = target.Position,
                    to = observed.Position, sampleAgeMs = now - observed.ObservedAt, remainingMs = deadline - now,
                    state = observed.State.ToString() }));
                if (now > deadline || observed.State != Playback.Playing) throw new MediaObservationException("未观察到实际播放进度");
                if (observed.Position > target.Position)
                {
                    if (observed.Position - target.Position > (now - (deadline - 3000)) / 1000.0 + 1)
                        throw new InvalidOperationException("播放进度发生未解释跳变");
                    target = observed; Phase = MediaPhase.Pausing; deadline = now + 2000;
                    environment.Invoke(observed, Playback.Playing, epoch);
                    SetStatus("正在确认暂停");
                }
            }
            else if (Phase == MediaPhase.Pausing)
            {
                if (now > deadline) throw new InvalidOperationException("暂停确认超时");
                if (observed.State == Playback.Paused)
                {
                    target = observed; Phase = MediaPhase.Owned;
                    SetStatus(Intent == WatchingIntent.UNKNOWN ? "识别暂时未知，视频保持暂停" : "已自动暂停，等待回看");
                    if (Intent == WatchingIntent.ATTENDING) Resume();
                }
            }
            else if (Phase == MediaPhase.Owned)
            {
                if (!target.HoldsPause(observed)) throw new InvalidOperationException("暂停锚点或状态已变");
            }
            else if (Phase == MediaPhase.Resuming)
            {
                if (now > deadline) throw new InvalidOperationException("恢复确认超时");
                if (observed.State == Playback.Playing && observed.Position > target.Position)
                {
                    var completedEpoch = epoch;
                    // 先消费本轮恢复资格；回退失败也不重新授予或补发播放。
                    Clear();
                    var rewind = Intent == WatchingIntent.ATTENDING && environment.Healthy && environment.Epoch == completedEpoch
                        ? environment.Rewind(observed, completedEpoch) : RewindResult.Skipped;
                    SetStatus(rewind == RewindResult.Attempted ? "已确认恢复播放，已尝试一次回退（效果待确认）" : "已确认恢复播放，安全回退不可用", observed.Position);
                    if (Intent is WatchingIntent.AWAY or WatchingIntent.NO_FACE) Pause(completedEpoch);
                }
            }
        }
        catch (Exception error) { Diagnose("tick", error); Cancel(FailureStatus(error)); }
    }

    private bool Valid() => Watching && environment.Healthy && environment.Epoch == epoch && target != null &&
        environment.Foreground == target.Window;
    private string FailureStatus(Exception error) => !environment.Healthy ? "输入监听已失效，请退出并重新打开 PausePal" :
        environment.Epoch != epoch ? "检测到输入或连接变化，恢复资格已取消" :
        error is MediaObservationException ? error.Message + "，本轮自动控制已取消" : "媒体或观察资格已改变，本轮自动控制已取消";
    private void Clear() { target = null; Phase = MediaPhase.Idle; }
    private void Cancel(string reason, double? observedPosition = null) { Clear(); SetStatus(reason, observedPosition); }
    private void Diagnose(string stage, Exception error) => Console.WriteLine(JsonSerializer.Serialize(new {
        kind = "qualification-failure", stage, phase = Phase.ToString(), healthy = environment.Healthy, input = environment.InputDiagnostics,
        sameInput = environment.Epoch == epoch, sameFront = target != null && environment.Foreground == target.Window,
        gapMs = environment.Now - lastObservation,
        reason = error is MediaObservationException || error.GetType() == typeof(InvalidOperationException) ? error.Message : error.GetType().Name }));
    private void SetStatus(string value, double? observedPosition = null)
    {
        Status = value;
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "media", phase = Phase.ToString(), status = value,
            watching = Watching, intent = Intent.ToString(), position = observedPosition ?? target?.Position }));
        Changed?.Invoke(value);
    }
}
