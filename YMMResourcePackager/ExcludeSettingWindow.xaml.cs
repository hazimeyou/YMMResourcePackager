using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using YMMResourcePackager.Shared;

namespace YMMResourcePackager
{
    public partial class ExcludeSettingWindow : Window
    {
        private readonly string _projectDirectory;

        public ObservableCollection<ExcludeRule> GlobalExcludeItems { get; }
        public ObservableCollection<ExcludeRule> LocalExcludeItems { get; }
        public ObservableCollection<ProjectMaterialItem> ProjectMaterials { get; }

        public ExcludeSettingWindow(
            string projectPath,
            IEnumerable<string> projectMaterialPaths,
            IEnumerable<ExcludeRule> globalItems,
            IEnumerable<ExcludeRule> localItems)
        {
            InitializeComponent();

            _projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            GlobalExcludeItems = new ObservableCollection<ExcludeRule>(NormalizeRules(globalItems, forceAbsolute: false, string.Empty));
            LocalExcludeItems = new ObservableCollection<ExcludeRule>(NormalizeRules(localItems, forceAbsolute: false, _projectDirectory));
            ProjectMaterials = new ObservableCollection<ProjectMaterialItem>(BuildProjectMaterials(projectMaterialPaths));
            DataContext = this;
        }

        private static IEnumerable<ExcludeRule> NormalizeRules(IEnumerable<ExcludeRule> items, bool forceAbsolute, string projectDirectory)
        {
            return items
                .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Path))
                .Select(x => new ExcludeRule
                {
                    Path = NormalizeRulePath(x.Path, x.IsFolder, forceAbsolute, projectDirectory),
                    IsFolder = x.IsFolder,
                    IsExcluded = x.IsExcluded
                })
                .GroupBy(x => (x.Path, x.IsFolder), ExcludeRuleKeyComparer.Instance)
                .Select(g => g.Last())
                .ToArray();
        }

        private IEnumerable<ProjectMaterialItem> BuildProjectMaterials(IEnumerable<string> projectMaterialPaths)
        {
            return projectMaterialPaths
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new ProjectMaterialItem
                {
                    Path = path,
                    Exists = MaterialExists(path)
                })
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private bool MaterialExists(string path)
        {
            try
            {
                var resolved = ResolveProjectMaterialPath(path);
                return resolved is not null && File.Exists(resolved);
            }
            catch
            {
                return false;
            }
        }

        private string? ResolveProjectMaterialPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            if (string.IsNullOrWhiteSpace(_projectDirectory))
                return null;

            return Path.GetFullPath(Path.Combine(_projectDirectory, path));
        }

        private void BtnAddGlobalFile_Click(object sender, RoutedEventArgs e) => AddGlobalEntries(selectFolder: false);
        private void BtnAddGlobalFolder_Click(object sender, RoutedEventArgs e) => AddGlobalEntries(selectFolder: true);
        private void BtnAddLocalFile_Click(object sender, RoutedEventArgs e) => AddLocalEntries(selectFolder: false);
        private void BtnAddLocalFolder_Click(object sender, RoutedEventArgs e) => AddLocalEntries(selectFolder: true);
        private void BtnRemoveGlobalSelected_Click(object sender, RoutedEventArgs e) => RemoveSelectedItems(GlobalExcludeListView, GlobalExcludeItems);
        private void BtnRemoveLocalSelected_Click(object sender, RoutedEventArgs e) => RemoveSelectedItems(LocalExcludeListView, LocalExcludeItems);
        private void BtnAddProjectToGlobal_Click(object sender, RoutedEventArgs e) => AddSelectedProjectItems(toGlobal: true);
        private void BtnAddProjectToLocal_Click(object sender, RoutedEventArgs e) => AddSelectedProjectItems(toGlobal: false);

        private void BtnSelectAllGlobal_Click(object sender, RoutedEventArgs e) => ToggleAll(GlobalExcludeItems, true, GlobalExcludeListView);
        private void BtnDeselectAllGlobal_Click(object sender, RoutedEventArgs e) => ToggleAll(GlobalExcludeItems, false, GlobalExcludeListView);
        private void BtnSelectAllLocal_Click(object sender, RoutedEventArgs e) => ToggleAll(LocalExcludeItems, true, LocalExcludeListView);
        private void BtnDeselectAllLocal_Click(object sender, RoutedEventArgs e) => ToggleAll(LocalExcludeItems, false, LocalExcludeListView);

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AddSelectedProjectItems(bool toGlobal)
        {
            var target = toGlobal ? GlobalExcludeItems : LocalExcludeItems;
            var selectedItems = ProjectMaterialListView.SelectedItems.Cast<ProjectMaterialItem>().ToArray();
            foreach (var item in selectedItems)
            {
                if (toGlobal)
                {
                    var absolutePath = ResolveProjectMaterialPath(item.Path);
                    if (string.IsNullOrWhiteSpace(absolutePath))
                        continue;

                    AddRule(target, absolutePath, isFolder: false, forceAbsolute: true);
                }
                else
                {
                    AddRule(target, item.Path, isFolder: false, forceAbsolute: false);
                }
            }
        }

        private void AddGlobalEntries(bool selectFolder)
        {
            try
            {
                if (selectFolder)
                {
                    var dialog = new OpenFolderDialog { Title = "グローバル除外フォルダーを選択" };
                    if (dialog.ShowDialog() != true)
                        return;

                    AddRule(GlobalExcludeItems, dialog.FolderName, isFolder: true, forceAbsolute: true);
                    return;
                }

                var fileDialog = new OpenFileDialog
                {
                    Title = "グローバル除外ファイルを選択",
                    Multiselect = true
                };

                if (fileDialog.ShowDialog() != true)
                    return;

                foreach (var file in fileDialog.FileNames)
                    AddRule(GlobalExcludeItems, file, isFolder: false, forceAbsolute: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddLocalEntries(bool selectFolder)
        {
            try
            {
                if (selectFolder)
                {
                    var dialog = new OpenFolderDialog { Title = "ローカル除外フォルダーを選択" };
                    if (dialog.ShowDialog() != true)
                        return;

                    AddRule(LocalExcludeItems, dialog.FolderName, isFolder: true, forceAbsolute: false);
                    return;
                }

                var fileDialog = new OpenFileDialog
                {
                    Title = "ローカル除外ファイルを選択",
                    Multiselect = true
                };

                if (fileDialog.ShowDialog() != true)
                    return;

                foreach (var file in fileDialog.FileNames)
                    AddRule(LocalExcludeItems, file, isFolder: false, forceAbsolute: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddRule(ObservableCollection<ExcludeRule> items, string path, bool isFolder, bool forceAbsolute)
        {
            var normalized = NormalizeRulePath(path, isFolder, forceAbsolute, _projectDirectory);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (items.Any(x => x.IsFolder == isFolder &&
                               string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            items.Add(new ExcludeRule
            {
                Path = normalized,
                IsFolder = isFolder,
                IsExcluded = true
            });
        }

        private static string NormalizeRulePath(string path, bool isFolder, bool forceAbsolute, string projectDirectory)
        {
            var normalized = path.Trim()
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (forceAbsolute)
            {
                normalized = Path.IsPathRooted(normalized)
                    ? Path.GetFullPath(normalized)
                    : Path.GetFullPath(Path.Combine(projectDirectory, normalized));
            }
            else if (!string.IsNullOrWhiteSpace(projectDirectory))
            {
                normalized = MakeProjectRelativeIfPossible(normalized, projectDirectory);
            }

            var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar);
            if (isFolder)
            {
                var root = Path.GetPathRoot(normalized);
                if (!string.IsNullOrWhiteSpace(root) &&
                    string.Equals(trimmed, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return root;
                }
            }

            return trimmed;
        }

        private static string MakeProjectRelativeIfPossible(string path, string projectDirectory)
        {
            try
            {
                var absolute = Path.IsPathRooted(path)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(Path.Combine(projectDirectory, path));
                var relative = Path.GetRelativePath(projectDirectory, absolute);
                if (!relative.StartsWith("..", StringComparison.Ordinal))
                    return relative;
                return absolute;
            }
            catch
            {
                return path;
            }
        }

        private static void RemoveSelectedItems(System.Windows.Controls.ListView listView, ObservableCollection<ExcludeRule> items)
        {
            var selected = listView.SelectedItems.Cast<ExcludeRule>().ToArray();
            foreach (var item in selected)
                items.Remove(item);
        }

        private static void ToggleAll(IEnumerable<ExcludeRule> items, bool isExcluded, System.Windows.Controls.ListView listView)
        {
            foreach (var item in items)
                item.IsExcluded = isExcluded;

            listView.Items.Refresh();
        }

        private sealed class ExcludeRuleKeyComparer : IEqualityComparer<(string Path, bool IsFolder)>
        {
            public static readonly ExcludeRuleKeyComparer Instance = new();

            public bool Equals((string Path, bool IsFolder) x, (string Path, bool IsFolder) y)
            {
                return x.IsFolder == y.IsFolder &&
                       string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string Path, bool IsFolder) obj)
            {
                return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path), obj.IsFolder);
            }
        }
    }

    public sealed class ProjectMaterialItem
    {
        public string Path { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string StatusText => Exists ? "存在" : "未発見";
    }
}
