using XColumn.Models;

namespace XColumn.Services
{
    /// <summary>
    /// 設定ファイルの永続化（暗号化を含む）を担うサービス。
    /// アプリ全体構成(app_config.json)と、プロファイルごとの詳細設定(.dat)の
    /// 読み書きを提供します。UIには一切依存しません。
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// ユーザーデータフォルダ（%AppData%\XColumn）。
        /// </summary>
        string UserDataFolder { get; }

        /// <summary>
        /// プロファイル設定(.dat)を格納するフォルダ。
        /// </summary>
        string ProfilesFolder { get; }

        /// <summary>
        /// アプリ全体構成ファイル(app_config.json)のパス。
        /// </summary>
        string AppConfigPath { get; }

        /// <summary>
        /// 指定プロファイルの設定ファイル(.dat)のフルパスを取得します。
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        /// <returns>プロファイル設定ファイル(.dat)のフルパス。</returns>
        string GetProfilePath(string profileName);

        /// <summary>
        /// app_config.json を読み込みます。存在しない/破損時は既定値を返します。
        /// </summary>
        /// <returns>読み込んだ構成。存在しない/破損時は既定値。</returns>
        AppConfig LoadAppConfig();

        /// <summary>
        /// app_config.json を保存します。
        /// </summary>
        /// <param name="config">保存する構成。</param>
        void SaveAppConfig(AppConfig config);

        /// <summary>
        /// 指定プロファイルの設定を復号して読み込みます。
        /// 存在しない/破損時は既定の AppSettings を返します。
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        /// <returns>復号した設定。存在しない/破損時は既定の AppSettings。</returns>
        AppSettings ReadSettings(string profileName);

        /// <summary>
        /// 指定プロファイルの設定を暗号化して保存します。
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        /// <param name="settings">保存する設定。</param>
        void SaveSettings(string profileName, AppSettings settings);
    }
}