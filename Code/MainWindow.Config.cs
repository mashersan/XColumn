using ModernWpf;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowの設定ファイル管理に関するロジックを管理する分割クラス。
    /// アプリ全体の構成（AppConfig）と、プロファイルごとの詳細設定（AppSettings）の読み書きを行います。
    /// プロファイル設定はDPAPIを用いて暗号化されます。
    /// </summary>
    public partial class MainWindow
    {
        // DPAPI暗号化に使用するエントロピー（追加のソルトのようなもの）
        // このバイト列を知らないと、同じユーザー権限でも復号を困難にするための追加キーです。
        private static readonly byte[] _entropy = { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E };

        /// <summary>
        /// 指定されたプロファイル名の設定ファイルパス(.dat)を取得します。
        /// </summary>
        private string GetProfilePath(string profileName) => Path.Combine(_profilesFolder, $"{profileName}.dat");

        /// <summary>
        /// アプリ全体の構成ファイル（app_config.json）を読み込みます。
        /// どのプロファイルが存在し、どれが最後にアクティブだったかを管理します。
        /// </summary>
        private void LoadAppConfig()
        {
            // app_config.jsonの読み込み
            AppConfig config;
            // ファイルが存在する場合は読み込みを試みる
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
                // ファイルが存在しない場合は新規作成
                config = new AppConfig();
            }

            // プロファイルリストの初期化
            _activeProfileName = config.ActiveProfile;
            _profileNames.Clear();

            // 言語設定の読み込み
            if (!string.IsNullOrEmpty(config.Language))
            {
                _appLanguage = config.Language;
            }

            // 起動時プロファイル設定の読み込み
            if (config.StartupProfile != null)
            {
                _startupProfileSetting = config.StartupProfile;
            }

            // 重複排除してリストに追加
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
        /// アプリ全体の構成ファイル（app_config.json）を保存します。
        /// プロファイルの追加・削除や切り替え時に呼び出されます。
        /// </summary>
        private void SaveAppConfig()
        {
            // app_config.jsonの保存
            var config = new AppConfig
            {
                ActiveProfile = _activeProfileName,
                ProfileNames = _profileNames.Select(p => p.Name).ToList(),
                Language = _appLanguage,
                StartupProfile = _startupProfileSetting
            };

            try
            {
                // インデント付きで保存
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_appConfigPath, json);
            }
            catch (Exception ex) { Logger.Log($"Failed to save app config: {ex.Message}"); }
        }

        /// <summary>
        /// 指定されたプロファイルの設定（AppSettings）を現在のUI状態から収集し、
        /// 暗号化してファイルに保存します。
        /// </summary>
        /// <param name="profileName">保存対象のプロファイル名</param>
        private void SaveSettings(string profileName)
        {
            // 既存の設定を読み込み
            AppSettings settings = ReadSettingsFromFile(profileName);

            // --- カラムリストの保存とURLの安全化 ---
            var saveColumns = new List<ColumnData>();
            foreach (var col in Columns)
            {
                // 現在の col.Url が設定ページや詳細ページなどの「一時的なURL」になっている場合、
                // 次回起動時に不具合（カラム消滅など）の原因となるため、
                // 保持しておいた「直前の有効なURL (LastValidUrl)」に書き戻して保存します。

                bool isUnsafeUrl = IsAllowedDomain(col.Url, true) ||
                                   col.Url.Contains("/compose/") ||
                                   col.Url.Contains("/intent/");

                if (isUnsafeUrl)
                {
                    // LastValidUrlがあればそれを使用、なければホームにフォールバック
                    col.Url = !string.IsNullOrEmpty(col.LastValidUrl)
                              ? col.LastValidUrl
                              : "https://x.com/home";
                }
                saveColumns.Add(col);
            }

            // 現在のUI状態から設定を収集
            settings.Columns = new List<ColumnData>(Columns);
            settings.Extensions = new List<ExtensionItem>(_extensionList);
            settings.IsFocusMode = _isFocusMode;
            settings.StopTimerWhenActive = StopTimerWhenActive;

            // 表示オプション保存
            settings.HideMenuInNonHome = _hideMenuInNonHome;
            settings.HideMenuInHome = _hideMenuInHome;
            settings.HideListHeader = _hideListHeader;
            settings.HideRightSidebar = _hideRightSidebar;

            // リスト自動ナビゲーション遅延時間保存
            settings.ListAutoNavDelay = _listAutoNavDelay;

            // テーマ設定保存
            settings.AppTheme = ThemeManager.Current.ApplicationTheme switch
            {
                ApplicationTheme.Light => "Light",
                ApplicationTheme.Dark => "Dark",
                _ => "System",
            };

            // サーバーチェック間隔保存
            settings.ServerCheckIntervalMinutes = _serverCheckIntervalMinutes;

            // その他設定保存
            settings.UseSoftRefresh = _useSoftRefresh;
            settings.KeepUnreadPosition = _keepUnreadPosition;
            settings.EnableWindowSnap = _enableWindowSnap;

            // 自動シャットダウン設定保存
            settings.AutoShutdownEnabled = _autoShutdownEnabled;
            settings.AutoShutdownMinutes = _autoShutdownMinutes;

            // メディアクリック時のフォーカスモード無効化設定
            settings.DisableFocusModeOnMediaClick = _disableFocusModeOnMediaClick;
            // 設定値の保存
            settings.DisableFocusModeOnTweetClick = _disableFocusModeOnTweetClick;
            // カスタムCSSの保存
            settings.CustomCss = _customCss;
            // 自動再生設定の保存
            settings.ForceDisableAutoPlay = _forceDisableAutoPlay;
            settings.AppVolume = _appVolume;

            // カラム表示設定保存
            settings.ColumnWidth = ColumnWidth;
            settings.UseUniformGrid = UseUniformGrid;
            settings.ShowColumnUrl = ShowColumnUrl;

            // カラム表示位置設定保存
            settings.AddColumnToLeft = _addColumnToLeft;

            // フォント設定保存
            settings.AppFontFamily = _appFontFamily;
            settings.AppFontSize = _appFontSize;

            // テーマ設定をオブジェクトにセット
            settings.AppTheme = _appTheme;

            // NGワードリストを保存対象に含める
            settings.NgWords = new List<string>(_ngWords);

            // フォーカスモードのURL保存
            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            // ウィンドウ位置とサイズの保存
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

            // アップデートチェック設定を保存
            settings.CheckForUpdates = _checkForUpdates;

            SaveAppSettingsToFile(profileName, settings);
        }

        /// <summary>
        /// 指定されたAppSettingsオブジェクトを暗号化してファイルに保存します。
        /// </summary>
        private void SaveAppSettingsToFile(string profileName, AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings);
                byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), _entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(GetProfilePath(profileName), encrypted);
            }
            catch (Exception ex) { Logger.Log($"Save failed: {ex.Message}"); }
        }

        /// <summary>
        /// 指定されたプロファイルの設定ファイルを読み込み、復号してAppSettingsオブジェクトとして返します。
        /// ファイルが存在しない、または破損している場合はデフォルト設定を返します。
        /// </summary>
        private AppSettings ReadSettingsFromFile(string profileName)
        {
            // プロファイル設定ファイルの読み込みと復号
            string path = GetProfilePath(profileName);
            if (!File.Exists(path)) return new AppSettings();

            try
            {
                // ファイルを読み込み、DPAPIで復号化してデシリアライズ
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] jsonBytes = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<AppSettings>(Encoding.UTF8.GetString(jsonBytes)) ?? new AppSettings();
            }
            catch
            {
                // 復号化またはデシリアライズに失敗した場合はデフォルト設定を返す
                return new AppSettings();
            }
        }

        /// <summary>
        /// 読み込んだ設定オブジェクト(AppSettings)の内容を、実際のウィンドウやフィールド変数に適用します。
        /// </summary>
        private void ApplySettingsToWindow(AppSettings settings)
        {
            // ウィンドウ位置・サイズの適用
            Top = settings.WindowTop;
            Left = settings.WindowLeft;
            Height = settings.WindowHeight;
            Width = settings.WindowWidth;
            WindowState = settings.WindowState;

            // 動作設定
            StopTimerWhenActive = settings.StopTimerWhenActive;

            // 表示オプションの適用
            _hideMenuInNonHome = settings.HideMenuInNonHome;
            _hideMenuInHome = settings.HideMenuInHome;
            _hideListHeader = settings.HideListHeader;
            _hideRightSidebar = settings.HideRightSidebar;
            _keepUnreadPosition = settings.KeepUnreadPosition;

            _useSoftRefresh = settings.UseSoftRefresh;
            _enableWindowSnap = settings.EnableWindowSnap;

            // 自動シャットダウン設定の適用
            _autoShutdownEnabled = settings.AutoShutdownEnabled;
            _autoShutdownMinutes = settings.AutoShutdownMinutes;

            // メディアクリック時のフォーカスモード無効化設定
            _disableFocusModeOnMediaClick = settings.DisableFocusModeOnMediaClick;
            // 設定値の適用
            _disableFocusModeOnTweetClick = settings.DisableFocusModeOnTweetClick;
            // カスタムCSSの適用
            _customCss = settings.CustomCss;

            // 自動再生設定の適用
            _forceDisableAutoPlay = settings.ForceDisableAutoPlay;

            // 音量設定の適用
            _appVolume = settings.AppVolume;
            VolumeSlider.Value = _appVolume * 100.0;

            // カラム表示設定の適用
            ColumnWidth = settings.ColumnWidth > 0 ? settings.ColumnWidth : 380;
            UseUniformGrid = settings.UseUniformGrid;
            ShowColumnUrl = settings.ShowColumnUrl;

            // アップデートチェック設定の適用
            _checkForUpdates = settings.CheckForUpdates;

            // カラム表示位置設定の適用
            _addColumnToLeft = settings.AddColumnToLeft;

            // リスト自動ナビゲーション遅延時間の適用
            _listAutoNavDelay = settings.ListAutoNavDelay > 0 ? settings.ListAutoNavDelay : 2000; // 追加

            // フォント設定の適用（未設定ならデフォルト値をセット）
            _appFontFamily = string.IsNullOrEmpty(settings.AppFontFamily) ? "Meiryo" : settings.AppFontFamily;
            _appFontSize = settings.AppFontSize > 0 ? settings.AppFontSize : 15;

            // サーバーチェック間隔の適用
            _serverCheckIntervalMinutes = settings.ServerCheckIntervalMinutes > 0 ? settings.ServerCheckIntervalMinutes : 5;

            // 正しいメソッド名で呼び出し、値を渡す
            UpdateStatusCheckTimer(_serverCheckIntervalMinutes);

            // 起動時に読み込んだNGワードをメモリ上の変数にセット
            _ngWords = settings.NgWords != null ? new List<string>(settings.NgWords) : new List<string>();

            // 拡張機能リストの適用
            _extensionList.Clear();
            if (settings.Extensions != null)
            {
                _extensionList.AddRange(settings.Extensions);
            }

            // テーマ設定の読み込み
            _appTheme = string.IsNullOrEmpty(settings.AppTheme) ? "System" : settings.AppTheme;
            // 読み込んだ直後にテーマを適用
            ApplyTheme(_appTheme);

            // ウィンドウ位置が画面外になっていないか確認
            ValidateWindowPosition();
        }

        /// <summary>
        /// ModernWpfのThemeManagerを使用してテーマを切り替えます。
        /// </summary>
        /// <param name="themeSetting">"Light", "Dark", "System"</param>
        private void ApplyTheme(string themeSetting)
        {
            try
            {
                var tm = ThemeManager.Current;
                if (themeSetting == "Light")
                {
                    tm.ApplicationTheme = ApplicationTheme.Light;
                }
                else if (themeSetting == "Dark")
                {
                    tm.ApplicationTheme = ApplicationTheme.Dark;
                }
                else
                {
                    // System or Default
                    tm.ApplicationTheme = null; // nullにするとシステム設定に従う
                }

                // Windows 11のような角丸ウィンドウにする場合などは以下も検討
                // WindowHelper.SetUseModernWindowStyle(this, true);
            }
            catch (Exception ex)
            {
                Logger.Log($"Theme apply error: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウ位置が現在の画面領域内に収まっているか確認し、
        /// 画面外（見えない位置）にある場合はメインディスプレイ内に戻します。
        /// マルチディスプレイ環境でのディスプレイ構成変更時などに有効です。
        /// </summary>
        private void ValidateWindowPosition()
        {
            // すべてのスクリーンの作業領域を取得
            var screens = System.Windows.Forms.Screen.AllScreens;
            var rect = new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height);

            // ウィンドウがいずれかのスクリーンの作業領域と交差しているか確認
            bool onScreen = false;
            foreach (var screen in screens)
            {
                if (screen.WorkingArea.IntersectsWith(rect))
                {
                    onScreen = true;
                    break;
                }
            }

            // 画面外にある場合はメインスクリーン内に移動
            if (!onScreen)
            {
                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                if (primary != null)
                {
                    Left = primary.WorkingArea.Left + 100;
                    Top = primary.WorkingArea.Top + 100;
                    // サイズが大きすぎる場合は調整
                    if (Width > primary.WorkingArea.Width) Width = primary.WorkingArea.Width;
                    if (Height > primary.WorkingArea.Height) Height = primary.WorkingArea.Height;
                }
                else
                {
                    // 念のためのフォールバック
                    Left = 100; Top = 100;
                }
            }
        }
    }
}