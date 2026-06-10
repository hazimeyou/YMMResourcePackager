using System.Text.Json;

namespace YMMResourcePackager.Shared;

public static class PackagingRules
{
    public static bool HasLegacyYmmpxLibFolder(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            return false;

        return Directory.Exists(GetLegacyYmmpxLibFolderPath(pluginDirectory));
    }

    public static string GetLegacyYmmpxLibFolderPath(string pluginDirectory) =>
        Path.Combine(pluginDirectory, "YmmpxLib");

    public static HashSet<string> NormalizeExcludedFiles(IEnumerable<string?>? excludedFiles)
    {
        return (excludedFiles ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsExcludedFile(string filePath, IEnumerable<string?>? excludedFiles)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return NormalizeExcludedFiles(excludedFiles).Contains(filePath.Trim());
    }

    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<string?>? excludedFiles)
    {
        var excluded = NormalizeExcludedFiles(excludedFiles);
        var jsonText = File.ReadAllText(projectPath);
        using JsonDocument doc = JsonDocument.Parse(jsonText);
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var files = FindFilePaths(doc.RootElement)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingCount = 0;
        foreach (var file in files)
        {
            if (excluded.Contains(file))
                continue;

            var resolved = ResolveMaterialPath(file, projectDir);
            if (resolved is null || !File.Exists(resolved))
                missingCount++;
        }

        return new PackagingValidationResult
        {
            DetectedMaterialCount = files.Length,
            ExcludedMaterialCount = files.Count(excluded.Contains),
            MissingMaterialCount = missingCount
        };
    }

    public static string GetStableAvailableFilePath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var pattern = $"^{System.Text.RegularExpressions.Regex.Escape(name)}_(\\d{{3}}){System.Text.RegularExpressions.Regex.Escape(ext)}$";
        var max = 0;

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, $"{name}_*{ext}"))
            {
                var fileName = Path.GetFileName(file);
                var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                    max = Math.Max(max, n);
            }
        }

        return Path.Combine(dir, $"{name}_{max + 1:000}{ext}");
    }

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

    private static string? ResolveMaterialPath(string filePath, string projectDir)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) && !uri.IsFile)
            return null;

        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);

        return Path.GetFullPath(Path.Combine(projectDir, filePath));
    }
}

public sealed class PackagingValidationResult
{
    public int DetectedMaterialCount { get; set; }
    public int ExcludedMaterialCount { get; set; }
    public int MissingMaterialCount { get; set; }
}
