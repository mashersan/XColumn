using System.IO;
using System.Text.Json;
using XColumn.Models;

namespace XColumn.Services
{
    /// <summary>
    /// IProfileService の既定実装。
    /// 設定ファイル(.dat)のパス解決は ISettingsService に委譲し、
    /// ブラウザデータフォルダの堅牢なコピー（ロック中ファイルのフォールバック付き）を提供します。
    /// </summary>
    public class ProfileService : IProfileService
    {
        // 設定ファイルのパス解決・ユーザーデータフォルダ取得を委譲する設定サービス
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// 依存する ISettingsService を受け取って初期化します。
        /// </summary>
        public ProfileService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <inheritdoc/>
        public string GetBrowserDataPath(string profileName)
            => Path.Combine(_settingsService.UserDataFolder, "BrowserData", profileName);

        /// <inheritdoc/>
        public bool SettingsFileExists(string profileName)
            => File.Exists(_settingsService.GetProfilePath(profileName));

        /// <inheritdoc/>
        public void RenameProfile(string oldName, string newName)
        {
            // 設定ファイル(.dat)のリネーム
            string oldSettingsPath = _settingsService.GetProfilePath(oldName);
            string newSettingsPath = _settingsService.GetProfilePath(newName);
            if (File.Exists(oldSettingsPath)) File.Move(oldSettingsPath, newSettingsPath);

            // ブラウザデータフォルダのリネーム（存在し、かつ移動先が未作成の場合のみ）
            string oldDataPath = GetBrowserDataPath(oldName);
            string newDataPath = GetBrowserDataPath(newName);
            if (Directory.Exists(oldDataPath) && !Directory.Exists(newDataPath))
            {
                Directory.Move(oldDataPath, newDataPath);
            }
        }

        /// <inheritdoc/>
        public void DuplicateProfileSettings(string sourceName, string destName)
        {
            string srcSettingsPath = _settingsService.GetProfilePath(sourceName);
            string destSettingsPath = _settingsService.GetProfilePath(destName);

            if (File.Exists(srcSettingsPath))
            {
                File.Copy(srcSettingsPath, destSettingsPath);
            }
            else
            {
                // コピー元が無ければ空設定を新規作成
                _settingsService.SaveSettings(destName, new AppSettings());
            }
        }

        /// <inheritdoc/>
        public void CopyBrowserData(string sourceName, string destName)
        {
            string srcDataPath = GetBrowserDataPath(sourceName);
            string destDataPath = GetBrowserDataPath(destName);
            if (Directory.Exists(srcDataPath))
            {
                CopyDirectory(srcDataPath, destDataPath);
            }
        }

        /// <inheritdoc/>
        public void DeleteProfile(string profileName)
        {
            // 設定ファイル(.dat)の削除
            string settingsPath = _settingsService.GetProfilePath(profileName);
            if (File.Exists(settingsPath)) File.Delete(settingsPath);

            // ブラウザデータフォルダの削除
            string dataPath = GetBrowserDataPath(profileName);
            if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true);
        }

        /// <inheritdoc/>
        public void SchedulePendingClone(string sourceName, string destName)
        {
            var info = new CloneInfo
            {
                SourcePath = GetBrowserDataPath(sourceName),
                DestPath = GetBrowserDataPath(destName)
            };
            string json = JsonSerializer.Serialize(info);
            File.WriteAllText(Path.Combine(_settingsService.UserDataFolder, "pending_clone.json"), json);
        }

        /// <inheritdoc/>
        public void ProcessPendingClone()
        {
            string pendingFile = Path.Combine(_settingsService.UserDataFolder, "pending_clone.json");
            if (!File.Exists(pendingFile)) return;

            try
            {
                string json = File.ReadAllText(pendingFile);
                var info = JsonSerializer.Deserialize<CloneInfo>(json);

                if (info != null && !string.IsNullOrEmpty(info.SourcePath) && !string.IsNullOrEmpty(info.DestPath))
                {
                    if (Directory.Exists(info.SourcePath))
                    {
                        // WebView2が起動していない今なら確実にコピーできる
                        CopyDirectory(info.SourcePath, info.DestPath);
                    }
                }
            }
            finally
            {
                // 処理が終わったら（成功・失敗にかかわらず）指示書を削除
                try { File.Delete(pendingFile); } catch { }
            }
        }

        /// <summary>
        /// ディレクトリを再帰的にコピーします（lockfile はスキップ）。
        /// 個々のファイルコピーは CopyFileRobust により堅牢に行います。
        /// </summary>
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                // lockfile はWebViewが使用中のロックファイルのためコピー不要
                if (file.Name.Equals("lockfile", StringComparison.OrdinalIgnoreCase)) continue;
                CopyFileRobust(file.FullName, Path.Combine(destinationDir, file.Name));
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
            }
        }

        /// <summary>
        /// ファイルのコピーを堅牢に行います。
        /// 通常の File.Copy が IOException（ロック中など）で失敗した場合は、
        /// 共有読み取りを許可した FileStream 経由でのコピーにフォールバックします。
        /// いずれも失敗した場合はそのファイルを諦めて処理を継続します。
        /// </summary>
        private void CopyFileRobust(string src, string dest)
        {
            try
            {
                File.Copy(src, dest, true);
            }
            catch (IOException)
            {
                // ロック中ファイルは共有読み取り(FileShare.ReadWrite)を許可してストリームコピーを試みる
                try
                {
                    using var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write);
                    sourceStream.CopyTo(destStream);
                }
                catch { /* フォールバックも失敗した場合は諦めて継続 */ }
            }
            catch { /* IOException 以外も諦めて継続 */ }
        }

        /// <summary>
        /// pending_clone.json の内容（コピー元・コピー先のブラウザデータパス）。
        /// </summary>
        private class CloneInfo
        {
            public string SourcePath { get; set; } = "";
            public string DestPath { get; set; } = "";
        }
    }
}