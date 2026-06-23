using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YMMResourcePackager.Shared;

// アプリ設定を、複数プロセスからでも壊れにくい形で保存する。
public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Sync = new();
    // 同一設定ファイルへの同時書き込みを避けるためのプロセス間ロック。
    private static readonly Mutex ProcessMutex = new(false, BuildMutexName());

    public static AppLoggingSettings Load()
    {
        lock (Sync)
        {
            AcquireProcessLock();
            try
            {
                Directory.CreateDirectory(AppPaths.BaseDirectory);
                if (!File.Exists(AppPaths.SettingsPath))
                {
                    var defaults = new AppLoggingSettings();
                    SaveInternal(defaults);
                    return defaults;
                }

                var json = ReadAllTextShared(AppPaths.SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppLoggingSettings>(json) ?? new AppLoggingSettings();
                return loaded;
            }
            catch
            {
                var defaults = new AppLoggingSettings();
                TrySave(defaults);
                return defaults;
            }
            finally
            {
                ReleaseProcessLock();
            }
        }
    }

    public static void Save(AppLoggingSettings settings)
    {
        lock (Sync)
        {
            AcquireProcessLock();
            try
            {
                TrySave(settings);
            }
            finally
            {
                ReleaseProcessLock();
            }
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
        var tempPath = AppPaths.SettingsPath + ".tmp";
        // 一旦テンポラリへ書いてから差し替えることで、途中失敗による破損を避ける。
        File.WriteAllText(tempPath, json, Encoding.UTF8);

        if (File.Exists(AppPaths.SettingsPath))
        {
            File.Replace(tempPath, AppPaths.SettingsPath, null);
        }
        else
        {
            File.Move(tempPath, AppPaths.SettingsPath);
        }
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void AcquireProcessLock()
    {
        try
        {
            ProcessMutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
        }
    }

    private static void ReleaseProcessLock()
    {
        try
        {
            ProcessMutex.ReleaseMutex();
        }
        catch
        {
        }
    }

    private static string BuildMutexName()
    {
        // 設定ファイルの実体パスをハッシュ化し、環境ごとに一意の名前にする。
        var normalized = Path.GetFullPath(AppPaths.SettingsPath).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "Global\\YMMResourcePackager.Settings." + Convert.ToHexString(bytes);
    }
}
