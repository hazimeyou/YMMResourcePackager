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
    public void ReturnsStableAvailableFilePathPastThreeDigits()
    {
        var output = Path.Combine(_root, "BigProject.ymmpx");
        File.WriteAllText(output, "base");
        File.WriteAllText(Path.Combine(_root, "BigProject_999.ymmpx"), "nine");

        var next = PackagingRules.GetStableAvailableFilePath(output);

        Assert.Equal(Path.Combine(_root, "BigProject_1000.ymmpx"), next);
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

    [Fact]
    public void ResolvesFolderRulesAgainstProjectFiles()
    {
        var projectDir = Path.Combine(_root, "folder-rule");
        var assetsDir = Path.Combine(projectDir, "assets", "bgm");
        Directory.CreateDirectory(assetsDir);

        File.WriteAllText(Path.Combine(assetsDir, "theme.wav"), "theme");
        File.WriteAllText(Path.Combine(projectDir, "assets", "keep.txt"), "keep");

        var projectPath = Path.Combine(projectDir, "sample.ymmp");
        File.WriteAllText(
            projectPath,
            """
            {
              "Items": [
                { "FilePath": "assets/bgm/theme.wav" },
                { "FilePath": "assets/keep.txt" }
              ]
            }
            """);

        var rules = new[]
        {
            new ExcludeRule
            {
                Path = Path.Combine(projectDir, "assets", "bgm"),
                IsFolder = true,
                IsExcluded = true
            }
        };

        var result = PackagingRules.ValidateProjectBeforePack(projectPath, rules);

        Assert.Equal(2, result.DetectedMaterialCount);
        Assert.Equal(1, result.ExcludedMaterialCount);
        Assert.Equal(0, result.MissingMaterialCount);
    }

    [Fact]
    public void ResolvesLocalRelativeRulesAgainstProjectFiles()
    {
        var projectDir = Path.Combine(_root, "local-rule");
        var assetsDir = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(assetsDir);

        File.WriteAllText(Path.Combine(assetsDir, "local.wav"), "local");
        File.WriteAllText(Path.Combine(assetsDir, "other.wav"), "other");

        var projectPath = Path.Combine(projectDir, "sample.ymmp");
        File.WriteAllText(
            projectPath,
            """
            {
              "Items": [
                { "FilePath": "assets/local.wav" },
                { "FilePath": "assets/other.wav" }
              ]
            }
            """);

        var rules = new[]
        {
            new ExcludeRule
            {
                Path = "assets/local.wav",
                IsFolder = false,
                IsExcluded = true
            }
        };

        var excluded = PackagingRules.ResolveExcludedFiles(projectPath, rules);

        Assert.Equal(new[] { "assets/local.wav" }, excluded.OrderBy(x => x));
    }

    [Fact]
    public void LoadsLegacyExcludeRulesFromJson()
    {
        var json = """
                   [
                     { "FilePath": "assets/legacy.wav", "IsExcluded": true },
                     { "FilePath": "assets/folder", "IsFolder": true, "IsExcluded": true }
                   ]
                   """;

        var rules = ExcludeRuleStore.LoadFromJson(json);

        Assert.Collection(
            rules.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase),
            first =>
            {
                Assert.Equal("assets/folder", first.Path);
                Assert.True(first.IsFolder);
                Assert.True(first.IsExcluded);
            },
            second =>
            {
                Assert.Equal("assets/legacy.wav", second.Path);
                Assert.False(second.IsFolder);
                Assert.True(second.IsExcluded);
            });
    }

    [Fact]
    public void CreatesTemporaryPackagePathBesideFinalOutput()
    {
        var finalPath = Path.Combine(_root, "MyProject.ymmpx");

        var temporaryPath = PackagingRules.CreateTemporaryPackagePath(finalPath);

        Assert.Equal(_root, Path.GetDirectoryName(temporaryPath));
        Assert.StartsWith(".MyProject.", Path.GetFileName(temporaryPath));
        Assert.EndsWith(".tmp.ymmpx", temporaryPath);
        Assert.NotEqual(temporaryPath, PackagingRules.CreateTemporaryPackagePath(finalPath));
    }

    [Fact]
    public void MovesGeneratedPackageAndOverwritesDestination()
    {
        var source = Path.Combine(_root, "source.tmp.ymmpx");
        var destination = Path.Combine(_root, "destination.ymmpx");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        PackagingRules.MoveGeneratedPackage(source, destination);

        Assert.False(File.Exists(source));
        Assert.Equal("new", File.ReadAllText(destination));
    }

    [Fact]
    public void RejectsMissingGeneratedPackage()
    {
        var source = Path.Combine(_root, "missing.tmp.ymmpx");
        var destination = Path.Combine(_root, "destination.ymmpx");

        Assert.Throws<FileNotFoundException>(() => PackagingRules.MoveGeneratedPackage(source, destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
