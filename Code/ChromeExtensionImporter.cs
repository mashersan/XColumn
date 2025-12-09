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
        private static string GetExtensionNameFromManifest(string path)
        {
            try
            {
                string jsonString = File.ReadAllText(path);
                var json = JsonNode.Parse(jsonString);
                string? name = json?["name"]?.GetValue<string>();

                // __MSG_appName__ のような多言語キーの場合は、default_localeから英語名などを取得する処理が必要ですが、
                // 簡易的にmanifest.jsonがあるフォルダのディレクトリ名をフォールバックとして使ったり、
                // messages.jsonを探す処理を入れるのが一般的です。
                // ここでは簡易的に "__MSG_" で始まる場合はフォルダIDやデフォルト名を返します。

                if (!string.IsNullOrEmpty(name) && name.StartsWith("__MSG_"))
                {
                    // 簡易対応: 本来は _locales/en/messages.json 等を見る必要がある
                    // ここではユーザーが識別しやすいように「Unknown Name (ID)」とするか
                    // 可能ならフォルダ名を返す
                    return "Chrome Extension (" + Path.GetFileName(Path.GetDirectoryName(path)) + ")";
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