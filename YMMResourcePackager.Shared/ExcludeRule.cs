namespace YMMResourcePackager.Shared;

// 除外対象を 1 件表す、シンプルな保存用モデル。
public sealed class ExcludeRule
{
    public string Path { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsExcluded { get; set; } = true;

    public string KindText => IsFolder ? "フォルダー" : "ファイル";
}
