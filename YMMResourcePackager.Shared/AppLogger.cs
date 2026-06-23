using System.Text;
using System.Text.RegularExpressions;

namespace YMMResourcePackager.Shared;

// 画面上の操作に影響しにくい、控えめなファイルロガー。
public static class AppLogger
{
    private static readonly object WriteSync = new();
    private static readonly object SettingsSync = new();
    private static readonly Regex AbsoluteWindowsPathPattern = new(
        @"(?i)(?<!\w)(?:[A-Z]:\\|\\\\)[^\r\n""']+",
        RegexOptions.Compiled);
    private static bool? _cachedEnableLogging;

    public static void LogInfo(string message) => Write("INFO", message);
    public static void LogWarning(string message) => Write("WARN", message);
    public static void LogError(string message) => Write("ERROR", message);

    public static void LogException(Exception ex, string? message = null)
    {
        var detail = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(message))
            detail.AppendLine(message);
        detail.AppendLine(ex.GetType().FullName ?? "Exception");
        detail.AppendLine(SanitizeForLog(ex.Message));
        detail.AppendLine(SanitizeForLog(ex.StackTrace ?? string.Empty));
        Write("EXCEPTION", detail.ToString().Trim());
    }

    public static string GetLogsDirectoryPath() => AppPaths.LogsDirectory;

    public static string? GetLatestLogFilePath()
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsDirectory))
                return null;
            return Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void Write(string level, string message)
    {
        try
        {
            if (!IsLoggingEnabled())
                return;

            lock (WriteSync)
            {
                // 1 日 1 ファイルで書き分ける。
                Directory.CreateDirectory(AppPaths.LogsDirectory);
                var logPath = Path.Combine(AppPaths.LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {SanitizeForLog(message)}";
                File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void RefreshSettingsCache()
    {
        lock (SettingsSync)
        {
            try
            {
                _cachedEnableLogging = AppSettingsStore.Load().EnableLogging;
            }
            catch
            {
                _cachedEnableLogging = false;
            }
        }
    }

    private static bool IsLoggingEnabled()
    {
        var cached = _cachedEnableLogging;
        if (cached.HasValue)
            return cached.Value;

        lock (SettingsSync)
        {
            if (_cachedEnableLogging.HasValue)
                return _cachedEnableLogging.Value;

            // 既定では設定を遅延読み込みし、初回だけキャッシュする。
            try
            {
                _cachedEnableLogging = AppSettingsStore.Load().EnableLogging;
            }
            catch
            {
                _cachedEnableLogging = false;
            }

            return _cachedEnableLogging.Value;
        }
    }

    private static string SanitizeForLog(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // ログに実パスを残しすぎないよう、Windows の絶対パスを伏せる。
        return AbsoluteWindowsPathPattern.Replace(text, "<path>");
    }
}
