using Microsoft.Extensions.DependencyInjection;
using ModernWpf;
using System.Windows;
using XColumn.Helpers;
using XColumn.Models;
using XColumn.Scripts;
using XColumn.Services;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindow の設定ファイル管理に関するロジックを管理する分割クラス。
    /// アプリ全体の構成（AppConfig）と、プロファイルごとの詳細設定（AppSettings）の橋渡しを行います。
    /// 実際のファイルI/Oと暗号化は ISettingsService に委譲しています。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Fields =====

        /// <summary>
        /// 設定の永続化（ファイルI/O・暗号化）を担うサービス（DIコンテナから取得）。
        /// </summary>
        private readonly ISettingsService _settingsService = App.Current.Services.GetRequiredService<ISettingsService>();

        // PiPウィンドウのサイズ・位置保持用（設定への保存/復元対象）
        private double _pipWindowTop = 0.0;
        private double _pipWindowLeft = 0.0;
        private double _pipWindowHeight = 225;
        private double _pipWindowWidth = 400;
        private bool _pipAlwaysOnTop = true;

        // ===== AppConfig (アプリ全体構成) =====

        /// <summary>
        /// 指定されたプロファイル名の設定ファイルパス(.dat)を取得します。
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        /// <returns>設定ファイルのフルパス。</returns>
        private string GetProfilePath(string profileName) => _settingsService.GetProfilePath(profileName);

        /// <summary>
        /// アプリ全体の構成ファイル（app_config.json）を読み込み、UI状態（プロファイル一覧・言語等）へ反映します。
        /// </summary>
        private void LoadAppConfig()
        {
            // ファイル読み込み・パースはサービスに委譲
            AppConfig config = _settingsService.LoadAppConfig();

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

            // 重複排除してプロファイル一覧へ追加（アクティブなものにフラグを立てる）
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
        /// 現在のUI状態からアプリ全体の構成（app_config.json）を組み立てて保存します。
        /// プロファイルの追加・削除や切り替え時に呼び出されます。
        /// </summary>
        private void SaveAppConfig()
        {
            var config = new AppConfig
            {
                ActiveProfile = _activeProfileName,
                ProfileNames = _profileNames.Select(p => p.Name).ToList(),
                Language = _appLanguage,
                StartupProfile = _startupProfileSetting
            };

            // 実際の書き込みはサービスに委譲
            _settingsService.SaveAppConfig(config);
        }

        // ===== AppSettings (プロファイル詳細設定) =====

        /// <summary>
        /// 指定されたプロファイルの設定ファイルを読み込み、復号して AppSettings として返します。
        /// （実処理は ISettingsService に委譲）
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        private AppSettings ReadSettingsFromFile(string profileName)
            => _settingsService.ReadSettings(profileName);

        /// <summary>
        /// 指定された AppSettings を暗号化してファイルに保存します。
        /// （実処理は ISettingsService に委譲）
        /// </summary>
        /// <param name="profileName">プロファイル名。</param>
        /// <param name="settings">保存する設定。</param>
        private void SaveAppSettingsToFile(string profileName, AppSettings settings)
            => _settingsService.SaveSettings(profileName, settings);

        /// <summary>
        /// 指定されたプロファイルの設定（AppSettings）を現在のUI状態から収集し、暗号化して保存します。
        /// </summary>
        /// <param name="profileName">保存対象のプロファイル名。</param>
        private void SaveSettings(string profileName)
        {
            // 既存の設定を読み込み（保存対象でないフィールドを維持するためベースとして使用）
            AppSettings settings = ReadSettingsFromFile(profileName);

            // --- カラムURLの安全化（保存固有の副作用） ---
            // 現在の col.Url が設定ページ/詳細ページ/作成画面などの「一時的なURL」になっている場合、
            // 次回起動時の不具合（カラム消滅など）を避けるため、直前の有効なURL(LastValidUrl)へ書き戻す。
            // ※ Columns の各要素(参照)を直接書き換えるため、後段の収集処理にも反映される。
            foreach (var col in Columns)
            {
                if (col.IsExternalSite) continue;   // ← 追加：外部サイトはURL書き換え対象外

                bool isUnsafeUrl = IsAllowedDomain(col.Url, true) ||
                                   col.Url.Contains("/compose/") ||
                                   col.Url.Contains("/intent/");

            if (isUnsafeUrl)
                {
                    // LastValidUrl があればそれを使用、なければホームへフォールバック
                    col.Url = !string.IsNullOrEmpty(col.LastValidUrl)
                              ? col.LastValidUrl
                              : "https://x.com/home";
                }
            }

            // --- 現在のUI状態から各設定値を収集（保存・エクスポート共通） ---
            CollectCurrentSettings(settings);

            // --- 以降は「保存時のみ」必要な項目 ---

            // フォーカスモード中ならその表示URLを保存、そうでなければクリア
            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.IsFocusMode = true;
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            // ウィンドウ位置とサイズの保存。
            // 最大化中は RestoreBounds（最大化前の通常時の矩形）を保存し、復元時に正しいサイズへ戻せるようにする。
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

            SaveAppSettingsToFile(profileName, settings);
        }

        /// <summary>
        /// 現在のUI状態（各フィールド・依存関係プロパティ）を、渡された AppSettings へ書き込みます。
        /// 設定保存(SaveSettings)とデバッグ情報エクスポート(ExportDebugInfo_Click)で共通利用します。
        /// ウィンドウ位置・FocusUrl・カラムURL安全化など「保存時のみ必要な処理」はここには含めません。
        /// </summary>
        /// <param name="settings">収集結果を書き込む対象。</param>
        private void CollectCurrentSettings(AppSettings settings)
        {
            // カラム構成・拡張機能
            settings.Columns = new List<ColumnData>(Columns);
            settings.Extensions = new List<ExtensionItem>(_extensionList);

            // 動作設定
            settings.StopTimerWhenActive = StopTimerWhenActive;
            settings.UseSoftRefresh = _useSoftRefresh;
            settings.UseRefreshJitter = _useRefreshJitter;
            settings.IgnoreRateLimit429 = _ignoreRateLimit429;
            settings.ShowRateLimit429Screen = _showRateLimit429Screen;
            settings.KeepUnreadPosition = _keepUnreadPosition;
            settings.ShowRateLimitRemaining = _showRateLimitRemaining;
            settings.EnableWindowSnap = _enableWindowSnap;
            settings.ScrollTopTolerance = _scrollTopTolerance;

            // 表示オプション
            settings.HideMenuInNonHome = _hideMenuInNonHome;
            settings.HideMenuInHome = _hideMenuInHome;
            settings.HideListHeader = _hideListHeader;
            settings.HideRightSidebar = _hideRightSidebar;

            // リスト自動ナビゲーション遅延時間
            settings.ListAutoNavDelay = _listAutoNavDelay;

            // サーバーチェック間隔
            settings.ServerCheckIntervalMinutes = _serverCheckIntervalMinutes;

            // 試験的機能
            settings.UseExperimentalFeatures = _useExperimentalFeatures;
            settings.UseTwoTierLayout = _useTwoTierLayout;
            settings.AutoPipForVideo = _autoPipForVideo;
            settings.ExternalLinkOpenMode = _externalLinkOpenMode;
            settings.AllowExternalSites = _allowExternalSites;
            settings.DisablePopupBlocking = _disablePopupBlocking;

            // PiPウィンドウのサイズ・位置・最前面設定
            settings.PipWindowTop = _pipWindowTop;
            settings.PipWindowLeft = _pipWindowLeft;
            settings.PipWindowHeight = _pipWindowHeight;
            settings.PipWindowWidth = _pipWindowWidth;
            settings.PipAlwaysOnTop = _pipAlwaysOnTop;

            // 自動シャットダウン設定
            settings.AutoShutdownEnabled = _autoShutdownEnabled;
            settings.AutoShutdownMinutes = _autoShutdownMinutes;

            // フォーカスモード遷移の無効化設定
            settings.DisableFocusModeOnMediaClick = _disableFocusModeOnMediaClick;
            settings.DisableFocusModeOnTweetClick = _disableFocusModeOnTweetClick;

            // カスタマイズ系
            settings.CustomCss = _customCss;
            settings.ForceDisableAutoPlay = _forceDisableAutoPlay;
            settings.AppVolume = _appVolume;

            // カラム表示設定
            settings.ColumnWidth = ColumnWidth;
            settings.UseUniformGrid = UseUniformGrid;
            settings.ShowColumnUrl = ShowColumnUrl;
            settings.AddColumnToLeft = _addColumnToLeft;

            // フォント設定
            settings.AppFontFamily = _appFontFamily;
            settings.AppFontSize = _appFontSize;

            // テーマ設定（アプリが保持する現在のテーマ値）
            settings.AppTheme = _appTheme;

            // NGワードリスト
            settings.NgWords = new List<string>(_ngWords);

            // 絶対時間表示設定
            settings.ShowAbsoluteTime = _showAbsoluteTime;

            // アップデートチェック設定
            settings.CheckForUpdates = _checkForUpdates;
        }

        /// <summary>
        /// 読み込んだ設定オブジェクト(AppSettings)を起動時にウィンドウへ適用します。
        /// 起動時固有の処理（ウィンドウ位置・音量・拡張機能・PiP位置・位置補正）のみを担い、
        /// フィールド代入やカラム/WebViewへの反映は ApplyLiveSettings に一元化しています。
        /// </summary>
        /// <param name="settings">適用する設定。</param>
        private void ApplySettingsToWindow(AppSettings settings)
        {
            // ウィンドウ位置・サイズ・状態（起動時固有）
            Top = settings.WindowTop;
            Left = settings.WindowLeft;
            Height = settings.WindowHeight;
            Width = settings.WindowWidth;
            WindowState = settings.WindowState;

            // 音量（設定ウィンドウには無い項目。起動時固有・スライダーへも反映）
            _appVolume = settings.AppVolume;
            VolumeSlider.Value = _appVolume * 100.0;

            // 拡張機能リスト（起動時固有）
            _extensionList.Clear();
            if (settings.Extensions != null)
            {
                _extensionList.AddRange(settings.Extensions);
            }

            // PiPウィンドウのサイズ・位置（起動時固有・未設定時は既定値）
            _pipWindowTop = settings.PipWindowTop;
            _pipWindowLeft = settings.PipWindowLeft;
            _pipWindowHeight = settings.PipWindowHeight > 0 ? settings.PipWindowHeight : 600;
            _pipWindowWidth = settings.PipWindowWidth > 0 ? settings.PipWindowWidth : 800;

            // 共通の反映（フィールド代入・テーマ・カラム/WebViewへの波及）を一元化。
            // 起動直後はカラム/FocusWebView/PiPが未生成のため、それらへの走査は空振り（無害）。
            ApplyLiveSettings(settings);

            // ウィンドウ位置が画面外なら補正（geometry確定後に実施）
            ValidateWindowPosition();

            // 起動時タイミング対策：UIが揃ったアイドル時にバインド依存プロパティを再適用
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UseTwoTierLayout = settings.UseTwoTierLayout;
                _autoPipForVideo = settings.AutoPipForVideo;
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 設定値を「既に開いているUI/カラム/WebView」へ即時反映します。
        /// ウィンドウ位置・サイズには触れません（ライブ反映で窓が動かないように）。
        /// 設定ウィンドウの変更イベントと、閉じる時の確定保存から呼ばれます。
        /// </summary>
        /// <param name="newSettings">反映する設定。</param>
        private void ApplyLiveSettings(AppSettings newSettings)
        {
            StopTimerWhenActive = newSettings.StopTimerWhenActive;
            _hideMenuInNonHome = newSettings.HideMenuInNonHome;
            _hideMenuInHome = newSettings.HideMenuInHome;
            _hideListHeader = newSettings.HideListHeader;
            _hideRightSidebar = newSettings.HideRightSidebar;

            _appFontFamily = string.IsNullOrEmpty(newSettings.AppFontFamily) ? "Meiryo" : newSettings.AppFontFamily;
            _appFontSize = newSettings.AppFontSize > 0 ? newSettings.AppFontSize : 15;

            _appTheme = string.IsNullOrEmpty(newSettings.AppTheme) ? "System" : newSettings.AppTheme;
            _useSoftRefresh = newSettings.UseSoftRefresh;
            _useRefreshJitter = newSettings.UseRefreshJitter;
            _ignoreRateLimit429 = newSettings.IgnoreRateLimit429;
            _showRateLimit429Screen = newSettings.ShowRateLimit429Screen;
            _keepUnreadPosition = newSettings.KeepUnreadPosition;
            _showRateLimitRemaining = newSettings.ShowRateLimitRemaining;
            _enableWindowSnap = newSettings.EnableWindowSnap;
            _scrollTopTolerance = newSettings.ScrollTopTolerance >= 0 ? newSettings.ScrollTopTolerance : 50;
            _customCss = newSettings.CustomCss;
            _disableFocusModeOnMediaClick = newSettings.DisableFocusModeOnMediaClick;
            _disableFocusModeOnTweetClick = newSettings.DisableFocusModeOnTweetClick;

            // 自動再生無効化（OFF化は再起動が必要。ON化はこの後の即時注入で反映）
            _forceDisableAutoPlay = newSettings.ForceDisableAutoPlay;

            _listAutoNavDelay = newSettings.ListAutoNavDelay > 0 ? newSettings.ListAutoNavDelay : 2000;
            _addColumnToLeft = newSettings.AddColumnToLeft;

            _useExperimentalFeatures = newSettings.UseExperimentalFeatures;
            _allowExternalSites = newSettings.AllowExternalSites;
            MenuAddSite.Visibility = _allowExternalSites ? Visibility.Visible : Visibility.Collapsed;

            _useTwoTierLayout = newSettings.UseTwoTierLayout;
            _disablePopupBlocking = newSettings.DisablePopupBlocking;
            _autoPipForVideo = newSettings.AutoPipForVideo;
            _externalLinkOpenMode = string.IsNullOrEmpty(newSettings.ExternalLinkOpenMode) ? "Default" : newSettings.ExternalLinkOpenMode;
            _pipAlwaysOnTop = newSettings.PipAlwaysOnTop;

            // PiP化フラグを開いている全カラム/フォーカスへ即時反映
            foreach (var col in Columns)
            {
                col.AssociatedWebView?.CoreWebView2?.ExecuteScriptAsync(
                    $"window.xColumnAutoPipForVideo = {_autoPipForVideo.ToString().ToLower()};");
            }
            if (FocusWebView?.CoreWebView2 != null)
            {
                FocusWebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.xColumnAutoPipForVideo = {_autoPipForVideo.ToString().ToLower()};");
            }

            // 既に開いているPiPウィンドウの最前面設定を即時反映
            // シャットダウン中は Application.Current が null になるため走査しない
            if (System.Windows.Application.Current != null)
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is PipWindow pipWin)
                    {
                        pipWin.Topmost = false;
                        pipWin.Topmost = _pipAlwaysOnTop;
                    }
                }
            }

            this.UseTwoTierLayout = newSettings.UseTwoTierLayout;
            MenuOtherProfileTimeline.Visibility = _useExperimentalFeatures ? Visibility.Visible : Visibility.Collapsed;

            // カラム幅は変更時のみ全カラムへ適用
            double newColumnWidth = newSettings.ColumnWidth > 0 ? newSettings.ColumnWidth : 380;
            bool isWidthChanged = Math.Abs(ColumnWidth - newColumnWidth) > 0.01;
            ColumnWidth = newColumnWidth;
            UseUniformGrid = newSettings.UseUniformGrid;
            if (!UseUniformGrid && isWidthChanged)
            {
                foreach (var col in Columns) col.Width = ColumnWidth;
            }

            ShowColumnUrl = newSettings.ShowColumnUrl;

            _autoShutdownEnabled = newSettings.AutoShutdownEnabled;
            _autoShutdownMinutes = newSettings.AutoShutdownMinutes;
            _checkForUpdates = newSettings.CheckForUpdates;

            _showAbsoluteTime = newSettings.ShowAbsoluteTime;
            ApplyAbsoluteTimeSettingsToAll();

            foreach (var col in Columns)
            {
                col.UseSoftRefresh = _useSoftRefresh;
                col.KeepUnreadPosition = _keepUnreadPosition;
                col.ShowRateLimitRemaining = _showRateLimitRemaining;
                col.UseRefreshJitter = _useRefreshJitter;

                if (_ignoreRateLimit429) col.ClearRateLimitState();

                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync($"window.xColumnScrollTolerance = {_scrollTopTolerance};");
                    if (_forceDisableAutoPlay)
                        col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                }
            }

            _serverCheckIntervalMinutes = newSettings.ServerCheckIntervalMinutes > 0 ? newSettings.ServerCheckIntervalMinutes : 5;
            _ngWords = newSettings.NgWords ?? new List<string>();

            ApplyTheme(_appTheme);
            ApplyFocusModeSettingsToAll();
            ApplyCssToAllColumns();
            ApplyNgWordsToAllColumns(_ngWords);
            UpdateStatusCheckTimer(_serverCheckIntervalMinutes);
        }

        // ===== Theme / Window Helpers =====

        /// <summary>
        /// ModernWpf の ThemeManager を使用してアプリのテーマを切り替えます。
        /// </summary>
        /// <param name="themeSetting">"Light" / "Dark" / "System" のいずれか。</param>
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
                    // System / Default: null を設定するとOSのシステム設定に追従する
                    tm.ApplicationTheme = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Theme apply error: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウ位置が現在の画面領域内に収まっているか確認し、
        /// 画面外（見えない位置）にある場合はメインディスプレイ内へ戻します。
        /// マルチディスプレイ構成の変更時などに有効です。
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

            if (onScreen) return;

            // 画面外にある場合はメインスクリーン内へ移動
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                Left = primary.WorkingArea.Left + 100;
                Top = primary.WorkingArea.Top + 100;
                // サイズが画面より大きい場合は収まるよう調整
                if (Width > primary.WorkingArea.Width) Width = primary.WorkingArea.Width;
                if (Height > primary.WorkingArea.Height) Height = primary.WorkingArea.Height;
            }
            else
            {
                // 念のためのフォールバック
                Left = 100;
                Top = 100;
            }
        }
    }
}