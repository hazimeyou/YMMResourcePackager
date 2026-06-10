using System.Security.Cryptography;
using System.Text;
using YMMResourcePackager.Shared;

namespace YMMResourcePackager.Tests;

public sealed class FileIntegrityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "YMMResourcePackager.Tests", Guid.NewGuid().ToString("N"), "sample.bin");

    public FileIntegrityTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "verified-content");
    }

    [Fact]
    public void AcceptsMatchingSha256()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("verified-content")));

        FileIntegrity.VerifySha256(_path, expected);
    }

    [Fact]
    public void RejectsMismatchedSha256()
    {
        Assert.Throws<InvalidDataException>(() => FileIntegrity.VerifySha256(_path, new string('0', 64)));
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
