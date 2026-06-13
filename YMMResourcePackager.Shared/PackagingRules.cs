using System.Text.Json;

namespace YMMResourcePackager.Shared;

public static class PackagingRules
{
    public static void Log(string message)
    {
        AppLogger.LogInfo(message);
    }

    // === 旧互換 / 移行対応 (Legacy) ===
    public static bool HasLegacyYmmpxLibFolder(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            return false;

        return Directory.Exists(GetLegacyYmmpxLibFolderPath(pluginDirectory));
    }

    public static string GetLegacyYmmpxLibFolderPath(string pluginDirectory) =>
        Path.Combine(pluginDirectory, "YmmpxLib");

    // === 除外 (Exclusion) ===
    public static HashSet<string> NormalizeExcludedFiles(IEnumerable<string?>? excludedFiles)
    {
        return PackagingExcluder.NormalizeExcludedFiles(excludedFiles);
    }

    // === 除外 (Exclusion) ===
    public static bool IsExcludedFile(string filePath, IEnumerable<string?>? excludedFiles)
    {
        return PackagingExcluder.IsExcludedFile(filePath, excludedFiles);
    }

    // === 除外 (Exclusion) ===
    // ここが除外の唯一の入口。
    // 入力された候補ファイルを、除外後の一覧へ変換する。
    public static HashSet<string> ResolveExcludedFiles(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
    {
        return PackagingExcluder.ResolveExcludedFiles(projectPath, excludedRules);
    }

    // === 除外 (Exclusion) ===
    // ResolveExcludedFiles から使う候補判定ヘルパー。
    public static bool IsExcludedFile(string filePath, string projectDir, IEnumerable<ExcludeRule>? excludedRules)
    {
        return PackagingExcluder.IsExcludedFile(filePath, projectDir, excludedRules);
    }

    // === 除外 (Exclusion) ===
    // ResolveExcludedFiles から使う除外ルール正規化ヘルパー。
    public static IReadOnlyList<ExcludeRule> NormalizeExcludeRules(IEnumerable<ExcludeRule>? excludedRules)
    {
        return PackagingExcluder.NormalizeExcludeRules(excludedRules);
    }

    // === 検証 (Validation) ===
    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<ExcludeRule>? excludedRules)
        => PackagingValidator.ValidateProjectBeforePack(projectPath, excludedRules);

    // === 検証 (Validation) ===
    public static PackagingValidationResult ValidateProjectBeforePack(string projectPath, IEnumerable<string?>? excludedFiles)
        => PackagingValidator.ValidateProjectBeforePack(projectPath, excludedFiles);

    // === 出力補助 (Output Helper) ===
    public static string GetStableAvailableFilePath(string path)
    {
        return PackagingOutputHelper.GetStableAvailableFilePath(path);
    }

    // === 出力補助 (Output Helper) ===
    public static string CreateTemporaryPackagePath(string finalPath)
    {
        return PackagingOutputHelper.CreateTemporaryPackagePath(finalPath);
    }

    // === 出力補助 (Output Helper) ===
    public static void MoveGeneratedPackage(string sourcePath, string destinationPath)
    {
        PackagingOutputHelper.MoveGeneratedPackage(sourcePath, destinationPath);
    }

    // === 検出 (Detection) ===
    // JSON 走査の補助。FilePath を再帰的に拾うだけで、ここでは最終出力にしない。
    public static IEnumerable<string> FindFilePaths(JsonElement root)
    {
        return PackagingDetector.FindFilePaths(root);
    }

    // === 検出 (Detection) ===
    // 素材パスの正規化補助。候補の解釈を整えるが、Detection の出口ではない。
    public static string? ResolveMaterialPath(string filePath, string projectDir)
    {
        return PackagingDetector.ResolveMaterialPath(filePath, projectDir);
    }

    // === 検出 (Detection) ===
    // JSON 読み込みと候補収集の補助。ここではまだ Detection を完結させない。
    private static string[] LoadProjectFilePaths(string projectPath)
    {
        return PackagingDetector.GetProjectFilePaths(projectPath).ToArray();
    }

    // === 検出 (Detection) ===
    // ここが Detection の最終出力。
    // 除外前の候補ファイル一覧を返し、ここから先は Exclusion に渡す。
    // Detection はここで完結する。
    public static IReadOnlyList<string> GetProjectFilePaths(string projectPath)
    {
        return PackagingDetector.GetProjectFilePaths(projectPath);
    }
}

public sealed class PackagingValidationResult
{
    public int DetectedMaterialCount { get; set; }
    public int ExcludedMaterialCount { get; set; }
    public int MissingMaterialCount { get; set; }
}
