namespace XColumn.Models
{
    /// <summary>
    /// プロファイル選択UI（コンボボックス）用の表示データ。
    /// </summary>
    public class ProfileItem
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }

        /// <summary>
        /// UIに表示する文字列。使用中の場合はサフィックスを付与。
        /// </summary>
        public string DisplayName => IsActive ? $"{Name} (使用中)" : Name;
    }
}