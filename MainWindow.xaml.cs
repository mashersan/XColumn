using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XColumn.Models;

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

        #region 依存関係プロパティ
        // アプリがアクティブな時にタイマーを停止するかどうか
        public static readonly DependencyProperty StopTimerWhenActiveProperty =
            DependencyProperty.Register(nameof(StopTimerWhenActive), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true, OnStopTimerWhenActiveChanged));

        /// <summary>
        /// アプリがアクティブ（操作中）な時に自動更新タイマーを停止するかどうか。
        /// </summary>
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
        #endregion

        // --- 設定値保持用フィールド ---
        private bool _hideMenuInNonHome = false;
        private bool _hideMenuInHome = false;
        private bool _hideListHeader = false;
        private bool _hideRightSidebar = false;

        // 動作設定
        private bool _useSoftRefresh = true;
        private string _customCss = "";
        private double _appVolume = 0.5;

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


        public MainWindow()
        {
            InitializeComponent();

            // ユーザーデータフォルダとプロファイルフォルダの初期化
            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _profilesFolder = Path.Combine(_userDataFolder, "Profiles");
            _appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
            Directory.CreateDirectory(_profilesFolder);

            // カラムItemsControlのデータコンテキストを設定
            ColumnItemsControl.ItemsSource = Columns;

            // プロファイル関連UI初期化
            InitializeProfilesUI();

            // グローバルカウントダウンタイマー（1秒刻み）
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // ウィンドウイベントハンドラ登録
            this.Closing += MainWindow_Closing;
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        /// <summary>
        /// ウィンドウ全体でのマウスホイールイベントをフックします。
        /// カラムヘッダーやツールバー上でのShift+ホイール操作を検知して横スクロールを行います。
        /// </summary>
        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Shiftキーが押されている場合のみ横スクロールとして処理
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
            {
                PerformHorizontalScroll(e.Delta);
                e.Handled = true; // イベントを処理済みとしてマーク
            }
        }

        /// <summary>
        /// 指定されたスクロール量に基づいて、メインのScrollViewerを水平方向にスクロールさせます。
        /// Windowのイベントハンドラや、WebViewからのJSメッセージ経由で呼び出されます。
        /// </summary>
        /// <param name="delta">ホイールの回転量（正: 左へ、負: 右へ）</param>
        public void PerformHorizontalScroll(int delta)
        {
            // Template内にあるScrollViewerを名前で検索して取得
            var scrollViewer = ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) as ScrollViewer;

            if (scrollViewer != null)
            {
                if (delta > 0)
                {
                    // 左へスクロール（感度調整のため複数回呼び出し）
                    scrollViewer.LineLeft();
                    scrollViewer.LineLeft();
                    scrollViewer.LineLeft();
                }
                else
                {
                    // 右へスクロール
                    scrollViewer.LineRight();
                    scrollViewer.LineRight();
                    scrollViewer.LineRight();
                }
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
            current.CustomCss = _customCss;
            current.AppVolume = _appVolume;
            current.ColumnWidth = ColumnWidth;
            current.UseUniformGrid = UseUniformGrid;
            current.HideRightSidebar = _hideRightSidebar;

            current.AppFontFamily = _appFontFamily;
            current.AppFontSize = _appFontSize;

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
                _customCss = newSettings.CustomCss;

                ColumnWidth = newSettings.ColumnWidth;
                UseUniformGrid = newSettings.UseUniformGrid;

                foreach (var col in Columns)
                {
                    col.UseSoftRefresh = _useSoftRefresh;
                }

                // 設定保存
                SaveSettings(_activeProfileName);

                // 開いている全WebViewにCSSを再適用
                ApplyCssToAllColumns();
            }
        }

        /// <summary>
        /// 音量スライダー変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _appVolume = e.NewValue / 100.0;
            ApplyVolumeToAllWebViews();
        }

        /// <summary>
        /// 「拡張機能」ボタンクリック時の処理。
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

        /// <summary>
        /// ウィンドウロード時の初期化処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 設定読み込み
                AppSettings settings = ReadSettingsFromFile(_activeProfileName);
                ApplySettingsToWindow(settings);

                // WebView環境初期化
                await InitializeWebViewEnvironmentAsync();

                // カラム復元
                LoadColumnsFromSettings(settings);

                // アップデート確認
                _ = CheckForUpdatesAsync(settings.SkippedVersion);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 終了時の保存処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isRestarting) return;

            SaveSettings(_activeProfileName);
            SaveAppConfig();

            _countdownTimer.Stop();
            foreach (var col in Columns) col.StopAndDisposeTimer();
        }

        /// <summary>
        /// アプリがアクティブになった時の処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            _isAppActive = true;
            if (StopTimerWhenActive) StopAllTimers();
        }

        /// <summary>
        /// アプリが非アクティブになった時の処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
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