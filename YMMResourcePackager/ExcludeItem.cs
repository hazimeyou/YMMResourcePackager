namespace YMMResourcePackagerPlugin.Models
{
    public class ExcludeItem
    {
        public string FilePath { get; set; } = string.Empty;
        // チェック有無は画面の一覧でそのままバインドする。
        public bool IsExcluded { get; set; } = false;
    }
}
