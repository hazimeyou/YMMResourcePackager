namespace YMMResourcePackager.Shared;

public static class UnpackerArguments
{
    public static LoggingSwitchResult StripLoggingSwitches(IEnumerable<string>? args)
    {
        var argArray = (args ?? []).ToArray();
        var enableLogging = argArray.Any(a => string.Equals(a, "--enable-logging", StringComparison.OrdinalIgnoreCase));
        var disableLogging = argArray.Any(a => string.Equals(a, "--disable-logging", StringComparison.OrdinalIgnoreCase));
        var remaining = argArray
            .Where(a => !string.Equals(a, "--enable-logging", StringComparison.OrdinalIgnoreCase))
            .Where(a => !string.Equals(a, "--disable-logging", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new LoggingSwitchResult(remaining, enableLogging, disableLogging);
    }

    public static string ResolveUnpackBaseDirectory(
        string mode,
        string? customDirectory,
        string ymmpxPath,
        string pluginDirectory)
    {
        if (string.Equals(mode, UnpackOutputModes.PluginFolder, StringComparison.Ordinal))
            return pluginDirectory;

        if (string.Equals(mode, UnpackOutputModes.YmmpxFolder, StringComparison.Ordinal))
            return Path.GetDirectoryName(ymmpxPath) ?? pluginDirectory;

        if (string.Equals(mode, UnpackOutputModes.CustomFolder, StringComparison.Ordinal))
        {
            var trimmed = customDirectory?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                throw new InvalidOperationException("展開先フォルダーが未設定です。");

            return Path.GetFullPath(trimmed);
        }

        throw new InvalidOperationException($"不明な展開先モードです: {mode}");
    }

    public sealed record LoggingSwitchResult(string[] RemainingArgs, bool EnableLogging, bool DisableLogging);
}
