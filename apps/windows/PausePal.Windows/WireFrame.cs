using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PausePal.Windows;

internal sealed record WireFrame
{
    public int v { get; init; } = 1;
    public required string kind { get; init; }
    public string? deviceId { get; init; }
    public string? name { get; init; }
    public string? token { get; init; }
    public string? session { get; init; }
    public long? seq { get; init; }
    public string? intent { get; init; }
    public string? status { get; init; }
    public bool? ok { get; init; }
    public string? error { get; init; }

    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public byte[] Encode()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, Options);
        if (bytes.Length == 0 || bytes.Length > 4095) throw new InvalidDataException("消息过长");
        return [.. bytes, 10];
    }

    public static WireFrame Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > 4095) throw new InvalidDataException("无效消息长度");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("无效消息");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.EnumerateObject())
            if (!names.Add(item.Name)) throw new InvalidDataException("重复字段");
        if (root.GetProperty("v").GetInt32() != 1) throw new InvalidDataException("不支持的版本");
        var kind = root.GetProperty("kind").GetString();
        string[] allowed = kind switch
        {
            "hello" => ["v", "kind", "deviceId", "name", "token"],
            "start" or "stop" or "ping" => ["v", "kind", "session", "seq"],
            "intent" => ["v", "kind", "session", "seq", "intent"],
            _ => throw new InvalidDataException("不支持的操作")
        };
        if (names.Any(x => !allowed.Contains(x))) throw new InvalidDataException("不支持的字段");
        return JsonSerializer.Deserialize<WireFrame>(text, Options) ?? throw new InvalidDataException("无效消息");
    }
}

// 不使用 StreamReader 的无界 ReadLine；每帧含 LF 最多 4096 字节。
internal sealed class FrameReader(Stream stream)
{
    private readonly byte[] buffer = new byte[4096];
    private int count;
    public async Task<byte[]> ReadAsync(CancellationToken cancellation)
    {
        while (true)
        {
            var end = Array.IndexOf(buffer, (byte)10, 0, count);
            if (end >= 0)
            {
                if (end == 0) throw new InvalidDataException("空消息");
                var result = buffer[..end];
                count -= end + 1;
                Buffer.BlockCopy(buffer, end + 1, buffer, 0, count);
                return result;
            }
            if (count == buffer.Length) throw new InvalidDataException("消息过长");
            var received = await stream.ReadAsync(buffer.AsMemory(count), cancellation).ConfigureAwait(false);
            if (received == 0) throw new EndOfStreamException();
            count += received;
        }
    }
}
