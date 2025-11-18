using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// アプリケーションのメインウィンドウ。
    /// カラムの管理、WebView2環境の初期化、設定の読み書き、UIイベントのハンドリングを行います。
    /// 詳細なロジックの一部は Code/ フォルダ内の partial class に分割されています。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 表示中のカラムデータのコレクション。UI（ItemsControl）にバインドされます。
        /// </summary>
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        /// <summary>
        /// 読み込まれた拡張機能のリストを保持します。
        /// </summary>
        private List<ExtensionItem> _extensionList = new List<ExtensionItem>();

        #region 依存関係プロパティ (StopTimerWhenActive)

        /// <summary>
        /// "アクティブ時停止" 設定の依存関係プロパティ。
        /// </summary>
        public static readonly DependencyProperty StopTimerWhenActiveProperty =
            DependencyProperty.Register(nameof(StopTimerWhenActive), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true, OnStopTimerWhenActiveChanged));

        /// <summary>
        /// アプリがアクティブな時に自動更新タイマーを停止するかどうか。
        /// </summary>
        public bool StopTimerWhenActive
        {
            get => (bool)GetValue(StopTimerWhenActiveProperty);
            set => SetValue(StopTimerWhenActiveProperty, value);
        }

        /// <summary>
        /// StopTimerWhenActive プロパティが変更された時のコールバック。
        /// 即座にタイマーの動作状態を反映させます。
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

        private Microsoft.Web.WebView2.Core.CoreWebView2Environment? _webViewEnvironment;
        private readonly DispatcherTimer _countdownTimer;
        private bool _isFocusMode = false;
        private bool _isAppActive = true;
        private ColumnData? _focusedColumnData = null;

        /// <summary>
        /// 再起動プロセス中かどうかを示すフラグ。
        /// trueの場合、Closingイベントでの設定保存をスキップします。
        /// </summary>
        internal bool _isRestarting = false;

        private readonly string _userDataFolder;
        private readonly string _profilesFolder;
        private readonly string _appConfigPath;

        public MainWindow()
        {
            InitializeComponent();

            // ユーザーデータフォルダのパス初期化
            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _profilesFolder = Path.Combine(_userDataFolder, "Profiles");
            _appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
            Directory.CreateDirectory(_profilesFolder);

            ColumnItemsControl.ItemsSource = Columns;

            // プロファイルUIの初期化
            InitializeProfilesUI();

            // グローバルカウントダウンタイマー（1秒刻み）の初期化
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            this.Closing += MainWindow_Closing;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        /// <summary>
        /// 「拡張機能」ボタンクリック時の処理。
        /// 管理ウィンドウを表示し、変更があれば保存して再起動を促します。
        /// </summary>
        private void ManageExtensions_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ExtensionWindow(_extensionList) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // 編集後のリストを受け取る
                _extensionList = new List<ExtensionItem>(dlg.Extensions);
                // 設定を保存
                SaveSettings(_activeProfileName);

                if (System.Windows.MessageBox.Show("拡張機能の設定を変更しました。\n反映するにはアプリの再起動が必要です。\n今すぐ再起動しますか？",
                    "再起動の確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    PerformProfileSwitch(_activeProfileName); // 現在のプロファイルで再起動
                }
            }
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 設定ファイルの読み込み
                AppSettings settings = ReadSettingsFromFile(_activeProfileName);
                ApplySettingsToWindow(settings);

                // WebView2環境の初期化（ここで拡張機能もロードされます）
                await InitializeWebViewEnvironmentAsync();

                // カラムの復元
                LoadColumnsFromSettings(settings);

                // GitHubからのアップデート確認
                _ = CheckForUpdatesAsync(settings.SkippedVersion);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 再起動中は保存しない（プロファイル切り替え時の上書き防止）
            if (_isRestarting) return;

            SaveSettings(_activeProfileName);
            SaveAppConfig();

            _countdownTimer.Stop();
            foreach (var col in Columns) col.StopAndDisposeTimer();
        }

        /// <summary>
        /// ウィンドウがアクティブになった時の処理。
        /// 設定に応じて自動更新タイマーを停止します。
        /// </summary>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            _isAppActive = true;
            if (StopTimerWhenActive) StopAllTimers();
        }

        /// <summary>
        /// ウィンドウが非アクティブになった時の処理。
        /// 停止していたタイマーがあれば再開します。
        /// </summary>
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _isAppActive = false;
            if (_isFocusMode) return;

            // アクティブ時停止が有効だった場合、非アクティブ化に伴いタイマーを再開
            if (StopTimerWhenActive) StartAllTimers(resume: true);
        }

        private void StopAllTimers()
        {
            _countdownTimer.Stop();
            foreach (var col in Columns) col.Timer?.Stop();
        }

        private void StartAllTimers(bool resume)
        {
            _countdownTimer.Start();
            // resume=trueなら残り時間を保持したまま再開
            foreach (var col in Columns) col.UpdateTimer(!resume);
        }

        /// <summary>
        /// グローバルタイマーのTickイベント。
        /// 各カラムの残り時間を減算します。
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                    column.RemainingSeconds--;
            }
        }
    }
}