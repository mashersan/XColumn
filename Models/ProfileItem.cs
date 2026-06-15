namespace XColumn.Models
{
    /// <summary>
    /// プロファイル選択UI（コンボボックス）用の表示データ。
    /// </summary>
    public class ProfileItem
    {
        // ===== Properties =====

        /// <summary>プロファイル名。</summary>
        public string Name { get; set; } = "";

        /// <summary>このプロファイルが現在使用中（アクティブ）かどうか。</summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// UIに表示する文字列。使用中の場合は「使用中」サフィックスを付与します。
        /// </summary>
        public string DisplayName => IsActive
            ? string.Format(Properties.Resources.Profile_ActiveSuffix, Name)
            : Name;
    }
}