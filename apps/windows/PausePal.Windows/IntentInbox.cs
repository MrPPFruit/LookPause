using System.Threading.Channels;

namespace PausePal.Windows;

internal interface IRevocation
{
    long Epoch { get; }
    void Revoke();
}

internal readonly record struct PendingIntent(string Value, long Epoch);

// 撤销在入口立刻生效，不能等待被 UIA 调用占用的媒体 owner。
internal sealed class IntentInbox(IRevocation revocation)
{
    private readonly Channel<PendingIntent> channel = Channel.CreateBounded<PendingIntent>(16);
    private int closed;
    public bool Closed => Volatile.Read(ref closed) != 0;

    public bool Post(string value)
    {
        if (Closed) return false;
        if (value is "STOP" or "DISCONNECT" or "EXIT") revocation.Revoke();
        if (value.Length > 32 || !channel.Writer.TryWrite(new PendingIntent(value, revocation.Epoch)))
        {
            Complete(); return false;
        }
        return true;
    }

    public bool TryRead(out PendingIntent command)
    {
        while (!Closed && channel.Reader.TryRead(out command))
            if (command.Value is "STOP" or "DISCONNECT" or "EXIT" || command.Epoch == revocation.Epoch) return true;
        command = default;
        return false;
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref closed, 1) == 0) revocation.Revoke();
        channel.Writer.TryComplete();
    }
}
