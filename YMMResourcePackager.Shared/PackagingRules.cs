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

    public static HashSet<string> ResolveExcludedFiles(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var files = GetProjectFilePaths(projectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (IsExcludedFile(file, projectDir, excludedRules))
                excluded.Add(file);
        }

        return excluded;
    }

    public static bool IsExcludedFile(string filePath, string projectDir, IEnumerable<ExcludeRule>? excludedRules)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var rules = NormalizeExcludeRules(excludedRules).Where(x => x.IsExcluded).ToArray();
        if (rules.Length == 0)
            return false;

        var candidates = BuildPathCandidates(filePath, projectDir);
        foreach (var rule in rules)
        {
            var ruleCandidates = BuildRuleCandidates(rule.Path, projectDir);
            foreach (var candidate in candidates)
            {
                foreach (var ruleCandidate in ruleCandidates)
                {
                    if (MatchesRule(candidate, ruleCandidate, rule.IsFolder))
                        return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<ExcludeRule> NormalizeExcludeRules(IEnumerable<ExcludeRule>? excludedRules)
    {
        return (excludedRules ?? [])
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Path))
            .Select(x => new ExcludeRule
            {
                Path = NormalizeExcludePath(x.Path),
                IsFolder = x.IsFolder,
                IsExcluded = x.IsExcluded
            })
            .GroupBy(x => (x.Path, x.IsFolder), StringTupleComparer.Instance)
            .Select(g => g.Last())
            .ToArray();
    }

    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
    {
        var excluded = ResolveExcludedFiles(projectPath, excludedRules);
        var files = GetProjectFilePaths(projectPath);

        var missingCount = 0;
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
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
            DetectedMaterialCount = files.Count,
            ExcludedMaterialCount = excluded.Count,
            MissingMaterialCount = missingCount
        };
    }

    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<string?>? excludedFiles)
    {
        var excluded = NormalizeExcludedFiles(excludedFiles);
        var files = GetProjectFilePaths(projectPath);
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;

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
            DetectedMaterialCount = files.Count,
            ExcludedMaterialCount = files.Count(excluded.Contains),
            MissingMaterialCount = missingCount
        };
    }

    public static string GetStableAvailableFilePath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var pattern = $"^{System.Text.RegularExpressions.Regex.Escape(name)}_(\\d+){System.Text.RegularExpressions.Regex.Escape(ext)}$";
        var max = 0;

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var fileName = Path.GetFileName(file);
                if (!fileName.StartsWith($"{name}_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(Path.GetExtension(fileName), ext, StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(
                    fileName,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                    max = Math.Max(max, n);
            }
        }

        return Path.Combine(dir, $"{name}_{max + 1:D3}{ext}");
    }

    public static string CreateTemporaryPackagePath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(finalPath);
        var extension = Path.GetExtension(finalPath);
        return Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp{extension}");
    }

    public static void MoveGeneratedPackage(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("一時出力ファイルが見つかりません。", sourcePath);

        File.Move(sourcePath, destinationPath, overwrite: true);
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

    private static string[] LoadProjectFilePaths(string projectPath)
    {
        var jsonText = File.ReadAllText(projectPath);
        using JsonDocument doc = JsonDocument.Parse(jsonText);
        return FindFilePaths(doc.RootElement)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetProjectFilePaths(string projectPath)
    {
        return LoadProjectFilePaths(projectPath);
    }

    private static IReadOnlyList<string> BuildPathCandidates(string path, string projectDir)
    {
        var candidates = new List<string>();
        if (!TryNormalizeInputPath(path, out var normalizedInput))
            return candidates;

        var normalized = NormalizeExcludePath(normalizedInput);
        if (!string.IsNullOrWhiteSpace(normalized))
            candidates.Add(normalized);

        if (Path.IsPathRooted(normalizedInput))
        {
            var absolute = NormalizeExcludePath(Path.GetFullPath(normalizedInput));
            if (!string.IsNullOrWhiteSpace(absolute))
                candidates.Add(absolute);
        }
        else if (!string.IsNullOrWhiteSpace(projectDir))
        {
            var resolved = NormalizeExcludePath(Path.GetFullPath(Path.Combine(projectDir, normalizedInput)));
            if (!string.IsNullOrWhiteSpace(resolved))
                candidates.Add(resolved);
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildRuleCandidates(string path, string projectDir)
    {
        return BuildPathCandidates(path, projectDir);
    }

    private static bool MatchesRule(string candidate, string ruleCandidate, bool isFolder)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(ruleCandidate))
            return false;

        if (!isFolder)
            return string.Equals(candidate, ruleCandidate, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(candidate, ruleCandidate, StringComparison.OrdinalIgnoreCase))
            return true;

        var separator = Path.DirectorySeparatorChar;
        if (!ruleCandidate.EndsWith(separator))
            ruleCandidate += separator;

        return candidate.StartsWith(ruleCandidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExcludePath(string path)
    {
        if (!TryNormalizeInputPath(path, out var normalizedInput))
            return string.Empty;

        var normalized = normalizedInput
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetPathRoot(normalized);
        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(trimmed, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return trimmed;
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

    private sealed class StringTupleComparer : IEqualityComparer<(string Path, bool IsFolder)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string Path, bool IsFolder) x, (string Path, bool IsFolder) y)
        {
            return x.IsFolder == y.IsFolder &&
                   string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Path, bool IsFolder) obj)
        {
            return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path), obj.IsFolder);
        }
    }
}

public sealed class PackagingValidationResult
{
    public int DetectedMaterialCount { get; set; }
    public int ExcludedMaterialCount { get; set; }
    public int MissingMaterialCount { get; set; }
}
