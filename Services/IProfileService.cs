namespace XColumn.Services
{
    /// <summary>
    /// プロファイル（複数ユーザー環境）の実体に対するファイル操作を担うサービス。
    /// 設定ファイル(.dat)とブラウザデータフォルダ(BrowserData)のリネーム・複製・削除、
    /// および再起動をまたぐ複製指示書(pending_clone.json)の読み書きを提供します。
    /// 確認ダイアログや再起動などのUI操作は含みません。
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// 指定プロファイルのブラウザデータフォルダ(BrowserData/プロファイル名)の絶対パスを取得します。
        /// </summary>
        string GetBrowserDataPath(string profileName);

        /// <summary>
        /// 指定プロファイルの設定ファイル(.dat)が存在するかどうかを返します。
        /// </summary>
        bool SettingsFileExists(string profileName);

        /// <summary>
        /// プロファイルをリネームします。設定ファイル(.dat)を移動し、
        /// ブラウザデータフォルダが存在し、かつ移動先が未作成の場合のみフォルダも移動します。
        /// </summary>
        void RenameProfile(string oldName, string newName);

        /// <summary>
        /// プロファイルの設定ファイル(.dat)を複製します。
        /// コピー元が存在すればコピーし、存在しなければ既定設定で新規作成します。
        /// （ブラウザデータフォルダはコピーしません。）
        /// </summary>
        void DuplicateProfileSettings(string sourceName, string destName);

        /// <summary>
        /// プロファイルのブラウザデータフォルダを複製します（コピー元が存在する場合のみ）。
        /// WebViewにロックされていない非アクティブプロファイル向けです。
        /// </summary>
        void CopyBrowserData(string sourceName, string destName);

        /// <summary>
        /// プロファイルを削除します。設定ファイル(.dat)とブラウザデータフォルダの双方を、
        /// それぞれ存在する場合に削除します。
        /// </summary>
        void DeleteProfile(string profileName);

        /// <summary>
        /// 再起動後に実行するブラウザデータの複製を pending_clone.json に予約します。
        /// アクティブプロファイルの複製時、WebViewによるフォルダロックを回避するために使用します。
        /// </summary>
        void SchedulePendingClone(string sourceName, string destName);

        /// <summary>
        /// pending_clone.json が存在する場合に、予約されたブラウザデータの複製を実行します。
        /// 実行後は指示書を削除します。アプリ起動直後（WebView未起動時）に呼び出します。
        /// </summary>
        void ProcessPendingClone();
    }
}