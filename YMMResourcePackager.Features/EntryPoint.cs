namespace YMMResourcePackager.Features;

public static class EntryPoint
{
    public static async Task RunPackAsync(
        string projectPath,
        string outputPath,
        string[] excludedFiles,
        bool includeProjectUiSettings,
        Action<string, double, long, long>? progress)
    {
        var (serviceType, optionsType, progressType) = ResolveYmmpxTypes();
        var excluded = excludedFiles.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var options = Activator.CreateInstance(optionsType) ?? throw new InvalidOperationException("Ymmpx options instance could not be created.");
        optionsType.GetProperty("IncludeProjectUiSettings")?.SetValue(options, includeProjectUiSettings);

        object? reporter = null;
        if (progress is not null)
        {
            var callback = new Action<object?>(p =>
            {
                if (p is null) return;
                var t = p.GetType();
                var message = t.GetProperty("Message")?.GetValue(p)?.ToString() ?? string.Empty;
                var percentage = ConvertToDouble(t.GetProperty("Percentage")?.GetValue(p));
                var processedBytes = ConvertToInt64(t.GetProperty("ProcessedBytes")?.GetValue(p));
                var totalBytes = ConvertToInt64(t.GetProperty("TotalBytes")?.GetValue(p));
                progress(message, percentage, processedBytes, totalBytes);
            });

            var progressImplType = typeof(ObjectProgress<>).MakeGenericType(progressType);
            reporter = Activator.CreateInstance(progressImplType, callback);
        }

        var createMethod = serviceType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CreatePackageAsync" && m.GetParameters().Length >= 5)
            ?? throw new MissingMethodException("CreatePackageAsync method not found.");

        var parameters = createMethod.GetParameters();
        object?[] args = parameters.Length >= 6
            ? [projectPath, outputPath, excluded, options, reporter, CancellationToken.None]
            : [projectPath, outputPath, excluded, options, reporter];

        var taskObj = createMethod.Invoke(null, args) ?? throw new InvalidOperationException("CreatePackageAsync returned null task.");
        if (taskObj is not Task task)
            throw new InvalidOperationException("CreatePackageAsync did not return Task.");

        await task;
    }

    public static string GetAvailableUnpackDirectory(string desiredDirectory)
    {
        var (serviceType, _, _) = ResolveYmmpxTypes();
        var method = serviceType.GetMethod("GetAvailableDirectoryPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException("GetAvailableDirectoryPath not found.");
        return method.Invoke(null, [desiredDirectory])?.ToString() ?? desiredDirectory;
    }

    public static string RunUnpack(string ymmpxPath, string extractDirectory, out int replacedPathCount)
    {
        var (serviceType, _, _) = ResolveYmmpxTypes();
        var method = serviceType.GetMethod("ExtractAndRestoreProject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException("ExtractAndRestoreProject not found.");

        var result = method.Invoke(null, [ymmpxPath, extractDirectory]) ?? throw new InvalidOperationException("Unpack result is null.");
        var t = result.GetType();
        replacedPathCount = ConvertToInt32(t.GetProperty("ReplacedPathCount")?.GetValue(result));
        return t.GetProperty("ProjectFilePath")?.GetValue(result)?.ToString()
            ?? throw new InvalidOperationException("ProjectFilePath is missing.");
    }

    private static (Type serviceType, Type optionsType, Type progressType) ResolveYmmpxTypes()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "YmmpxLib", StringComparison.OrdinalIgnoreCase));

        if (assembly is null)
        {
            var pluginRoot = ResolvePluginRoot();
            var dllPath = Directory.EnumerateFiles(pluginRoot, "YmmpxLib.dll", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                throw new FileNotFoundException("YmmpxLib.dll が見つかりません。YmmpxLib Shared Library を導入してください。");
            assembly = System.Reflection.Assembly.LoadFrom(dllPath);
        }

        var serviceType = assembly.GetType("YmmpxLib.YmmpxPackageService") ?? throw new TypeLoadException("YmmpxPackageService type not found.");
        var optionsType = assembly.GetType("YmmpxLib.YmmpxPackagingOptions") ?? throw new TypeLoadException("YmmpxPackagingOptions type not found.");
        var progressType = assembly.GetType("YmmpxLib.YmmpxPackagingProgress") ?? throw new TypeLoadException("YmmpxPackagingProgress type not found.");
        return (serviceType, optionsType, progressType);
    }

    private static string ResolvePluginRoot()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var marker = Path.Combine("user", "plugin", "YMMResourcePackager") + Path.DirectorySeparatorChar;
        var normalized = baseDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var idx = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return normalized.Substring(0, idx + Path.Combine("user", "plugin").Length);

        var parent = Directory.GetParent(baseDir)?.Parent;
        return parent?.FullName ?? baseDir;
    }

    private static int ConvertToInt32(object? value) { try { return value is null ? 0 : Convert.ToInt32(value); } catch { return 0; } }
    private static long ConvertToInt64(object? value) { try { return value is null ? 0L : Convert.ToInt64(value); } catch { return 0L; } }
    private static double ConvertToDouble(object? value) { try { return value is null ? 0.0 : Convert.ToDouble(value); } catch { return 0.0; } }

    private sealed class ObjectProgress<T> : IProgress<T>
    {
        private readonly Action<object?> _report;
        public ObjectProgress(Action<object?> report) { _report = report; }
        public void Report(T value) { _report(value); }
    }
}
