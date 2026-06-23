using System.Text.Json;

namespace YMMResourcePackager.Shared;

// 旧形式と新形式の両方を読み書きできる、除外ルールの永続化層。
public static class ExcludeRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<ExcludeRule> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return [];

        return LoadFromJson(File.ReadAllText(path));
    }

    public static List<ExcludeRule> LoadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // 新形式の Path ベース JSON を優先し、なければ旧形式 FilePath を読む。
                var hasModernShape = doc.RootElement.EnumerateArray()
                    .Any(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("Path", out _));

                if (hasModernShape)
                {
                    var modern = JsonSerializer.Deserialize<List<ExcludeRule>>(json) ?? [];
                    if (modern.Any(x => !string.IsNullOrWhiteSpace(x.Path)))
                        return modern;
                }

                var legacy = doc.RootElement.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("FilePath", out _))
                    .Select(x => new ExcludeRule
                    {
                        Path = x.TryGetProperty("FilePath", out var filePath) ? filePath.GetString() ?? string.Empty : string.Empty,
                        IsFolder = x.TryGetProperty("IsFolder", out var isFolder) && isFolder.ValueKind == JsonValueKind.True,
                        IsExcluded = !x.TryGetProperty("IsExcluded", out var excluded) || excluded.ValueKind != JsonValueKind.False
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Path))
                    .ToList();

                if (legacy.Count > 0)
                    return legacy;
            }
        }
        catch (JsonException)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ExcludeRule>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SaveToJson(IEnumerable<ExcludeRule> rules)
    {
        // 保存時は重複を除き、扱いやすい形に正規化してから書き出す。
        var normalized = PackagingRules.NormalizeExcludeRules(rules);
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }
}
