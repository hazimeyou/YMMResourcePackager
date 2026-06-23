namespace YMMResourcePackager.Shared;

// プラグイン実行環境に依存する、ベースパスの解決をまとめる。
public static class AppPaths
{
    public static string BaseDirectory => ResolvePluginDirectory();

    public static string SettingsPath => Path.Combine(BaseDirectory, "settings.json");
    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public static string TempDirectory => Path.Combine(BaseDirectory, "temp");

    private static string ResolvePluginDirectory()
    {
        try
        {
            // 実行場所から、YMM の plugin 配下かどうかを判定して補正する。
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDir))
                return Directory.GetCurrentDirectory();

            var normalized = Path.GetFullPath(baseDir);
            var marker = Path.Combine("user", "plugin", "YMMResourcePackager") + Path.DirectorySeparatorChar;
            var idx = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return normalized.Substring(0, idx + marker.Length).TrimEnd(Path.DirectorySeparatorChar);
            }

            if (File.Exists(Path.Combine(normalized, "YMMResourcePackager.dll")) ||
                File.Exists(Path.Combine(normalized, "YMMResourceUnpackerApp.exe")))
            {
                return normalized.TrimEnd(Path.DirectorySeparatorChar);
            }

            var candidate = Path.Combine(normalized, "user", "plugin", "YMMResourcePackager");
            return Directory.Exists(candidate) ? candidate : normalized.TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
