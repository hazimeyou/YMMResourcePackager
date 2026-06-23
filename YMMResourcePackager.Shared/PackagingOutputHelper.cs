namespace YMMResourcePackager.Shared;

public static class PackagingOutputHelper
{
    public static string GetStableAvailableFilePath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var pattern = $"^{System.Text.RegularExpressions.Regex.Escape(name)}_(\\d+){System.Text.RegularExpressions.Regex.Escape(ext)}$";
        var max = 0;

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var fileName = Path.GetFileName(file);
                if (!fileName.StartsWith($"{name}_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(Path.GetExtension(fileName), ext, StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(
                    fileName,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                    max = Math.Max(max, n);
            }
        }

        return Path.Combine(dir, $"{name}_{max + 1:D3}{ext}");
    }

    public static string CreateTemporaryPackagePath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(finalPath);
        var extension = Path.GetExtension(finalPath);
        return Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp{extension}");
    }

    public static void MoveGeneratedPackage(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("一時出力ファイルが見つかりません。", sourcePath);

        PackagingRules.Log($"[Output] 出力先: {destinationPath}");
        File.Move(sourcePath, destinationPath, overwrite: true);
    }
}
