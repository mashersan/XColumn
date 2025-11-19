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
        private static readonly byte[] _entropy = { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E };

        private string GetProfilePath(string profileName) => Path.Combine(_profilesFolder, $"{profileName}.dat");

        /// <summary>
        /// アプリ全体の構成（プロファイル一覧）を読み込みます。
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

            foreach (var name in config.ProfileNames.Distinct())
            {
                _profileNames.Add(new ProfileItem
                {
                    Name = name,
                    IsActive = (name == _activeProfileName)
                });
            }
        }

        /// <summary>
        /// アプリ全体の構成を保存します。
        /// </summary>
        private void SaveAppConfig()
        {
            var config = new AppConfig
            {
                ActiveProfile = _activeProfileName,
                ProfileNames = _profileNames.Select(p => p.Name).ToList()
            };

            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_appConfigPath, json);
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to save app config: {ex.Message}"); }
        }

        /// <summary>
        /// 指定プロファイルの設定を暗号化して保存します。
        /// </summary>
        private void SaveSettings(string profileName)
        {
            AppSettings settings = ReadSettingsFromFile(profileName);

            settings.Columns = new List<ColumnData>(Columns);
            settings.Extensions = new List<ExtensionItem>(_extensionList);
            settings.IsFocusMode = _isFocusMode;
            settings.StopTimerWhenActive = StopTimerWhenActive;

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

        /// <summary>
        /// プロファイル設定ファイルを復号して読み込みます。
        /// </summary>
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

        /// <summary>
        /// 読み込んだ設定をウィンドウや変数に適用します。
        /// </summary>
        private void ApplySettingsToWindow(AppSettings settings)
        {
            Top = settings.WindowTop;
            Left = settings.WindowLeft;
            Height = settings.WindowHeight;
            Width = settings.WindowWidth;
            WindowState = settings.WindowState;
            StopTimerWhenActive = settings.StopTimerWhenActive;

            _extensionList.Clear();
            if (settings.Extensions != null)
            {
                _extensionList.AddRange(settings.Extensions);
            }

            ValidateWindowPosition();
        }

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