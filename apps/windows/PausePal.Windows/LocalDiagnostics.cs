using System.Text;

namespace PausePal.Windows;

// 只接收本程序的固定诊断，最多两个 2 MiB 文件；不写原始帧、名称、URL 或凭据。
internal sealed class LocalDiagnostics : TextWriter
{
    private readonly object sync = new();
    private readonly string path;
    private StreamWriter? writer;
    public override Encoding Encoding => Encoding.UTF8;
    public LocalDiagnostics()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PausePal", "logs");
        Directory.CreateDirectory(directory); path = Path.Combine(directory, "receiver.log");
    }
    public override void WriteLine(string? value)
    {
        lock (sync)
        {
            try
            {
                if (writer?.BaseStream.Length >= 2 * 1024 * 1024 || writer == null && File.Exists(path) && new FileInfo(path).Length >= 2 * 1024 * 1024)
                {
                    CloseWriter(); File.Move(path, path + ".previous", true);
                }
                writer ??= new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(DateTimeOffset.Now.ToString("O") + " " + value);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException) { CloseWriter(); }
        }
    }
    private void CloseWriter()
    {
        var previous = writer; writer = null;
        try { previous?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException) { }
    }
    protected override void Dispose(bool disposing) { lock (sync) CloseWriter(); base.Dispose(disposing); }
}
