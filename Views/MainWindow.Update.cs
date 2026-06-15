using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using XColumn.Helpers;
using XColumn.Models;

// WPFとWinFormsのApplication型の曖昧さ回避
using Application = System.Windows.Application;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindowのアップデート確認機能に関するロジックを管理する分割クラス。
    /// GitHub APIを使用して最新リリース情報を取得し、新バージョンがあればユーザーに通知します。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Fields =====

        /// <summary>アップデートチェックを行うかどうかの設定フラグ。</summary>
        private bool _checkForUpdates = true;

        // ===== Private Methods =====

        /// <summary>
        /// 非同期でGitHubのリリースページを確認し、新しいバージョンがあれば通知ダイアログを表示します。
        /// ユーザーがスキップしたバージョンは次回以降通知しません。
        /// </summary>
        /// <param name="skippedVersion">ユーザーが以前にスキップしたバージョン番号文字列。</param>
        private async Task CheckForUpdatesAsync(string skippedVersion)
        {
            if (!_checkForUpdates)
            {
                Logger.Log("Update check skipped by user settings.");
                return;
            }

            // 起動処理の負荷軽減のため少し待つ
            await Task.Delay(3000);

            try
            {
                // 現在のアプリケーションバージョンを取得
                string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                Version current = new Version(ver);

                using (var client = new HttpClient())
                {
                    // GitHub APIの利用規約に従いUser-Agentヘッダーを設定
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XColumn", ver));

                    // 最新リリース情報のエンドポイントにGETリクエストを送信
                    var res = await client.GetAsync("https://api.github.com/repos/mashersan/XColumn/releases/latest");
                    if (!res.IsSuccessStatusCode) return;

                    // レスポンスのJSONを解析
                    var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
                    if (json == null) return;

                    // リリース情報からタグ名、URL、説明文を取得
                    string tag = json["tag_name"]?.GetValue<string>() ?? "v0.0.0";
                    string url = json["html_url"]?.GetValue<string>() ?? "";
                    string body = json["body"]?.GetValue<string>() ?? "";

                    // バージョン文字列(先頭の 'v' を除去)を解析してVersionオブジェクトを作成
                    string remoteVerStr = tag.TrimStart('v');
                    Version remote = new Version(remoteVerStr);

                    // 新しいバージョンがあり、かつスキップ指定されていない場合のみ通知
                    if (remote > current && remoteVerStr != skippedVersion)
                    {
                        // UIスレッドでダイアログを表示
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // GitHub / スキップ / 後で の3択ダイアログ
                            var result = MessageWindow.Show(
                                this, // 親ウィンドウ
                                string.Format(Properties.Resources.Msg_UpdateAvailable, tag, body),
                                Properties.Resources.UpdateNotification,
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Information,
                                yesText: Properties.Resources.GitHub,
                                noText: Properties.Resources.SkipVersion,
                                cancelText: Properties.Resources.Later
                            );

                            if (result == MessageBoxResult.Yes)
                            {
                                // 「GitHubへ」: ブラウザでリリースページを開く
                                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                            }
                            else if (result == MessageBoxResult.No)
                            {
                                // 「このバージョンをスキップ」: 設定ファイルに記録し次回から通知しない
                                AppSettings s = ReadSettingsFromFile(_activeProfileName);
                                s.SkippedVersion = remoteVerStr;
                                SaveAppSettingsToFile(_activeProfileName, s);
                            }
                            // 「後で」(MessageBoxResult.Cancel): 何もしない（次回起動時に再通知）
                        });
                    }
                }
            }
            catch
            {
                // ネットワークエラーやAPI制限などで確認に失敗しても、
                // アプリのメイン動作には影響させないため、例外は無視して処理を終える
            }
        }
    }
}