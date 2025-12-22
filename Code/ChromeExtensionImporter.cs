using System.IO;
using System.Text.Json.Nodes;
using XColumn.Models;

namespace XColumn.Code
{
    /// <summary>
    /// ローカルのChromeデータフォルダをスキャンし、インストール済みの拡張機能を検索するヘルパークラス。
    /// </summary>
    public static class ChromeExtensionImporter
    {
        /// <summary>
        /// Chromeのデフォルトプロファイルから拡張機能を検索して返します。
        /// </summary>
        public static List<ExtensionItem> ScanChromeExtensions()
        {
            var results = new List<ExtensionItem>();

            // Chromeのユーザーデータパス (通常: %LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeExtPath = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Extensions");

            // Edgeの場合も考慮するならこちら（今回はChrome優先）
            // string edgeExtPath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Extensions");

            if (!Directory.Exists(chromeExtPath))
            {
                return results;
            }

            try
            {
                // 各拡張機能IDフォルダをループ
                foreach (string idDir in Directory.GetDirectories(chromeExtPath))
                {
                    // 最新のバージョンフォルダを取得（通常、IDフォルダの中にバージョン番号のフォルダがある）
                    string? versionDir = Directory.GetDirectories(idDir)
                                                 .OrderByDescending(d => Directory.GetCreationTime(d))
                                                 .FirstOrDefault();

                    if (string.IsNullOrEmpty(versionDir)) continue;

                    string manifestPath = Path.Combine(versionDir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        string name = GetExtensionNameFromManifest(manifestPath);

                        // 名前が取得できた場合のみリストに追加
                        if (!string.IsNullOrEmpty(name))
                        {
                            results.Add(new ExtensionItem
                            {
                                Name = name,
                                Path = versionDir, // バージョンフォルダがルートになります
                                IsEnabled = true,
                                Id = Path.GetFileName(idDir) // フォルダ名がID
                            });
                        }
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
        /// manifest.json を解析して拡張機能名を取得します。
        /// "_locales" による多言語対応は簡易的に処理します。
        /// </summary>
        private static string GetExtensionNameFromManifest(string manifestPath)
        {
            try
            {
                string jsonString = File.ReadAllText(manifestPath);
                var json = JsonNode.Parse(jsonString);
                string? name = json?["name"]?.GetValue<string>();
                string? defaultLocale = json?["default_locale"]?.GetValue<string>() ?? "en"; // デフォルトロケール取得

                // 名前が __MSG_ で始まる場合 (例: __MSG_appName__)
                if (!string.IsNullOrEmpty(name) && name.StartsWith("__MSG_"))
                {
                    string messageKey = name.Replace("__MSG_", "").Replace("__", "");
                    string extensionDir = Path.GetDirectoryName(manifestPath);

                    // 1. 優先: default_locale のフォルダを探す (例: _locales/en/messages.json)
                    string localePath = Path.Combine(extensionDir, "_locales", defaultLocale, "messages.json");

                    // 2. なければ 'en' や 'en_US' を探す
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
                        catch { /* 解析失敗時はフォールバック */ }
                    }

                    // 解決できなかった場合はフォルダ名を返す (Tampermonkeyのフォルダ名など)
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
        /// 選択した拡張機能をアプリの管理下（XColumn/Extensions）にコピーします。
        /// </summary>
        public static string CopyExtensionToAppFolder(string sourcePath, string extensionName)
        {
            try
            {
                // アプリの実行フォルダ/Extensions/拡張機能名
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string destRoot = Path.Combine(appDir, "Extensions");

                // ファイル名に使えない文字を除去
                string safeName = string.Join("_", extensionName.Split(Path.GetInvalidFileNameChars()));
                string destDir = Path.Combine(destRoot, safeName);

                if (Directory.Exists(destDir))
                {
                    // 既に存在する場合は一度削除するか、別名にする（今回は上書き削除）
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

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}