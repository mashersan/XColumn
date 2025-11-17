using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    public partial class MainWindow
    {
        // DPAPI暗号化用の追加エントロピー
        private static readonly byte[] _entropy = { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E };

        private string GetProfilePath(string profileName) => Path.Combine(_profilesFolder, $"{profileName}.dat");

        /// <summary>
        /// アプリ設定（プロファイル一覧など）を読み込みます。
        /// </summary>
        private void LoadAppConfig()
        {
            AppConfig config;
            if (File.Exists(_appConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(_appConfigPath);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch { config = new AppConfig(); }
            }
            else
            {
                config = new AppConfig();
            }

            _activeProfileName = config.ActiveProfile;
            _profileNames.Clear();
            foreach (var name in config.ProfileNames.Distinct()) _profileNames.Add(name);
        }

        /// <summary>
        /// アプリ設定を保存します。
        /// </summary>
        private void SaveAppConfig()
        {
            var config = new AppConfig { ActiveProfile = _activeProfileName, ProfileNames = _profileNames.ToList() };
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_appConfigPath, json);
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to save app config: {ex.Message}"); }
        }

        /// <summary>
        /// 指定したプロファイルの設定を保存します。
        /// </summary>
        private void SaveSettings(string profileName)
        {
            AppSettings settings = ReadSettingsFromFile(profileName); // 既存値を保持するため読込

            // UI状態を反映
            settings.Columns = new List<ColumnData>(Columns);
            settings.IsFocusMode = _isFocusMode;
            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            if (WindowState == WindowState.Maximized)
            {
                settings.WindowState = WindowState.Maximized;
                settings.WindowTop = RestoreBounds.Top;
                settings.WindowLeft = RestoreBounds.Left;
                settings.WindowHeight = RestoreBounds.Height;
                settings.WindowWidth = RestoreBounds.Width;
            }
            else
            {
                settings.WindowState = WindowState.Normal;
                settings.WindowTop = Top;
                settings.WindowLeft = Left;
                settings.WindowHeight = Height;
                settings.WindowWidth = Width;
            }

            try
            {
                string json = JsonSerializer.Serialize(settings);
                byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), _entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(GetProfilePath(profileName), encrypted);
            }
            catch (Exception ex) { Debug.WriteLine($"Save failed: {ex.Message}"); }
        }

        private AppSettings ReadSettingsFromFile(string profileName)
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
                return new AppSettings();
            }
        }

        private void ApplySettingsToWindow(AppSettings settings)
        {
            Top = settings.WindowTop;
            Left = settings.WindowLeft;
            Height = settings.WindowHeight;
            Width = settings.WindowWidth;
            WindowState = settings.WindowState;
            ValidateWindowPosition();
        }

        /// <summary>
        /// ウィンドウ位置が画面外にある場合の復帰処理（マルチディスプレイ対応）。
        /// </summary>
        private void ValidateWindowPosition()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var rect = new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height);

            bool onScreen = false;
            foreach (var screen in screens)
            {
                if (screen.WorkingArea.IntersectsWith(rect))
                {
                    onScreen = true;
                    break;
                }
            }

            if (!onScreen)
            {
                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                if (primary != null)
                {
                    Left = primary.WorkingArea.Left + 100;
                    Top = primary.WorkingArea.Top + 100;
                    if (Width > primary.WorkingArea.Width) Width = primary.WorkingArea.Width;
                    if (Height > primary.WorkingArea.Height) Height = primary.WorkingArea.Height;
                }
                else
                {
                    Left = 100; Top = 100;
                }
            }
        }
    }
}