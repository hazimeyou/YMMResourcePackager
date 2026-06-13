global using System.Diagnostics;
global using Microsoft.Win32;
global using System.Runtime.InteropServices;
global using System.Runtime.Versioning;
using System.Reflection;
using YMMResourcePackager.Shared;

namespace YMMResourceUnpackerApp
{
    class Program
    {
        static int Main(string[] args)
        {
            var remainingArgs = HandleLoggingSwitches(args);
            if (remainingArgs.Length == 0 && args.Length > 0)
                return 1;

            AppLogger.LogInfo("Unpacker app started.");
            AppLogger.LogInfo($"Arguments: {string.Join(" ", remainingArgs.Select(SanitizeArg))}");

            if (remainingArgs.Length > 0 && remainingArgs[0] == "--associate")
            {
                if (!OperatingSystem.IsWindows())
                {
                    Console.WriteLine("この機能は Windows でのみ利用できます。");
                    return 1;
                }

                return EnsureFileAssociation() ? 0 : 1;
            }

            Console.WriteLine("=== YMM Resource Unpacker ===");

            string ymmpxPath;
            if (remainingArgs.Length > 0 && File.Exists(remainingArgs[0]))
            {
                ymmpxPath = remainingArgs[0];
            }
            else
            {
                Console.WriteLine("ymmpx ファイルを指定してください:");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || !File.Exists(input))
                {
                    Console.WriteLine("ファイルが存在しません。終了します。");
                    AppLogger.LogWarning("Input ymmpx path is missing or invalid.");
                    return 1;
                }

                ymmpxPath = input;
            }

            if (!TryCreateFeatureService(out var service, out var serviceError))
            {
                Console.WriteLine(serviceError);
                AppLogger.LogError($"Feature service creation failed: {serviceError}");
                return 1;
            }

            var baseName = Path.GetFileNameWithoutExtension(ymmpxPath);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "unpacked_ymmpx";

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var suffixToRemove = @"user\plugin\YMMResourcePackager\";
            var ymmRootDir = appDir;
            if (ymmRootDir.EndsWith(suffixToRemove, StringComparison.OrdinalIgnoreCase))
            {
                ymmRootDir = ymmRootDir.Substring(0, ymmRootDir.Length - suffixToRemove.Length);
            }

            var ymmExe = Path.GetFullPath(Path.Combine(ymmRootDir, "YukkuriMovieMaker.exe"));

            try
            {
                var unpackBaseDir = ResolveUnpackBaseDirectory(ymmpxPath, appDir);
                Directory.CreateDirectory(unpackBaseDir);
                var desiredDir = Path.Combine(unpackBaseDir, baseName);
                var finalDir = service.GetAvailableDirectoryPath(desiredDir);
                Console.WriteLine("展開中...");
                AppLogger.LogInfo("Unpack started.");
                var unpackResult = service.ExtractAndRestoreProject(ymmpxPath, finalDir);
                var ymmpPath = unpackResult.ProjectFilePath;
                Console.WriteLine($"リンク復元完了: {unpackResult.ReplacedPathCount} 件");
                AppLogger.LogInfo($"Unpack succeeded. ReplacedPathCount={unpackResult.ReplacedPathCount}");

                return LaunchProjectWithYmmPreferredPath(ymmpPath, ymmExe) ? 0 : 1;
            }
            catch (Exception ex)
            {
                if (IsMissingYmmpxLibException(ex))
                {
                    AnnounceMissingYmmpxLib();
                    AppLogger.LogWarning("YmmpxLib Shared Library is missing.");
                    return 1;
                }

                Console.WriteLine($"エラー: {ex.Message}");
                AppLogger.LogException(ex, "Unpack failed.");
                return 1;
            }
        }

        private static bool LaunchProjectWithYmmPreferredPath(string ymmpPath, string fallbackYmmExePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ymmpPath,
                    UseShellExecute = true
                });
                AppLogger.LogInfo("Project launch requested via .ymmp association.");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Association launch failed. Trying direct YMM executable.");
            }

            if (File.Exists(fallbackYmmExePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fallbackYmmExePath,
                    Arguments = $"\"{ymmpPath}\"",
                    UseShellExecute = true
                });
                AppLogger.LogInfo("Project launch requested via direct YukkuriMovieMaker.exe path.");
                return true;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{ymmpPath}\"",
                    UseShellExecute = true
                });
                AppLogger.LogInfo("Project revealed in Explorer because .ymmp launch was unavailable.");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Project launch failed: association, direct YMM path, and Explorer are all unavailable.");
                AppLogger.LogException(ex, "Explorer fallback launch failed.");
            }

            Console.WriteLine("YMM の起動先が見つかりませんでした。");
            Console.WriteLine("Enterキーで終了します...");
            Console.ReadLine();
            return false;
        }

        private static string ResolveUnpackBaseDirectory(string ymmpxPath, string pluginDirectory)
        {
            var settings = AppSettingsStore.Load();
            try
            {
                return UnpackerArguments.ResolveUnpackBaseDirectory(
                    settings.UnpackOutputMode,
                    settings.CustomUnpackDirectory,
                    ymmpxPath,
                    pluginDirectory);
            }
            catch (InvalidOperationException ex) when (
                string.Equals(settings.UnpackOutputMode, UnpackOutputModes.CustomFolder, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(settings.CustomUnpackDirectory))
            {
                Console.WriteLine("展開先フォルダーが未設定です。プラグインフォルダーに展開します。");
                AppLogger.LogWarning($"Custom unpack directory was missing: {ex.Message}");
                return pluginDirectory;
            }
        }

        private static string[] HandleLoggingSwitches(string[] args)
        {
            var parsed = UnpackerArguments.StripLoggingSwitches(args);

            if (parsed.EnableLogging)
            {
                var settings = AppSettingsStore.Load();
                settings.EnableLogging = true;
                AppSettingsStore.Save(settings);
                AppLogger.RefreshSettingsCache();
                Console.WriteLine("Logging enabled.");
            }

            if (parsed.DisableLogging)
            {
                var settings = AppSettingsStore.Load();
                settings.EnableLogging = false;
                AppSettingsStore.Save(settings);
                AppLogger.RefreshSettingsCache();
                Console.WriteLine("Logging disabled.");
            }

            return parsed.RemainingArgs;
        }

        private static bool TryCreateFeatureService(out FeatureServiceProxy service, out string error)
        {
            service = default;
            error = string.Empty;

            var featurePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YMMResourcePackager.Features.dll");
            if (!File.Exists(featurePath))
            {
                error = $"Features DLL が見つかりません: {featurePath}";
                return false;
            }

            try
            {
                var assembly = Assembly.LoadFrom(featurePath);
                var entryType = assembly.GetType("YMMResourcePackager.Features.EntryPoint")
                    ?? throw new InvalidOperationException("Features EntryPoint 型が見つかりません。");

                var unpackMethod = entryType.GetMethod("RunUnpack", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException("RunUnpack メソッドが見つかりません。");
                var getAvailableDirMethod = entryType.GetMethod("GetAvailableUnpackDirectory", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException("GetAvailableUnpackDirectory メソッドが見つかりません。");

                service = new FeatureServiceProxy(getAvailableDirMethod, unpackMethod);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Features の読み込みに失敗しました: {ex.Message}";
                AppLogger.LogException(ex, "Features load failed.");
                return false;
            }
        }

        private readonly record struct YmmpxExtractResult(string ProjectFilePath, int ReplacedPathCount);

        private readonly struct FeatureServiceProxy
        {
            private readonly MethodInfo _getAvailableDirMethod;
            private readonly MethodInfo _unpackMethod;

            public FeatureServiceProxy(MethodInfo getAvailableDirMethod, MethodInfo unpackMethod)
            {
                _getAvailableDirMethod = getAvailableDirMethod;
                _unpackMethod = unpackMethod;
            }

            public string GetAvailableDirectoryPath(string desiredDir)
            {
                return (string?)_getAvailableDirMethod.Invoke(null, [desiredDir]) ?? desiredDir;
            }

            public YmmpxExtractResult ExtractAndRestoreProject(string ymmpxPath, string finalDir)
            {
                object[] args = [ymmpxPath, finalDir, 0];
                var projectPath = _unpackMethod.Invoke(null, args)?.ToString()
                    ?? throw new InvalidOperationException("展開後のプロジェクトパスが取得できません。");
                var count = args[2] is int i ? i : 0;
                return new YmmpxExtractResult(projectPath, count);
            }
        }

        [SupportedOSPlatform("windows")]
        static bool EnsureFileAssociation()
        {
            try
            {
                var ext = ".ymmpx";
                var progId = "YMMResourcePackagerFile";
                var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YMMResourceUnpackerApp.exe");

                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}"))
                {
                    key?.SetValue("", progId);
                }

                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    key?.SetValue("", "YMM Resource Packager File");
                    using (var iconKey = key?.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", $"\"{appPath}\",0");
                    }
                    using (var shellKey = key?.CreateSubKey("shell\\open\\command"))
                    {
                        shellKey?.SetValue("", $"\"{appPath}\" \"%1\"");
                    }
                }

                NotifyShellAssociationChanged();
                Console.WriteLine(".ymmpx の関連付けが完了しました（ユーザー単位）。");
                AppLogger.LogInfo("File association updated.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"関連付けに失敗しました: {ex.Message}");
                AppLogger.LogException(ex, "File association failed.");
                return false;
            }
        }

        private static bool IsMissingYmmpxLibException(Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is FileNotFoundException && current.Message.Contains("YmmpxLib.dll", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (current.Message.Contains("YmmpxLib", StringComparison.OrdinalIgnoreCase) &&
                    (current is TargetInvocationException || current is InvalidOperationException))
                    return true;
            }

            return false;
        }

        private static void AnnounceMissingYmmpxLib()
        {
            Console.WriteLine("YmmpxLib Shared Library が見つかりません。");
            Console.WriteLine("YmmpxLib Shared Library を追加してから再実行してください。");
        }

        [SupportedOSPlatform("windows")]
        private static void NotifyShellAssociationChanged()
        {
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        private static string SanitizeArg(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return "<empty>";
            return Path.GetFileName(arg);
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
