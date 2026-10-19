namespace PausePal.Windows;

// 心跳中断后粘性失效；计时器重新运行也不能复活原监听资格。
internal sealed class InputHeartbeat(long startedAt, Action invalidate)
{
    private long last = startedAt;
    private int failed;
    internal long LastPulse => Interlocked.Read(ref last);
    public bool Healthy(long now)
    {
        if (now - Interlocked.Read(ref last) >= 1000) Fail();
        return Volatile.Read(ref failed) == 0;
    }
    public void Pulse(long now)
    {
        var previous = Interlocked.Read(ref last);
        if (now - previous >= 1000) Fail();
        Interlocked.Exchange(ref last, now);
    }
    private void Fail()
    {
        if (Interlocked.Exchange(ref failed, 1) == 0) invalidate();
    }
}
