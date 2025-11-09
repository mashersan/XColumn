using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;    // List
using System.Collections.ObjectModel; // ObservableCollection
using System.ComponentModel;       // INotifyPropertyChanged
using System.Diagnostics;          // Debug
using System.IO;                   // Path, File
using System.Linq;                 // Linq (Select, Any)
using System.Net;                  // WebUtility
using System.Runtime.CompilerServices; // CallerMemberName
using System.Text.Json;            // JsonSerializer
using System.Text.Json.Serialization; // JsonIgnore
using System.Threading.Tasks;      // Task
using System.Windows;
using System.Windows.Controls;     // Button
using System.Windows.Media;        // VisualTreeHelper
using System.Windows.Threading;    // DispatcherTimer

namespace TweetDesk
{
    /// <summary>
    /// 各カラムのデータを表現し、自動更新タイマーを管理するクラス
    /// INotifyPropertyChangedを実装し、UIの変更通知をサポート
    /// </summary>
    public class ColumnData : INotifyPropertyChanged
    {
        /// <summary>
        /// 削除時にカラムを特定するための一意のID
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _url = "";
        /// <summary>
        /// このカラムが現在表示しているURL
        /// </summary>
        public string Url
        {
            get => _url;
            set { SetField(ref _url, value); }
        }

        private int _refreshIntervalSeconds = 300;
        /// <summary>
        /// 自動更新の間隔（秒）
        /// </summary>
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetField(ref _refreshIntervalSeconds, value))
                {
                    UpdateTimer();
                }
            }
        }

        private bool _isAutoRefreshEnabled = false;
        /// <summary>
        /// このカラムで自動更新が有効かどうか
        /// </summary>
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetField(ref _isAutoRefreshEnabled, value))
                {
                    UpdateTimer();
                }
            }
        }

        private int _remainingSeconds;
        /// <summary>
        /// 次の更新までの残り秒数（UI表示用）
        /// </summary>
        [JsonIgnore]
        public int RemainingSeconds
        {
            get => _remainingSeconds;
            set
            {
                if (SetField(ref _remainingSeconds, value))
                {
                    UpdateCountdownText();
                }
            }
        }

        private string _countdownText = "";
        /// <summary>
        /// UIに表示するカウントダウン文字列（例: "(4:59)"）
        /// </summary>
        [JsonIgnore]
        public string CountdownText
        {
            get => _countdownText;
            private set => SetField(ref _countdownText, value);
        }

        /// <summary>
        /// このカラム専用のリロード用タイマー
        /// </summary>
        [JsonIgnore]
        public DispatcherTimer? Timer { get; private set; }

        /// <summary>
        /// このデータに紐づくWebView2コントロールのインスタンス
        /// </summary>
        [JsonIgnore]
        public Microsoft.Web.WebView2.Wpf.WebView2? AssociatedWebView { get; set; }

        /// <summary>
        /// タイマーを初期化し、設定に基づいて開始または停止する
        /// </summary>
        public void InitializeTimer()
        {
            Timer = new DispatcherTimer();
            Timer.Tick += (sender, e) => ReloadWebView();
            UpdateTimer();
        }

        /// <summary>
        /// 関連付けられたWebViewをリロードし、カウントダウンをリセットする
        /// </summary>
        public void ReloadWebView()
        {
            if (AssociatedWebView != null && AssociatedWebView.CoreWebView2 != null)
            {
                try
                {
                    AssociatedWebView.CoreWebView2.Reload();
                    Debug.WriteLine($"[ColumnData] Reloaded: {Url}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ColumnData] Reload failed for {Url}: {ex.Message}");
                }
            }
            ResetCountdown();
        }

        /// <summary>
        /// 現在の設定（有効/無効、間隔）に基づき、タイマーを更新（再起動または停止）する
        /// </summary>
        public void UpdateTimer()
        {
            if (Timer == null) return;

            Timer.Stop();
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                Timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
                Timer.Start();
                ResetCountdown();
                Debug.WriteLine($"[ColumnData] Timer started for {Url}. Interval: {RefreshIntervalSeconds}s");
            }
            else
            {
                Debug.WriteLine($"[ColumnData] Timer stopped for {Url}.");
                RemainingSeconds = 0;
            }
        }

        /// <summary>
        /// カウントダウンの秒数をリセットする
        /// </summary>
        public void ResetCountdown()
        {
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                RemainingSeconds = RefreshIntervalSeconds;
            }
            else
            {
                RemainingSeconds = 0;
            }
        }

        /// <summary>
        /// UI用のカウントダウン文字列 (m:ss) を更新する
        /// </summary>
        private void UpdateCountdownText()
        {
            if (!IsAutoRefreshEnabled || RemainingSeconds <= 0)
            {
                CountdownText = "";
            }
            else
            {
                var timeSpan = TimeSpan.FromSeconds(RemainingSeconds);
                CountdownText = $"({timeSpan:m\\:ss})";
            }
        }

        /// <summary>
        /// カラム削除時に、タイマーを安全に停止・破棄する
        /// </summary>
        public void StopAndDisposeTimer()
        {
            if (Timer != null)
            {
                Timer.Stop();
                Timer.Tick -= (sender, e) => ReloadWebView();
                Timer = null;
            }
            RemainingSeconds = 0;
            AssociatedWebView = null;
        }

        #region INotifyPropertyChanged 実装

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// プロパティの値を設定し、変更があればUIに通知する
        /// </summary>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        #endregion
    }

    /// <summary>
    /// settings.json に保存するすべての設定
    /// </summary>
    public class AppSettings
    {
        public List<ColumnData> Columns { get; set; } = new List<ColumnData>();
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowHeight { get; set; } = 800;
        public double WindowWidth { get; set; } = 1200;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public bool IsFocusMode { get; set; } = false;
        public string? FocusUrl { get; set; } = null;
    }


    /// <summary>
    /// メインウィンドウのロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        #region --- メンバー変数 ---

        /// <summary>
        /// UIにバインドされているカラムのコレクション
        /// </summary>
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        /// <summary>
        /// 全てのWebViewで共有されるブラウザ環境（Cookie, ログイン情報など）
        /// </summary>
        private CoreWebView2Environment? _webViewEnvironment;

        /// <summary>
        /// 全カラムのカウントダウンを1秒ずつ進めるためのグローバルタイマー
        /// </summary>
        private readonly DispatcherTimer _countdownTimer;

        /// <summary>
        /// 現在フォーカスモード（単一カラム表示）かどうか
        /// </summary>
        private bool _isFocusMode = false;

        private readonly string _userDataFolder;
        private readonly string _settingsFilePath;

        // デフォルトURLの定数
        private const string DefaultHomeUrl = "https://x.com/home";
        private const string DefaultNotifyUrl = "https://x.com/notifications";
        private const string SearchUrlFormat = "https://x.com/search?q={0}";
        private const string ListUrlFormat = "https://x.com/i/lists/{0}";

        #endregion

        #region --- コンストラクタとウィンドウイベント ---

        public MainWindow()
        {
            // XAMLとC#を接続
            InitializeComponent();

            // ItemsControlのデータソースを 'Columns' プロパティに設定
            ColumnItemsControl.ItemsSource = Columns;

            // 1秒ごとのグローバルカウントダウンタイマーを初期化
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // ユーザー設定の保存先パスを決定
            _userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TweetDesk");
            _settingsFilePath = Path.Combine(_userDataFolder, "settings.json");

            // ウィンドウが閉じるときに設定を保存するイベントを登録
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// ウィンドウ読み込み時: 設定を復元し、WebView環境を準備する
        /// </summary>
        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 設定ファイルを読み込み、ウィンドウサイズとフォーカス状態を取得
                AppSettings settings = LoadSettings();

                // 2. WebViewの環境を非同期で初期化 (Cookie保存フォルダを指定)
                Directory.CreateDirectory(_userDataFolder);
                var options = new CoreWebView2EnvironmentOptions();
                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);

                // 3. フォーカス用WebViewを初期化
                await InitializeFocusWebView();

                // 4. 起動時にフォーカスモードだったか確認
                if (settings.IsFocusMode && !string.IsNullOrEmpty(settings.FocusUrl))
                {
                    EnterFocusMode(settings.FocusUrl);
                }

                // 5. 保存されていたカラムを（裏側で）復元
                LoadColumnsFromSettings(settings);

                // 6. グローバルカウントダウンタイマーを開始
                _countdownTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView環境の重大な初期化エラー: {ex.Message}", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ウィンドウが閉じるとき: 設定を保存し、タイマーを破棄する
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveSettings();

            _countdownTimer.Stop(); // グローバルタイマーを停止
            foreach (var col in Columns)
            {
                col.StopAndDisposeTimer(); // 各カラムのタイマーも破棄
            }
        }

        #endregion

        #region --- カラム管理 (追加・削除) ---

        /// <summary>
        /// データコレクションに新しいカラムを追加する
        /// </summary>
        private void AddNewColumn(string url)
        {
            Columns.Add(new ColumnData { Url = url });
        }

        private void AddHome_Click(object? sender, RoutedEventArgs e)
        {
            AddNewColumn(DefaultHomeUrl);
        }

        private void AddNotify_Click(object? sender, RoutedEventArgs e)
        {
            AddNewColumn(DefaultNotifyUrl);
        }

        private void AddSearch_Click(object? sender, RoutedEventArgs e)
        {
            string? keyword = ShowInputWindow("検索", "検索キーワードを入力してください:");
            if (!string.IsNullOrEmpty(keyword))
            {
                string encodedKeyword = WebUtility.UrlEncode(keyword);
                AddNewColumn(string.Format(SearchUrlFormat, encodedKeyword));
            }
        }

        private void AddList_Click(object? sender, RoutedEventArgs e)
        {
            string? input = ShowInputWindow("リストの追加", "リストID (数字のみ) または リストの完全なURLを入力してください:");
            if (string.IsNullOrEmpty(input)) return;

            if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                AddNewColumn(input);
            }
            else if (long.TryParse(input, out _))
            {
                AddNewColumn(string.Format(ListUrlFormat, input));
            }
            else
            {
                MessageBox.Show("入力形式が正しくありません。\nリストID（数字のみ）か、完全なURLを入力してください。", "入力エラー");
            }
        }

        /// <summary>
        /// カラムの「✖」ボタンが押された時
        /// </summary>
        private void DeleteColumn_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ColumnData columnData)
            {
                columnData.StopAndDisposeTimer(); // タイマーを安全に停止
                Columns.Remove(columnData); // コレクションから削除
            }
        }

        /// <summary>
        /// 検索/リスト追加用の入力ダイアログを表示する
        /// </summary>
        private string? ShowInputWindow(string title, string prompt)
        {
            var dialog = new InputWindow(title, prompt)
            {
                Owner = this // このウィンドウを親として中央に表示
            };

            if (dialog.ShowDialog() == true) // OKが押されたら
            {
                return dialog.InputText?.Trim();
            }
            return null; // キャンセルされたら
        }

        #endregion

        #region --- WebView 初期化 & フォーカス処理 ---

        /// <summary>
        /// XAMLでWebView2コントロールがロードされたときに呼ばれる
        /// (堅牢な CoreWebView2InitializationCompleted 方式を採用)
        /// </summary>
        private void WebView_Loaded(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;

            if (_webViewEnvironment == null)
            {
                // 環境がまだ準備できていない（起動直後など）。
                // 準備ができたら再度 WebView_Loaded が呼ばれることを期待して、今は何もしない。
                Debug.WriteLine("[WebView_Loaded] WebView Environment is not ready. Aborting (will retry).");
                return;
            }

            if (webView.CoreWebView2 != null)
            {
                // 既に初期化済み（ドラッグ＆ドロップでの再ロード時など）
                return;
            }

            // 初期化完了イベントを登録
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

            // 初期化を開始（await しない）
            // 共有環境（Cookie）を使う
            _ = webView.EnsureCoreWebView2Async(_webViewEnvironment);
        }

        /// <summary>
        /// WebViewの初期化が「本当に」完了したときに呼ばれる
        /// </summary>
        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;

            // イベントハンドラを解除 (二重実行防止)
            webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;

            if (!e.IsSuccess)
            {
                Debug.WriteLine($"[CoreWebView2Init] Initialization Failed: {e.InitializationException.Message}");
                return;
            }

            // このUI (WebView) に紐づくデータ (ColumnData) を取得
            if (!(webView.DataContext is ColumnData columnData))
            {
                Debug.WriteLine("[CoreWebView2Init] DataContext is not ColumnData.");
                return;
            }

            Debug.WriteLine($"[CoreWebView2Init] Success for {columnData.Url}. Setting up Core...");

            try
            {
                if (webView.CoreWebView2 == null) return;

                // WebViewの設定
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // ColumnData にこの WebView インスタンスを関連付ける
                columnData.AssociatedWebView = webView;

                // このカラム専用のタイマーを初期化（＆必要なら開始）
                columnData.InitializeTimer();

                // JSによるページ内遷移（URL変更）を検知し、ColumnData.Url を更新する
                webView.CoreWebView2.SourceChanged += (coreSender, args) =>
                {
                    var coreWebView = coreSender as CoreWebView2;
                    if (coreWebView != null)
                    {
                        string newUrl = coreWebView.Source;

                        // 「status」URLでない場合のみ、カラムのURLとして保存する
                        if (!newUrl.Contains("/status/"))
                        {
                            columnData.Url = newUrl;
                            Debug.WriteLine($"[Column_SourceChanged] Column URL updated to: {columnData.Url}");
                        }
                        else
                        {
                            // 「status」URLの場合は、フォーカスモードに入る
                            if (!_isFocusMode)
                            {
                                Debug.WriteLine($"[Column_SourceChanged] Entering Focus Mode for: {newUrl}");
                                EnterFocusMode(newUrl);
                            }
                        }
                    }
                };

                // 保存されていたURL（または追加時のURL）に移動
                webView.CoreWebView2.Navigate(columnData.Url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreWebView2Init] Error during setup: {ex.Message}");
            }
        }


        /// <summary>
        /// フォーカスモード用のWebViewを初期化
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            if (FocusWebView == null) return;

            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            if (FocusWebView.CoreWebView2 != null)
            {
                // フォーカスWebViewのURLが変わったら
                FocusWebView.CoreWebView2.SourceChanged += (sender, args) =>
                {
                    var coreWebView = sender as CoreWebView2;
                    if (coreWebView == null) return;

                    string newUrl = coreWebView.Source;
                    Debug.WriteLine($"[Focus_SourceChanged] Focus URL updated to: {newUrl}");

                    // 「status」URL *以外* に移動したら、カラム一覧に戻る
                    if (_isFocusMode &&
                        !newUrl.Contains("/status/") &&
                        !newUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                    {
                        ExitFocusMode();
                    }
                };
            }
        }

        /// <summary>
        /// フォーカスモードに入り、UIを切り替える
        /// </summary>
        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url);

            // UIを切り替え
            ColumnItemsControl.Visibility = Visibility.Collapsed;
            FocusViewGrid.Visibility = Visibility.Visible;

            // 全カラムのタイマーを一時停止
            foreach (var col in Columns) col.Timer?.Stop();
        }

        /// <summary>
        /// フォーカスモードを終了し、UIを元に戻す
        /// </summary>
        private void ExitFocusMode()
        {
            _isFocusMode = false;

            // UIを切り替え
            FocusViewGrid.Visibility = Visibility.Collapsed;
            ColumnItemsControl.Visibility = Visibility.Visible;

            FocusWebView?.CoreWebView2?.Navigate("about:blank");

            // 全カラムのタイマーを再開（設定が有効なもの）
            foreach (var col in Columns) col.UpdateTimer();
        }

        /// <summary>
        /// 「カラム一覧に戻る」ボタンのクリック
        /// </summary>
        private void CloseFocusView_Click(object? sender, RoutedEventArgs e)
        {
            ExitFocusMode();
        }

        #endregion

        #region --- カウントダウン & 手動更新 ---

        /// <summary>
        /// 1秒ごとに呼ばれ、自動更新が有効な全カラムの残り秒数を減らす
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            // フォーカスモード中はカウントダウンしない
            if (_isFocusMode) return;

            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                {
                    column.RemainingSeconds--; // 1秒減らす (UIはColumnData側で自動更新)
                }
            }
        }

        /// <summary>
        /// カラムの「↻」ボタン（手動更新）が押された時
        /// </summary>
        private void ColumnManualRefresh_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ColumnData columnData)
            {
                columnData.ReloadWebView(); // これが自動でカウントダウンもリセットする
            }
        }

        #endregion

        #region --- 状態の保存と復元 ---

        /// <summary>
        /// ウィンドウの状態と、全カラムのデータ(URL, 更新設定)をJSONファイルに保存する
        /// </summary>
        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                Columns = new List<ColumnData>(Columns),
                IsFocusMode = _isFocusMode
            };

            // フォーカスモードだった場合、そのURLも保存
            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                // フォーカスモードでない場合、IsFocusModeをfalseにし、FocusUrlをnullクリアする
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            // ウィンドウの状態を保存
            if (this.WindowState == WindowState.Maximized)
            {
                settings.WindowState = WindowState.Maximized;
                // 最大化時は、復元後のサイズを保存
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowHeight = this.RestoreBounds.Height;
                settings.WindowWidth = this.RestoreBounds.Width;
            }
            else
            {
                // 通常時（または最小化時）は、現在のサイズを保存
                settings.WindowState = WindowState.Normal;
                settings.WindowTop = this.Top;
                settings.WindowLeft = this.Left;
                settings.WindowHeight = this.Height;
                settings.WindowWidth = this.Width;
            }

            try
            {
                // JSONにシリアライズしてファイルに書き込み
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                Debug.WriteLine($"Settings saved to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// JSONファイルから設定を読み込み、ウィンドウの状態を復元する
        /// </summary>
        /// <returns>読み込んだ設定オブジェクト (カラム復元用)</returns>
        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                Debug.WriteLine("No settings file found. Using default settings.");
                return new AppSettings(); // デフォルト設定を返す
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    // ウィンドウのサイズと位置を復元
                    this.Top = settings.WindowTop;
                    this.Left = settings.WindowLeft;
                    this.Height = settings.WindowHeight;
                    this.Width = settings.WindowWidth;
                    this.WindowState = settings.WindowState;

                    // ウィンドウが画面外（例：マルチモニタ環境の変更）に表示されるのを防ぐ
                    ValidateWindowPosition();

                    Debug.WriteLine($"Settings loaded from {_settingsFilePath}");
                    return settings; // 読み込んだ設定を返す
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings (file might be corrupt): {ex.Message}");
            }

            return new AppSettings(); // 失敗したらデフォルト設定を返す
        }

        /// <summary>
        /// 読み込まれた設定オブジェクトからカラムのリストを復元する
        /// (WebView環境の初期化 *後* に呼ばれる)
        /// </summary>
        private void LoadColumnsFromSettings(AppSettings settings)
        {
            try
            {
                if (settings.Columns.Any()) // 引数の settings を使用
                {
                    // 保存されていたカラムデータをそのままUIのコレクションに追加
                    foreach (var columnData in settings.Columns)
                    {
                        Columns.Add(columnData);
                    }
                    Debug.WriteLine($"Columns loaded from settings object.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load column URLs: {ex.Message}");
            }

            // 復元するカラムがなかった場合、デフォルト（ホーム）を追加
            Debug.WriteLine("No columns in settings. Loading default column.");
            AddNewColumn(DefaultHomeUrl);
        }

        /// <summary>
        /// ウィンドウがモニターの表示領域外に復元されるのを防ぐ
        /// </summary>
        private void ValidateWindowPosition()
        {
            // プライマリスクリーン（メインモニター）の作業領域を取得
            var screen = System.Windows.SystemParameters.WorkArea;

            // ウィンドウが画面外（左右）に行っていないか？
            if (this.Left + this.Width < 0 || this.Left > screen.Width)
            {
                this.Left = 100;
            }

            // ウィンドウが画面外（上下）に行っていないか？
            if (this.Top + this.Height < 0 || this.Top > screen.Height)
            {
                this.Top = 100;
            }
        }

        #endregion

        #region --- ヘルパーメソッド ---

        /// <summary>
        /// ビジュアルツリーを探索して、指定した型(T)の最初の子要素を見つける
        /// (例: ItemsControl のコンテナから内部の WebView2 を見つける)
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        #endregion
    }
}