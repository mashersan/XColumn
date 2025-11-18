namespace XColumn.Models
{
    /// <summary>
    /// 読み込むChrome拡張機能の情報を管理するモデルクラス。
    /// </summary>
    public class ExtensionItem
    {
        /// <summary>
        /// 拡張機能の名称（フォルダ名など）。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 拡張機能のフォルダパス（manifest.jsonが存在するディレクトリ）。
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// この拡張機能が有効かどうか。
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// UI（リストボックス等）に表示するためのフォーマット済み文字列。
        /// </summary>
        public string DisplayText => $"{Name} ({Path})";
    }
}