namespace YMMResourcePackagerPlugin.ViewModel
{
    public class ToolViewModel : BaseViewModel
    {
        private const string OptionsFileName = "packaging_options.json";
        private const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.txt";
        private static readonly JsonSerializerOptions WriteIndentedJsonOptions = new() { WriteIndented = true };
        private string? _selectedProject;
        private readonly AsyncRelayCommand _packageCommand;
        public static string PluginDirectory => AppDirectories.PluginDirectory;

        public string? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (!SetProperty(ref _selectedProject, value))
                    return;

                _packageCommand.RaiseCanExecuteChanged();
            }
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

        public ICommand PackageCommand => _packageCommand;
        public ICommand SelectProjectCommand { get; }
        public ICommand UseOpenedProjectCommand { get; }
        public ICommand AssociateYmmpxCommand { get; }
        public ICommand ShowLicensesCommand { get; }
        public ICommand OpenExcludeSettingCommand { get; }

        public ToolViewModel()
        {
            LoadPackagingOptions();
            _packageCommand = new AsyncRelayCommand(PackageProjectAsync, CanPackageProject);
            SelectProjectCommand = new RelayCommand(OpenProjectDialog);
            UseOpenedProjectCommand = new RelayCommand(UseOpenedProject);
            AssociateYmmpxCommand = new RelayCommand(AssociateYmmpx);
            ShowLicensesCommand = new RelayCommand(ShowLicenses);
            OpenExcludeSettingCommand = new RelayCommand(OpenExcludeSetting);
        }

        private bool CanPackageProject()
        {
            return !string.IsNullOrWhiteSpace(SelectedProject) && File.Exists(SelectedProject);
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
                try { value = prop.GetValue(obj); }
                catch { continue; }

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

        private static bool LooksLikeYmmpPath(string path) => path.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase);
        private static string GetExcludePath() => Path.Combine(PluginDirectory, "YMMResourcePackager", "exclude.json");
        private static string GetPackagingOptionsPath() => Path.Combine(PluginDirectory, "YMMResourcePackager", OptionsFileName);

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

                var state = new PackagingOptionsState { IncludeProjectUiSettings = IncludeProjectUiSettings };
                File.WriteAllText(optionsPath, JsonSerializer.Serialize(state, WriteIndentedJsonOptions));
            }
            catch
            {
            }
        }

        private static List<ExcludeItem> LoadExcludeItems()
        {
            var excludePath = GetExcludePath();
            if (!File.Exists(excludePath))
                return [];

            try { return JsonSerializer.Deserialize<List<ExcludeItem>>(File.ReadAllText(excludePath)) ?? []; }
            catch (JsonException) { return []; }
        }

        private static HashSet<string> LoadExcludedFiles()
        {
            return LoadExcludeItems()
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

                var allFiles = FindFilePaths(doc.RootElement)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(f => new ExcludeItem { FilePath = f, IsExcluded = false })
                    .ToList();

                string excludePath = GetExcludePath();

                if (File.Exists(excludePath))
                {
                    var map = LoadExcludeItems()
                        .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                        .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Last().IsExcluded, StringComparer.OrdinalIgnoreCase);

                    foreach (var item in allFiles)
                    {
                        if (map.TryGetValue(item.FilePath, out bool isExcluded))
                            item.IsExcluded = isExcluded;
                    }
                }

                var dlg = new ExcludeSettingWindow(allFiles) { Owner = Application.Current.MainWindow };
                if (dlg.ShowDialog() != true)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
                File.WriteAllText(excludePath, JsonSerializer.Serialize(dlg.ExcludeItems, WriteIndentedJsonOptions));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenProjectDialog()
        {
            var dlg = new OpenFileDialog { Filter = "YMMプロジェクト (*.ymmp)|*.ymmp", Title = "プロジェクトを選択" };
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

                Process.Start(new ProcessStartInfo { FileName = appExe, Arguments = "--associate", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowLicenses()
        {
            try
            {
                var noticesPath = Path.Combine(PluginDirectory, "YMMResourcePackager", ThirdPartyNoticesFileName);
                if (!File.Exists(noticesPath))
                {
                    MessageBox.Show($"ライセンス情報ファイルが見つかりません:\n{noticesPath}", "ライセンス", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo { FileName = noticesPath, UseShellExecute = true });
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
                    var r = MessageBox.Show("出力先に同名ファイルがあります。上書きしますか？", "確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (r == MessageBoxResult.Cancel)
                    {
                        Status = "キャンセルされました。";
                        return;
                    }
                    if (r == MessageBoxResult.No)
                    {
                        outputPath = GetAvailableFilePath(outputPath);
                    }
                    else
                    {
                        File.Delete(outputPath);
                    }
                }

                var excludedFiles = LoadExcludedFiles().ToArray();
                await InvokeFeaturePackAsync(
                    SelectedProject,
                    outputPath,
                    excludedFiles,
                    IncludeProjectUiSettings,
                    (message, percentage, processedBytes, totalBytes) =>
                    {
                        Progress = Math.Clamp(percentage, 0, 100);
                        Status = totalBytes > 0
                            ? $"{message} {FormatBytes(processedBytes)}/{FormatBytes(totalBytes)} ({Progress:F1}%)"
                            : message;
                    });

                Progress = 100;
                Status = $"完了: {outputPath}";
                MessageBox.Show($"パッケージ化が完了しました。\n\n{outputPath}", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Status = $"エラー: {ex.Message}";
                Progress = 0;
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static async Task InvokeFeaturePackAsync(
            string projectPath,
            string outputPath,
            string[] excludedFiles,
            bool includeProjectUiSettings,
            Action<string, double, long, long> progress)
        {
            var featurePath = Path.Combine(AppDirectories.PluginDirectory, "YMMResourcePackager", "YMMResourcePackager.Features.dll");
            if (!File.Exists(featurePath))
                throw new FileNotFoundException("Features DLL が見つかりません。", featurePath);

            var assembly = System.Reflection.Assembly.LoadFrom(featurePath);
            var type = assembly.GetType("YMMResourcePackager.Features.EntryPoint")
                ?? throw new InvalidOperationException("Features EntryPoint が見つかりません。");
            var method = type.GetMethod("RunPackAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("RunPackAsync メソッドが見つかりません。");

            var callback = new Action<string, double, long, long>((message, percentage, processedBytes, totalBytes) =>
            {
                Application.Current.Dispatcher.Invoke(() => progress(message, percentage, processedBytes, totalBytes));
            });

            var taskObj = method.Invoke(null, [projectPath, outputPath, excludedFiles, includeProjectUiSettings, callback]);
            if (taskObj is not Task task)
                throw new InvalidOperationException("RunPackAsync の戻り値が Task ではありません。");
            await task;
        }

        private static string GetAvailableFilePath(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var candidate = path;
            var i = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(dir, $"{name}_{i++}{ext}");
            }
            return candidate;
        }

        private static IEnumerable<string> FindFilePaths(JsonElement root)
        {
            var stack = new Stack<JsonElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var element = stack.Pop();
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var prop in element.EnumerateObject())
                        {
                            if (prop.NameEquals("FilePath") && prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var path = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(path))
                                    yield return path;
                            }
                            else
                            {
                                stack.Push(prop.Value);
                            }
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var child in element.EnumerateArray())
                            stack.Push(child);
                        break;
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.##}{units[unit]}";
        }

        private sealed class PackagingOptionsState
        {
            public bool IncludeProjectUiSettings { get; set; } = true;
        }
    }
}
