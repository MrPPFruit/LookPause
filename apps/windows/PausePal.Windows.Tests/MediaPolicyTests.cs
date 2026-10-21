using System.Text.Json;
using System.IO;
using PausePal.Windows;

internal static class MediaPolicyTests
{
    internal static Dictionary<string, Action> Checks()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"media-policy-cases.json")));
        if (document.RootElement.GetProperty("version").GetInt32() != 1) throw new Exception("共用媒体用例版本不受支持");
        var checks = new Dictionary<string, Action>();
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var test = item.Clone();
            checks.Add("共用媒体规则：" + test.GetProperty("title").GetString(), () =>
            {
                var actual = test.GetProperty("kind").GetString() switch
                {
                    "cluster" => MediaCluster.IsClosed(test.GetProperty("scope").GetString()!,
                        test.GetProperty("members").EnumerateArray().Select(x => new MediaClusterMember(
                            Enum.Parse<MediaMember>(x.GetProperty("kind").GetString()!),x.GetProperty("parent").GetString()!)).ToArray()),
                    "document" => MediaCluster.SameDocumentBranch(
                        test.GetProperty("left").EnumerateArray().Select(x=>x.GetString()!).ToArray(),
                        test.GetProperty("right").EnumerateArray().Select(x=>x.GetString()!).ToArray()),
                    _ => throw new Exception("共用媒体用例类型不受支持")
                };
                if (actual != test.GetProperty("expected").GetBoolean()) throw new Exception("共用媒体规则结果不符");
            });
        }
        return checks;
    }
}
