using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XColumn.Helpers;
using XColumn.Models;

namespace XColumn.Services
{
    /// <summary>
    /// ISettingsService の既定実装。
    /// DPAPI(ProtectedData)でプロファイル設定を暗号化し、ファイルへ永続化します。
    /// </summary>
    public class SettingsService : ISettingsService
    {
        // ===== Constants =====

        /// <summary>
        /// DPAPI暗号化に使用するエントロピー（追加のソルトのようなもの）。
        /// 旧 MainWindow._entropy と同一の値を維持すること。
        /// これを変えると既存ユーザーの設定が復号できなくなります。
        /// </summary>
        private static readonly byte[] _entropy = { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E };

        // ===== Properties =====

        /// <inheritdoc/>
        public string UserDataFolder { get; }

        /// <inheritdoc/>
        public string ProfilesFolder { get; }

        /// <inheritdoc/>
        public string AppConfigPath { get; }

        // ===== Constructor =====

        /// <summary>
        /// 各種パスを解決し、必要なフォルダを作成します。
        /// </summary>
        public SettingsService()
        {
            UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            ProfilesFolder = Path.Combine(UserDataFolder, "Profiles");
            AppConfigPath = Path.Combine(UserDataFolder, "app_config.json");

            // 念のため必要なフォルダを確保しておく
            Directory.CreateDirectory(ProfilesFolder);
        }

        // ===== Public Methods (Path) =====

        /// <inheritdoc/>
        public string GetProfilePath(string profileName)
            => Path.Combine(ProfilesFolder, $"{profileName}.dat");

        // ===== Public Methods (App Config I/O) =====

        /// <inheritdoc/>
        public AppConfig LoadAppConfig()
        {
            if (!File.Exists(AppConfigPath)) return new AppConfig();

            try
            {
                string json = File.ReadAllText(AppConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        /// <inheritdoc/>
        public void SaveAppConfig(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AppConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save app config: {ex.Message}");
            }
        }

        // ===== Public Methods (Profile Settings I/O) =====

        /// <inheritdoc/>
        public AppSettings ReadSettings(string profileName)
        {
            string path = GetProfilePath(profileName);
            if (!File.Exists(path)) return new AppSettings();

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] jsonBytes = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<AppSettings>(Encoding.UTF8.GetString(jsonBytes)) ?? new AppSettings();
            }
            catch
            {
                // 復号またはデシリアライズに失敗した場合はデフォルト設定を返す
                return new AppSettings();
            }
        }

        /// <inheritdoc/>
        public void SaveSettings(string profileName, AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings);
                byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), _entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(GetProfilePath(profileName), encrypted);
            }
            catch (Exception ex)
            {
                Logger.Log($"Save failed: {ex.Message}");
            }
        }
    }
}