using System.IO;
using System.Text.Json.Nodes;
using XColumn.Models;

namespace XColumn.Helpers
{
    /// <summary>
    /// ローカルのChromeデータフォルダをスキャンし、インストール済みの拡張機能を検索・複製するヘルパークラス。
    /// X(XColumn)へChrome拡張機能を取り込むための一連の静的メソッドを提供します。
    /// </summary>
    public static class ChromeExtensionImporter
    {
        // ===== Public Methods =====

        /// <summary>
        /// Chromeのデフォルトプロファイルにインストールされている拡張機能を検索して返します。
        /// </summary>
        /// <returns>検出された拡張機能のリスト。フォルダが存在しない場合や検出ゼロの場合は空リスト。</returns>
        public static List<ExtensionItem> ScanChromeExtensions()
        {
            var results = new List<ExtensionItem>();

            // Chromeのユーザーデータパス (通常: %LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeExtPath = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Extensions");

            if (!Directory.Exists(chromeExtPath))
            {
                return results;
            }

            try
            {
                // 各拡張機能IDフォルダをループ（フォルダ名 = 拡張機能ID）
                foreach (string idDir in Directory.GetDirectories(chromeExtPath))
                {
                    // IDフォルダ配下にはバージョン番号のサブフォルダが複数存在しうるため、
                    // 作成日時が最も新しいフォルダ（＝最新バージョン）を採用する
                    string? versionDir = Directory.GetDirectories(idDir)
                                                 .OrderByDescending(d => Directory.GetCreationTime(d))
                                                 .FirstOrDefault();

                    if (string.IsNullOrEmpty(versionDir)) continue;

                    string manifestPath = Path.Combine(versionDir, "manifest.json");
                    if (!File.Exists(manifestPath)) continue;

                    string name = GetExtensionNameFromManifest(manifestPath);

                    // 名前が取得できた場合のみリストに追加
                    if (!string.IsNullOrEmpty(name))
                    {
                        results.Add(new ExtensionItem
                        {
                            Name = name,
                            Path = versionDir,            // バージョンフォルダを拡張機能のルートとして扱う
                            IsEnabled = true,
                            Id = Path.GetFileName(idDir)  // フォルダ名がそのまま拡張機能ID
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Chrome Extension Scan Error: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// 選択した拡張機能をアプリの管理下（実行フォルダ配下の Extensions/拡張機能名）にコピーします。
        /// </summary>
        /// <param name="sourcePath">コピー元（Chrome側のバージョンフォルダ）。</param>
        /// <param name="extensionName">拡張機能名（コピー先フォルダ名に使用。不正文字は除去）。</param>
        /// <returns>コピー先のフルパス。</returns>
        public static string CopyExtensionToAppFolder(string sourcePath, string extensionName)
        {
            try
            {
                // アプリの実行フォルダ/Extensions/拡張機能名
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string destRoot = Path.Combine(appDir, "Extensions");

                // ファイル名（フォルダ名）に使えない文字をアンダースコアへ置換
                string safeName = string.Join("_", extensionName.Split(Path.GetInvalidFileNameChars()));
                string destDir = Path.Combine(destRoot, safeName);

                // 既に同名フォルダが存在する場合は上書きするため、いったん完全削除する
                if (Directory.Exists(destDir))
                {
                    Directory.Delete(destDir, true);
                }
                Directory.CreateDirectory(destDir);

                // ディレクトリを再帰的にコピー
                CopyDirectory(sourcePath, destDir);

                return destDir;
            }
            catch (Exception ex)
            {
                Logger.Log($"Copy Error: {ex.Message}");
                throw;
            }
        }

        // ===== Private Methods =====

        /// <summary>
        /// manifest.json を解析して拡張機能名を取得します。
        /// 名前が "__MSG_xxx__" 形式の場合は "_locales" による多言語リソースを簡易的に解決します。
        /// </summary>
        /// <param name="manifestPath">manifest.json のフルパス。</param>
        /// <returns>解決された拡張機能名。解析失敗時は空文字、名前未定義時は "Unknown Extension"。</returns>
        private static string GetExtensionNameFromManifest(string manifestPath)
        {
            try
            {
                string jsonString = File.ReadAllText(manifestPath);
                var json = JsonNode.Parse(jsonString);
                string? name = json?["name"]?.GetValue<string>();
                string? defaultLocale = json?["default_locale"]?.GetValue<string>() ?? "en";

                // 名前が __MSG_ で始まる場合 (例: __MSG_appName__) はローカライズキーなので解決を試みる
                if (!string.IsNullOrEmpty(name) && name.StartsWith("__MSG_"))
                {
                    // "__MSG_appName__" → "appName" のようにメッセージキーを抽出
                    string messageKey = name.Replace("__MSG_", "").Replace("__", "");
                    string? extensionDir = Path.GetDirectoryName(manifestPath);
                    if (string.IsNullOrEmpty(extensionDir))
                    {
                        return name;
                    }

                    // 1. 優先: default_locale のフォルダを探す (例: _locales/en/messages.json)
                    string localePath = Path.Combine(extensionDir, "_locales", defaultLocale, "messages.json");

                    // 2. 見つからなければ 'en' → 'en_US' の順でフォールバック
                    if (!File.Exists(localePath))
                        localePath = Path.Combine(extensionDir, "_locales", "en", "messages.json");
                    if (!File.Exists(localePath))
                        localePath = Path.Combine(extensionDir, "_locales", "en_US", "messages.json");

                    if (File.Exists(localePath))
                    {
                        try
                        {
                            string msgJsonStr = File.ReadAllText(localePath);
                            var msgJson = JsonNode.Parse(msgJsonStr);
                            // messages.json の構造は { "appName": { "message": "Tampermonkey" } }
                            string? resolvedName = msgJson?[messageKey]?["message"]?.GetValue<string>();

                            if (!string.IsNullOrEmpty(resolvedName))
                            {
                                return resolvedName;
                            }
                        }
                        catch { /* 解析失敗時は後続のフォールバックへ */ }
                    }

                    // ローカライズ解決に失敗した場合はフォルダ名（拡張機能IDの親）を返す
                    return Path.GetFileName(Path.GetDirectoryName(extensionDir)) ?? "Unknown Extension";
                }

                return name ?? "Unknown Extension";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// ディレクトリを再帰的にコピーします（サブフォルダ・ファイルをすべて複製）。
        /// 個々のファイルコピー失敗（ロック中・アクセス拒否など）はスキップして処理を継続します。
        /// </summary>
        /// <param name="sourceDir">コピー元ディレクトリ。</param>
        /// <param name="destinationDir">コピー先ディレクトリ。</param>
        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    // 上書きを許可してコピー。ロック中ファイル等で失敗してもスキップして継続する
                    file.CopyTo(Path.Combine(destinationDir, file.Name), true);
                }
                catch { /* アクセス拒否・ロック中などは無視 */ }
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}