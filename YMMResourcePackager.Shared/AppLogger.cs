using System.Text;

namespace YMMResourcePackager.Shared;

public static class AppLogger
{
    public static void LogInfo(string message) => Write("INFO", message);
    public static void LogWarning(string message) => Write("WARN", message);
    public static void LogError(string message) => Write("ERROR", message);

    public static void LogException(Exception ex, string? message = null)
    {
        var detail = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(message))
            detail.AppendLine(message);
        detail.AppendLine(ex.GetType().FullName ?? "Exception");
        detail.AppendLine(ex.Message);
        detail.AppendLine(ex.StackTrace ?? string.Empty);
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
            if (!AppSettingsStore.Load().EnableLogging)
                return;

            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var logPath = Path.Combine(AppPaths.LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }
}
