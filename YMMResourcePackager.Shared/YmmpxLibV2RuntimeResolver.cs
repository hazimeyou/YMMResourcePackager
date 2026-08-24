using System.Reflection;

namespace YMMResourcePackager.Shared;

/// <summary>Resolves the v2 Core DLL from its sibling YMM4 plugin when Features is loaded dynamically.</summary>
public static class YmmpxLibV2RuntimeResolver
{
    private static readonly object Sync = new();
    private static string? _registeredPluginRoot;

    public static void EnsureRegistered(string pluginRoot)
    {
        var root = Path.GetFullPath(pluginRoot);
        lock (Sync)
        {
            if (string.Equals(_registeredPluginRoot, root, StringComparison.OrdinalIgnoreCase)) return;
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) => Resolve(args, root);
            var candidate = GetCandidatePath(root);
            if (!File.Exists(candidate))
                throw new FileNotFoundException("YmmpxLibV2.dll がYmmpxLibV2Pluginに見つかりません。", candidate);

            if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                    string.Equals(assembly.GetName().Name, "YmmpxLibV2", StringComparison.OrdinalIgnoreCase)))
            {
                Assembly.LoadFrom(candidate);
                AppLogger.LogInfo("YmmpxLibV2 runtime was preloaded for dynamic Features loading.");
            }
            _registeredPluginRoot = root;
        }
    }

    private static Assembly? Resolve(ResolveEventArgs args, string pluginRoot)
    {
        var requested = new AssemblyName(args.Name).Name;
        if (!string.Equals(requested, "YmmpxLibV2", StringComparison.OrdinalIgnoreCase)) return null;
        var candidate = GetCandidatePath(pluginRoot);
        if (!File.Exists(candidate))
        {
            AppLogger.LogWarning("YmmpxLibV2 runtime resolution failed because the sibling DLL is missing.");
            return null;
        }

        AppLogger.LogInfo("YmmpxLibV2 runtime was resolved for dynamic Features loading.");
        return Assembly.LoadFrom(candidate);
    }

    private static string GetCandidatePath(string pluginRoot) =>
        Path.Combine(pluginRoot, "..", "YmmpxLibV2Plugin", "YmmpxLibV2.dll");
}
