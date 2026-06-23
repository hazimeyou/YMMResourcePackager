using YMMResourcePackager.Shared;

namespace YMMResourcePackager.Tests;

public sealed class UnpackerArgumentsTests
{
    // 引数の分解と展開先解決の境界条件を確認する。
    [Fact]
    public void StripLoggingSwitchesReturnsRemainingArgsAndFlags()
    {
        var result = UnpackerArguments.StripLoggingSwitches(new[] { "--enable-logging", "sample.ymmpx", "--disable-logging" });

        Assert.Equal(new[] { "sample.ymmpx" }, result.RemainingArgs);
        Assert.True(result.EnableLogging);
        Assert.True(result.DisableLogging);
    }

    [Fact]
    public void ResolvesPluginFolderMode()
    {
        var pluginDirectory = Path.Combine("C:", "Plugins", "YMMResourcePackager");
        var result = UnpackerArguments.ResolveUnpackBaseDirectory(
            UnpackOutputModes.PluginFolder,
            null,
            Path.Combine("C:", "Projects", "sample.ymmpx"),
            pluginDirectory);

        Assert.Equal(pluginDirectory, result);
    }

    [Fact]
    public void ResolvesYmmpxFolderMode()
    {
        var ymmpxPath = Path.Combine("C:", "Projects", "sample.ymmpx");
        var result = UnpackerArguments.ResolveUnpackBaseDirectory(
            UnpackOutputModes.YmmpxFolder,
            null,
            ymmpxPath,
            Path.Combine("C:", "Plugins", "YMMResourcePackager"));

        Assert.Equal(Path.GetDirectoryName(ymmpxPath), result);
    }

    [Fact]
    public void ResolvesRelativeYmmpxFolderModeToPluginFolder()
    {
        var pluginDirectory = Path.Combine("C:", "Plugins", "YMMResourcePackager");
        var result = UnpackerArguments.ResolveUnpackBaseDirectory(
            UnpackOutputModes.YmmpxFolder,
            null,
            "sample.ymmpx",
            pluginDirectory);

        Assert.Equal(pluginDirectory, result);
    }

    [Fact]
    public void ResolvesCustomFolderMode()
    {
        var result = UnpackerArguments.ResolveUnpackBaseDirectory(
            UnpackOutputModes.CustomFolder,
            @"  C:\Custom Output  ",
            Path.Combine("C:", "Projects", "sample.ymmpx"),
            Path.Combine("C:", "Plugins", "YMMResourcePackager"));

        Assert.Equal(Path.GetFullPath(@"C:\Custom Output"), result);
    }

    [Fact]
    public void RejectsBlankCustomFolderMode()
    {
        Assert.Throws<InvalidOperationException>(() =>
            UnpackerArguments.ResolveUnpackBaseDirectory(
                UnpackOutputModes.CustomFolder,
                " ",
                Path.Combine("C:", "Projects", "sample.ymmpx"),
                Path.Combine("C:", "Plugins", "YMMResourcePackager")));
    }

    [Fact]
    public void FallsBackToPluginFolderForUnknownMode()
    {
        var pluginDirectory = Path.Combine("C:", "Plugins", "YMMResourcePackager");

        var result = UnpackerArguments.ResolveUnpackBaseDirectory(
            "legacy-mode",
            null,
            Path.Combine("C:", "Projects", "sample.ymmpx"),
            pluginDirectory);

        Assert.Equal(pluginDirectory, result);
    }
}
