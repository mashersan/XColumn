using System.Text.Json.Serialization;

namespace XColumn.Models
{
    /// <summary>
    /// 読み込むChrome拡張機能の情報を管理するモデルクラス。
    /// 設定ファイルへの保存対象となる基本情報と、実行時のみ保持する状態を含みます。
    /// </summary>
    public class ExtensionItem
    {
        /// <summary>
        /// 拡張機能の名称（フォルダ名など）。設定ファイルに保存されます。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 拡張機能のフォルダパス（manifest.jsonが存在するディレクトリ）。設定ファイルに保存されます。
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// この拡張機能が有効かどうか。設定ファイルに保存されます。
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// WebView2によって割り当てられた拡張機能ID。
        /// 実行時にロードしたタイミングで設定されるため、保存対象外です。
        /// </summary>
        [JsonIgnore]
        public string Id { get; set; } = "";

        /// <summary>
        /// 設定（オプション）ページの相対パス。manifest.jsonから解析されます。
        /// 実行時のみ保持するため、保存対象外です。
        /// </summary>
        [JsonIgnore]
        public string OptionsPage { get; set; } = "";

        /// <summary>
        /// 設定ページが存在し、かつロード済み（ID取得済み）かどうか。
        /// UIの「設定」ボタンの有効無効判定に使用します。
        /// </summary>
        [JsonIgnore]
        public bool CanOpenOptions => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(OptionsPage);

        /// <summary>
        /// UI（リストボックス等）に表示するためのフォーマット済み文字列。
        /// </summary>
        public string DisplayText => $"{Name} ({Path})";
    }
}