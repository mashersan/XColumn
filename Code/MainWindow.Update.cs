using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowのアップデート確認機能に関するロジックを管理する分割クラス。
    /// GitHub APIを使用して最新リリース情報を取得し、ユーザーに通知します。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 非同期でGitHubのリリースページを確認し、新しいバージョンがあれば通知ダイアログを表示します。
        /// ユーザーがスキップしたバージョンは次回以降通知しません。
        /// </summary>
        /// <param name="skippedVersion">ユーザーが以前にスキップしたバージョン番号文字列</param>
        private async Task CheckForUpdatesAsync(string skippedVersion)
        {
            // 起動処理の負荷軽減のため少し待つ
            await Task.Delay(3000); 
            try
            {
                // 現在のアプリケーションバージョンを取得
                string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                Version current = new Version(ver);

                // GitHub APIを使用して最新リリース情報を取得
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

                    // バージョン文字列を解析してVersionオブジェクトを作成
                    string remoteVerStr = tag.TrimStart('v');
                    Version remote = new Version(remoteVerStr);

                    // 新しいバージョンがあり、かつスキップバージョンでない場合に通知
                    if (remote > current && remoteVerStr != skippedVersion)
                    {
                        // ユーザーに更新を通知するダイアログを表示
                        if (System.Windows.MessageBox.Show($"新バージョン {tag} があります。\n\n{body}\n\n更新ページを開きますか？",
                            "更新通知", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            // 「はい」を選択した場合はデフォルトのブラウザでリリースページを開く
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                        else
                        {
                            // 「いいえ」を選択した場合はスキップバージョンとして記録
                            AppSettings s = ReadSettingsFromFile(_activeProfileName);
                            s.SkippedVersion = remoteVerStr;
                            SaveSettings(_activeProfileName);
                        }
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