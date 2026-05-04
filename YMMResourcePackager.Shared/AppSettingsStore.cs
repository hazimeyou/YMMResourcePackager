using System.Text.Json;

namespace YMMResourcePackager.Shared;

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Sync = new();

    public static AppLoggingSettings Load()
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.BaseDirectory);
                if (!File.Exists(AppPaths.SettingsPath))
                {
                    var defaults = new AppLoggingSettings();
                    SaveInternal(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(AppPaths.SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppLoggingSettings>(json) ?? new AppLoggingSettings();
                return loaded;
            }
            catch
            {
                var defaults = new AppLoggingSettings();
                TrySave(defaults);
                return defaults;
            }
        }
    }

    public static void Save(AppLoggingSettings settings)
    {
        lock (Sync)
        {
            TrySave(settings);
        }
    }

    private static void TrySave(AppLoggingSettings settings)
    {
        try
        {
            SaveInternal(settings);
        }
        catch
        {
        }
    }

    private static void SaveInternal(AppLoggingSettings settings)
    {
        Directory.CreateDirectory(AppPaths.BaseDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.SettingsPath, json);
    }
}
