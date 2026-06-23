using System.Text.Json;

namespace YMMResourcePackager.Shared;

public static class PackagingDetector
{
    public static IReadOnlyList<string> GetProjectFilePaths(string projectPath)
    {
        var paths = LoadProjectFilePaths(projectPath);
        PackagingRules.Log($"[Detect] ファイル検出: {paths.Length} 件");
        return paths;
    }

    public static string? ResolveMaterialPath(string filePath, string projectDir)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        if (!TryNormalizeInputPath(filePath, out var normalizedPath))
            return null;

        if (Path.IsPathRooted(normalizedPath))
            return Path.GetFullPath(normalizedPath);

        return Path.GetFullPath(Path.Combine(projectDir, normalizedPath));
    }

    // JSON 走査の補助。FilePath を再帰的に拾うだけで、ここでは最終出力にしない。
    public static IEnumerable<string> FindFilePaths(JsonElement root)
    {
        var stack = new Stack<JsonElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.NameEquals("FilePath") && prop.Value.ValueKind == JsonValueKind.String)
                        {
                            var path = prop.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(path))
                                yield return path;
                        }
                        else
                        {
                            stack.Push(prop.Value);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var child in element.EnumerateArray())
                        stack.Push(child);
                    break;
            }
        }
    }

    // JSON 読み込みと候補収集の補助。ここではまだ Detection を完結させない。
    private static string[] LoadProjectFilePaths(string projectPath)
    {
        var jsonText = File.ReadAllText(projectPath);
        using JsonDocument doc = JsonDocument.Parse(jsonText);
        return FindFilePaths(doc.RootElement)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryNormalizeInputPath(string path, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
                return false;

            trimmed = uri.LocalPath;
        }

        normalized = trimmed.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return true;
    }
}
