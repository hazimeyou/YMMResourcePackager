namespace YMMResourcePackager.Shared;

public static class PackagingValidator
{
    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
    {
        var excluded = PackagingRules.ResolveExcludedFiles(projectPath, excludedRules);
        var files = PackagingRules.GetProjectFilePaths(projectPath);

        var missingCount = 0;
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        foreach (var file in files)
        {
            if (excluded.Contains(file))
                continue;

            var resolved = PackagingRules.ResolveMaterialPath(file, projectDir);
            if (resolved is null || !File.Exists(resolved))
                missingCount++;
        }

        var result = new PackagingValidationResult
        {
            DetectedMaterialCount = files.Count,
            ExcludedMaterialCount = excluded.Count,
            MissingMaterialCount = missingCount
        };

        LogValidationResult(result);
        return result;
    }

    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<string?>? excludedFiles)
    {
        var excluded = PackagingRules.NormalizeExcludedFiles(excludedFiles);
        var files = PackagingRules.GetProjectFilePaths(projectPath);
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;

        var missingCount = 0;
        foreach (var file in files)
        {
            if (excluded.Contains(file))
                continue;

            var resolved = PackagingRules.ResolveMaterialPath(file, projectDir);
            if (resolved is null || !File.Exists(resolved))
                missingCount++;
        }

        var result = new PackagingValidationResult
        {
            DetectedMaterialCount = files.Count,
            ExcludedMaterialCount = files.Count(excluded.Contains),
            MissingMaterialCount = missingCount
        };

        LogValidationResult(result);
        return result;
    }

    private static void LogValidationResult(PackagingValidationResult result)
    {
        if (result.MissingMaterialCount == 0)
        {
            PackagingRules.Log("[Validate] 検証OK");
            return;
        }

        PackagingRules.Log($"[Validate] 不足: {result.MissingMaterialCount} 件");
    }
}
