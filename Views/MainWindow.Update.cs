using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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
        private async Task CheckForUpdatesAsync(string skippedVersion, bool manual = false)
        {
            if (!_checkForUpdates && !manual)
            {
                Logger.Log("Update check skipped by user settings.");
                return;
            }

            // 起動処理の負荷軽減のため少し待つ（手動確認では待たない）
            if (!manual) await Task.Delay(3000);

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
                    if (!res.IsSuccessStatusCode)
                    {
                        if (manual)
                            Application.Current.Dispatcher.Invoke(() =>
                                MessageWindow.Show(this,
                                    string.Format(Properties.Resources.Update_CheckFailed, $"HTTP {(int)res.StatusCode}"),
                                    Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

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
                        // 自分のビルド種別に合致する更新zipアセットを選ぶ（無ければ自己更新は出さずGitHub導線のみ）
                        var (assetName, assetUrl) = SelectUpdateAsset(json);
                        string? shaUrl = (assetName != null) ? FindAssetSha256Url(json, assetName) : null;

                        bool doSelfUpdate = false;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBoxResult result;
                            if (assetUrl != null)
                            {
                                // 4択: 今すぐ更新(OK) / GitHub(Yes) / スキップ(No) / 後で(Cancel)
                                result = MessageWindow.Show(
                                    this,
                                    string.Format(Properties.Resources.Msg_UpdateAvailable, tag, body),
                                    Properties.Resources.UpdateNotification,
                                    MessageBoxButton.YesNoCancel,            // allButtons:true 指定時は無視
                                    MessageBoxImage.Information,
                                    yesText: Properties.Resources.GitHub,
                                    noText: Properties.Resources.SkipShort,  // 4択用の短いラベル
                                    cancelText: Properties.Resources.Later,
                                    okText: Properties.Resources.UpdateNow,
                                    allButtons: true);
                            }
                            else
                            {
                                // 従来どおり3択（合致アセットが無い場合のフォールバック）
                                result = MessageWindow.Show(
                                    this,
                                    string.Format(Properties.Resources.Msg_UpdateAvailable, tag, body),
                                    Properties.Resources.UpdateNotification,
                                    MessageBoxButton.YesNoCancel,
                                    MessageBoxImage.Information,
                                    yesText: Properties.Resources.GitHub,
                                    noText: Properties.Resources.SkipVersion,
                                    cancelText: Properties.Resources.Later);
                            }

                            if (result == MessageBoxResult.OK)
                            {
                                doSelfUpdate = true; // 実ダウンロードは Invoke 外で実施
                            }
                            else if (result == MessageBoxResult.Yes)
                            {
                                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                            }
                            else if (result == MessageBoxResult.No)
                            {
                                AppSettings s = ReadSettingsFromFile(_activeProfileName);
                                s.SkippedVersion = remoteVerStr;
                                SaveAppSettingsToFile(_activeProfileName, s);
                            }
                            // Cancel(後で): 何もしない（次回起動時に再通知）
                        });

                        if (doSelfUpdate && assetName != null && assetUrl != null)
                        {
                            await StartSelfUpdateAsync(assetName, assetUrl, shaUrl);
                        }
                    }
                    else if (manual)
                    {
                        // 手動確認で、かつ更新が無い場合のみ「最新です」を通知
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageWindow.Show(this,
                                string.Format(Properties.Resources.Update_UpToDate, ver),
                                Properties.Resources.UpdateNotification,
                                MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                }
            }
            catch (Exception ex)
            {
                if (manual)
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageWindow.Show(this,
                            string.Format(Properties.Resources.Update_CheckFailed, ex.Message),
                            Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error));
                // 自動確認時はメイン動作に影響させないため無視
            }
        }

        /// <summary>
        /// ヘルプメニューからの能動的アップデート確認（設定・スキップを無視し結果を必ず通知）。
        /// </summary>
        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            _ = CheckForUpdatesAsync(string.Empty, manual: true);
        }

        /// <summary>
        /// 現在の実行ファイルが自己完結(SelfContained)版として publish されたかを判定します。
        /// .csproj の AssemblyMetadata("BuildType", $(SelfContained)) を読み取り、"true" 以外は false。
        /// </summary>
        private static bool IsSelfContainedBuild()
        {
            var attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => string.Equals(a.Key, "BuildType", StringComparison.OrdinalIgnoreCase));
            return string.Equals(attr?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// リリースJSONの assets から、自分のビルド種別に合致する更新zipを1つ選びます。
        /// 命名規則: フルセット版は名前に "_Contained" を含み、FW依存版は含まない。HTTPSのみ採用。
        /// </summary>
        /// <returns>(アセット名, ダウンロードURL)。該当なしは (null, null)。</returns>
        private static (string? name, string? url) SelectUpdateAsset(JsonNode releaseJson)
        {
            bool wantContained = IsSelfContainedBuild();
            var assets = releaseJson["assets"]?.AsArray();
            if (assets == null) return (null, null);

            foreach (var a in assets)
            {
                string name = a?["name"]?.GetValue<string>() ?? "";
                string dl = a?["browser_download_url"]?.GetValue<string>() ?? "";
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                bool isContained = name.Contains("_Contained", StringComparison.OrdinalIgnoreCase);
                if (isContained != wantContained) continue;
                if (!dl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;

                return (name, dl);
            }
            return (null, null);
        }

        /// <summary>
        /// 対象zipに対応する "&lt;zip名&gt;.sha256" アセットがあれば、そのダウンロードURLを返します（無ければ null）。
        /// </summary>
        private static string? FindAssetSha256Url(JsonNode releaseJson, string zipAssetName)
        {
            var assets = releaseJson["assets"]?.AsArray();
            if (assets == null) return null;

            string target = zipAssetName + ".sha256";
            foreach (var a in assets)
            {
                string name = a?["name"]?.GetValue<string>() ?? "";
                if (string.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                    return a?["browser_download_url"]?.GetValue<string>();
            }
            return null;
        }

        /// <summary>
        /// 更新zipをダウンロード→検証→新exe展開→現exeをリネーム退避→新exe配置→新exe起動→自己終了します。
        /// 適用前(リネーム前)に失敗した場合は中断し、リネーム後のCopy失敗時はロールバックします。
        /// </summary>
        private async Task StartSelfUpdateAsync(string assetName, string assetUrl, string? shaUrl)
        {
            // 1) ダウンロード前の最終確認（UIスレッド）
            bool proceed = Application.Current.Dispatcher.Invoke(() =>
                MessageWindow.Show(this,
                    Properties.Resources.Update_ConfirmDownload,
                    Properties.Resources.UpdateNotification,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question) == MessageBoxResult.OK);
            if (!proceed) return;

            string tempZip = Path.Combine(Path.GetTempPath(), assetName);
            string stageDir = Path.Combine(Path.GetTempPath(), "XColumn_update_" + Guid.NewGuid().ToString("N"));

            try
            {
                if (!assetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Non-HTTPS asset URL.");

                // 2) ダウンロード
                using (var client = new HttpClient())
                {
                    string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XColumn", ver));

                    using (var res = await client.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        res.EnsureSuccessStatusCode();
                        await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                        await res.Content.CopyToAsync(fs);
                    }

                    // 3) SHA-256照合（.sha256 アセットがある場合のみ）
                    if (!string.IsNullOrEmpty(shaUrl))
                    {
                        string shaText = await client.GetStringAsync(shaUrl);
                        string expected = new string(shaText.Trim().TakeWhile(Uri.IsHexDigit).ToArray());
                        if (expected.Length == 64)
                        {
                            string actual = ComputeSha256Hex(tempZip);
                            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("SHA-256 mismatch.");
                            Logger.Log("Self-update: SHA-256 verified.");
                        }
                    }
                }

                // 4) zip検証＆新exe展開（Zip Slip対策／XColumn.exeが1個であること）
                Directory.CreateDirectory(stageDir);
                string newExePath = ExtractSingleExe(tempZip, stageDir);

                // 5) リネーム方式で適用
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName
                                    ?? throw new InvalidOperationException("Cannot resolve current exe path.");
                string oldExe = currentExe + ".old";

                if (File.Exists(oldExe))
                {
                    // ReadOnly等が付いていると削除に失敗するため属性を解除してから削除を試みる
                    try { File.SetAttributes(oldExe, FileAttributes.Normal); } catch { }
                    try { File.Delete(oldExe); } catch { /* 消せなくても下のMoveで上書きする */ }
                }

                File.Move(currentExe, oldExe, overwrite: true);   // 残骸 .old があっても上書きしてリネーム
                try
                {
                    File.Copy(newExePath, currentExe, overwrite: true);
                }
                catch
                {
                    // Copy失敗時はロールバック
                    try { if (File.Exists(currentExe)) File.Delete(currentExe); File.Move(oldExe, currentExe); } catch { }
                    throw;
                }

                // 6) 新exe起動 → 自己終了（既存の再起動イディオムに合わせ、現在のプロファイルを引き継ぐ）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isRestarting = true; // MainWindow_Closing の終了時保存をスキップ
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = currentExe,
                        Arguments = $"--profile \"{_activeProfileName}\"",
                        UseShellExecute = true
                    });
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Logger.Log("Self-update failed: " + ex.Message);
                Application.Current.Dispatcher.Invoke(() =>
                    MessageWindow.Show(this,
                        string.Format(Properties.Resources.Update_ApplyFailed, ex.Message),
                        Properties.Resources.Title_Error,
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true); } catch { }
            }
        }

        /// <summary>
        /// zip内の "XColumn.exe"（フォルダ階層は問わない）を1個だけ stageDir に展開し、そのパスを返します。
        /// 複数/不在はエラー。全エントリのパスが stageDir 配下に収まることを確認（Zip Slip対策）。
        /// </summary>
        private static string ExtractSingleExe(string zipPath, string stageDir)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            string fullStage = Path.GetFullPath(stageDir);

            // Zip Slip対策：親ディレクトリ参照・絶対パス等で stageDir を脱出するzipは拒否
            foreach (var e in archive.Entries)
            {
                string full = Path.GetFullPath(Path.Combine(fullStage, e.FullName));
                if (!full.StartsWith(fullStage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Zip Slip detected: " + e.FullName);
            }

            var exeEntries = archive.Entries
                .Where(e => string.Equals(e.Name, "XColumn.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exeEntries.Count != 1)
                throw new InvalidOperationException($"Expected exactly one XColumn.exe in zip, found {exeEntries.Count}.");

            string destPath = Path.Combine(fullStage, "XColumn.exe");
            exeEntries[0].ExtractToFile(destPath, overwrite: true);
            return destPath;
        }

        /// <summary>ファイルのSHA-256を16進小文字で返します。</summary>
        private static string ComputeSha256Hex(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
    }
}