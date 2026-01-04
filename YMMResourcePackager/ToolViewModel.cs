
namespace YMMResourcePackagerPlugin.ViewModel
{
    public class ToolViewModel : BaseViewModel
    {
        private string? _selectedProject;
        public static string PluginDirectory => AppDirectories.PluginDirectory;

        public string? SelectedProject
        {
            get => _selectedProject;
            set => SetProperty(ref _selectedProject, value);
        }

        private string _status = "";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public ICommand PackageCommand { get; }
        public ICommand SelectProjectCommand { get; }
        public ICommand AssociateYmmpxCommand { get; }
        public ICommand OpenExcludeSettingCommand { get; }

        public ToolViewModel()
        {
            PackageCommand = new RelayCommand(async () => await PackageProjectAsync());
            SelectProjectCommand = new RelayCommand(OpenProjectDialog);
            AssociateYmmpxCommand = new RelayCommand(AssociateYmmpx);
            OpenExcludeSettingCommand = new RelayCommand(OpenExcludeSetting);
        }
        private void OpenExcludeSetting()
        {
            if (string.IsNullOrEmpty(SelectedProject) || !File.Exists(SelectedProject))
            {
                MessageBox.Show("先にプロジェクトを選択してください。", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(SelectedProject);
                using JsonDocument doc = JsonDocument.Parse(jsonText);

                var allFiles = FindFilePaths(doc.RootElement)
                    .Distinct()
                    .Select(f => new ExcludeItem { FilePath = f, IsExcluded = false })
                    .ToList();

                string excludePath = Path.Combine(
                    PluginDirectory, "YMMResourcePackager", "exclude.json");

                if (File.Exists(excludePath))
                {
                    var saved = JsonSerializer.Deserialize<List<ExcludeItem>>(
                        File.ReadAllText(excludePath)) ?? new();

                    var map = saved.ToDictionary(x => x.FilePath, x => x.IsExcluded);

                    foreach (var item in allFiles)
                        if (map.TryGetValue(item.FilePath, out bool v))
                            item.IsExcluded = v;
                }

                var dlg = new ExcludeSettingWindow(allFiles)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();

                Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
                File.WriteAllText(
                    excludePath,
                    JsonSerializer.Serialize(dlg.ExcludeItems, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenProjectDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "YMMプロジェクト (*.ymmp)|*.ymmp",
                Title = "プロジェクトを選択"
            };

            if (dlg.ShowDialog() == true)
            {
                SelectedProject = dlg.FileName;
                Status = $"選択: {SelectedProject}";
                Progress = 0;
            }
        }

        private void AssociateYmmpx()
        {
            try
            {
                string appExe = Path.Combine(PluginDirectory, "YMMResourcePackager", "YMMResourceUnpackerApp.exe");

                if (!File.Exists(appExe))
                {
                    MessageBox.Show($"アプリが見つかりません:\n{appExe}", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = "--associate",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PackageProjectAsync()
        {
            if (string.IsNullOrEmpty(SelectedProject) || !File.Exists(SelectedProject))
            {
                Status = "プロジェクトが選択されていません。";
                return;
            }

            try
            {
                Status = "素材収集中...";
                Progress = 0;

                string baseDir = Path.GetDirectoryName(SelectedProject)!;
                string projectName = Path.GetFileNameWithoutExtension(SelectedProject);
                string outputPath = Path.Combine(baseDir, $"{projectName}.ymmpx");

                // 上書き確認
                if (File.Exists(outputPath))
                {
                    var r = MessageBox.Show(
                        "既存ファイルがあります。上書きしますか？",
                        "確認",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (r == MessageBoxResult.Cancel) return;
                    if (r == MessageBoxResult.No)
                    {
                        int i = 1;
                        while (File.Exists(outputPath))
                            outputPath = Path.Combine(baseDir, $"{projectName}_{i++}.ymmpx");
                    }
                    else File.Delete(outputPath);
                }

                // 素材取得
                List<string> resources = new();
                using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(SelectedProject)))
                {
                    foreach (var p in FindFilePaths(doc.RootElement).Distinct())
                        if (File.Exists(p))
                            resources.Add(p);
                }

                Status = $"ZIP作成中... ({resources.Count} 個)";
                Progress = 0;

                await Task.Run(() =>
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "YMMResourcePackager", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    string linksFile = Path.Combine(tempDir, "links.txt");

                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    using (var writer = new StreamWriter(linksFile))
                    {
                        for (int i = 0; i < resources.Count; i++)
                        {
                            string src = resources[i];
                            string name = Path.GetFileName(src);
                            string unique = name;
                            int c = 1;

                            while (usedNames.Contains(unique))
                            {
                                unique = $"{Path.GetFileNameWithoutExtension(name)}_{c++}{Path.GetExtension(name)}";
                            }

                            usedNames.Add(unique);
                            string zipPath = $"resources/{unique}";
                            fileMap[src] = zipPath;

                            writer.WriteLine($"{src},{zipPath}");

                            int index = i;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Progress = (double)(index + 1) / resources.Count * 100;
                                Status = $"ZIP作成中... {index + 1}/{resources.Count}";
                            });
                        }
                    }

                    using (var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(SelectedProject, "project.ymmp");
                        zip.CreateEntryFromFile(linksFile, "links.txt");

                        foreach (var kv in fileMap)
                            zip.CreateEntryFromFile(kv.Key, kv.Value);
                    }

                    Directory.Delete(tempDir, true);
                });

                // ★ 完了通知（ここが追加点）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Progress = 100;
                    Status = $"完了: {outputPath}";

                    MessageBox.Show(
                        $"パッケージ作成が完了しました。\n\n{outputPath}",
                        "完了",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Status = $"エラー: {ex.Message}";
                Progress = 0;

                MessageBox.Show(
                    ex.Message,
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private IEnumerable<string> FindFilePaths(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in element.EnumerateObject())
                {
                    if (p.Name == "FilePath" && p.Value.ValueKind == JsonValueKind.String)
                        yield return p.Value.GetString()!;
                    else
                        foreach (var c in FindFilePaths(p.Value))
                            yield return c;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var i in element.EnumerateArray())
                    foreach (var c in FindFilePaths(i))
                        yield return c;
            }
        }
    }
}
