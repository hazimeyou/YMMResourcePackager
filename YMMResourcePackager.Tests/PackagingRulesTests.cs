using YMMResourcePackager.Shared;

namespace YMMResourcePackager.Tests;

public sealed class PackagingRulesTests : IDisposable
{
    private readonly string _root;

    public PackagingRulesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "YMMResourcePackager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void DetectsLegacyYmmpxLibFolder()
    {
        var pluginRoot = Path.Combine(_root, "plugins");
        var legacyFolder = Path.Combine(pluginRoot, "YmmpxLib");
        Directory.CreateDirectory(legacyFolder);

        Assert.True(PackagingRules.HasLegacyYmmpxLibFolder(pluginRoot));
        Assert.Equal(legacyFolder, PackagingRules.GetLegacyYmmpxLibFolderPath(pluginRoot));
    }

    [Fact]
    public void ReturnsStableAvailableFilePathUsingHighestExistingSuffix()
    {
        var output = Path.Combine(_root, "MyProject.ymmpx");
        File.WriteAllText(output, "base");
        File.WriteAllText(Path.Combine(_root, "MyProject_001.ymmpx"), "one");
        File.WriteAllText(Path.Combine(_root, "MyProject_003.ymmpx"), "three");

        var next = PackagingRules.GetStableAvailableFilePath(output);

        Assert.Equal(Path.Combine(_root, "MyProject_004.ymmpx"), next);
    }

    [Fact]
    public void ValidatesDetectedExcludedAndMissingMaterials()
    {
        var projectDir = Path.Combine(_root, "project");
        var assetsDir = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(assetsDir);

        File.WriteAllText(Path.Combine(assetsDir, "kept.txt"), "kept");
        File.WriteAllText(Path.Combine(assetsDir, "excluded.txt"), "excluded");

        var projectPath = Path.Combine(projectDir, "sample.ymmp");
        File.WriteAllText(
            projectPath,
            """
            {
              "Items": [
                { "FilePath": "assets/kept.txt" },
                { "Nested": { "FilePath": "assets/excluded.txt" } },
                { "FilePath": "assets/missing.txt" }
              ]
            }
            """);

        var result = PackagingRules.ValidateProjectBeforePack(projectPath, new[] { "  ASSETS/EXCLUDED.TXT  " });

        Assert.Equal(3, result.DetectedMaterialCount);
        Assert.Equal(1, result.ExcludedMaterialCount);
        Assert.Equal(1, result.MissingMaterialCount);
    }

    [Fact]
    public void ExcludedFileCheckIsCaseInsensitive()
    {
        var excluded = new[] { "  Assets/File.TXT  ", "" };

        Assert.True(PackagingRules.IsExcludedFile("assets/file.txt", excluded));
        Assert.False(PackagingRules.IsExcludedFile("assets/other.txt", excluded));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
