global using System.Diagnostics;
global using System.IO.Compression;
global using System.Text.Json;
global using System.Text.Json.Nodes;

namespace YMMResourceUnpackerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // 管理者権限での関連付け
            if (args.Length > 0 && args[0] == "--associate")
            {
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
            if (!File.Exists(ymmExe))
            {
                Console.WriteLine("YukkuriMovieMaker.exe が見つかりません。終了します。1");
                return;
            }

            // 以下展開・リンク書き換えは元コードのまま
            string baseName = Path.GetFileNameWithoutExtension(ymmpxPath);
            string tempDir = Path.Combine(appDir, baseName);
            int suffix = 1;
            string finalDir = tempDir;
            while (Directory.Exists(finalDir))
            {
                finalDir = tempDir + $"_{suffix}";
                suffix++;
            }
            Directory.CreateDirectory(finalDir);

            try
            {
                Console.WriteLine("展開中...");
                ZipFile.ExtractToDirectory(ymmpxPath, finalDir);

                // links.txt 読み込み
                string linksPath = Path.Combine(finalDir, "links.txt");
                var linkMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(linksPath))
                {
                    foreach (var line in File.ReadAllLines(linksPath))
                    {
                        var parts = line.Split(',', 2);
                        if (parts.Length == 2)
                            linkMap[parts[0].Trim()] = Path.Combine(finalDir, parts[1].Trim());
                    }
                }

                // ymmp ファイル探索
                string ymmpPath = Directory.GetFiles(finalDir, "*.ymmp", SearchOption.AllDirectories).FirstOrDefault() ?? "";
                if (string.IsNullOrEmpty(ymmpPath))
                {
                    Console.WriteLine("プロジェクトファイル (.ymmp) が見つかりません。");
                    return;
                }

                // JSON 読み込み
                string json = File.ReadAllText(ymmpPath);
                JsonNode? root = JsonNode.Parse(json);
                if (root == null)
                {
                    Console.WriteLine("JSON の解析に失敗しました。");
                    return;
                }

                // FilePath の書き換え
                ReplaceFilePaths(root, linkMap);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                File.WriteAllText(ymmpPath, root.ToJsonString(options));

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
        /// 再帰的に FilePath を書き換える
        /// </summary>
        static int ReplaceFilePaths(JsonNode node, Dictionary<string, string> linkMap)
        {
            int count = 0;
            if (node is JsonObject obj)
            {
                foreach (var key in obj.ToList())
                {
                    if (key.Key == "FilePath" && key.Value is JsonValue val)
                    {
                        string? path = val.GetValue<string>();
                        if (!string.IsNullOrEmpty(path))
                        {
                            var match = linkMap.FirstOrDefault(x => path.Contains(x.Key, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(match.Key))
                            {
                                obj["FilePath"] = match.Value;
                                count++;
                            }
                        }
                    }
                    else if (key.Value != null)
                        count += ReplaceFilePaths(key.Value, linkMap);
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var child in arr)
                    count += ReplaceFilePaths(child, linkMap);
            }
            return count;
        }

        /// <summary>
        /// .ymmpx を自作アプリに関連付け
        /// </summary>
        static void EnsureFileAssociation()
        {
            try
            {
                string ext = ".ymmpx";
                string progId = "YMMResourcePackagerFile";
                string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YMMResourceUnpackerApp.exe");

                using (var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(ext))
                {
                    key.SetValue("", progId);
                }

                using (var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(progId))
                {
                    key.SetValue("", "YMM Resource Packager File");
                    using (var shellKey = key.CreateSubKey("shell\\open\\command"))
                    {
                        shellKey.SetValue("", $"\"{appPath}\" \"%1\"");
                    }
                }

                Console.WriteLine(".ymmpx の関連付けが完了しました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"関連付けに失敗しました: {ex.Message}");
            }
        }

    }
}

