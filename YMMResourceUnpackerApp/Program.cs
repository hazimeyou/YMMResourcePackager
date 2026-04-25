global using System.Diagnostics;
global using Microsoft.Win32;
global using System.Runtime.InteropServices;
global using System.Runtime.Versioning;
global using YmmpxLib;
using System.Runtime;

namespace YMMResourceUnpackerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // ユーザー単位での関連付け
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

            Console.WriteLine("=== YMM Resource Unpacker ===");

            string ymmpxPath;

            // 引数対応
            if (args.Length > 0 && File.Exists(args[0]))
            {
                ymmpxPath = args[0];
            }
            else
            {
                Console.WriteLine("ymmpx ファイルを指定してください:");
                string? input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || !File.Exists(input))
                {
                    Console.WriteLine("ファイルが存在しません。終了します。");
                    return;
                }
                ymmpxPath = input;
            }

            // 自作アプリの実行フォルダ
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            // "user\plugin\YMMResourcePackager" を削除して YMM.exe の親フォルダを取得
            string suffixToRemove = @"user\plugin\YMMResourcePackager\";
            string ymmRootDir = appDir;
            if (ymmRootDir.EndsWith(suffixToRemove, StringComparison.OrdinalIgnoreCase))
            {
                ymmRootDir = ymmRootDir.Substring(0, ymmRootDir.Length - suffixToRemove.Length);
            }

            string ymmExe = Path.Combine(ymmRootDir, "YukkuriMovieMaker.exe");
            ymmExe = Path.GetFullPath(ymmExe);
            string baseName = Path.GetFileNameWithoutExtension(ymmpxPath);
            string desiredDir = Path.Combine(appDir, baseName);
            string finalDir = YmmpxPackageService.GetAvailableDirectoryPath(desiredDir);

            if (!File.Exists(ymmExe))
            {
                Console.WriteLine("YukkuriMovieMaker.exe が見つかりません。");
                try
                {
                    Console.WriteLine("展開中...");
                    var unpackResult = YmmpxPackageService.ExtractAndRestoreProject(ymmpxPath, finalDir);
                    var ymmpPath = unpackResult.ProjectFilePath;
                    Console.WriteLine($"リンク復元完了: {unpackResult.ReplacedPathCount} 件");

                    // .ymmp の既定アプリで開く（ダブルクリックと同じ挙動）
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ymmpPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"エラー: {ex.Message}");
                }
                return;
            }

            try
            {
                Console.WriteLine("展開中...");
                var unpackResult = YmmpxPackageService.ExtractAndRestoreProject(ymmpxPath, finalDir);
                var ymmpPath = unpackResult.ProjectFilePath;
                Console.WriteLine($"リンク復元完了: {unpackResult.ReplacedPathCount} 件");

                // YMM 起動
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

        /// <summary>
        /// .ymmpx を自作アプリに関連付け
        /// </summary>
        [SupportedOSPlatform("windows")]
        static void EnsureFileAssociation()
        {
            try
            {
                string ext = ".ymmpx";
                string progId = "YMMResourcePackagerFile";
                string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YMMResourceUnpackerApp.exe");

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

