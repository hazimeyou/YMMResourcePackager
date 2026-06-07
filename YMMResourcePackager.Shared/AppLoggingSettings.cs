namespace YMMResourcePackager.Shared;

public sealed class AppLoggingSettings
{
    public bool EnableLogging { get; set; } = false;
    public bool SuppressLegacyYmmpxLibFolderWarning { get; set; } = false;
    public bool SuppressYmmpxLibInstallPrompt { get; set; } = false;
    public string UnpackOutputMode { get; set; } = UnpackOutputModes.PluginFolder;
    public string CustomUnpackDirectory { get; set; } = string.Empty;
}
