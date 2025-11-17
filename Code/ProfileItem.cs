namespace XColumn.Models
{
    /// <summary>
    /// プロファイル選択用コンボボックスに表示するデータ項目。
    /// </summary>
    public class ProfileItem
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }

        /// <summary>
        /// UIに表示される文字列。使用中の場合はサフィックスを付ける。
        /// </summary>
        public string DisplayName => IsActive ? $"{Name} (使用中)" : Name;
    }
}