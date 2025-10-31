using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YMMResourcePackager;
using YMMResourcePackagerPlugin.Models;  // ← ExcludeItem の定義場所を統一
using YukkuriMovieMaker.Commons;

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

                var psi = new ProcessStartInfo
                {
                    FileName = appExe,
                    Arguments = "--associate",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"関連付け実行中にエラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                // プロジェクト JSON を読み込む
                string jsonText = File.ReadAllText(SelectedProject);
                using JsonDocument doc = JsonDocument.Parse(jsonText);

                // プロジェクト内の素材パスを抽出
                var allFiles = FindFilePaths(doc.RootElement)
                               .Select(f => new ExcludeItem { FilePath = f, IsExcluded = false })
                               .ToList();

                // 除外設定ファイル
                string excludePath = Path.Combine(PluginDirectory, "YMMResourcePackager", "exclude.json");

                // 既存の除外設定を読み込んで反映
                if (File.Exists(excludePath))
                {
                    try
                    {
                        var excludedFiles = JsonSerializer.Deserialize<List<ExcludeItem>>(File.ReadAllText(excludePath)) ?? new();
                        var excludedDict = excludedFiles.ToDictionary(e => e.FilePath, e => e.IsExcluded);

                        foreach (var item in allFiles)
                        {
                            if (excludedDict.TryGetValue(item.FilePath, out bool isEx))
                                item.IsExcluded = isEx;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        MessageBox.Show($"除外リストの読み込みに失敗しました。\n{jsonEx.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // GUIを開く
                var dlg = new ExcludeSettingWindow(allFiles);
                dlg.Owner = Application.Current.MainWindow;
                dlg.ShowDialog();

                // 保存
                var updatedList = dlg.ExcludeItems;
                Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
                File.WriteAllText(excludePath, JsonSerializer.Serialize(updatedList, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"除外設定の表示に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PackageProjectAsync()
        {
            if (string.IsNullOrEmpty(SelectedProject) || !File.Exists(SelectedProject))
            {
                Status = "プロジェクトが選択されていないか、存在しません。";
                Progress = 0;
                return;
            }

            try
            {
                Status = "素材収集中...";
                Progress = 0;

                string baseDir = Path.GetDirectoryName(SelectedProject)!;
                string projectName = Path.GetFileNameWithoutExtension(SelectedProject);
                string outputPath = Path.Combine(baseDir, $"{projectName}.ymmpx");


                if (File.Exists(outputPath))
                {
                    var result = MessageBox.Show(
                        $"出力先に既存ファイルがあります:\n{outputPath}\n\n上書きしますか？\nいいえで連番、キャンセルで処理中止。",
                        "ファイルが存在します",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning,
                        MessageBoxResult.Cancel);

                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            File.Delete(outputPath);
                            break;
                        case MessageBoxResult.No:
                            int counter = 1;
                            string baseName = Path.GetFileNameWithoutExtension(outputPath);
                            string ext = Path.GetExtension(outputPath);
                            string dir = Path.GetDirectoryName(outputPath)!;
                            string newPath = outputPath;
                            while (File.Exists(newPath))
                            {
                                newPath = Path.Combine(dir, $"{baseName}_{counter}{ext}");
                                counter++;
                            }
                            outputPath = newPath;
                            break;
                        case MessageBoxResult.Cancel:
                            Status = "処理をキャンセルしました。";
                            Progress = 0;
                            return;
                    }
                }

                // プロジェクト内素材を探索
                List<string> resources = new();
                string jsonText = await File.ReadAllTextAsync(SelectedProject);
                using (JsonDocument doc = JsonDocument.Parse(jsonText))
                {
                    var filePaths = doc.RootElement
                                       .EnumerateObject()
                                       .SelectMany(e => FindFilePaths(e.Value))
                                       .Distinct();

                    foreach (var path in filePaths)
                        if (File.Exists(path))
                            resources.Add(path);
                }

                // 除外設定を適用
                string excludePathFile = Path.Combine(PluginDirectory, "YMMResourcePackager", "exclude.json");
                HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(excludePathFile))
                {
                    var list = JsonSerializer.Deserialize<List<ExcludeItem>>(File.ReadAllText(excludePathFile)) ?? new();
                    excluded = new HashSet<string>(
                        list.Where(x => x.IsExcluded).Select(x => x.FilePath),
                        StringComparer.OrdinalIgnoreCase);
                }

                resources = resources
                    .Where(r => !excluded.Contains(r))
                    .ToList();

                if (excluded.Count > 0)
                {
                    Status = $"{excluded.Count} 個の素材を除外しました。";
                }

                Status = $"ZIP作成中... ({resources.Count} 個の素材)";
                Progress = 0;

                await Task.Run(() =>
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "YMMResourcePackager", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);
                    string linksFile = Path.Combine(tempDir, "links.txt");
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    using (var writer = new StreamWriter(linksFile))
                    {
                        for (int i = 0; i < resources.Count; i++)
                        {
                            string file = resources[i];
                            string fileName = Path.GetFileName(file);
                            string uniqueName = fileName;
                            int counter = 1;

                            while (usedNames.Contains(uniqueName))
                            {
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                string ext = Path.GetExtension(fileName);
                                uniqueName = $"{nameWithoutExt}_{counter}{ext}";
                                counter++;
                            }

                            usedNames.Add(uniqueName);
                            writer.WriteLine($"{file},resources/{uniqueName}");

                            int progressIndex = i;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Progress = (double)(progressIndex + 1) / resources.Count * 100;
                                Status = $"ZIP作成中... {progressIndex + 1}/{resources.Count}";
                            });
                        }
                    }

                    using (var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(SelectedProject, "project.ymmp");
                        zip.CreateEntryFromFile(linksFile, "links.txt");

                        foreach (var file in resources)
                        {
                            string uniqueName = Path.GetFileName(file);
                            zip.CreateEntryFromFile(file, "resources/" + uniqueName);
                        }
                    }
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = $".ymmpx パッケージ作成完了: {outputPath}";
                    Progress = 100;
                });
            }
            catch (Exception ex)
            {
                Status = $"エラー: {ex.Message}";
                Progress = 0;
            }
        }

        private IEnumerable<string> FindFilePaths(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "FilePath" && prop.Value.ValueKind == JsonValueKind.String)
                        yield return prop.Value.GetString()!;
                    else
                        foreach (var child in FindFilePaths(prop.Value))
                            yield return child;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    foreach (var child in FindFilePaths(item))
                        yield return child;
            }
        }
    }
}
