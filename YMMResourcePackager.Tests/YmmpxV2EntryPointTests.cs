using System.IO.Compression;
using System.Text.Json.Nodes;
using YMMResourcePackager.Features;

namespace YMMResourcePackager.Tests;

public sealed class YmmpxV2EntryPointTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "YMMResourcePackager.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreatesV2PackageWithOptionsAndExtractsIt()
    {
        Directory.CreateDirectory(_root);
        var included = Path.Combine(_root, "included.png");
        var excluded = Path.Combine(_root, "excluded.wav");
        await File.WriteAllBytesAsync(included, [1, 2, 3]);
        await File.WriteAllBytesAsync(excluded, [4, 5, 6]);
        var project = Path.Combine(_root, "sample.ymmp");
        await File.WriteAllTextAsync(project, $$"""{"LayoutXml":"layout","ToolStates":{},"Items":[{"FilePath":"{{Path.GetFileName(included)}}"},{"FilePath":"{{Path.GetFileName(excluded)}}"}]}""");
        var package = Path.Combine(_root, "sample.ymmpx");
        var progress = new List<string>();

        await EntryPoint.RunPackAsync(project, package, [excluded], false,
            (message, _, _, _) => progress.Add(message));

        using (var archive = ZipFile.OpenRead(package))
        {
            Assert.NotNull(archive.GetEntry("_ymmpx.json"));
            Assert.NotNull(archive.GetEntry("manifest.v2.json"));
            Assert.NotNull(archive.GetEntry("resources/included.png"));
            Assert.Null(archive.GetEntry("resources/excluded.wav"));
        }
        Assert.Contains("パッケージ作成完了", progress);

        var result = await EntryPoint.RunUnpackAsync(package, Path.Combine(_root, "extracted"));
        var extracted = JsonNode.Parse(await File.ReadAllTextAsync(result.ProjectFilePath))!.AsObject();
        Assert.False(extracted.ContainsKey("LayoutXml"));
        Assert.False(extracted.ContainsKey("ToolStates"));
        Assert.Equal(1, result.ReplacedPathCount);
    }

    [Fact]
    public async Task RejectsFutureAndNonYmmpxWithDifferentErrors()
    {
        Directory.CreateDirectory(_root);
        var future = Path.Combine(_root, "future.ymmpx");
        using (var archive = ZipFile.Open(future, ZipArchiveMode.Create))
        {
            await WriteEntryAsync(archive, "_ymmpx.json", "{\"format\":\"ymmpx\",\"majorVersion\":3,\"minorVersion\":0,\"manifest\":\"manifest.v2.json\"}");
            await WriteEntryAsync(archive, "manifest.v2.json", "{\"schemaVersion\":1,\"resources\":[]}");
        }
        var futureError = await Assert.ThrowsAsync<YmmpxPackageOperationException>(() => EntryPoint.RunUnpackAsync(future, Path.Combine(_root, "future-out")));
        Assert.Equal(YmmpxPackageOperationError.UnsupportedFutureVersion, futureError.Error);

        var text = Path.Combine(_root, "not-ymmpx.txt");
        await File.WriteAllTextAsync(text, "not a zip");
        var textError = await Assert.ThrowsAsync<YmmpxPackageOperationException>(() => EntryPoint.RunUnpackAsync(text, Path.Combine(_root, "text-out")));
        Assert.Equal(YmmpxPackageOperationError.InvalidPackage, textError.Error);
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string path, string content)
    {
        await using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        await writer.WriteAsync(content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
