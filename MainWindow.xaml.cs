using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
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

// 曖昧さ回避
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

        // 現在アクティブ（選択中）のカラムデータ
        private ColumnData? _activeColumnData;

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

        // カラム追加時に左端に追加するかどうか
        private bool _addColumnToLeft = false;

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

            // ModernWpfのモダンウィンドウスタイルを適用
            WindowHelper.SetUseModernWindowStyle(this, true);

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

        // ポスト(ツイート)クリック時にフォーカスモードへ遷移しないかどうか
        private bool _disableFocusModeOnTweetClick = false;

        // フォント設定
        private string _appFontFamily = "Meiryo";
        private int _appFontSize = 15;

        // テーマ設定
        private string _appTheme = "System";


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

        /// <summary>
        /// ビジュアルツリーを遡って特定の親要素を探すヘルパーメソッド
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

            // 2. WebView内での操作（文字入力やスクロール）を阻害しないためのチェック
            // イベント発生元がWebView2、またはその子要素である場合は、WPF側での処理を行わずブラウザに任せます。
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Microsoft.Web.WebView2.Wpf.WebView2>(dep) != null)
                {
                    return; // WebViewにお任せ
                }
            }

            // ★追加: 現在フォーカスを持っている要素がWebView2の場合も処理を中断し、ブラウザに任せる
            if (System.Windows.Input.Keyboard.FocusedElement is Microsoft.Web.WebView2.Wpf.WebView2)
            {
                return;
            }

            if (Columns.Count == 0) return;

            bool handled = true;
            switch (e.Key)
            {

                case Key.Left: MoveColumnFocus(-1); break;
                case Key.Right: MoveColumnFocus(1); break;
                case Key.PageUp: ScrollSelectedColumnVertical(true); break;
                case Key.PageDown: ScrollSelectedColumnVertical(false); break;

                // 1-9キー
                case Key.D1: JumpToColumn(0); break;
                case Key.D2: JumpToColumn(1); break;
                case Key.D3: JumpToColumn(2); break;
                case Key.D4: JumpToColumn(3); break;
                case Key.D5: JumpToColumn(4); break;
                case Key.D6: JumpToColumn(5); break;
                case Key.D7: JumpToColumn(6); break;
                case Key.D8: JumpToColumn(7); break;
                case Key.D9: JumpToColumn(8); break;
                case Key.NumPad1: JumpToColumn(0); break;
                case Key.NumPad2: JumpToColumn(1); break;
                case Key.NumPad3: JumpToColumn(2); break;
                case Key.NumPad4: JumpToColumn(3); break;
                case Key.NumPad5: JumpToColumn(4); break;
                case Key.NumPad6: JumpToColumn(5); break;
                case Key.NumPad7: JumpToColumn(6); break;
                case Key.NumPad8: JumpToColumn(7); break;
                case Key.NumPad9: JumpToColumn(8); break;
                default: handled = false; break;
            }

            if (handled) e.Handled = true;
        }

        /// <summary>
        /// 現在画面の中央付近にある（メインで見ている）カラムを特定し、
        /// そのカラムのWebViewに対してスクロール命令を送ります。
        /// </summary>
        /// <param name="scrollDown">trueなら下へ、falseなら上へスクロール</param>
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

            // カラムのインデックスを計算
            // (UniformGrid使用時など、ColumnWidthが可変の場合はロジック調整が必要ですが、基本はこれで動作します)
            int index = -1;
            if (UseUniformGrid)
            {
                // 等分割モードの場合
                double widthPerCol = scrollViewer.ViewportWidth / Columns.Count;
                index = (int)(centerOffset / widthPerCol); // 単純化
                // UniformGridの場合はスクロールバーが出ない設定が多いので、
                // 実際にはマウスカーソル下のカラムを判定するのが理想的ですが、
                // ここでは「画面内の全カラム」または「最初のカラム」に送る簡易実装とします。
                // 多くの場合は1画面に収まっているので、全カラムスクロールでも違和感は少ないかもしれません。
                // 今回は「一番左（0番目）」または「フォーカスがあるカラム」を対象とします。
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
            current.DisableFocusModeOnTweetClick = _disableFocusModeOnTweetClick;

            current.AddColumnToLeft = _addColumnToLeft;

            current.ColumnWidth = ColumnWidth;
            current.UseUniformGrid = UseUniformGrid;
            current.HideRightSidebar = _hideRightSidebar;

            current.AppFontFamily = _appFontFamily;
            current.AppFontSize = _appFontSize;

            current.AppTheme = _appTheme;

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

                // 設定画面から戻ってきた値を変数に保存
                _appTheme = newSettings.AppTheme;
                _useSoftRefresh = newSettings.UseSoftRefresh;
                _enableWindowSnap = newSettings.EnableWindowSnap;
                _customCss = newSettings.CustomCss;
                _disableFocusModeOnMediaClick = newSettings.DisableFocusModeOnMediaClick;
                _disableFocusModeOnTweetClick = newSettings.DisableFocusModeOnTweetClick;


                _addColumnToLeft = newSettings.AddColumnToLeft;

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

                // テーマの適用
                ApplyTheme(_appTheme);

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
                if (MessageWindow.Show(this,"拡張機能の設定を変更しました。\n反映するにはアプリの再起動が必要です。\n今すぐ再起動しますか？",
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
        /// カラムごとの「リプライ非表示」チェックボックスクリック時の処理
        /// </summary>
        private void ReplyHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox chk && chk.Tag is ColumnData col)
            {
                // 設定を保存し、CSSを即時再適用
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    // 既にロード済みのページに対してCSSを適用しなおす（表示/非表示の切り替え）
                    // ※WebView.cs側の ApplyCustomCss を public にするか、
                    // 内部的に呼び出せるようにしておく必要があります。
                    // ここでは既存の RetweetHidden_Click と同様の実装を想定しています。

                    // ★注意: partial classで分かれているため、このメソッドから ApplyCustomCss を呼ぶには
                    // ApplyCustomCss のアクセス修飾子を private から internal または public に変更するか、
                    // 同等の処理を行ってください。
                    ApplyCssToAllColumns(); // 簡易的に全適用でも可
                }
                SaveSettings(_activeProfileName);
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
                MessageWindow.Show($"初期化エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>
        /// アプリ終了
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// バージョン情報表示 (簡易)
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageWindow.Show($"{this.Title}\n\n快適なXライフを。", "バージョン情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// プロファイルメニューが開かれたときに、プロファイル一覧を動的に生成します。
        /// </summary>
        private void MenuProfile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // 区切り線より下のアイテム（以前に動的追加されたプロファイル項目）をクリア
            // ※XAML上の構成: [0]新規, [1]管理, [2]Separator, [3]以降がプロファイル一覧と仮定
            while (MenuProfile.Items.Count > 3)
            {
                MenuProfile.Items.RemoveAt(3);
            }

            // _profileNamesは ObservableCollection<ProfileItem> であると想定
            // MainWindow.Profiles.cs 等で定義されているコレクションを使用
            if (_profileNames != null)
            {
                foreach (var profile in _profileNames)
                {
                    var menuItem = new MenuItem
                    {
                        Header = profile.Name,
                        IsCheckable = true,
                        IsChecked = profile.IsActive, // 現在のプロファイルならチェック
                        Tag = profile
                    };
                    menuItem.Click += OnProfileMenuItemClick;
                    MenuProfile.Items.Add(menuItem);
                }
            }
        }

        /// <summary>
        /// メニューからプロファイルが選択されたときの処理
        /// </summary>
        private void OnProfileMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ProfileItem selectedProfile)
            {
                if (selectedProfile.Name == _activeProfileName) return; // 同じなら何もしない

                // プロファイル切り替え確認
                if (MessageWindow.Show($"プロファイルを「{selectedProfile.Name}」に切り替えますか？\n(アプリが再起動します)",
                    "プロファイル切り替え", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 既存の切り替えロジックを利用
                    PerformProfileSwitch(selectedProfile.Name);
                }
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

        /// <summary>
        /// カラムフォーカスを隣へ移動させます。
        /// </summary>
        /// <param name="direction">移動方向 (-1: 左, 1: 右)</param>
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
                // 1. WebViewにフォーカスを当てる (これでキー入力がそちらへ移ります)
                targetColumn.AssociatedWebView.Focus();

                // 2. そのカラムが見える位置までスクロールする
                // (ItemsControl内の要素を特定して BringIntoView するのが理想ですが、簡易的にスクロール計算します)
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
        /// 指定したインデックス（0始まり）のカラムに直接フォーカスを移動します。
        /// キーボードショートカット '1' -> 0, '2' -> 1 ... から呼び出されます。
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
        /// カラム内を垂直スクロールします。
        /// </summary>
        /// <param name="up"></param>
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

                // WebViewにフォーカスを移動（これにより ←→ キーや PageUp/Down が即座に効くようになります）
                col.AssociatedWebView?.Focus();
            }
        }
    }
}