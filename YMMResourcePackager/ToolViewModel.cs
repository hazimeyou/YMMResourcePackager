using YmmpxLib;

namespace YMMResourcePackagerPlugin.ViewModel
{
    public class ToolViewModel : BaseViewModel
    {
        private const string OptionsFileName = "packaging_options.json";
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

        private bool _includeProjectUiSettings = true;
        public bool IncludeProjectUiSettings
        {
            get => _includeProjectUiSettings;
            set
            {
                if (!SetProperty(ref _includeProjectUiSettings, value))
                    return;

                SavePackagingOptions();
            }
        }

        public ICommand PackageCommand { get; }
        public ICommand SelectProjectCommand { get; }
        public ICommand UseOpenedProjectCommand { get; }
        public ICommand AssociateYmmpxCommand { get; }
        public ICommand OpenExcludeSettingCommand { get; }

        public ToolViewModel()
        {
            LoadPackagingOptions();
            PackageCommand = new RelayCommand(async () => await PackageProjectAsync());
            SelectProjectCommand = new RelayCommand(OpenProjectDialog);
            UseOpenedProjectCommand = new RelayCommand(UseOpenedProject);
            AssociateYmmpxCommand = new RelayCommand(AssociateYmmpx);
            OpenExcludeSettingCommand = new RelayCommand(OpenExcludeSetting);
        }

        private void UseOpenedProject()
        {
            try
            {
                if (!TryGetOpenedProjectPath(out var projectPath, out var message))
                {
                    Status = message;
                    MessageBox.Show(message, "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SelectedProject = projectPath;
                Status = $"選択: {SelectedProject}";
                Progress = 0;
            }
            catch (Exception ex)
            {
                Status = $"エラー: {ex.Message}";
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool TryGetOpenedProjectPath(out string projectPath, out string message)
        {
            projectPath = string.Empty;
            message = string.Empty;

            var candidates = CollectOpenedProjectPathCandidates()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            projectPath = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
            if (!string.IsNullOrEmpty(projectPath))
                return true;

            var rawPath = candidates.FirstOrDefault() ?? string.Empty;
            if (!string.IsNullOrEmpty(rawPath))
            {
                message = $"開いているプロジェクト候補は見つかりましたが、ファイルが存在しません:\n{rawPath}";
                return false;
            }

            message = "開いているプロジェクトが見つかりません。";
            return false;
        }

        private static IEnumerable<string> CollectOpenedProjectPathCandidates()
        {
            var settings = PluginLoader.Settings?.Where(x => x is not null).ToArray() ?? [];
            foreach (var setting in settings)
            {
                foreach (var path in GetProjectPathCandidatesFromObject(setting!, 3))
                    yield return path;
            }
        }

        private static IEnumerable<string> GetProjectPathCandidatesFromObject(object? obj, int depth)
        {
            if (obj is null || depth < 0)
                yield break;

            if (obj is string str)
            {
                if (LooksLikeYmmpPath(str))
                    yield return str;
                yield break;
            }

            if (obj is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                    foreach (var path in GetProjectPathCandidatesFromObject(item, depth - 1))
                        yield return path;
                yield break;
            }

            var type = obj.GetType();
            foreach (var prop in type.GetProperties())
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                object? value;
                try
                {
                    value = prop.GetValue(obj);
                }
                catch
                {
                    continue;
                }

                if (value is null)
                    continue;

                if (value is string s)
                {
                    if (LooksLikeYmmpPath(s) || prop.Name.Equals("ProjectPath", StringComparison.OrdinalIgnoreCase))
                        yield return s;
                    continue;
                }

                if (depth <= 0)
                    continue;

                if (prop.Name.Contains("Project", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("WindowState", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("State", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("File", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var path in GetProjectPathCandidatesFromObject(value, depth - 1))
                        yield return path;
                }
            }
        }

        private static bool LooksLikeYmmpPath(string path)
        {
            return path.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetExcludePath()
        {
            return Path.Combine(PluginDirectory, "YMMResourcePackager", "exclude.json");
        }

        private static string GetPackagingOptionsPath()
        {
            return Path.Combine(PluginDirectory, "YMMResourcePackager", OptionsFileName);
        }

        private void LoadPackagingOptions()
        {
            try
            {
                var optionsPath = GetPackagingOptionsPath();
                if (!File.Exists(optionsPath))
                    return;

                var saved = JsonSerializer.Deserialize<PackagingOptionsState>(File.ReadAllText(optionsPath));
                if (saved is not null)
                    _includeProjectUiSettings = saved.IncludeProjectUiSettings;
            }
            catch
            {
                _includeProjectUiSettings = true;
            }
        }

        private void SavePackagingOptions()
        {
            try
            {
                var optionsPath = GetPackagingOptionsPath();
                var directory = Path.GetDirectoryName(optionsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var state = new PackagingOptionsState
                {
                    IncludeProjectUiSettings = IncludeProjectUiSettings
                };

                File.WriteAllText(
                    optionsPath,
                    JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Ignore persistence failures and keep current in-memory setting.
            }
        }

        private static HashSet<string> LoadExcludedFiles()
        {
            string excludePath = GetExcludePath();
            if (!File.Exists(excludePath))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var saved = JsonSerializer.Deserialize<List<ExcludeItem>>(File.ReadAllText(excludePath)) ?? new();
            return saved
                .Where(x => x.IsExcluded && !string.IsNullOrWhiteSpace(x.FilePath))
                .Select(x => x.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private void OpenExcludeSetting()
        {
            if (string.IsNullOrEmpty(SelectedProject) || !File.Exists(SelectedProject))
            {
                MessageBox.Show("先にプロジェクトを選択してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(SelectedProject);
                using JsonDocument doc = JsonDocument.Parse(jsonText);

                var allFiles = YmmpxProjectJson.FindFilePaths(doc.RootElement)
                    .Distinct()
                    .Select(f => new ExcludeItem { FilePath = f, IsExcluded = false })
                    .ToList();

                string excludePath = GetExcludePath();

                if (File.Exists(excludePath))
                {
                    var saved = JsonSerializer.Deserialize<List<ExcludeItem>>(File.ReadAllText(excludePath)) ?? new();
                    var map = saved
                        .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                        .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Last().IsExcluded, StringComparer.OrdinalIgnoreCase);

                    foreach (var item in allFiles)
                    {
                        if (map.TryGetValue(item.FilePath, out bool isExcluded))
                            item.IsExcluded = isExcluded;
                    }
                }

                var dlg = new ExcludeSettingWindow(allFiles)
                {
                    Owner = Application.Current.MainWindow
                };
                if (dlg.ShowDialog() != true)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
                File.WriteAllText(
                    excludePath,
                    JsonSerializer.Serialize(dlg.ExcludeItems, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"アプリが見つかりません:\n{appExe}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = "--associate",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
                Status = "素材同梱を開始します...";
                Progress = 0;

                string baseDir = Path.GetDirectoryName(SelectedProject)!;
                string projectName = Path.GetFileNameWithoutExtension(SelectedProject);
                string outputPath = Path.Combine(baseDir, $"{projectName}.ymmpx");

                if (File.Exists(outputPath))
                {
                    var r = MessageBox.Show(
                        "出力先に同名ファイルがあります。上書きしますか？",
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
                    else
                    {
                        File.Delete(outputPath);
                    }
                }

                var excludedFiles = LoadExcludedFiles();
                var progressReporter = new Progress<YmmpxPackagingProgress>(p =>
                {
                    Progress = p.Percentage;
                    Status = $"ZIP作成中... {p.CompletedCount}/{p.TotalCount}";
                });

                var packageResult = await YmmpxPackageService.CreatePackageAsync(
                    SelectedProject,
                    outputPath,
                    excludedFiles,
                    new YmmpxPackagingOptions
                    {
                        IncludeProjectUiSettings = IncludeProjectUiSettings
                    },
                    progressReporter);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Progress = 100;
                    Status = $"完了: {outputPath} (素材 {packageResult.ResourceCount} 件)";

                    MessageBox.Show(
                        $"パッケージ化が完了しました。\n素材同梱数: {packageResult.ResourceCount} 件\n\n{outputPath}",
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

        private sealed class PackagingOptionsState
        {
            public bool IncludeProjectUiSettings { get; set; } = true;
        }
    }
}


