using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// メインウィンドウのライフサイクルとグローバルタイマーを管理します。
    /// 詳細なロジックは Code/ フォルダ内の partial class に分割されています。
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        private Microsoft.Web.WebView2.Core.CoreWebView2Environment? _webViewEnvironment;
        private readonly DispatcherTimer _countdownTimer;
        private bool _isFocusMode = false;
        private bool _isAppActive = true;
        private ColumnData? _focusedColumnData = null;

        // 再起動中フラグ (Closingイベントでの上書き保存をスキップするために使用)
        internal bool _isRestarting = false;

        private readonly string _userDataFolder;
        private readonly string _profilesFolder;
        private readonly string _appConfigPath;

        public MainWindow()
        {
            InitializeComponent();

            // パスの初期化
            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _profilesFolder = Path.Combine(_userDataFolder, "Profiles");
            _appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
            Directory.CreateDirectory(_profilesFolder);

            ColumnItemsControl.ItemsSource = Columns;

            // プロファイル関連UI初期化 (Code/MainWindow.Profiles.cs)
            InitializeProfilesUI();

            // グローバルカウントダウンタイマー（1秒刻み）
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            this.Closing += MainWindow_Closing;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 設定読み込み (Code/MainWindow.Config.cs)
                AppSettings settings = ReadSettingsFromFile(_activeProfileName);
                ApplySettingsToWindow(settings);

                // WebView環境初期化 (Code/MainWindow.WebView.cs)
                await InitializeWebViewEnvironmentAsync();

                // カラム復元 (Code/MainWindow.Columns.cs)
                LoadColumnsFromSettings(settings);

                // アップデート確認 (Code/MainWindow.Update.cs)
                _ = CheckForUpdatesAsync(settings.SkippedVersion);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 再起動中は現在の状態を保存しない（切り替え先のプロファイルへの上書き防止）
            if (_isRestarting) return;

            // 通常終了時は現在のアクティブプロファイルに保存
            SaveSettings(_activeProfileName);
            SaveAppConfig();

            _countdownTimer.Stop();
            foreach (var col in Columns) col.StopAndDisposeTimer();
        }

        /// <summary>
        /// アプリがアクティブ（フォアグラウンド）になった時。
        /// 操作中とみなし、自動更新を停止します。
        /// </summary>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            _isAppActive = true;
            _countdownTimer.Stop();
            foreach (var col in Columns) col.Timer?.Stop();
        }

        /// <summary>
        /// アプリが非アクティブ（バックグラウンド）になった時。
        /// 自動更新を再開します。
        /// </summary>
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _isAppActive = false;
            if (_isFocusMode) return;

            _countdownTimer.Start();
            foreach (var col in Columns) col.UpdateTimer();
        }

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