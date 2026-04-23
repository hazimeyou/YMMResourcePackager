global using System.Diagnostics;
global using Microsoft.Win32;
global using System.Runtime.InteropServices;
global using System.Runtime.Versioning;
global using YmmpxLib;

namespace YMMResourceUnpackerApp
{
    class Program
    {
        private const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.txt";

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--associate")
            {
                if (!OperatingSystem.IsWindows())
                {
                    Console.WriteLine("この機能は Windows でのみ利用できます。");
                    return;
                }

                EnsureFileAssociation();
                return;
            }

            if (args.Length > 0 && IsLicenseArgument(args[0]))
            {
                PrintThirdPartyNotices();
                return;
            }

            Console.WriteLine("=== YMM Resource Unpacker ===");

            string ymmpxPath;
            if (args.Length > 0 && File.Exists(args[0]))
            {
                ymmpxPath = args[0];
            }
            else
            {
                Console.WriteLine("ymmpx ファイルを指定してください:");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || !File.Exists(input))
                {
                    Console.WriteLine("ファイルが存在しません。終了します。");
                    return;
                }

                ymmpxPath = input;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            var suffixToRemove = @"user\plugin\YMMResourcePackager\";
            var ymmRootDir = appDir;
            if (ymmRootDir.EndsWith(suffixToRemove, StringComparison.OrdinalIgnoreCase))
            {
                ymmRootDir = ymmRootDir.Substring(0, ymmRootDir.Length - suffixToRemove.Length);
            }

            var ymmExe = Path.GetFullPath(Path.Combine(ymmRootDir, "YukkuriMovieMaker.exe"));
            if (!File.Exists(ymmExe))
            {
                Console.WriteLine("YukkuriMovieMaker.exe が見つかりません。終了します。");
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(ymmpxPath);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "unpacked_ymmpx";

            var desiredDir = Path.Combine(appDir, baseName);
            var finalDir = YmmpxPackageService.GetAvailableDirectoryPath(desiredDir);

            try
            {
                Console.WriteLine("展開中...");
                var unpackResult = YmmpxPackageService.ExtractAndRestoreProject(ymmpxPath, finalDir);
                var ymmpPath = unpackResult.ProjectFilePath;
                Console.WriteLine($"リンク復元完了: {unpackResult.ReplacedPathCount} 件");

                Process.Start(new ProcessStartInfo
                {
                    FileName = ymmExe,
                    Arguments = $"\"{ymmpPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
            }
        }

        private static bool IsLicenseArgument(string argument)
        {
            return string.Equals(argument, "--licenses", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argument, "--license", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintThirdPartyNotices()
        {
            var noticesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThirdPartyNoticesFileName);
            if (!File.Exists(noticesPath))
            {
                Console.WriteLine("Third-party notices file was not found.");
                Console.WriteLine("YMMPXLib (MIT)");
                Console.WriteLine("SharpCompress 0.38.0 (MIT)");
                Console.WriteLine("ZstdSharp.Port 0.8.1 (MIT)");
                return;
            }

            Console.WriteLine(File.ReadAllText(noticesPath));
        }

        [SupportedOSPlatform("windows")]
        static void EnsureFileAssociation()
        {
            try
            {
                var ext = ".ymmpx";
                var progId = "YMMResourcePackagerFile";
                var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YMMResourceUnpackerApp.exe");

                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}"))
                {
                    key.SetValue("", progId);
                }

                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    key.SetValue("", "YMM Resource Packager File");
                    using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    {
                        iconKey?.SetValue("", $"\"{appPath}\",0");
                    }
                    using (var shellKey = key.CreateSubKey("shell\\open\\command"))
                    {
                        shellKey.SetValue("", $"\"{appPath}\" \"%1\"");
                    }
                }

                NotifyShellAssociationChanged();

                Console.WriteLine(".ymmpx の関連付けが完了しました（ユーザー単位）。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"関連付けに失敗しました: {ex.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void NotifyShellAssociationChanged()
        {
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
