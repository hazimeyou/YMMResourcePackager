using System.Security.Cryptography;

namespace YMMResourcePackager.Shared;

public static class FileIntegrity
{
    public static void VerifySha256(string path, string expectedHash)
    {
        using var stream = File.OpenRead(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ファイルの整合性を確認できませんでした。");
    }
}
