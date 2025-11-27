using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XColumn.Models;

// WinFormsとWPFのButtonクラスの競合を回避するための明示的な指定
using Button = System.Windows.Controls.Button;

namespace XColumn
{
    /// <summary>
    /// アプリケーションのメインウィンドウ。
    /// 全体的な状態管理、UIイベント、設定の適用、およびウィンドウレベルの入力制御を行います。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// UIに表示されるカラムのコレクション。
        /// </summary>
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        /// <summary>
        /// メモリ上に保持されている拡張機能リスト。
        /// </summary>
        private List<ExtensionItem> _extensionList = new List<ExtensionItem>();

        // アプリがアクティブな時にタイマーを停止するかどうか
        public static readonly DependencyProperty StopTimerWhenActiveProperty =
            DependencyProperty.Register(nameof(StopTimerWhenActive), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true, OnStopTimerWhenActiveChanged));

        // 現在アクティブなプロファイル名
        private string? _startupProfileName;

        // サーバー監視間隔（分）
        private int _serverCheckIntervalMinutes = 5;

        /// <summary>
        /// メインウィンドウのコンストラクタ（プロファイル名指定なし）。
        /// </summary>
        public MainWindow() : this(null) { }

        /// <summary>
        /// メインウィンドウのコンストラクタ。
        /// </summary>
        /// <param name="profileName">起動時に指定されたプロファイル名</param>
        public MainWindow(string? profileName)
        {
            InitializeComponent();
            _startupProfileName = profileName;

            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _profilesFolder = Path.Combine(_userDataFolder, "Profiles");
            _appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
            Directory.CreateDirectory(_profilesFolder);

            ColumnItemsControl.ItemsSource = Columns;

            InitializeProfilesUI();

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            this.Closing += MainWindow_Closing;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        public bool StopTimerWhenActive
        {
            get => (bool)GetValue(StopTimerWhenActiveProperty);
            set => SetValue(StopTimerWhenActiveProperty, value);
        }

        /// <summary>
        /// 設定変更時に即座にタイマー状態を更新します。
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

        // 各カラムの基本幅（固定幅モード時）
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(MainWindow),
                new PropertyMetadata(380.0));
        /// <summary>
        /// 各カラムの基本幅（固定幅モード時）。
        /// </summary>
        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        // ウィンドウ幅に合わせてカラムを等分割するかどうか
        public static readonly DependencyProperty UseUniformGridProperty =
            DependencyProperty.Register(nameof(UseUniformGrid), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));
        /// <summary>
        /// ウィンドウ幅に合わせてカラムを等分割するかどうか。
        /// </summary>
        public bool UseUniformGrid
        {
            get => (bool)GetValue(UseUniformGridProperty);
            set => SetValue(UseUniformGridProperty, value);
        }

        // --- 設定値保持用フィールド ---
        private bool _hideMenuInNonHome = false;
        private bool _hideMenuInHome = false;
        private bool _hideListHeader = false;
        private bool _hideRightSidebar = false;

        // 動作設定
        private bool _useSoftRefresh = true;
        private string _customCss = "";
        private double _appVolume = 0.5;

        // メディアクリック時にフォーカスモードを解除しないかどうか
        private bool _disableFocusModeOnMediaClick = false;

        // フォント設定
        private string _appFontFamily = "Meiryo";
        private int _appFontSize = 15;

        // --- 内部状態管理 ---
        private Microsoft.Web.WebView2.Core.CoreWebView2Environment? _webViewEnvironment;
        private readonly DispatcherTimer _countdownTimer;
        private bool _isFocusMode = false;
        private bool _isAppActive = true;
        private ColumnData? _focusedColumnData = null;

        /// <summary>
        /// 再起動処理中フラグ（終了時の二重保存防止用）。
        /// </summary>
        internal bool _isRestarting = false;

        private readonly string _userDataFolder;
        private readonly string _profilesFolder;
        private readonly string _appConfigPath;

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
        /// 指定されたスクロール量に基づいて、メインのScrollViewerを水平方向にスクロールさせます。
        /// </summary>
        /// <param name="delta">スクロール量（ピクセル単位に近い値）</param>
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

        /// <summary>
        /// 「設定」ボタンクリック時の処理。
        /// </summary>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            // 現在の設定を読み込んで渡す
            AppSettings current = ReadSettingsFromFile(_activeProfileName);

            current.StopTimerWhenActive = StopTimerWhenActive;
            current.UseSoftRefresh = _useSoftRefresh;
            current.EnableWindowSnap = _enableWindowSnap;
            current.CustomCss = _customCss;
            current.AppVolume = _appVolume;
            current.DisableFocusModeOnMediaClick = _disableFocusModeOnMediaClick;

            current.ColumnWidth = ColumnWidth;
            current.UseUniformGrid = UseUniformGrid;
            current.HideRightSidebar = _hideRightSidebar;

            current.AppFontFamily = _appFontFamily;
            current.AppFontSize = _appFontSize;

            current.ServerCheckIntervalMinutes = _serverCheckIntervalMinutes;

            var dlg = new SettingsWindow(current) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // 設定を反映
                AppSettings newSettings = dlg.Settings;

                StopTimerWhenActive = newSettings.StopTimerWhenActive;
                _hideMenuInNonHome = newSettings.HideMenuInNonHome;
                _hideMenuInHome = newSettings.HideMenuInHome;
                _hideListHeader = newSettings.HideListHeader;
                _hideRightSidebar = newSettings.HideRightSidebar;

                _appFontFamily = newSettings.AppFontFamily;
                _appFontSize = newSettings.AppFontSize;

                _useSoftRefresh = newSettings.UseSoftRefresh;
                _enableWindowSnap = newSettings.EnableWindowSnap;
                _customCss = newSettings.CustomCss;
                _disableFocusModeOnMediaClick = newSettings.DisableFocusModeOnMediaClick;

                ColumnWidth = newSettings.ColumnWidth;
                UseUniformGrid = newSettings.UseUniformGrid;

                foreach (var col in Columns)
                {
                    col.UseSoftRefresh = _useSoftRefresh;
                }

                // サーバー監視間隔の更新
                _serverCheckIntervalMinutes = newSettings.ServerCheckIntervalMinutes;

                // 設定保存
                SaveSettings(_activeProfileName);

                // 開いている全WebViewにCSSを再適用
                ApplyCssToAllColumns();

                // サーバー監視タイマーの間隔を更新
                UpdateStatusCheckTimer(newSettings.ServerCheckIntervalMinutes);
            }
        }

        /// <summary>
        /// 音量スライダー変更時の処理。
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _appVolume = e.NewValue / 100.0;
            ApplyVolumeToAllWebViews();
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
                if (System.Windows.MessageBox.Show("拡張機能の設定を変更しました。\n反映するにはアプリの再起動が必要です。\n今すぐ再起動しますか？",
                    "再起動の確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    PerformProfileSwitch(_activeProfileName);
                }
            }
        }

        /// <summary>
        /// カラムごとの「RT非表示」チェックボックスクリック時の処理。
        /// </summary>
        private void RetweetHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox chk && chk.Tag is ColumnData col)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, col.Url, col);
                }
                SaveSettings(_activeProfileName);
            }
        }

        private void LaunchNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is ProfileItem item)
            {
                string targetProfile = item.Name;
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (exePath != null) Process.Start(exePath, $"--profile \"{targetProfile}\"");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"新しいウィンドウの起動に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

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
                System.Windows.MessageBox.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            if (_isFocusMode) return;
            if (StopTimerWhenActive) StartAllTimers(resume: true);
        }

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
        /// <param name="resume"></param>
        private void StartAllTimers(bool resume)
        {
            _countdownTimer.Start();
            foreach (var col in Columns) col.UpdateTimer(!resume);
        }

        /// <summary>
        /// 1秒ごとに呼び出されるカウントダウンタイマーの処理。
        /// </summary>

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                    column.RemainingSeconds--;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableWindowSnap();
        }
    }
}