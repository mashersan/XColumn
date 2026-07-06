using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XColumn.Models;
using XColumn.Properties;
using XColumn.Services;

namespace XColumn.ViewModels
{
    /// <summary>
    /// 設定ウィンドウのビューモデル。
    /// 現在の設定を各プロパティへ反映し、OK時に編集結果を Result(AppSettings) と AppConfig へ書き戻します。
    /// ウィンドウの開閉・再起動は View へイベントで要求します。
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        #region Nested Types

        /// <summary>
        /// 起動時プロファイル選択コンボボックスの表示用アイテム。
        /// </summary>
        public class ProfileComboItem
        {
            /// <summary>表示名。</summary>
            public string Display { get; set; } = "";

            /// <summary>実際の値（プロファイル名。空文字は「前回終了時のプロファイル」）。</summary>
            public string Value { get; set; } = "";
        }

        #endregion

        #region Fields

        /// <summary>
        /// ダイアログ表示を担うサービス。
        /// </summary>
        private readonly IDialogService _dialogService;

        /// <summary>
        /// アプリ全体構成（言語・起動プロファイル設定を含む）。
        /// </summary>
        private readonly AppConfig _appConfig;

        /// <summary>
        /// app_config.json のパス。
        /// </summary>
        private readonly string _appConfigPath;

        /// <summary>
        /// 編集結果。UI で編集しないフィールド（ウィンドウ位置・カラム・拡張機能など）は
        /// コンストラクタでのディープコピー値を保持し、OK時に編集可能フィールドのみ上書きします。
        /// </summary>
        private readonly AppSettings _result;

        /// <summary>
        /// コンストラクタでの初期化中はライブ反映を抑止するフラグ。
        /// </summary>
        private bool _isInitializing = true;

        /// <summary>
        /// 再起動要否判定用：開いた時点の自動再生無効化設定。
        /// </summary>
        private bool _initialForceDisableAutoPlay;

        /// <summary>
        /// ポップアップブロッカー無効化設定の初期値（再起動要否判定用）。
        /// </summary>
        private bool _initialDisablePopupBlocking;

        /// <summary>
        /// 再起動要否判定用：開いた時点の言語設定。
        /// </summary>
        private string _initialLanguage = "ja-JP";

        #endregion

        #region Observable Properties

        /// <summary>
        /// ホームカラムでメニューを非表示にするか。
        /// </summary>
        [ObservableProperty] private bool hideMenuInHome;

        /// <summary>
        /// ホーム以外のカラムでメニューを非表示にするか。
        /// </summary>
        [ObservableProperty] private bool hideMenuInNonHome;

        /// <summary>
        /// リストヘッダーを非表示にするか。
        /// </summary>
        [ObservableProperty] private bool hideListHeader;

        /// <summary>
        /// 右サイドバーを非表示にするか。
        /// </summary>
        [ObservableProperty] private bool hideRightSidebar;

        /// <summary>
        /// 投稿時刻を絶対時間で表示するか。
        /// </summary>
        [ObservableProperty] private bool showAbsoluteTime;

        /// <summary>
        /// 言語設定（カルチャ識別子。"ja-JP" / "en-US"）。
        /// </summary>
        [ObservableProperty] private string selectedLanguage = "ja-JP";

        /// <summary>
        /// テーマ設定（"System" / "Light" / "Dark"）。
        /// </summary>
        [ObservableProperty] private string selectedTheme = "System";

        /// <summary>
        /// アプリ全体のフォントファミリ名。
        /// </summary>
        [ObservableProperty] private string fontFamily = "";

        /// <summary>
        /// アプリ全体のフォントサイズ（px、テキスト入力）。
        /// </summary>
        [ObservableProperty] private string fontSizeText = "15";

        /// <summary>
        /// ウィンドウ幅に合わせてカラムを等分割するか。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsColumnWidthSliderEnabled))]
        private bool useUniformGrid;

        /// <summary>
        /// カラム上部にURLを表示するか。
        /// </summary>
        [ObservableProperty] private bool showColumnUrl;

        /// <summary>
        /// 各カラムの基本幅（固定幅モード時）。
        /// </summary>
        [ObservableProperty] private double columnWidth = 380;

        /// <summary>
        /// カラム追加時に左端へ追加するか。
        /// </summary>
        [ObservableProperty] private bool addColumnToLeft;

        /// <summary>
        /// ソフト更新（JSによる更新）を使用するか。
        /// </summary>
        [ObservableProperty] private bool useSoftRefresh = true;

        /// <summary>
        /// 自動更新タイミングを分散するか。
        /// </summary>
        [ObservableProperty] private bool useRefreshJitter = false;

        /// <summary>
        /// API制限(429)検知時に自動休止しないか。
        /// </summary>
        [ObservableProperty] private bool ignoreRateLimit429;

        /// <summary>
        /// 429検知時に警告画面を表示するか。
        /// </summary>
        [ObservableProperty] private bool showRateLimit429Screen;

        /// <summary>
        /// 未読位置を保持するか。
        /// </summary>
        [ObservableProperty] private bool keepUnreadPosition;

        /// <summary>
        /// API制限(429)の残り回数を表示するか。
        /// </summary>
        [ObservableProperty] private bool showRateLimitRemaining;

        /// <summary>
        /// タイムライン最上部判定のスクロール許容誤差（px、テキスト入力）。
        /// </summary>
        [ObservableProperty] private string scrollTopToleranceText = "50";

        /// <summary>
        /// ウィンドウスナップを有効にするか。
        /// </summary>
        [ObservableProperty] private bool enableWindowSnap = true;

        /// <summary>
        /// 非アクティブ時の自動シャットダウンを有効にするか。
        /// </summary>
        [ObservableProperty] private bool autoShutdownEnabled;

        /// <summary>
        /// 自動シャットダウンまでの待機時間（分、テキスト入力）。
        /// </summary>
        [ObservableProperty] private string autoShutdownMinutesText = "30";

        /// <summary>
        /// メディアクリック時にフォーカスモードへ遷移しないか。
        /// </summary>
        [ObservableProperty] private bool disableFocusModeOnMediaClick;

        /// <summary>
        /// ポストクリック時にフォーカスモードへ遷移しないか。
        /// </summary>
        [ObservableProperty] private bool disableFocusModeOnTweetClick;

        /// <summary>
        /// 動画の自動再生を強制無効化するか。
        /// </summary>
        [ObservableProperty] private bool forceDisableAutoPlay;

        /// <summary>
        /// 起動時に開くプロファイルの選択。
        /// </summary>
        [ObservableProperty] private ProfileComboItem? selectedStartupProfile;

        /// <summary>
        /// アップデートを確認するか。
        /// </summary>
        [ObservableProperty] private bool checkForUpdates = true;

        /// <summary>
        /// サーバー監視間隔（分の文字列。コンボの Tag 値）。
        /// </summary>
        [ObservableProperty] private string selectedServerInterval = "5";

        /// <summary>
        /// リスト自動遷移の待機時間（ミリ秒、テキスト入力）。
        /// </summary>
        [ObservableProperty] private string listAutoNavDelayText = "2000";

        /// <summary>
        /// 試験的機能を有効にするか。
        /// </summary>
        [ObservableProperty] private bool useExperimentalFeatures;

        /// <summary>
        /// 【試験的】X以外のサイトをカラム登録できるようにするか。
        /// </summary>
        [ObservableProperty] private bool allowExternalSites;

        /// <summary>
        /// 【試験的】ポップアップブロッカーを無効化するか（要再起動）。
        /// </summary>
        [ObservableProperty] private bool disablePopupBlocking;

        /// <summary>
        /// 2段レイアウトを使用するか（試験的）。
        /// </summary>
        [ObservableProperty] private bool useTwoTierLayout;

        /// <summary>
        /// 動画コンテンツの自動PiP化を有効にするか（試験的）。
        /// </summary>
        [ObservableProperty] private bool autoPipForVideo;

        /// <summary>
        /// 外部リンクを開く場所（"Default" / "Pip" / "Focus"）。
        /// </summary>
        [ObservableProperty] private string selectedExternalLinkMode = "Default";

        /// <summary>
        /// PiPを常に最前面に表示するか。
        /// </summary>
        [ObservableProperty] private bool pipAlwaysOnTop = true;

        /// <summary>
        /// ユーザー定義のカスタムCSS。
        /// </summary>
        [ObservableProperty] private string customCss = "";

        /// <summary>
        /// NGワード入力欄のテキスト。
        /// </summary>
        [ObservableProperty] private string ngWordInput = "";

        #endregion

        #region Computed Properties & Collections

        /// <summary>
        /// カラム幅スライダーが有効か（等分割が無効のときのみ有効）。
        /// </summary>
        public bool IsColumnWidthSliderEnabled => !UseUniformGrid;

        /// <summary>
        /// システムフォント一覧（ja-jp名を優先、ソート済み）。
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<string> FontList { get; }

        /// <summary>
        /// 起動時プロファイル選択肢。
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<ProfileComboItem> StartupProfiles { get; }

        /// <summary>
        /// NGワード一覧。
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<string> NgWords { get; }

        /// <summary>
        /// 編集結果の設定。OK確定後に View が読み取ります。
        /// </summary>
        public AppSettings Result => _result;

        #endregion

        #region Events

        /// <summary>
        /// ウィンドウを閉じる要求（true=OK / false=キャンセル）。
        /// </summary>
        public event Action<bool>? CloseRequested;

        /// <summary>
        /// 再起動の要求（言語または自動再生設定が変更され、ユーザーが承諾したとき）。
        /// </summary>
        public event Action? RestartRequested;

        /// <summary>
        /// いずれかの設定が変更され、即時反映が必要なときに発火します。
        /// </summary>
        public event Action? SettingsChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// 現在の設定値を各プロパティへ反映してビューモデルを初期化します。
        /// </summary>
        /// <param name="currentSettings">現在のプロファイル設定。</param>
        /// <param name="appConfig">アプリ全体構成。</param>
        /// <param name="configPath">app_config.json のパス。</param>
        /// <param name="dialogService">ダイアログ表示サービス。</param>
        public SettingsViewModel(AppSettings currentSettings, AppConfig appConfig, string configPath, IDialogService dialogService)
        {
            _dialogService = dialogService;
            if (currentSettings == null) currentSettings = new AppSettings();
            _appConfig = appConfig;
            _appConfigPath = configPath;

            // 編集しないフィールドを保持するためのディープコピー（旧 Settings = new AppSettings{...} と同一）
            _result = new AppSettings
            {
                WindowTop = currentSettings.WindowTop,
                WindowLeft = currentSettings.WindowLeft,
                WindowHeight = currentSettings.WindowHeight,
                WindowWidth = currentSettings.WindowWidth,
                WindowState = currentSettings.WindowState,
                Columns = currentSettings.Columns,
                Extensions = currentSettings.Extensions,
                IsFocusMode = currentSettings.IsFocusMode,
                FocusUrl = currentSettings.FocusUrl,
                StopTimerWhenActive = currentSettings.StopTimerWhenActive,
                SkippedVersion = currentSettings.SkippedVersion,
                ListAutoNavDelay = currentSettings.ListAutoNavDelay,
                HideMenuInHome = currentSettings.HideMenuInHome,
                HideMenuInNonHome = currentSettings.HideMenuInNonHome,
                HideListHeader = currentSettings.HideListHeader,
                HideRightSidebar = currentSettings.HideRightSidebar,
                ColumnWidth = currentSettings.ColumnWidth,
                UseUniformGrid = currentSettings.UseUniformGrid,
                ShowColumnUrl = currentSettings.ShowColumnUrl,
                AddColumnToLeft = currentSettings.AddColumnToLeft,
                AppFontFamily = currentSettings.AppFontFamily,
                AppFontSize = currentSettings.AppFontSize,
                UseSoftRefresh = currentSettings.UseSoftRefresh,
                UseRefreshJitter = currentSettings.UseRefreshJitter,
                IgnoreRateLimit429 = currentSettings.IgnoreRateLimit429,
                ShowRateLimit429Screen = currentSettings.ShowRateLimit429Screen,
                EnableWindowSnap = currentSettings.EnableWindowSnap,
                ScrollTopTolerance = currentSettings.ScrollTopTolerance,
                DisableFocusModeOnMediaClick = currentSettings.DisableFocusModeOnMediaClick,
                DisableFocusModeOnTweetClick = currentSettings.DisableFocusModeOnTweetClick,
                AppVolume = currentSettings.AppVolume,
                CustomCss = currentSettings.CustomCss,
                ServerCheckIntervalMinutes = currentSettings.ServerCheckIntervalMinutes,
                KeepUnreadPosition = currentSettings.KeepUnreadPosition,
                ShowRateLimitRemaining = currentSettings.ShowRateLimitRemaining,
                AutoShutdownEnabled = currentSettings.AutoShutdownEnabled,
                AutoShutdownMinutes = currentSettings.AutoShutdownMinutes,
                ForceDisableAutoPlay = currentSettings.ForceDisableAutoPlay,
                CheckForUpdates = currentSettings.CheckForUpdates,
                NgWords = currentSettings.NgWords != null ? new List<string>(currentSettings.NgWords) : new List<string>(),
                AppTheme = currentSettings.AppTheme,
                ShowAbsoluteTime = currentSettings.ShowAbsoluteTime,
                UseExperimentalFeatures = currentSettings.UseExperimentalFeatures,
                AllowExternalSites = currentSettings.AllowExternalSites,
                DisablePopupBlocking = currentSettings.DisablePopupBlocking,
                UseTwoTierLayout = currentSettings.UseTwoTierLayout,
                AutoPipForVideo = currentSettings.AutoPipForVideo,
                PipAlwaysOnTop = currentSettings.PipAlwaysOnTop,
                ExternalLinkOpenMode = currentSettings.ExternalLinkOpenMode
            };

            // 再起動要否判定用の初期値スナップショット
            _initialForceDisableAutoPlay = _result.ForceDisableAutoPlay;
            _initialDisablePopupBlocking = _result.DisablePopupBlocking;
            _initialLanguage = _appConfig.Language == "en-US" ? "en-US" : "ja-JP";

            // 言語
            SelectedLanguage = _appConfig.Language == "en-US" ? "en-US" : "ja-JP";

            // 起動時プロファイル
            StartupProfiles = new System.Collections.ObjectModel.ObservableCollection<ProfileComboItem>
            {
                new ProfileComboItem { Display = Resources.Settings_StartupProfileLastUsed, Value = "" }
            };
            foreach (var name in _appConfig.ProfileNames)
            {
                StartupProfiles.Add(new ProfileComboItem { Display = name, Value = name });
            }
            string currentStartup = _appConfig.StartupProfile ?? "";
            SelectedStartupProfile = StartupProfiles.FirstOrDefault(x => x.Value == currentStartup) ?? StartupProfiles[0];

            // テーマ（不明値は System へ正規化＝旧ctorのindex0フォールバック相当）
            string theme = currentSettings.AppTheme;
            SelectedTheme = (theme == "Light" || theme == "Dark" || theme == "System") ? theme : "System";

            // フォント一覧（ja-jp名を優先）
            var fonts = new List<string>();
            foreach (var font in Fonts.SystemFontFamilies)
            {
                if (font.FamilyNames.TryGetValue(XmlLanguage.GetLanguage("ja-jp"), out string? jaName))
                {
                    fonts.Add(jaName);
                }
                else
                {
                    fonts.Add(font.Source);
                }
            }
            fonts.Sort();
            FontList = new System.Collections.ObjectModel.ObservableCollection<string>(fonts);

            // 各設定値の反映
            FontFamily = currentSettings.AppFontFamily;
            FontSizeText = currentSettings.AppFontSize.ToString();

            HideMenuInHome = currentSettings.HideMenuInHome;
            HideMenuInNonHome = currentSettings.HideMenuInNonHome;
            HideListHeader = currentSettings.HideListHeader;
            HideRightSidebar = currentSettings.HideRightSidebar;
            ShowAbsoluteTime = currentSettings.ShowAbsoluteTime;

            ColumnWidth = currentSettings.ColumnWidth;
            UseUniformGrid = currentSettings.UseUniformGrid;
            ShowColumnUrl = currentSettings.ShowColumnUrl;
            AddColumnToLeft = currentSettings.AddColumnToLeft;

            UseSoftRefresh = currentSettings.UseSoftRefresh;
            UseRefreshJitter = currentSettings.UseRefreshJitter;
            IgnoreRateLimit429 = currentSettings.IgnoreRateLimit429;
            ShowRateLimit429Screen = currentSettings.ShowRateLimit429Screen;
            KeepUnreadPosition = currentSettings.KeepUnreadPosition;
            ShowRateLimitRemaining = currentSettings.ShowRateLimitRemaining;
            EnableWindowSnap = currentSettings.EnableWindowSnap;
            ScrollTopToleranceText = currentSettings.ScrollTopTolerance.ToString();
            AutoShutdownEnabled = currentSettings.AutoShutdownEnabled;
            AutoShutdownMinutesText = currentSettings.AutoShutdownMinutes.ToString();
            DisableFocusModeOnMediaClick = currentSettings.DisableFocusModeOnMediaClick;
            DisableFocusModeOnTweetClick = currentSettings.DisableFocusModeOnTweetClick;
            ForceDisableAutoPlay = currentSettings.ForceDisableAutoPlay;

            CheckForUpdates = currentSettings.CheckForUpdates;
            SelectedServerInterval = MapServerInterval(currentSettings.ServerCheckIntervalMinutes);
            ListAutoNavDelayText = currentSettings.ListAutoNavDelay.ToString();
            UseExperimentalFeatures = currentSettings.UseExperimentalFeatures;
            AllowExternalSites = currentSettings.AllowExternalSites;
            DisablePopupBlocking = currentSettings.DisablePopupBlocking;
            UseTwoTierLayout = currentSettings.UseTwoTierLayout;
            AutoPipForVideo = currentSettings.AutoPipForVideo;
            PipAlwaysOnTop = currentSettings.PipAlwaysOnTop;

            string elm = currentSettings.ExternalLinkOpenMode;
            SelectedExternalLinkMode = (elm == "Pip" || elm == "Focus") ? elm : "Default";

            NgWords = new System.Collections.ObjectModel.ObservableCollection<string>(
                currentSettings.NgWords ?? new List<string>());

            CustomCss = currentSettings.CustomCss;

            // NGワードの増減も即時反映の対象にする
            NgWords.CollectionChanged += (s, e) =>
            {
                if (_isInitializing) return;
                WriteToResult();
                SettingsChanged?.Invoke();
            };

            // 初期化完了。以降のプロパティ変更は即時反映する
            _isInitializing = false;
        }

        #endregion

        #region Live Apply

        /// <summary>
        /// いずれかの設定プロパティが変化したら Result へ集約し、即時反映イベントを発火します。
        /// </summary>
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (_isInitializing) return;

            WriteToResult();
            SettingsChanged?.Invoke();
        }

        /// <summary>
        /// 現在のUI値を編集結果(_result)へ書き戻します。（旧 Ok() の代入部を集約）
        /// 言語・起動時プロファイルは再起動が必要なため、ここではなく閉じる時(Ok)に確定します。
        /// </summary>
        private void WriteToResult()
        {
            _result.HideMenuInHome = HideMenuInHome;
            _result.HideMenuInNonHome = HideMenuInNonHome;
            _result.HideListHeader = HideListHeader;
            _result.HideRightSidebar = HideRightSidebar;

            _result.AppFontFamily = (FontFamily ?? "").Trim();
            _result.AppFontSize = int.TryParse(FontSizeText, out int size) ? size : 15;

            _result.ColumnWidth = ColumnWidth;
            _result.UseUniformGrid = UseUniformGrid;
            _result.UseTwoTierLayout = UseTwoTierLayout;
            _result.ShowColumnUrl = ShowColumnUrl;
            _result.AddColumnToLeft = AddColumnToLeft;

            _result.UseSoftRefresh = UseSoftRefresh;
            _result.UseRefreshJitter = UseRefreshJitter;
            _result.IgnoreRateLimit429 = IgnoreRateLimit429;
            _result.ShowRateLimit429Screen = ShowRateLimit429Screen;
            _result.KeepUnreadPosition = KeepUnreadPosition;
            _result.ShowRateLimitRemaining = ShowRateLimitRemaining;
            _result.EnableWindowSnap = EnableWindowSnap;
            _result.ScrollTopTolerance = (int.TryParse(ScrollTopToleranceText, out int tolerance) && tolerance >= 0) ? tolerance : 50;

            _result.CheckForUpdates = CheckForUpdates;
            _result.ShowAbsoluteTime = ShowAbsoluteTime;
            _result.UseExperimentalFeatures = UseExperimentalFeatures;
            _result.AllowExternalSites = AllowExternalSites;
            _result.DisablePopupBlocking = DisablePopupBlocking;

            _result.AutoShutdownEnabled = AutoShutdownEnabled;
            _result.AutoShutdownMinutes = (int.TryParse(AutoShutdownMinutesText, out int minutes) && minutes > 0) ? minutes : 30;

            _result.DisableFocusModeOnMediaClick = DisableFocusModeOnMediaClick;
            _result.DisableFocusModeOnTweetClick = DisableFocusModeOnTweetClick;
            _result.ForceDisableAutoPlay = ForceDisableAutoPlay;

            _result.AutoPipForVideo = AutoPipForVideo;
            _result.PipAlwaysOnTop = PipAlwaysOnTop;

            _result.ExternalLinkOpenMode = SelectedExternalLinkMode;

            _result.ServerCheckIntervalMinutes = int.TryParse(SelectedServerInterval, out int interval) ? interval : 5;

            _result.NgWords = NgWords.ToList();
            _result.CustomCss = CustomCss;
            _result.AppTheme = !string.IsNullOrEmpty(SelectedTheme) ? SelectedTheme : "System";
            _result.ListAutoNavDelay = int.TryParse(ListAutoNavDelayText, out int delay) ? delay : 2000;
        }

        #endregion

        #region Commands

        /// <summary>
        /// 閉じる処理。値は変更時に即時反映済みのため、ここでは
        /// 再起動が必要な設定（言語・自動再生）の確定と確認のみ行います。
        /// </summary>
        [RelayCommand]
        private void Ok()
        {
            // 入力途中のテキスト系も確実に取り込む
            WriteToResult();

            bool appConfigChanged = false;

            // 言語（再起動が必要なため、ここで app_config.json へ確定）
            string langCode = SelectedLanguage;
            if (_appConfig.Language != langCode)
            {
                _appConfig.Language = langCode;
                appConfigChanged = true;
            }

            // 起動時プロファイル
            if (SelectedStartupProfile != null && _appConfig.StartupProfile != SelectedStartupProfile.Value)
            {
                _appConfig.StartupProfile = SelectedStartupProfile.Value;
                appConfigChanged = true;
            }

            if (appConfigChanged)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_appConfigPath, json);
                }
                catch { }
            }

            bool languageChanged = _appConfig.Language != _initialLanguage;
            bool restartRequired = _result.ForceDisableAutoPlay != _initialForceDisableAutoPlay
                || _result.DisablePopupBlocking != _initialDisablePopupBlocking;

            // 再起動確認は CloseRequested より前に行う。
            // CloseRequested でウィンドウが閉じると OnClosed が
            // RestartRequested の購読を解除するため、後から Invoke しても届かない。
            if (languageChanged || restartRequired)
            {
                if (_dialogService.ShowMessage(Resources.Msg_LanguageChanged_Restart,
                                               Resources.Settings_Title,
                                               MessageBoxButton.YesNo,
                                               MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Shutdown が走るため CloseRequested は不要
                    RestartRequested?.Invoke();
                    return;
                }
            }

            CloseRequested?.Invoke(true);
        }
        
        /*
        /// <summary>
        /// OK処理。各UIの値を Result と AppConfig に書き戻し、必要に応じて再起動を確認します。
        /// </summary>
        [RelayCommand]
        private void Ok()
        {
            bool languageChanged = false;
            bool appConfigChanged = false;
            bool restartRequired = false;

            // 自動再生設定の変更があれば再起動フラグを立てる
            bool newAutoPlaySetting = ForceDisableAutoPlay;
            if (_result.ForceDisableAutoPlay != newAutoPlaySetting)
            {
                restartRequired = true;
            }

            // 言語設定
            string langCode = SelectedLanguage;
            if (_appConfig.Language != langCode)
            {
                _appConfig.Language = langCode;
                languageChanged = true;
                appConfigChanged = true;
            }

            // 起動時プロファイル設定
            if (SelectedStartupProfile != null && _appConfig.StartupProfile != SelectedStartupProfile.Value)
            {
                _appConfig.StartupProfile = SelectedStartupProfile.Value;
                appConfigChanged = true;
            }

            // AppConfig の保存
            if (appConfigChanged)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_appConfigPath, json);
                }
                catch { }
            }

            // 編集可能フィールドを Result へ書き戻し
            _result.HideMenuInHome = HideMenuInHome;
            _result.HideMenuInNonHome = HideMenuInNonHome;
            _result.HideListHeader = HideListHeader;
            _result.HideRightSidebar = HideRightSidebar;

            _result.AppFontFamily = (FontFamily ?? "").Trim();
            _result.AppFontSize = int.TryParse(FontSizeText, out int size) ? size : 15;

            _result.ColumnWidth = ColumnWidth;
            _result.UseUniformGrid = UseUniformGrid;
            _result.UseTwoTierLayout = UseTwoTierLayout;
            _result.ShowColumnUrl = ShowColumnUrl;
            _result.AddColumnToLeft = AddColumnToLeft;

            _result.UseSoftRefresh = UseSoftRefresh;
            _result.UseRefreshJitter = UseRefreshJitter;
            _result.IgnoreRateLimit429 = IgnoreRateLimit429;
            _result.ShowRateLimit429Screen = ShowRateLimit429Screen;
            _result.KeepUnreadPosition = KeepUnreadPosition;
            _result.ShowRateLimitRemaining = ShowRateLimitRemaining;
            _result.EnableWindowSnap = EnableWindowSnap;
            _result.ScrollTopTolerance = (int.TryParse(ScrollTopToleranceText, out int tolerance) && tolerance >= 0) ? tolerance : 50;

            _result.CheckForUpdates = CheckForUpdates;
            _result.ShowAbsoluteTime = ShowAbsoluteTime;
            _result.UseExperimentalFeatures = UseExperimentalFeatures;
            _result.AllowExternalSites = AllowExternalSites;

            _result.AutoShutdownEnabled = AutoShutdownEnabled;
            _result.AutoShutdownMinutes = (int.TryParse(AutoShutdownMinutesText, out int minutes) && minutes > 0) ? minutes : 30;

            _result.DisableFocusModeOnMediaClick = DisableFocusModeOnMediaClick;
            _result.DisableFocusModeOnTweetClick = DisableFocusModeOnTweetClick;
            _result.ForceDisableAutoPlay = newAutoPlaySetting;

            _result.AutoPipForVideo = AutoPipForVideo;
            _result.PipAlwaysOnTop = PipAlwaysOnTop;

            _result.ExternalLinkOpenMode = SelectedExternalLinkMode;

            _result.ServerCheckIntervalMinutes = int.TryParse(SelectedServerInterval, out int interval) ? interval : 5;

            _result.NgWords = NgWords.ToList();
            _result.CustomCss = CustomCss;
            _result.AppTheme = !string.IsNullOrEmpty(SelectedTheme) ? SelectedTheme : "System";
            _result.ListAutoNavDelay = int.TryParse(ListAutoNavDelayText, out int delay) ? delay : 2000;

            // 先に閉じる（旧実装と同じ順序：Close → 再起動確認）
            CloseRequested?.Invoke(true);

            // 言語変更または自動再生設定変更時の再起動確認
            if (languageChanged || restartRequired)
            {
                if (_dialogService.ShowMessage(Resources.Msg_LanguageChanged_Restart,
                                               Resources.Settings_Title,
                                               MessageBoxButton.YesNo,
                                               MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    RestartRequested?.Invoke();
                }
            }
        }

        /// <summary>
        /// キャンセル処理。変更を破棄して閉じます。
        /// </summary>
        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(false);

        */

        /// <summary>
        /// NGワード追加。入力欄の語を重複なしでリストへ追加します。
        /// </summary>
        [RelayCommand]
        private void AddNgWord()
        {
            string word = (NgWordInput ?? "").Trim();
            if (!string.IsNullOrEmpty(word) && !NgWords.Contains(word))
            {
                NgWords.Add(word);
                NgWordInput = "";
            }
        }

        /// <summary>
        /// NGワード削除。選択中の語をリストから削除します。
        /// </summary>
        /// <param name="selectedItems">ListBox の選択項目（SelectedItems）。</param>
        [RelayCommand]
        private void DeleteNgWord(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null) return;

            var items = selectedItems.Cast<string>().ToList();
            foreach (var item in items)
            {
                NgWords.Remove(item);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// サーバー監視間隔（分）を、コンボの Tag に存在する値の文字列へ変換します。
        /// 該当がなければ既定の "5" を返します。
        /// </summary>
        private static string MapServerInterval(int minutes)
        {
            return minutes switch
            {
                1 or 3 or 5 or 10 or 15 or 30 or 60 => minutes.ToString(),
                _ => "5"
            };
        }

        #endregion
    }
}