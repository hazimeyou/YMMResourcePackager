namespace YMMResourcePackager.Shared;

// ログ出力と一部の起動時挙動を制御するユーザー設定。
public sealed class AppLoggingSettings
{
    public bool EnableLogging { get; set; } = false;
    public bool SuppressLegacyYmmpxLibFolderWarning { get; set; } = false;
    public bool SuppressYmmpxLibInstallPrompt { get; set; } = false;
    public string UnpackOutputMode { get; set; } = UnpackOutputModes.PluginFolder;
    public string CustomUnpackDirectory { get; set; } = string.Empty;
}
