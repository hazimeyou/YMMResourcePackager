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
            _registeredPluginRoot = root;
        }
    }

    private static Assembly? Resolve(ResolveEventArgs args, string pluginRoot)
    {
        var requested = new AssemblyName(args.Name).Name;
        if (!string.Equals(requested, "YmmpxLibV2", StringComparison.OrdinalIgnoreCase)) return null;
        var candidate = Path.Combine(pluginRoot, "YmmpxLibV2Plugin", "YmmpxLibV2.dll");
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
