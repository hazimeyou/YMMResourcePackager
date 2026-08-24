using YmmpxLibV2;
using YMMResourcePackager.Shared;

namespace YMMResourcePackager.Features;

/// <summary>Type-safe YMMPX v2 boundary used by the plugin UI and unpacker.</summary>
public static class EntryPoint
{
    public static async Task RunPackAsync(string projectPath, string outputPath, string[] excludedFiles,
        bool includeProjectUiSettings, Action<string, double, long, long>? progress)
    {
        try
        {
            AppLogger.LogInfo("YMMPX v2 package creation started.");
            var options = new YmmpxV2WriteOptions
            {
                ExcludedResources = excludedFiles.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray(),
                IncludeProjectUiSettings = includeProjectUiSettings,
                Progress = progress is null ? null : new Progress<YmmpxV2WriteProgress>(value =>
                    progress(GetProgressMessage(value.Stage), value.Fraction * 100, value.Current, value.Total))
            };
            await YmmpxV2Writer.WriteAsync(new YmmpxV2WriteRequest(projectPath, outputPath) { Options = options }).ConfigureAwait(false);
            AppLogger.LogInfo("YMMPX v2 package creation succeeded.");
        }
        catch (Exception ex)
        {
            AppLogger.LogException(ex, "YMMPX v2 package creation failed.");
            throw new YmmpxPackageOperationException(YmmpxPackageOperationError.ProcessingFailed,
                "パッケージの作成に失敗しました。ログを確認してください。", ex);
        }
    }

    public static string GetAvailableUnpackDirectory(string desiredDirectory)
    {
        var candidate = desiredDirectory;
        var suffix = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate))
            candidate = $"{desiredDirectory}_{suffix++}";
        return candidate;
    }

    public static string RunUnpack(string ymmpxPath, string extractDirectory, out int replacedPathCount)
    {
        var result = RunUnpackAsync(ymmpxPath, extractDirectory).GetAwaiter().GetResult();
        replacedPathCount = result.ReplacedPathCount;
        return result.ProjectFilePath;
    }

    public static async Task<YmmpxUnpackResult> RunUnpackAsync(string ymmpxPath, string extractDirectory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AppLogger.LogInfo("YMMPX package extraction started.");
            await using var stream = new FileStream(ymmpxPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
            var detection = await YmmpxFormatDetector.DetectAsync(stream, cancellationToken).ConfigureAwait(false);
            AppLogger.LogInfo($"YMMPX detected format: {detection.Status}; route: {detection.ReaderRoute}.");
            if (!detection.IsSupported)
                throw CreateUnsupportedFormatException(detection);

            stream.Seek(0, SeekOrigin.Begin);
            await using var session = detection.ReaderRoute switch
            {
                YmmpxReaderRoute.LegacyV1 => await LegacyV1Reader.OpenAsync(stream, cancellationToken).ConfigureAwait(false),
                YmmpxReaderRoute.V2 => await YmmpxV2Reader.OpenAsync(stream, cancellationToken).ConfigureAwait(false),
                _ => throw CreateUnsupportedFormatException(detection)
            };

            var references = ProjectResourceReferenceMapper.FromPackage(session.Package);
            var resolution = YmmpxProjectReferenceResolver.Resolve(session.Package.Project, references, extractDirectory, cancellationToken);
            await YmmpxPackageExtractor.ExtractAsync(session, extractDirectory,
                new YmmpxExtractionOptions { ProjectOverride = resolution.Project }, cancellationToken).ConfigureAwait(false);
            var projectPath = Path.Combine(extractDirectory, resolution.Project.OriginalFileName);
            AppLogger.LogInfo($"YMMPX package extraction succeeded. Route: {detection.ReaderRoute}; replacements: {resolution.ReplacedReferenceCount}.");
            return new YmmpxUnpackResult(projectPath, resolution.ReplacedReferenceCount, detection.Status);
        }
        catch (YmmpxPackageOperationException) { throw; }
        catch (Exception ex)
        {
            AppLogger.LogException(ex, "YMMPX package extraction failed.");
            throw new YmmpxPackageOperationException(YmmpxPackageOperationError.InvalidPackage,
                "YMMPX ファイルが壊れているか、内容が正しくありません。", ex);
        }
    }

    private static YmmpxPackageOperationException CreateUnsupportedFormatException(YmmpxFormatDetectionResult detection) => detection.Status switch
    {
        YmmpxFormatDetectionStatus.UnsupportedFutureVersion => new(YmmpxPackageOperationError.UnsupportedFutureVersion,
            $"この YMMPX は現在のライブラリより新しい形式です (v{detection.MajorVersion}.{detection.MinorVersion})。ライブラリを更新してください。"),
        YmmpxFormatDetectionStatus.UnsupportedMinorVersion => new(YmmpxPackageOperationError.UnsupportedMinorVersion,
            $"この YMMPX の形式 v{detection.MajorVersion}.{detection.MinorVersion} は現在のライブラリでは未対応です。ライブラリを更新してください。"),
        YmmpxFormatDetectionStatus.NotYmmpx => new(YmmpxPackageOperationError.NotYmmpx, "選択されたファイルは YMMPX ではありません。"),
        _ => new(YmmpxPackageOperationError.InvalidPackage, "YMMPX ファイルが壊れているか、内容が正しくありません。")
    };

    private static string GetProgressMessage(YmmpxV2WriteStage stage) => stage switch
    {
        YmmpxV2WriteStage.DiscoveringResources => "素材を確認中",
        YmmpxV2WriteStage.ProcessingProject => "プロジェクトを処理中",
        YmmpxV2WriteStage.HashingResource => "素材を処理中",
        YmmpxV2WriteStage.WritingPackage => "パッケージを書き込み中",
        YmmpxV2WriteStage.WritingResource => "素材を書き込み中",
        YmmpxV2WriteStage.Finalizing => "パッケージを確定中",
        YmmpxV2WriteStage.Completed => "パッケージ作成完了",
        _ => "パッケージを処理中"
    };
}

public sealed record YmmpxUnpackResult(string ProjectFilePath, int ReplacedPathCount, YmmpxFormatDetectionStatus FormatStatus);

public enum YmmpxPackageOperationError { ProcessingFailed, UnsupportedFutureVersion, UnsupportedMinorVersion, InvalidPackage, NotYmmpx }

public sealed class YmmpxPackageOperationException : Exception
{
    public YmmpxPackageOperationError Error { get; }
    public YmmpxPackageOperationException(YmmpxPackageOperationError error, string message, Exception? innerException = null) : base(message, innerException) => Error = error;
}
