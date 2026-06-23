namespace YMMResourcePackager.Shared;

public static class PackagingExcluder
{
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

    // ここが除外の唯一の入口。
    // 入力された候補ファイルを、除外後の一覧へ変換する。
    public static HashSet<string> ResolveExcludedFiles(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var files = PackagingDetector.GetProjectFilePaths(projectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (IsExcludedFile(file, projectDir, excludedRules))
                excluded.Add(file);
        }

        PackagingRules.Log($"[Exclude] 除外後: {excluded.Count} 件");
        // ここから先は Detection には戻らない。
        return excluded;
    }

    // ResolveExcludedFiles から使う候補判定ヘルパー。
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

    // ResolveExcludedFiles から使う除外ルール正規化ヘルパー。
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

    // ResolveExcludedFiles から使うパス候補生成ヘルパー。
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

    // ResolveExcludedFiles から使うルール候補生成ヘルパー。
    private static IReadOnlyList<string> BuildRuleCandidates(string path, string projectDir)
    {
        return BuildPathCandidates(path, projectDir);
    }

    // ResolveExcludedFiles から使う一致判定ヘルパー。
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

    // ResolveExcludedFiles から使う除外パス正規化ヘルパー。
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

    // ResolveExcludedFiles から使う入力パス正規化ヘルパー。
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
