namespace YMMResourcePackagerPlugin.ViewModel
{
    public class ToolViewModel : BaseViewModel
    {
        private const string OptionsFileName = "packaging_options.json";
        private const string YmmpxLibPluginDownloadUrl = "https://github.com/hazimeyou/YmmpxLib/releases/download/v0.3.0/YmmpxLibPlugin.ymme";
        private const string YmmpxLibPluginSha256 = "cc9af0b7541fbd9552f93e2f8573b65c5ba80b2e71572093deace67f456ffaa8";
        private static readonly JsonSerializerOptions WriteIndentedJsonOptions = new() { WriteIndented = true };
        private string? _selectedProject;
        private readonly AsyncRelayCommand _packageCommand;
        private bool _enableLogging;
        private int _detectedMaterialCount;
        private int _excludedMaterialCount;
        private int _missingMaterialCount;
        private bool _startupPrerequisitePromptHandled;
        private string _selectedUnpackOutputMode = YMMResourcePackager.Shared.UnpackOutputModes.PluginFolder;
        private string _customUnpackDirectory = string.Empty;
        private static readonly UnpackOutputOption[] UnpackOutputOptionsInternal =
        [
            new("プラグインフォルダー", YMMResourcePackager.Shared.UnpackOutputModes.PluginFolder),
            new(".ymmpx と同じフォルダー", YMMResourcePackager.Shared.UnpackOutputModes.YmmpxFolder),
            new("指定フォルダー", YMMResourcePackager.Shared.UnpackOutputModes.CustomFolder)
        ];
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

        public bool EnableLogging
        {
            get => _enableLogging;
            set
            {
                if (!SetProperty(ref _enableLogging, value))
                    return;

                var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();
                settings.EnableLogging = value;
                YMMResourcePackager.Shared.AppSettingsStore.Save(settings);
                YMMResourcePackager.Shared.AppLogger.RefreshSettingsCache();
                YMMResourcePackager.Shared.AppLogger.LogInfo($"Logging setting changed: EnableLogging={value}");
            }
        }

        public IEnumerable<UnpackOutputOption> UnpackOutputOptions => UnpackOutputOptionsInternal;

        public string SelectedUnpackOutputMode
        {
            get => _selectedUnpackOutputMode;
            set
            {
                if (!SetProperty(ref _selectedUnpackOutputMode, NormalizeUnpackOutputMode(value)))
                    return;

                OnPropertyChanged(nameof(IsCustomUnpackDirectoryEnabled));
                SaveUnpackOutputSettings();
            }
        }

        public string CustomUnpackDirectory
        {
            get => _customUnpackDirectory;
            set
            {
                if (!SetProperty(ref _customUnpackDirectory, value))
                    return;

                SaveUnpackOutputSettings();
            }
        }

        public bool IsCustomUnpackDirectoryEnabled =>
            string.Equals(SelectedUnpackOutputMode, YMMResourcePackager.Shared.UnpackOutputModes.CustomFolder, StringComparison.Ordinal);

        public ICommand PackageCommand => _packageCommand;
        public ICommand SelectProjectCommand { get; }
        public ICommand UseOpenedProjectCommand { get; }
        public ICommand AssociateYmmpxCommand { get; }
        public ICommand OpenExcludeSettingCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand OpenLatestLogCommand { get; }
        public ICommand InstallYmmpxLibCommand { get; }
        public ICommand BrowseCustomUnpackDirectoryCommand { get; }
        public int DetectedMaterialCount
        {
            get => _detectedMaterialCount;
            set => SetProperty(ref _detectedMaterialCount, value);
        }

        public int ExcludedMaterialCount
        {
            get => _excludedMaterialCount;
            set => SetProperty(ref _excludedMaterialCount, value);
        }

        public int MissingMaterialCount
        {
            get => _missingMaterialCount;
            set => SetProperty(ref _missingMaterialCount, value);
        }

        public ToolViewModel()
        {
            var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();
            LoadPackagingOptions();
            _enableLogging = settings.EnableLogging;
            _selectedUnpackOutputMode = NormalizeUnpackOutputMode(settings.UnpackOutputMode);
            _customUnpackDirectory = settings.CustomUnpackDirectory ?? string.Empty;
            _packageCommand = new AsyncRelayCommand(PackageProjectAsync, CanPackageProject);
            SelectProjectCommand = new RelayCommand(OpenProjectDialog);
            UseOpenedProjectCommand = new RelayCommand(UseOpenedProject);
            AssociateYmmpxCommand = new RelayCommand(AssociateYmmpx);
            OpenExcludeSettingCommand = new RelayCommand(OpenExcludeSetting);
            OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
            OpenLatestLogCommand = new RelayCommand(OpenLatestLog);
            InstallYmmpxLibCommand = new AsyncRelayCommand(InstallYmmpxLibAsync, CanInstallYmmpxLib);
            BrowseCustomUnpackDirectoryCommand = new RelayCommand(BrowseCustomUnpackDirectory);
            YMMResourcePackager.Shared.AppLogger.LogInfo("ToolViewModel initialized.");
        }

        public async Task InitializeAsync()
        {
            if (_startupPrerequisitePromptHandled)
                return;

            _startupPrerequisitePromptHandled = true;

            try
            {
                if (PromptRemoveLegacyYmmpxLibFolderIfNeeded())
                {
                    Status = "古い YmmpxLib フォルダーを削除して、YMM を再起動してください。";
                    YMMResourcePackager.Shared.AppLogger.LogInfo("Legacy YmmpxLib folder prompt shown on startup.");
                    return;
                }

                if (IsYmmpxLibInstalled())
                    return;

                var prompt = ShowWarningPrompt(
                    windowTitle: "前提プラグインの確認",
                    message: "YmmpxLib Shared Library が見つかりません。\n今すぐダウンロードしてインストールしますか？",
                    yesButtonText: "ダウンロード",
                    noButtonText: "後で",
                    suppressSettingSelector: s => s.SuppressYmmpxLibInstallPrompt,
                    suppressSettingApplier: (s, value) => s.SuppressYmmpxLibInstallPrompt = value);

                if (!prompt.confirmed)
                {
                    Status = "YmmpxLib Shared Library は未導入です。必要になったときにパッケージ化から導入できます。";
                    YMMResourcePackager.Shared.AppLogger.LogInfo("YmmpxLib Shared Library download prompt declined on startup.");
                    return;
                }

                var installed = await TryInstallYmmpxLibPluginAsync();
                if (installed)
                {
                    Status = "前提プラグインを起動しました。インストーラに沿ってください。再起動が必要です。";
                    MessageBox.Show(
                        "前提プラグインを起動しました。インストーラに沿ってください。",
                        "再起動が必要です",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Startup YmmpxLib Shared Library prompt failed.");
                Status = $"前提プラグイン確認でエラー: {ex.Message}";
            }
        }

        private bool PromptRemoveLegacyYmmpxLibFolderIfNeeded()
        {
            try
            {
                var legacyPath = GetLegacyYmmpxLibFolderPath();
                if (!YMMResourcePackager.Shared.PackagingRules.HasLegacyYmmpxLibFolder(AppDirectories.PluginDirectory))
                    return false;

                var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();
                if (settings.SuppressLegacyYmmpxLibFolderWarning)
                    return false;

                var prompt = ShowWarningPrompt(
                    windowTitle: "YmmpxLib フォルダーを削除してください",
                    message: "プラグイン DLL と同じフォルダーに旧 YmmpxLib フォルダーが残っています。\nこのフォルダーを先に削除してください。\n削除後は YMM を再起動してください。\n\n「はい」でフォルダーを開きます。",
                    yesButtonText: "フォルダーを開く",
                    noButtonText: "閉じる",
                    suppressSettingSelector: s => s.SuppressLegacyYmmpxLibFolderWarning,
                    suppressSettingApplier: (s, value) => s.SuppressLegacyYmmpxLibFolderWarning = value);

                if (prompt.confirmed)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{legacyPath}\"",
                        UseShellExecute = true
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Legacy YmmpxLib folder prompt failed.");
                return false;
            }
        }

        private static (bool confirmed, bool suppress) ShowWarningPrompt(
            string windowTitle,
            string message,
            string yesButtonText,
            string noButtonText,
            Func<YMMResourcePackager.Shared.AppLoggingSettings, bool> suppressSettingSelector,
            Action<YMMResourcePackager.Shared.AppLoggingSettings, bool> suppressSettingApplier)
        {
            var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();

            if (suppressSettingSelector(settings))
                return (false, false);

            var dialog = new WarningPromptWindow
            {
                Owner = Application.Current.MainWindow,
                WindowTitle = windowTitle,
                Message = message,
                YesButtonText = yesButtonText,
                NoButtonText = noButtonText
            };

            var result = dialog.ShowDialog() == true;
            if (dialog.SuppressThisWarning)
            {
                suppressSettingApplier(settings, true);
                YMMResourcePackager.Shared.AppSettingsStore.Save(settings);
            }

            return (result, dialog.SuppressThisWarning);
        }

        private bool CanInstallYmmpxLib() => true;

        private async Task InstallYmmpxLibAsync()
        {
            try
            {
                if (IsYmmpxLibInstalled())
                {
                    MessageBox.Show(
                        "YmmpxLib Shared Library はすでにインストールされています。",
                        "情報",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    "YmmpxLib Shared Library をインストールしますか？",
                    "YmmpxLib のインストール",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                var installed = await TryInstallYmmpxLibPluginAsync();
                if (installed)
                {
                    MessageBox.Show(
                        "YmmpxLib Shared Library のインストーラーを起動しました。",
                        "再起動が必要です",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Manual YmmpxLib install failed.");
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseCustomUnpackDirectory()
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "展開先フォルダーを選択",
                    Multiselect = false
                };

                if (!string.IsNullOrWhiteSpace(CustomUnpackDirectory) && Directory.Exists(CustomUnpackDirectory))
                    dialog.InitialDirectory = CustomUnpackDirectory;

                if (dialog.ShowDialog() == true)
                    CustomUnpackDirectory = dialog.FolderName;
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Browse custom unpack directory failed.");
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveUnpackOutputSettings()
        {
            try
            {
                var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();
                settings.UnpackOutputMode = NormalizeUnpackOutputMode(SelectedUnpackOutputMode);
                settings.CustomUnpackDirectory = CustomUnpackDirectory?.Trim() ?? string.Empty;
                YMMResourcePackager.Shared.AppSettingsStore.Save(settings);
            }
            catch
            {
            }
        }

        private static string NormalizeUnpackOutputMode(string? mode)
        {
            return mode switch
            {
                YMMResourcePackager.Shared.UnpackOutputModes.PluginFolder => YMMResourcePackager.Shared.UnpackOutputModes.PluginFolder,
                YMMResourcePackager.Shared.UnpackOutputModes.YmmpxFolder => YMMResourcePackager.Shared.UnpackOutputModes.YmmpxFolder,
                YMMResourcePackager.Shared.UnpackOutputModes.CustomFolder => YMMResourcePackager.Shared.UnpackOutputModes.CustomFolder,
                _ => YMMResourcePackager.Shared.UnpackOutputModes.PluginFolder
            };
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
                YMMResourcePackager.Shared.AppLogger.LogInfo("Opened project selected from current project.");
            }
            catch (Exception ex)
            {
                Status = $"エラー: {ex.Message}";
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "UseOpenedProject failed.");
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

        private static bool LooksLikeYmmpPath(string path) =>
            path.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ymmpx", StringComparison.OrdinalIgnoreCase);
        private static string GetGlobalExcludePath() => Path.Combine(PluginDirectory, "YMMResourcePackager", "exclude.json");
        private static string GetLocalExcludePath(string projectPath)
        {
            var directory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(projectPath);
            return Path.Combine(directory, $"{baseName}.exclude.json");
        }
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

        private static List<YMMResourcePackager.Shared.ExcludeRule> LoadExcludeItems(string path)
        {
            return YMMResourcePackager.Shared.ExcludeRuleStore.LoadFromFile(path);
        }

        private static void SaveExcludeItems(string path, IEnumerable<YMMResourcePackager.Shared.ExcludeRule> rules)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, YMMResourcePackager.Shared.ExcludeRuleStore.SaveToJson(rules));
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
                var projectPath = ExpandYmmpxIfNeeded(SelectedProject);
                var globalExcludePath = GetGlobalExcludePath();
                var localExcludePath = GetLocalExcludePath(projectPath);
                var dlg = new ExcludeSettingWindow(
                    projectPath,
                    LoadExcludeItems(globalExcludePath),
                    LoadExcludeItems(localExcludePath))
                {
                    Owner = Application.Current.MainWindow
                };
                if (dlg.ShowDialog() != true)
                    return;

                SaveExcludeItems(globalExcludePath, dlg.GlobalExcludeItems);
                SaveExcludeItems(localExcludePath, dlg.LocalExcludeItems);
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
                Filter = "YMMプロジェクト (*.ymmp;*.ymmpx)|*.ymmp;*.ymmpx",
                Title = "プロジェクトを選択"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectedProject = dlg.FileName;
                Status = $"選択: {SelectedProject}";
                Progress = 0;
                YMMResourcePackager.Shared.AppLogger.LogInfo("Project selected from file dialog.");
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                var logsDir = YMMResourcePackager.Shared.AppLogger.GetLogsDirectoryPath();
                Directory.CreateDirectory(logsDir);
                Process.Start(new ProcessStartInfo { FileName = logsDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Failed to open log folder.");
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLatestLog()
        {
            try
            {
                var latest = YMMResourcePackager.Shared.AppLogger.GetLatestLogFilePath();
                if (string.IsNullOrWhiteSpace(latest) || !File.Exists(latest))
                {
                    MessageBox.Show("最新ログが見つかりません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo { FileName = latest, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Failed to open latest log file.");
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = "--associate",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                if (process is null)
                {
                    MessageBox.Show("関連付け設定ツールの起動に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);
                var output = outputTask.Result;
                var error = errorTask.Result;
                var details = string.IsNullOrWhiteSpace(output) ? error : output;
                var hasFailureText = details.Contains("失敗", StringComparison.OrdinalIgnoreCase) ||
                                     details.Contains("error", StringComparison.OrdinalIgnoreCase);

                if (process.ExitCode == 0 && !hasFailureText)
                {
                    MessageBox.Show("`.ymmpx` の関連付けに成功しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"`.ymmpx` の関連付けに失敗しました。\n\n{details}".Trim(),
                        "失敗",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
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

            string? outputPath = null;
            string? temporaryOutputPath = null;
            bool generatedPackageNeedsCleanup = false;
            try
            {
                YMMResourcePackager.Shared.AppLogger.LogInfo("Pack requested.");
                if (SelectedProject.EndsWith(".ymmpx", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedProject = ExpandYmmpxIfNeeded(SelectedProject);
                    Status = $"展開しました: {SelectedProject}";
                    YMMResourcePackager.Shared.AppLogger.LogInfo("Selected ymmpx was expanded instead of packaged.");
                    return;
                }

                if (!IsYmmpxLibInstalled())
                {
                    YMMResourcePackager.Shared.AppLogger.LogWarning("YmmpxLib not found. Starting prerequisite install flow.");
                    var installed = await TryInstallYmmpxLibPluginAsync();
                    if (!installed)
                        return;

                    Status = "前提プラグインを起動しました。インストーラに沿ってください。再起動が必要です。";
                    MessageBox.Show(
                        "前提プラグインを起動しました。インストーラに沿ってください。",
                        "再起動が必要です",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Status = "素材同梱を開始します...";
                Progress = 0;
                YMMResourcePackager.Shared.AppLogger.LogInfo("Pack started.");

                string baseDir = Path.GetDirectoryName(SelectedProject)!;
                string projectName = Path.GetFileNameWithoutExtension(SelectedProject);
                outputPath = Path.Combine(baseDir, $"{projectName}.ymmpx");
                var globalRules = LoadExcludeItems(GetGlobalExcludePath());
                var localRules = LoadExcludeItems(GetLocalExcludePath(SelectedProject!));
                var excludedFiles = YMMResourcePackager.Shared.PackagingRules.ResolveExcludedFiles(
                    SelectedProject,
                    globalRules.Concat(localRules)).ToArray();
                var validation = ValidateProjectBeforePack(SelectedProject, excludedFiles);
                DetectedMaterialCount = validation.DetectedMaterialCount;
                ExcludedMaterialCount = validation.ExcludedMaterialCount;
                MissingMaterialCount = validation.MissingMaterialCount;

                if (validation.MissingMaterialCount > 0)
                {
                    var proceed = MessageBox.Show(
                        $"見つからない素材が {validation.MissingMaterialCount} 件あります。\nこのままパッケージ化を続行しますか？",
                        "事前チェック",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes)
                    {
                        Status = "事前チェックでキャンセルされました。";
                        return;
                    }
                }

                temporaryOutputPath = CreateTemporaryPackagePath(outputPath);
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
                        outputPath = GetStableAvailableFilePath(outputPath);
                        temporaryOutputPath = CreateTemporaryPackagePath(outputPath);
                    }
                    else
                    {
                        temporaryOutputPath = CreateTemporaryPackagePath(outputPath);
                    }
                }

                generatedPackageNeedsCleanup = true;
                await InvokeFeaturePackAsync(
                    SelectedProject,
                    temporaryOutputPath,
                    excludedFiles,
                    IncludeProjectUiSettings,
                    (message, percentage, processedBytes, totalBytes) =>
                    {
                        Progress = Math.Clamp(percentage, 0, 100);
                        Status = totalBytes > 0
                            ? $"{message} {FormatBytes(processedBytes)}/{FormatBytes(totalBytes)} ({Progress:F1}%)"
                            : message;
                    });

                MoveGeneratedPackage(temporaryOutputPath, outputPath);

                generatedPackageNeedsCleanup = false;
                temporaryOutputPath = null;
                Progress = 100;
                Status = $"完了: {outputPath}";
                YMMResourcePackager.Shared.AppLogger.LogInfo("Pack completed successfully.");
                MessageBox.Show(
                    $"パッケージ化が完了しました。\n\n出力: {outputPath}\n検出素材数: {DetectedMaterialCount}\n除外数: {ExcludedMaterialCount}\n見つからない素材数: {MissingMaterialCount}",
                    "完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (generatedPackageNeedsCleanup && !string.IsNullOrWhiteSpace(temporaryOutputPath))
                {
                    DeleteTempFileQuietly(temporaryOutputPath);
                }

                Status = $"エラー: {ex.Message}";
                Progress = 0;
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "Pack failed.");
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsYmmpxLibInstalled()
        {
            try
            {
                var exists = TryGetLoadedYmmpxLibAssembly() is not null || File.Exists(GetInstalledYmmpxLibPath());
                YMMResourcePackager.Shared.AppLogger.LogInfo($"YmmpxLib install check: {(exists ? "installed" : "not installed")}.");
                return exists;
            }
            catch
            {
                return false;
            }
        }

        private static string GetInstalledYmmpxLibPath() =>
            Path.Combine(AppDirectories.PluginDirectory, "YmmpxLibPlugin", "YmmpxLib.dll");

        private static System.Reflection.Assembly? TryGetLoadedYmmpxLibAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "YmmpxLib", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> TryInstallYmmpxLibPluginAsync()
        {
            var ymmePath = CreateTemporaryYmmpxLibPackagePath();
            try
            {
                var installerPath = Path.Combine(
                    AppDirectories.ResourceDirectory,
                    "bin",
                    "Installer",
                    "YukkuriMovieMaker.Plugin.Installer.exe");

                if (!File.Exists(installerPath))
                {
                    MessageBox.Show(
                        $"インストーラーが見つかりません:\n{installerPath}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                YMMResourcePackager.Shared.AppLogger.LogInfo("YmmpxLib Shared Library download started.");
                using var http = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                using var response = await http.GetAsync(
                    YmmpxLibPluginDownloadUrl,
                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using (var fs = File.Create(ymmePath))
                {
                    await response.Content.CopyToAsync(fs);
                }
                YMMResourcePackager.Shared.FileIntegrity.VerifySha256(ymmePath, YmmpxLibPluginSha256);
                YMMResourcePackager.Shared.AppLogger.LogInfo("YmmpxLib Shared Library download completed.");

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = $"\"{ymmePath}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(installerPath) ?? AppDirectories.ResourceDirectory
                });
                if (process is null)
                    throw new InvalidOperationException("Installer process could not be started.");

                YMMResourcePackager.Shared.AppLogger.LogInfo("Installer launched for YmmpxLib Shared Library.");
                await process.WaitForExitAsync();
                YMMResourcePackager.Shared.AppLogger.LogInfo($"Installer exited with code {process.ExitCode}.");
                if (process.ExitCode != 0)
                {
                    MessageBox.Show(
                        $"YmmpxLib Shared Library の導入に失敗しました。\nインストーラーが終了コード {process.ExitCode} を返しました。",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                YMMResourcePackager.Shared.AppLogger.LogWarning("YmmpxLib Shared Library download timeout.");
                MessageBox.Show(
                    "YmmpxLib Shared Library のダウンロードがタイムアウトしました。(30秒)\nネットワーク接続を確認して再試行してください。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                var detail = ex.StatusCode is null
                    ? ex.Message
                    : $"HTTP {(int)ex.StatusCode} ({ex.StatusCode})";
                YMMResourcePackager.Shared.AppLogger.LogWarning($"YmmpxLib Shared Library download failed: {detail}");
                MessageBox.Show(
                    $"YmmpxLib Shared Library のダウンロードに失敗しました。\n{detail}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                YMMResourcePackager.Shared.AppLogger.LogException(ex, "YmmpxLib Shared Library installation flow failed.");
                MessageBox.Show(
                    $"YmmpxLib Shared Library の導入に失敗しました。\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            finally
            {
                DeleteTempFileQuietly(ymmePath);
            }
        }

        private static void DeleteTempFileQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static string CreateTemporaryYmmpxLibPackagePath()
        {
            Directory.CreateDirectory(YMMResourcePackager.Shared.AppPaths.TempDirectory);
            return Path.Combine(
                YMMResourcePackager.Shared.AppPaths.TempDirectory,
                "YmmpxLibPlugin.ymme");
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

        private static string ExpandYmmpxIfNeeded(string path)
        {
            if (!path.EndsWith(".ymmpx", StringComparison.OrdinalIgnoreCase))
                return path;

            var extractedProjectPath = InvokeFeatureUnpack(path, out var replacedCount);
            if (string.IsNullOrWhiteSpace(extractedProjectPath) || !File.Exists(extractedProjectPath))
                throw new InvalidOperationException("`.ymmpx` の展開は完了しましたが、`.ymmp` が見つかりませんでした。");

            MessageBox.Show(
                $"`.ymmpx` を展開しました。\nリンク復元件数: {replacedCount}\n\n{extractedProjectPath}",
                "展開完了",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return extractedProjectPath;
        }

        private static string InvokeFeatureUnpack(string ymmpxPath, out int replacedPathCount)
        {
            var featurePath = Path.Combine(AppDirectories.PluginDirectory, "YMMResourcePackager", "YMMResourcePackager.Features.dll");
            if (!File.Exists(featurePath))
                throw new FileNotFoundException("Features DLL が見つかりません。", featurePath);

            var assembly = System.Reflection.Assembly.LoadFrom(featurePath);
            var type = assembly.GetType("YMMResourcePackager.Features.EntryPoint")
                ?? throw new InvalidOperationException("Features EntryPoint が見つかりません。");
            var getAvailableMethod = type.GetMethod("GetAvailableUnpackDirectory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("GetAvailableUnpackDirectory メソッドが見つかりません。");
            var method = type.GetMethod("RunUnpack", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("RunUnpack メソッドが見つかりません。");

            var desiredDirectory = GetPreferredUnpackDirectory(ymmpxPath);
            var unpackDirectory = getAvailableMethod.Invoke(null, [desiredDirectory])?.ToString() ?? desiredDirectory;
            object[] args = [ymmpxPath, unpackDirectory, 0];
            var projectPath = method.Invoke(null, args)?.ToString()
                ?? throw new InvalidOperationException("展開後のプロジェクトパスが取得できません。");
            replacedPathCount = args[2] is int i ? i : 0;
            return projectPath;
        }

        private static string GetLegacyYmmpxLibFolderPath() =>
            YMMResourcePackager.Shared.PackagingRules.GetLegacyYmmpxLibFolderPath(AppDirectories.PluginDirectory);

        private static YMMResourcePackager.Shared.PackagingValidationResult ValidateProjectBeforePack(string projectPath, string[] excludedFiles)
        {
            return YMMResourcePackager.Shared.PackagingRules.ValidateProjectBeforePack(projectPath, excludedFiles);
        }

        private static string GetStableAvailableFilePath(string path)
        {
            return YMMResourcePackager.Shared.PackagingRules.GetStableAvailableFilePath(path);
        }

        private static string GetPreferredUnpackDirectory(string ymmpxPath)
        {
            var settings = YMMResourcePackager.Shared.AppSettingsStore.Load();

            try
            {
                var baseDirectory = YMMResourcePackager.Shared.UnpackerArguments.ResolveUnpackBaseDirectory(
                    settings.UnpackOutputMode,
                    settings.CustomUnpackDirectory,
                    ymmpxPath,
                    AppDirectories.PluginDirectory);

                var fileName = Path.GetFileNameWithoutExtension(ymmpxPath);
                return Path.Combine(baseDirectory, fileName);
            }
            catch (InvalidOperationException) when (
                string.Equals(settings.UnpackOutputMode, YMMResourcePackager.Shared.UnpackOutputModes.CustomFolder, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(settings.CustomUnpackDirectory))
            {
                var fileName = Path.GetFileNameWithoutExtension(ymmpxPath);
                return Path.Combine(AppDirectories.PluginDirectory, fileName);
            }
        }

        private static string CreateTemporaryPackagePath(string finalPath)
            => YMMResourcePackager.Shared.PackagingRules.CreateTemporaryPackagePath(finalPath);

        private static void MoveGeneratedPackage(string sourcePath, string destinationPath)
            => YMMResourcePackager.Shared.PackagingRules.MoveGeneratedPackage(sourcePath, destinationPath);

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

        public sealed class UnpackOutputOption
        {
            public UnpackOutputOption(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public string Value { get; }
        }
    }
}
