namespace YMMResourcePackager.Shared;

// パッケージング関連の出力先をまとめる。
public static class PackagerPaths
{
    private const string PackagerFolderName = "YMMResourcePackager";
    private const string GlobalExcludeFileName = "exclude.json";
    private const string PackagingOptionsFileName = "packaging_options.json";
    private const string FeaturesAssemblyName = "YMMResourcePackager.Features.dll";
    private const string UnpackerAppExeName = "YMMResourceUnpackerApp.exe";
    private const string YmmpxLibDllName = "YmmpxLib.dll";
    private const string YmmpxLibPluginFolderName = "YmmpxLibPlugin";
    private const string YmmpxLibPluginAssetName = "YmmpxLibPlugin.ymme";

    public static string PackagerDataDirectory(string pluginDirectory) =>
        Path.Combine(pluginDirectory, PackagerFolderName);

    public static string GetGlobalExcludePath(string pluginDirectory) =>
        Path.Combine(PackagerDataDirectory(pluginDirectory), GlobalExcludeFileName);

    public static string GetPackagingOptionsPath(string pluginDirectory) =>
        Path.Combine(PackagerDataDirectory(pluginDirectory), PackagingOptionsFileName);

    public static string GetFeatureAssemblyPath(string pluginDirectory) =>
        Path.Combine(PackagerDataDirectory(pluginDirectory), FeaturesAssemblyName);

    public static string GetFeatureAssemblyPathInBaseDirectory(string baseDirectory) =>
        Path.Combine(baseDirectory, FeaturesAssemblyName);

    public static string GetUnpackerAppPath(string pluginDirectory) =>
        Path.Combine(PackagerDataDirectory(pluginDirectory), UnpackerAppExeName);

    public static string GetInstalledYmmpxLibPath(string pluginDirectory) =>
        Path.Combine(pluginDirectory, YmmpxLibPluginFolderName, YmmpxLibDllName);

    public static string GetLocalExcludePath(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectPath);
        return Path.Combine(directory, $"{baseName}.exclude.json");
    }

    public static string GetProjectOutputPath(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectPath);
        return Path.Combine(directory, $"{baseName}.ymmpx");
    }

    public static string GetTemporaryYmmpxLibPackagePath()
    {
        Directory.CreateDirectory(AppPaths.TempDirectory);
        return Path.Combine(AppPaths.TempDirectory, YmmpxLibPluginAssetName);
    }
}
