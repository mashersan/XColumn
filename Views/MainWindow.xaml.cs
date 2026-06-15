using ModernWpf.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using XColumn.Models;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using XColumn.Messages;
using XColumn.ViewModels;
using XColumn.Helpers;
using XColumn.Scripts;

// WPFとWinFormsの型名の曖昧さ回避
using Button = System.Windows.Controls.Button;
using Keyboard = System.Windows.Input.Keyboard;


namespace XColumn.Views
{
    /// <summary>
    /// アプリケーションのメインウィンドウ。
    /// 全体的な状態管理、UIイベント、設定の適用、およびウィンドウレベルの入力制御を行います。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Dependency Properties

        /// <summary>
        /// 2段レイアウトを使用するかどうか（試験的機能）の依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty UseTwoTierLayoutProperty =
            DependencyProperty.Register(nameof(UseTwoTierLayout), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        /// <summary>2段レイアウトを使用するかどうか（試験的機能）。</summary>
        public bool UseTwoTierLayout
        {
            get => (bool)GetValue(UseTwoTierLayoutProperty);
            set => SetValue(UseTwoTierLayoutProperty, value);
        }

        /// <summary>
        /// アプリがアクティブな時にカラムの自動更新タイマーを停止するかどうかの依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty StopTimerWhenActiveProperty =
            DependencyProperty.Register(nameof(StopTimerWhenActive), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true, OnStopTimerWhenActiveChanged));

        /// <summary>アプリがアクティブな時にカラムの自動更新タイマーを停止するかどうか。</summary>
        public bool StopTimerWhenActive
        {
            get => (bool)GetValue(StopTimerWhenActiveProperty);
            set => SetValue(StopTimerWhenActiveProperty, value);
        }

        /// <summary>
        /// カラム上部にURLを表示するかどうかの依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty ShowColumnUrlProperty =
            DependencyProperty.Register(nameof(ShowColumnUrl), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true));

        /// <summary>カラム上部にURLを表示するかどうか。</summary>
        public bool ShowColumnUrl
        {
            get => (bool)GetValue(ShowColumnUrlProperty);
            set => SetValue(ShowColumnUrlProperty, value);
        }

        /// <summary>
        /// 各カラムの基本幅（固定幅モード時）の依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(MainWindow),
                new PropertyMetadata(380.0));

        /// <summary>各カラムの基本幅（固定幅モード時）。</summary>
        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        /// <summary>
        /// ウィンドウ幅に合わせてカラムを等分割するかどうかの依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty UseUniformGridProperty =
            DependencyProperty.Register(nameof(UseUniformGrid), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));

        /// <summary>ウィンドウ幅に合わせてカラムを等分割するかどうか。</summary>
        public bool UseUniformGrid
        {
            get => (bool)GetValue(UseUniformGridProperty);
            set => SetValue(UseUniformGridProperty, value);
        }

        #endregion

        #region Fields

        /// <summary>現在アクティブ（選択中）のカラムデータ。</summary>
        private ColumnData? _activeColumnData;

        /// <summary>メモリ上に保持されている拡張機能リスト。</summary>
        private List<ExtensionItem> _extensionList = new List<ExtensionItem>();

        /// <summary>起動時に指定されたプロファイル名。</summary>
        private string? _startupProfileName;

        /// <summary>サーバー監視間隔（分）。</summary>
        private int _serverCheckIntervalMinutes = 5;

        /// <summary>カラム追加時に左端へ追加するかどうか。</summary>
        private bool _addColumnToLeft = false;

        // --- 設定値保持用フィールド（UI表示） ---

        /// <summary>ホーム以外のカラムでメニューを非表示にするか。</summary>
        private bool _hideMenuInNonHome = false;

        /// <summary>ホームカラムでメニューを非表示にするか。</summary>
        private bool _hideMenuInHome = false;

        /// <summary>リストヘッダーを非表示にするか。</summary>
        private bool _hideListHeader = false;

        /// <summary>右サイドバーを非表示にするか。</summary>
        private bool _hideRightSidebar = false;

        // --- 設定値保持用フィールド（動作） ---

        /// <summary>ソフト更新（JSによる更新）を使用するか。</summary>
        private bool _useSoftRefresh = true;

        /// <summary>未読位置を保持するか。</summary>
        private bool _keepUnreadPosition = false;

        /// <summary>ユーザー定義のカスタムCSS。</summary>
        private string _customCss = "";

        /// <summary>アプリ音量（0.0～1.0）。</summary>
        private double _appVolume = 0.5;

        /// <summary>非アクティブ時の自動シャットダウンを有効にするか。</summary>
        private bool _autoShutdownEnabled = false;

        /// <summary>自動シャットダウンまでの待機時間（分）。</summary>
        private int _autoShutdownMinutes = 30;

        /// <summary>最後に非アクティブ化した時刻（自動シャットダウン判定用）。</summary>
        private DateTime? _lastDeactivatedTime = null;

        /// <summary>リスト自動遷移の待機時間（ミリ秒）。</summary>
        private int _listAutoNavDelay = 2000;

        /// <summary>メディアクリック時にフォーカスモードへ遷移しないか。</summary>
        private bool _disableFocusModeOnMediaClick = false;

        /// <summary>ポスト（ツイート）クリック時にフォーカスモードへ遷移しないか。</summary>
        private bool _disableFocusModeOnTweetClick = false;

        /// <summary>動画コンテンツの自動PiP化を有効にするか（試験的機能）。</summary>
        private bool _autoPipForVideo = false;

        /// <summary>アプリ全体のフォントファミリ名。</summary>
        private string _appFontFamily = "Meiryo";

        /// <summary>アプリ全体のフォントサイズ(px)。</summary>
        private int _appFontSize = 15;

        /// <summary>テーマ設定（"Light" / "Dark" / "System"）。</summary>
        private string _appTheme = "System";

        /// <summary>NGワードリスト。</summary>
        private List<string> _ngWords = new List<string>();

        /// <summary>言語設定（カルチャ識別子）。</summary>
        private string _appLanguage = "ja-JP";

        /// <summary>起動時に強制的に開くプロファイル設定。</summary>
        private string _startupProfileSetting = "";

        /// <summary>DevToolsを有効化するか（起動引数由来）。</summary>
        private bool _enableDevTools = false;

        /// <summary>GPUを無効化するか（起動引数由来）。</summary>
        private bool _disableGpu = false;

        /// <summary>動画の自動再生を強制無効化するか。</summary>
        private bool _forceDisableAutoPlay = false;

        /// <summary>投稿時刻を絶対時間で表示するか。</summary>
        private bool _showAbsoluteTime = false;

        /// <summary>タイムライン最上部判定のスクロール許容誤差（px）。</summary>
        private int _scrollTopTolerance = 50;

        // --- 内部状態管理 ---

        /// <summary>カウントダウン更新用の1秒タイマー。</summary>
        private readonly DispatcherTimer _countdownTimer;

        /// <summary>現在フォーカスモード（単一ビュー）かどうか。</summary>
        private bool _isFocusMode = false;

        /// <summary>アプリが現在アクティブかどうか。</summary>
        private bool _isAppActive = true;

        /// <summary>フォーカスモードで表示中のカラムデータ。</summary>
        private ColumnData? _focusedColumnData = null;

        /// <summary>
        /// 再起動処理中フラグ（終了時の二重保存防止用）。
        /// </summary>
        internal bool _isRestarting = false;

        /// <summary>ユーザーデータフォルダ（%AppData%\XColumn）。</summary>
        private readonly string _userDataFolder;

        /// <summary>プロファイル設定の格納フォルダ。</summary>
        private readonly string _profilesFolder;

        /// <summary>アプリ全体構成ファイル(app_config.json)のパス。</summary>
        private readonly string _appConfigPath;

        /// <summary>試験的な機能を有効にするか。</summary>
        private bool _useExperimentalFeatures = false;

        /// <summary>2段レイアウトを使用するか（フィールド側の保持値）。</summary>
        private bool _useTwoTierLayout = false;

        #endregion

        #region Properties

        /// <summary>
        /// UIに表示されるカラムのコレクション。
        /// </summary>
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        /// <summary>
        /// MainWindow のビューモデル。
        /// </summary>
        public MainWindowViewModel ViewModel { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// メインウィンドウのコンストラクタ（プロファイル名指定なし）。
        /// </summary>
        public MainWindow() : this(null, false) { }

        /// <summary>
        /// メインウィンドウのコンストラクタ。
        /// </summary>
        /// <param name="profileName">起動時に指定されたプロファイル名。</param>
        /// <param name="enableDevTools">DevToolsを有効化するか。</param>
        /// <param name="disableGpu">GPUを無効化するか。</param>
        public MainWindow(string? profileName, bool enableDevTools = false, bool disableGpu = false)
        {
            InitializeComponent();

            // ViewModel を DI から取得し、DataContext に設定
            ViewModel = App.Current.Services.GetRequiredService<MainWindowViewModel>();
            // 注意: ItemsControl 等は既存どおり Columns / 各プロパティに ElementName バインドしているため、
            //       Window 全体の DataContext は ViewModel にしてよい（既存バインドは ElementName 指定で影響を受けない）。
            DataContext = ViewModel;

            // ViewModel からの要求(メッセージ)を購読
            RegisterMessages();

            _startupProfileName = profileName;
            _enableDevTools = enableDevTools;
            _disableGpu = disableGpu;

            // ModernWpfのモダンウィンドウスタイルを適用
            WindowHelper.SetUseModernWindowStyle(this, true);

            // ユーザーデータフォルダとプロファイルフォルダの初期化
            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _profilesFolder = Path.Combine(_userDataFolder, "Profiles");
            _appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
            Directory.CreateDirectory(_profilesFolder);

            ColumnItemsControl.ItemsSource = Columns;

            // カラムリストの変更監視（プロパティ変更検知のため）
            Columns.CollectionChanged += OnColumnsCollectionChanged;

            InitializeProfilesUI();

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            this.Closing += MainWindow_Closing;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        #endregion

        #region Setup & Messaging

        /// <summary>
        /// ViewModel から送られる要求メッセージを購読します。
        /// </summary>
        private void RegisterMessages()
        {
            var messenger = WeakReferenceMessenger.Default;

            // 「カラム追加」要求 → 既存の AddNewColumn を呼び出す
            messenger.Register<AddColumnRequest>(this, (recipient, message) =>
            {
                AddNewColumn(message.Url);
            });

            // 「リスト自動遷移カラム追加」要求 → 専用の生成処理を呼び出す
            messenger.Register<AddListAutoColumnRequest>(this, (recipient, message) =>
            {
                AddListAutoColumn();
            });

            // 「アプリ終了」要求 → Close
            messenger.Register<CloseAppRequest>(this, (recipient, message) =>
            {
                Close();
            });
        }

        #endregion

        #region Window Lifecycle Events

        /// <summary>
        /// ウィンドウロード時の初期化処理。
        /// </summary>
        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_startupProfileName))
                {
                    _activeProfileName = _startupProfileName;
                    var existing = _profileNames.FirstOrDefault(p => p.Name == _activeProfileName);
                    if (existing == null) _profileNames.Add(new ProfileItem { Name = _activeProfileName, IsActive = true });
                    else foreach (var p in _profileNames) p.IsActive = (p.Name == _activeProfileName);

                    ProfileComboBox.SelectedItem = _profileNames.FirstOrDefault(p => p.Name == _activeProfileName);
                }

                // ウィンドウタイトル更新
                UpdateWindowTitle();

                // 念のため選択状態を保証
                if (ProfileComboBox.SelectedItem == null)
                {
                    ProfileComboBox.SelectedItem = _profileNames.FirstOrDefault(p => p.Name == _activeProfileName);
                }

                AppSettings settings = ReadSettingsFromFile(_activeProfileName);
                ApplySettingsToWindow(settings);

                // WebView環境初期化
                await InitializeWebViewEnvironmentAsync();

                // カラム復元
                LoadColumnsFromSettings(settings);

                // アップデート確認
                _ = CheckForUpdatesAsync(settings.SkippedVersion);

                // 接続監視機能の初期化
                InitializeStatusChecker();
            }
            catch (Exception ex)
            {
                string msg = string.Format(Properties.Resources.Err_InitFailed, ex.Message);
                MessageWindow.Show(msg, Properties.Resources.Common_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ウィンドウハンドル初期化時の処理。スナップ機能を有効化します。
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableWindowSnap();
        }

        /// <summary>
        /// 終了時の保存処理。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            DisableWindowSnap();

            if (_isRestarting) return;

            SaveSettings(_activeProfileName);
            SaveAppConfig();

            _countdownTimer.Stop();
            foreach (var col in Columns) col.StopAndDisposeTimer();
        }

        /// <summary>
        /// アプリがアクティブになった時の処理。
        /// </summary>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            _isAppActive = true;

            // 自動シャットダウン用の時間記録をクリア
            _lastDeactivatedTime = null;

            if (StopTimerWhenActive) StopAllTimers();

            // アクティブ化されたとき、スナップしている他のウィンドウも前面に持ってくる
            BringSnappedWindowsToFront();
        }

        /// <summary>
        /// アプリが非アクティブになった時の処理。
        /// </summary>
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _isAppActive = false;

            // 自動シャットダウン用の時間を記録
            _lastDeactivatedTime = DateTime.Now;
            if (_isFocusMode) return;
            if (StopTimerWhenActive) StartAllTimers(resume: true);
        }

        #endregion

        #region Column Collection & Property Changes

        /// <summary>
        /// カラムコレクションの変更監視ハンドラ。
        /// </summary>
        private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 追加されたアイテムのイベント購読
            if (e.NewItems != null)
            {
                foreach (ColumnData item in e.NewItems)
                {
                    item.PropertyChanged += OnColumnPropertyChanged;
                }
                // カラム追加時も設定保存
                SaveSettings(_activeProfileName);
            }

            // 削除されたアイテムのイベント解除
            if (e.OldItems != null)
            {
                foreach (ColumnData item in e.OldItems)
                {
                    item.PropertyChanged -= OnColumnPropertyChanged;
                }
                // カラム削除時も設定保存
                SaveSettings(_activeProfileName);
            }
        }

        /// <summary>
        /// カラムのプロパティが変更されたときの処理。
        /// </summary>
        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 保存すべきプロパティが変更された場合に設定を保存
            if (e.PropertyName == nameof(ColumnData.RefreshIntervalSeconds) ||
                e.PropertyName == nameof(ColumnData.IsAutoRefreshEnabled) ||
                e.PropertyName == nameof(ColumnData.IsRetweetHidden) ||
                e.PropertyName == nameof(ColumnData.IsReplyHidden) ||
                e.PropertyName == nameof(ColumnData.Url) ||
                e.PropertyName == nameof(ColumnData.MediaScalePercentage))
            {
                // TextBoxはLostFocusで更新されるようになったため、ここでは即時保存で問題ない。
                SaveSettings(_activeProfileName);
            }
        }

        /// <summary>
        /// StopTimerWhenActive 設定変更時に即座にタイマー状態を更新します。
        /// </summary>
        private static void OnStopTimerWhenActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window && window._isAppActive)
            {
                bool shouldStop = (bool)e.NewValue;
                if (shouldStop) window.StopAllTimers();
                else window.StartAllTimers(resume: true);
            }
        }

        #endregion

        #region Menu & Toolbar Handlers

        /// <summary>
        /// ツールバーのボタンクリック時にドロップダウンメニューを表示する汎用ハンドラ。
        /// </summary>
        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                // ボタンの直下にメニューを表示
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// 「設定」ボタンクリック時の処理。
        /// </summary>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            // AppConfig (言語設定用) の読み込み
            AppConfig currentAppConfig = new AppConfig();
            if (File.Exists(_appConfigPath))
            {
                try
                {
                    currentAppConfig = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_appConfigPath)) ?? new AppConfig();
                }
                catch { }
            }

            // 現在の設定を読み込んで渡す
            AppSettings current = ReadSettingsFromFile(_activeProfileName);

            current.StopTimerWhenActive = StopTimerWhenActive;
            current.UseSoftRefresh = _useSoftRefresh;
            current.EnableWindowSnap = _enableWindowSnap;
            current.KeepUnreadPosition = _keepUnreadPosition;
            current.ScrollTopTolerance = _scrollTopTolerance;
            current.CustomCss = _customCss;
            current.AppVolume = _appVolume;
            current.DisableFocusModeOnMediaClick = _disableFocusModeOnMediaClick;
            current.DisableFocusModeOnTweetClick = _disableFocusModeOnTweetClick;

            current.AddColumnToLeft = _addColumnToLeft;

            current.ColumnWidth = ColumnWidth;
            current.UseUniformGrid = UseUniformGrid;
            current.HideRightSidebar = _hideRightSidebar;

            current.ShowColumnUrl = ShowColumnUrl;

            current.AppFontFamily = _appFontFamily;
            current.AppFontSize = _appFontSize;

            current.AppTheme = _appTheme;

            current.ServerCheckIntervalMinutes = _serverCheckIntervalMinutes;

            current.AutoShutdownEnabled = _autoShutdownEnabled;
            current.AutoShutdownMinutes = _autoShutdownMinutes;

            current.CheckForUpdates = _checkForUpdates;

            // リスト自動遷移待機時間
            current.ListAutoNavDelay = _listAutoNavDelay;

            // NGワードをセット
            current.NgWords = new List<string>(_ngWords);

            // 試験的な機能のフラグ
            current.UseExperimentalFeatures = _useExperimentalFeatures;

            // 2段レイアウトの設定を反映
            current.UseTwoTierLayout = _useTwoTierLayout;

            // PiP化設定
            current.AutoPipForVideo = _autoPipForVideo;

            // PiPを常に最前面に表示するかどうかの設定
            current.PipAlwaysOnTop = _pipAlwaysOnTop;

            var dlg = new SettingsWindow(current, currentAppConfig, _appConfigPath) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // 設定を反映
                AppSettings newSettings = dlg.Settings;

                // 言語設定が変更されたかどうかはSettingsWindow内で処理・保存済み
                // Main側に反映（次回起動時のロード用にメモリ更新）
                if (currentAppConfig.Language != null)
                {
                    _appLanguage = currentAppConfig.Language;
                }

                // 起動時プロファイル設定をMain側にも反映して保持する
                if (currentAppConfig.StartupProfile != null)
                {
                    _startupProfileSetting = currentAppConfig.StartupProfile;
                }

                StopTimerWhenActive = newSettings.StopTimerWhenActive;
                _hideMenuInNonHome = newSettings.HideMenuInNonHome;
                _hideMenuInHome = newSettings.HideMenuInHome;
                _hideListHeader = newSettings.HideListHeader;
                _hideRightSidebar = newSettings.HideRightSidebar;

                _appFontFamily = newSettings.AppFontFamily;
                _appFontSize = newSettings.AppFontSize;

                _appTheme = newSettings.AppTheme;
                _useSoftRefresh = newSettings.UseSoftRefresh;
                _keepUnreadPosition = newSettings.KeepUnreadPosition;
                _enableWindowSnap = newSettings.EnableWindowSnap;
                _scrollTopTolerance = newSettings.ScrollTopTolerance;
                _customCss = newSettings.CustomCss;
                _disableFocusModeOnMediaClick = newSettings.DisableFocusModeOnMediaClick;
                _disableFocusModeOnTweetClick = newSettings.DisableFocusModeOnTweetClick;

                // 自動再生無効化設定の反映
                _forceDisableAutoPlay = newSettings.ForceDisableAutoPlay;

                // リスト自動遷移待機時間
                _listAutoNavDelay = newSettings.ListAutoNavDelay;

                _addColumnToLeft = newSettings.AddColumnToLeft;

                // 試験的な機能のフラグの反映
                _useExperimentalFeatures = newSettings.UseExperimentalFeatures;

                // 2段レイアウトの設定の反映
                _useTwoTierLayout = newSettings.UseTwoTierLayout;

                // ビデオコンテンツの自動PiP化設定の反映
                _autoPipForVideo = newSettings.AutoPipForVideo;

                // PiPを常に最前面に表示するかどうかの設定の反映
                _pipAlwaysOnTop = newSettings.PipAlwaysOnTop;

                // 既に開いているPiPウィンドウがあれば、最前面設定を即時反映する
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is PipWindow pipWin)
                    {
                        // OSに設定変更を確実に伝達するため、一旦falseにする
                        pipWin.Topmost = false;
                        pipWin.Topmost = _pipAlwaysOnTop;
                    }
                }

                // UIと連動しているプロパティにも値をセットする
                this.UseTwoTierLayout = newSettings.UseTwoTierLayout;

                MenuOtherProfileTimeline.Visibility = _useExperimentalFeatures ? Visibility.Visible : Visibility.Collapsed;

                // 変更検知：設定画面を開く前の値(ColumnWidth)と新しい値(newSettings.ColumnWidth)を比較
                bool isWidthChanged = Math.Abs(ColumnWidth - newSettings.ColumnWidth) > 0.01;

                ColumnWidth = newSettings.ColumnWidth;
                UseUniformGrid = newSettings.UseUniformGrid;

                // 設定画面で指定された幅が変更された場合のみ全カラムに適用
                if (!UseUniformGrid && isWidthChanged)
                {
                    foreach (var col in Columns)
                    {
                        col.Width = ColumnWidth;
                    }
                }

                ShowColumnUrl = newSettings.ShowColumnUrl;

                _autoShutdownEnabled = newSettings.AutoShutdownEnabled;
                _autoShutdownMinutes = newSettings.AutoShutdownMinutes;

                _checkForUpdates = newSettings.CheckForUpdates;

                // 絶対時間表示の設定を反映
                _showAbsoluteTime = newSettings.ShowAbsoluteTime;
                ApplyAbsoluteTimeSettingsToAll();

                foreach (var col in Columns)
                {
                    col.UseSoftRefresh = _useSoftRefresh;
                    col.KeepUnreadPosition = _keepUnreadPosition;

                    if (col.AssociatedWebView?.CoreWebView2 != null)
                    {
                        // 1. スクロール検知の許容範囲を既存のWebViewへ即時反映
                        col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync($"window.xColumnScrollTolerance = {_scrollTopTolerance};");

                        // 2. 自動再生無効化設定がONになった場合、即時注入
                        if (_forceDisableAutoPlay)
                        {
                            col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                        }
                    }
                }

                _serverCheckIntervalMinutes = newSettings.ServerCheckIntervalMinutes;

                // NGワードの更新と反映
                _ngWords = newSettings.NgWords ?? new List<string>();

                // 設定保存 (NGワード含む)
                SaveSettings(_activeProfileName);

                // テーマの適用
                ApplyTheme(_appTheme);

                // フォーカスモード設定を全WebViewに即時反映
                ApplyFocusModeSettingsToAll();

                // 開いている全WebViewにCSSを再適用
                ApplyCssToAllColumns();

                // NGワードスクリプトの再適用
                ApplyNgWordsToAllColumns(_ngWords);

                // サーバー監視タイマーの間隔を更新
                UpdateStatusCheckTimer(newSettings.ServerCheckIntervalMinutes);
            }
        }

        /// <summary>
        /// 「拡張機能」メニュークリック時の処理。
        /// </summary>
        private void ManageExtensions_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ExtensionWindow(_extensionList) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _extensionList = new List<ExtensionItem>(dlg.Extensions);
                SaveSettings(_activeProfileName);
                if (MessageWindow.Show(this, Properties.Resources.Msg_RestartConfirm,
                    Properties.Resources.Settings_Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    PerformProfileSwitch(_activeProfileName);
                }
            }
        }

        /// <summary>
        /// 「ご支援・寄付」メニュークリック時の処理。
        /// </summary>
        private void Support_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SupportWindow { Owner = this };
            dlg.ShowDialog();
        }

        /// <summary>
        /// アプリ終了メニュークリック時の処理。
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// バージョン情報表示メニュークリック時の処理。
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            string message = string.Format(Properties.Resources.Msg_About_Body, this.Title);
            MessageWindow.Show(message, Properties.Resources.Title_About, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Profile Menu Handlers

        /// <summary>
        /// プロファイルメニューが開かれたときに、プロファイル一覧を動的に生成します。
        /// </summary>
        private void MenuProfile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource != sender) return;

            // 静的アイテム（[0]新規作成, [1]セパレータ）以外をクリア
            while (MenuProfile.Items.Count > 2)
            {
                MenuProfile.Items.RemoveAt(2);
            }

            if (_profileNames != null)
            {
                foreach (var profile in _profileNames)
                {
                    // 親アイテム（プロファイル名）
                    var parentItem = new MenuItem
                    {
                        Header = profile.Name
                        // IsChecked は設定しない（サブメニュー展開の阻害要因になるため）
                    };

                    // アクティブなら太字で強調
                    if (profile.IsActive)
                    {
                        parentItem.FontWeight = FontWeights.Bold;
                    }

                    // --- 子メニューの構築 ---

                    // 1. 切り替え
                    var switchItem = new MenuItem
                    {
                        Header = Properties.Resources.Menu_Profile_Switch,
                        Tag = profile.Name,
                        IsEnabled = !profile.IsActive
                    };
                    switchItem.Click += SwitchProfile_Click;
                    parentItem.Items.Add(switchItem);

                    parentItem.Items.Add(new Separator());

                    // 2. 名前変更
                    var renameItem = new MenuItem
                    {
                        Header = Properties.Resources.Menu_Profile_Rename,
                        Tag = profile.Name,
                        IsEnabled = !profile.IsActive
                    };
                    renameItem.Click += RenameProfile_Click;
                    parentItem.Items.Add(renameItem);

                    // 3. 複製
                    var dupItem = new MenuItem
                    {
                        Header = Properties.Resources.Menu_Profile_Duplicate,
                        Tag = profile.Name
                    };
                    dupItem.Click += DuplicateProfile_Click;
                    parentItem.Items.Add(dupItem);

                    // 4. 別窓で起動
                    var launchItem = new MenuItem
                    {
                        Header = Properties.Resources.Menu_Profile_LaunchNew,
                        Tag = profile.Name
                    };
                    launchItem.Click += LaunchNewWindow_Click;
                    parentItem.Items.Add(launchItem);

                    parentItem.Items.Add(new Separator());

                    // 5. 削除
                    var deleteItem = new MenuItem
                    {
                        Header = Properties.Resources.Menu_Profile_Delete,
                        Tag = profile.Name,
                        Foreground = System.Windows.Media.Brushes.Red,
                        IsEnabled = !profile.IsActive
                    };
                    deleteItem.Click += DeleteProfile_Click;
                    parentItem.Items.Add(deleteItem);

                    // 親アイテムをメニューに追加
                    MenuProfile.Items.Add(parentItem);
                }
            }
        }

        /// <summary>
        /// 「別プロファイルのタイムライン」メニューが開かれたときの処理。
        /// 登録されている別プロファイルを動的に子メニューとして追加します。
        /// </summary>
        private void MenuOtherProfileTimeline_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu) return;

            // まず中身をすべてクリア（初期化）
            menu.Items.Clear();

            // 念のためのチェック
            if (!_useExperimentalFeatures || _profileNames == null) return;

            // 自分（現在のアクティブプロファイル）以外のリストを取得
            var otherProfiles = _profileNames.Where(p => p.Name != _activeProfileName).ToList();

            // 他のプロファイルが存在しない場合
            if (otherProfiles.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = Properties.Resources.Msg_NoOtherProfiles, IsEnabled = false });
                return;
            }

            // 別プロファイルのメニューを追加
            foreach (var profile in otherProfiles)
            {
                var profileItem = new MenuItem
                {
                    Header = profile.Name,
                    Tag = profile.Name // タグにはそのままプロファイル名を入れるだけでOK
                };
                profileItem.Click += AddOtherProfileColumn_Click;
                menu.Items.Add(profileItem);
            }
        }

        /// <summary>
        /// 別プロファイルのカラムを追加するクリックイベント。
        /// </summary>
        private void AddOtherProfileColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string targetProfileName)
            {
                var newColumn = new ColumnData
                {
                    Url = "https://x.com/home",
                    UseSoftRefresh = _useSoftRefresh,
                    Width = this.ColumnWidth > 0 ? this.ColumnWidth : 380,
                    ProfileName = targetProfileName // プロファイル名を紐づける
                };

                AddColumnObject(newColumn);
            }
        }

        #endregion

        #region Column UI Handlers

        /// <summary>
        /// 音量スライダー変更時の処理。
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _appVolume = e.NewValue / 100.0;

            // WebView内にJSを流し込むのではなく、プロセス音量を直接変更する
            SetWebView2Volume(_appVolume);
        }

        /// <summary>
        /// カラムごとの「RT非表示」チェックボックスクリック時の処理。
        /// </summary>
        private void RetweetHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is ColumnData col)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, col.Url, col);
                }
                SaveSettings(_activeProfileName);
            }
        }

        /// <summary>
        /// カラムごとの「リプライ非表示」チェックボックスクリック時の処理。
        /// </summary>
        private void ReplyHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is ColumnData col)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    // カラム個別にCSSを再適用
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, col.Url, col);
                }
                SaveSettings(_activeProfileName);
            }
        }

        /// <summary>
        /// カラムヘッダーがクリックされたときの処理。
        /// そのカラムを選択状態にし、WebViewにフォーカスを当てます。
        /// </summary>
        private void ColumnHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ColumnData col)
            {
                // アクティブカラムを更新
                _activeColumnData = col;
                foreach (var c in Columns)
                {
                    c.IsActive = (c == col);
                }

                // WebViewにフォーカスを移動
                col.AssociatedWebView?.Focus();
            }
        }

        /// <summary>
        /// カラム設定の入力欄でキーが押された時の処理。
        /// Enterキーで値を即座に反映し、タイマーをリセットします。
        /// </summary>
        private void ColumnSetting_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is System.Windows.Controls.TextBox txt && txt.DataContext is ColumnData col)
                {
                    // 1. 入力された値を強制的にバインディングソース（ColumnData）へ反映
                    var binding = txt.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();

                    // 2. 値が変わっていなくても、明示的にタイマーをリセットして再始動
                    // (これにより「適用された感」をユーザーにフィードバックします)
                    col.UpdateTimer(reset: true);

                    // 3. 入力欄からフォーカスを外す（入力完了の合図）
                    Keyboard.ClearFocus();

                    // 4. ビープ音防止
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// カラム設定ボタン（歯車）クリック時の処理。
        /// 右クリック用メニューを左クリックで開くようにします。
        /// </summary>
        private void ColumnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                // メニューの表示位置の基準をボタン自身に設定
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

                // メニューを開く
                btn.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// WebViewがフォーカスを得たときに呼び出されます。
        /// 現在アクティブなカラムを記録します。
        /// </summary>
        private void WebView_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.Web.WebView2.Wpf.WebView2 webView && webView.DataContext is ColumnData col)
            {
                _activeColumnData = col;
                // 全カラムの IsActive を更新
                foreach (var c in Columns)
                {
                    c.IsActive = (c == col);
                }
            }
        }

        #endregion

        #region Keyboard & Mouse Input

        /// <summary>
        /// マウスホイール操作時の処理。Shift併用時のみ横スクロールとして扱います。
        /// </summary>
        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Shiftキーが押されている場合のみ横スクロールとして処理
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
            {
                PerformHorizontalScroll(e.Delta);
                // イベントを処理済みに設定して、縦スクロールを防止
                e.Handled = true;
            }
        }

        /// <summary>
        /// キーボードショートカットの処理。
        /// WebView以外にフォーカスがある場合のナビゲーションを担当します。
        /// </summary>
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 1. 入力欄(TextBox)での誤動作防止
            if (e.OriginalSource is System.Windows.Controls.TextBox ||
                e.OriginalSource is System.Windows.Controls.PasswordBox ||
                e.OriginalSource is ModernWpf.Controls.NumberBox)
            {
                return;
            }

            // フォーカスモード時はアプリ側での左右キー処理は行わない
            if (_isFocusMode)
            {
                return;
            }

            // 2. アクティブなカラムの状態を確認し、入力中や画像表示中なら処理をスキップ（Web側に任せる）
            if (_activeColumnData != null)
            {
                // A. 入力中なら処理しない
                if (_activeColumnData.IsInputActive)
                {
                    return;
                }

                // B. 画像/動画ビューアが開いているかチェック (URLで判定)
                string currentUrl = _activeColumnData.Url;

                // 念のため、WebViewから直接最新のURL取得を試みる
                if (_activeColumnData.AssociatedWebView?.CoreWebView2 != null)
                {
                    try { currentUrl = _activeColumnData.AssociatedWebView.CoreWebView2.Source; }
                    catch { /* 無視 */ }
                }

                // URLに /photo/ や /video/ が含まれている場合、左右キーは画像送りに使うためアプリ側では処理しない
                bool isMediaView = !string.IsNullOrEmpty(currentUrl) &&
                                   (currentUrl.Contains("/photo/") || currentUrl.Contains("/video/"));

                if (isMediaView && (e.Key == Key.Left || e.Key == Key.Right))
                {
                    return;
                }
            }

            if (Columns.Count == 0) return;

            // Ctrlキーが押されているかチェック
            bool isCtrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

            bool handled = true;
            switch (e.Key)
            {
                case Key.Left: MoveColumnFocus(-1); break;
                case Key.Right: MoveColumnFocus(1); break;
                case Key.PageUp: ScrollSelectedColumnVertical(true); break;
                case Key.PageDown: ScrollSelectedColumnVertical(false); break;

                // 1-9キー
                case Key.D1: if (isCtrl) JumpToColumn(0); else handled = false; break;
                case Key.D2: if (isCtrl) JumpToColumn(1); else handled = false; break;
                case Key.D3: if (isCtrl) JumpToColumn(2); else handled = false; break;
                case Key.D4: if (isCtrl) JumpToColumn(3); else handled = false; break;
                case Key.D5: if (isCtrl) JumpToColumn(4); else handled = false; break;
                case Key.D6: if (isCtrl) JumpToColumn(5); else handled = false; break;
                case Key.D7: if (isCtrl) JumpToColumn(6); else handled = false; break;
                case Key.D8: if (isCtrl) JumpToColumn(7); else handled = false; break;
                case Key.D9: if (isCtrl) JumpToColumn(8); else handled = false; break;

                // テンキー
                case Key.NumPad1: if (isCtrl) JumpToColumn(0); else handled = false; break;
                case Key.NumPad2: if (isCtrl) JumpToColumn(1); else handled = false; break;
                case Key.NumPad3: if (isCtrl) JumpToColumn(2); else handled = false; break;
                case Key.NumPad4: if (isCtrl) JumpToColumn(3); else handled = false; break;
                case Key.NumPad5: if (isCtrl) JumpToColumn(4); else handled = false; break;
                case Key.NumPad6: if (isCtrl) JumpToColumn(5); else handled = false; break;
                case Key.NumPad7: if (isCtrl) JumpToColumn(6); else handled = false; break;
                case Key.NumPad8: if (isCtrl) JumpToColumn(7); else handled = false; break;
                case Key.NumPad9: if (isCtrl) JumpToColumn(8); else handled = false; break;

                default: handled = false; break;
            }

            if (handled) e.Handled = true;
        }

        #endregion

        #region Column Navigation & Scrolling

        /// <summary>
        /// カラムフォーカスを隣へ移動させます。
        /// </summary>
        /// <param name="direction">移動方向 (-1: 左, 1: 右)。</param>
        private void MoveColumnFocus(int direction)
        {
            if (Columns.Count == 0) return;

            int currentIndex = -1;

            // 現在のアクティブカラムの位置を探す
            if (_activeColumnData != null)
            {
                currentIndex = Columns.IndexOf(_activeColumnData);
            }

            // 見つからない、または未選択なら、方向に応じて端を選択
            if (currentIndex == -1)
            {
                currentIndex = (direction > 0) ? 0 : Columns.Count - 1;
            }
            else
            {
                // インデックス移動
                currentIndex += direction;
            }

            // 範囲制限
            if (currentIndex < 0) currentIndex = 0;
            if (currentIndex >= Columns.Count) currentIndex = Columns.Count - 1;

            // ターゲットのカラムを取得してフォーカス
            var targetColumn = Columns[currentIndex];
            if (targetColumn.AssociatedWebView != null)
            {
                // 1. WebViewにフォーカスを当てる
                targetColumn.AssociatedWebView.Focus();

                // 2. そのカラムが見える位置までスクロールする
                ScrollToColumn(targetColumn);
            }
        }

        /// <summary>
        /// 指定したインデックス（0始まり）のカラムに直接フォーカスを移動します。
        /// </summary>
        private void JumpToColumn(int index)
        {
            if (index < 0 || index >= Columns.Count) return;

            var targetColumn = Columns[index];
            if (targetColumn.AssociatedWebView != null)
            {
                // フォーカスを当てて、アクティブ状態を更新
                targetColumn.AssociatedWebView.Focus();

                // 画面外にあればスクロール
                ScrollToColumn(targetColumn);
            }
        }

        /// <summary>
        /// 指定したカラムが見える位置までスクロールします。
        /// </summary>
        private void ScrollToColumn(ColumnData col)
        {
            var scrollViewer = ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) as ScrollViewer;
            if (scrollViewer == null) return;

            int index = Columns.IndexOf(col);
            if (index < 0) return;

            double targetOffset;
            if (UseUniformGrid)
            {
                // 等幅モード
                double colWidth = scrollViewer.ViewportWidth / Columns.Count;
                targetOffset = index * colWidth;
            }
            else
            {
                // 固定幅モード
                targetOffset = index * ColumnWidth;
            }

            scrollViewer.ScrollToHorizontalOffset(targetOffset);
        }

        /// <summary>
        /// 現在画面の中央付近にある（メインで見ている）カラムを特定し、
        /// そのカラムのWebViewに対してスクロール命令を送ります。
        /// </summary>
        /// <param name="scrollDown">trueなら下へ、falseなら上へスクロール。</param>
        private void ScrollActiveColumn(bool scrollDown)
        {
            // フォーカスモード（シングルビュー）の場合は FocusWebView を操作
            if (_isFocusMode && FocusWebView != null && FocusWebView.CoreWebView2 != null)
            {
                ExecuteScrollScript(FocusWebView.CoreWebView2, scrollDown);
                return;
            }

            // 通常モード: ScrollViewerの現在位置から、中心にあるカラムを特定
            var scrollViewer = ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) as ScrollViewer;
            if (scrollViewer == null || Columns.Count == 0) return;

            // 現在のスクロール位置 + 画面幅の半分 = 中心座標
            double centerOffset = scrollViewer.HorizontalOffset + (scrollViewer.ViewportWidth / 2);

            int index = -1;
            if (UseUniformGrid)
            {
                // 等分割モードの場合
                double widthPerCol = scrollViewer.ViewportWidth / Columns.Count;
                index = (int)(centerOffset / widthPerCol);
                if (index < 0 || index >= Columns.Count) index = 0;
            }
            else
            {
                // 固定幅モードの場合
                index = (int)(centerOffset / ColumnWidth);
            }

            // 範囲チェック
            if (index >= 0 && index < Columns.Count)
            {
                var targetColumn = Columns[index];
                if (targetColumn.AssociatedWebView?.CoreWebView2 != null)
                {
                    ExecuteScrollScript(targetColumn.AssociatedWebView.CoreWebView2, scrollDown);
                }
            }
        }

        /// <summary>
        /// カラム内を垂直スクロールします。
        /// </summary>
        /// <param name="up">trueなら上へ、falseなら下へスクロール。</param>
        private async void ScrollSelectedColumnVertical(bool up)
        {
            if (_activeColumnData?.AssociatedWebView?.CoreWebView2 != null)
            {
                string direction = up ? "-" : "";
                string script = $"window.scrollBy({{ top: {direction}window.innerHeight * 0.8, behavior: 'smooth' }});";
                try { await _activeColumnData.AssociatedWebView.ExecuteScriptAsync(script); }
                catch { }
            }
        }

        /// <summary>
        /// WebView2に対してJSを実行し、スクロールさせます。
        /// </summary>
        private void ExecuteScrollScript(Microsoft.Web.WebView2.Core.CoreWebView2 webView, bool scrollDown)
        {
            // 画面の80%分をスクロール
            string direction = scrollDown ? "1" : "-1";
            string script = $"window.scrollBy(0, window.innerHeight * 0.8 * {direction});";
            webView.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 指定されたスクロール量に基づいて、メインのScrollViewerを水平方向にスクロールさせます。
        /// </summary>
        /// <param name="delta">スクロール量（ピクセル単位に近い値）。</param>
        public void PerformHorizontalScroll(double delta)
        {
            // Template内にあるScrollViewerを名前で検索して取得
            var scrollViewer = ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) as ScrollViewer;
            if (scrollViewer != null)
            {
                // ガタガタ対策 & 方向修正:
                // Windows標準: deltaが正(右操作)ならOffsetを増やす(右へ)、負なら減らす(左へ)
                double currentOffset = scrollViewer.HorizontalOffset;
                double newOffset = currentOffset + delta;

                scrollViewer.ScrollToHorizontalOffset(newOffset);
            }
        }

        #endregion

        #region Focus Mode & PiP

        /// <summary>
        /// 指定されたURLをフォーカスモード（全画面）で開きます。
        /// 設定により動画は PiP ウィンドウで開きます。
        /// </summary>
        public void OpenFocusMode(string url, bool isVideo = false)
        {
            // 自動PiPの判定処理
            // 念のため、URLに /video/ が含まれていれば無条件で動画扱いとする
            if (url.Contains("/video/"))
            {
                isVideo = true;
            }

            // 自動PiPの判定
            if (_autoPipForVideo && isVideo)
            {
                // PiPウィンドウを生成して表示
                var pipWin = new PipWindow(url, _userDataFolder);
                // 設定値に応じて「常に手前に表示」を適用
                pipWin.Topmost = _pipAlwaysOnTop;

                // --- 設定からサイズと位置を復元 ---
                if (!double.IsNaN(_pipWindowTop) && !double.IsNaN(_pipWindowLeft))
                {
                    pipWin.Top = _pipWindowTop;
                    pipWin.Left = _pipWindowLeft;

                    // 画面外に行ってしまった場合の対策（マルチモニター切替時など）
                    var rect = new System.Drawing.Rectangle((int)pipWin.Left, (int)pipWin.Top, (int)_pipWindowWidth, (int)_pipWindowHeight);
                    bool onScreen = false;
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
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
                            pipWin.Left = primary.WorkingArea.Left + 50;
                            pipWin.Top = primary.WorkingArea.Top + 50;
                        }
                    }
                }
                pipWin.Width = _pipWindowWidth;
                pipWin.Height = _pipWindowHeight;

                pipWin.Closed += (s, e) =>
                {
                    // 閉じられた時点の最新のサイズと位置を変数に保持
                    _pipWindowTop = pipWin.Top;
                    _pipWindowLeft = pipWin.Left;
                    _pipWindowWidth = pipWin.Width;
                    _pipWindowHeight = pipWin.Height;

                    // 設定ファイルに上書き保存
                    SaveSettings(_activeProfileName);
                };

                pipWin.Show();
                // WPFの仕様上、Show()の前にTopmostを設定してもOSに伝わらないことがあるため
                // 一旦 false にしてから真の設定値を適用することで確実に反映させます
                pipWin.Topmost = false;
                pipWin.Topmost = _pipAlwaysOnTop;

                // 明示的にアクティブにして手前に持ってくる
                pipWin.Activate();

                return;
            }

            _isFocusMode = true;

            // FocusViewGridを表示状態にする（前回作成したモーダル表示）
            FocusViewGrid.Visibility = Visibility.Visible;

            // カラム表示を一時的に隠す
            ColumnItemsControl.Visibility = Visibility.Hidden;

            // 遷移前にカウントダウンタイマーなどを一時停止
            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();

            // WebViewを目的のURL（/photo/1 が付与されたもの）へ飛ばす
            if (FocusWebView?.CoreWebView2 != null)
            {
                FocusWebView.CoreWebView2.Navigate(url);
            }
        }

        #endregion

        #region Timers

        /// <summary>
        /// タイマーをすべて停止します。
        /// </summary>
        private void StopAllTimers()
        {
            _countdownTimer.Stop();
            foreach (var col in Columns) col.Timer?.Stop();
        }

        /// <summary>
        /// タイマーをすべて開始または再開します。
        /// </summary>
        /// <param name="resume">trueなら現在の残り時間で再開、falseならリセットして開始。</param>
        private void StartAllTimers(bool resume)
        {
            _countdownTimer.Start();
            foreach (var col in Columns) col.UpdateTimer(!resume);
        }

        /// <summary>
        /// 1秒ごとに呼び出されるカウントダウンタイマーの処理。
        /// 残り時間の減算と、自動シャットダウン判定を行います。
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                    column.RemainingSeconds--;
            }
            // 自動シャットダウン判定
            if (_autoShutdownEnabled && !_isAppActive && _lastDeactivatedTime.HasValue)
            {
                var elapsed = DateTime.Now - _lastDeactivatedTime.Value;
                if (elapsed.TotalMinutes >= _autoShutdownMinutes)
                {
                    // 念のためタイマーを止めてから終了
                    _countdownTimer.Stop();
                    Close();
                }
            }
        }

        #endregion

        #region Helpers & Apply Methods

        /// <summary>
        /// ビジュアルツリーを遡って特定の親要素を探すヘルパーメソッド。
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        /// <summary>
        /// ウィンドウのタイトルを更新します。
        /// 「アプリ名 + バージョン + (プロファイル名)」の形式にします。
        /// </summary>
        private void UpdateWindowTitle()
        {
            // バージョン情報の取得
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            // ベースとなるタイトル (例: "XColumn v1.29.0")
            string baseTitle = $"XColumn v{version?.Major}.{version?.Minor}.{version?.Build}";

            // プロファイル名の付与判定
            if (string.IsNullOrEmpty(_activeProfileName) || _activeProfileName == "default")
            {
                // デフォルトの場合はバージョンのみ
                this.Title = baseTitle;
            }
            else
            {
                // プロファイルがある場合は後ろに追記 (例: "XColumn v1.29.0 - 趣味用")
                this.Title = $"{baseTitle} - {_activeProfileName}";
            }
        }

        /// <summary>
        /// NAudioを使用して、WebView2とアプリ自体の音量をOSレベルで変更します。
        /// </summary>
        /// <param name="volume">0.0 ～ 1.0 の音量値。</param>
        private void SetWebView2Volume(double volume)
        {
            try
            {
                // 音量範囲を 0.0 ～ 1.0 に制限
                float vol = (float)Math.Clamp(volume, 0.0, 1.0);

                // 対象となるプロセスIDのリストを作成 (アプリ自身 + すべてのWebView2プロセス)
                var targetProcessIds = System.Diagnostics.Process.GetProcessesByName("msedgewebview2")
                    .Select(p => p.Id)
                    .ToList();
                targetProcessIds.Add(System.Diagnostics.Process.GetCurrentProcess().Id);

                // NAudioを使用してCoreAudioAPIを呼び出し
                var deviceEnumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();

                // 有効なオーディオデバイス（スピーカー等）が存在するかチェック（エラー回避）
                if (!deviceEnumerator.HasDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia))
                {
                    return;
                }

                var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
                var sessionManager = defaultDevice.AudioSessionManager;
                var sessionCollection = sessionManager.Sessions;

                // セッションを走査し、対象プロセスの音量を変更
                for (int i = 0; i < sessionCollection.Count; i++)
                {
                    var session = sessionCollection[i];
                    if (targetProcessIds.Contains((int)session.GetProcessID))
                    {
                        session.SimpleAudioVolume.Volume = vol;
                        // 音量が0ならミュートも連動させる
                        session.SimpleAudioVolume.Mute = (vol <= 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to set application volume: " + ex.Message);
            }
        }

        /// <summary>
        /// フォーカスモード設定（クリック時の遷移無効化設定）の有効/無効をすべてのWebViewに即時反映させます。
        /// </summary>
        private void ApplyFocusModeSettingsToAll()
        {
            string mediaFlagScript = $"window.xColumnDisableMediaFocus = {_disableFocusModeOnMediaClick.ToString().ToLower()};";
            string tweetFlagScript = $"window.xColumnDisableTweetFocus = {_disableFocusModeOnTweetClick.ToString().ToLower()};";

            foreach (var col in Columns)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    try
                    {
                        col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(mediaFlagScript);
                        col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(tweetFlagScript);
                    }
                    catch { /* WebViewが破棄されている場合は無視 */ }
                }
            }

            if (FocusWebView?.CoreWebView2 != null)
            {
                try
                {
                    FocusWebView.CoreWebView2.ExecuteScriptAsync(mediaFlagScript);
                    FocusWebView.CoreWebView2.ExecuteScriptAsync(tweetFlagScript);
                }
                catch { /* 無視 */ }
            }
        }

        #endregion
    }
}