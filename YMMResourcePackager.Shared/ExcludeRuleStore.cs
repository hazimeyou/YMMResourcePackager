using System.Text.Json;

namespace YMMResourcePackager.Shared;

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
        }

        return JsonSerializer.Deserialize<List<ExcludeRule>>(json) ?? [];
    }

    public static string SaveToJson(IEnumerable<ExcludeRule> rules)
    {
        var normalized = PackagingRules.NormalizeExcludeRules(rules);
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }
}
