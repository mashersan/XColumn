using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

// アップデートチェックのために追加
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Nodes; // JsonNode のために System.Text.Json.Nodes を使用

// 設定ファイル暗号化 (DPAPI) のために追加
using System.Security.Cryptography;
using System.Text;

namespace XColumn
{
    /// <summary>
    /// 各カラムのデータを表現し、自動更新タイマーを管理するクラスです。
    /// INotifyPropertyChangedを実装し、UIの変更通知（データバインディング）をサポートします。
    /// </summary>
    public class ColumnData : INotifyPropertyChanged
    {
        #region --- Properties ---

        /// <summary>
        /// 削除時などにカラムを一意に特定するためのID。
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _url = "";
        /// <summary>
        /// このカラムが現在表示しているURL。
        /// </summary>
        public string Url
        {
            get => _url;
            set { SetField(ref _url, value); }
        }

        private int _refreshIntervalSeconds = 300;
        /// <summary>
        /// 自動更新の間隔（秒）。
        /// 値が変更されると、タイマー設定も自動的に更新されます。
        /// </summary>
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetField(ref _refreshIntervalSeconds, value))
                {
                    UpdateTimer(); // 値の変更をタイマーに反映
                }
            }
        }

        private bool _isAutoRefreshEnabled = false;
        /// <summary>
        /// このカラムで自動更新が有効かどうか。
        /// 値が変更されると、タイマー設定も自動的に更新されます。
        /// </summary>
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetField(ref _isAutoRefreshEnabled, value))
                {
                    UpdateTimer(); // 値の変更をタイマーに反映
                }
            }
        }

        private int _remainingSeconds;
        /// <summary>
        /// 次の自動更新までの残り秒数（UI表示用）。
        /// </summary>
        [JsonIgnore] // このプロパティは設定ファイルに保存しない
        public int RemainingSeconds
        {
            get => _remainingSeconds;
            set
            {
                if (SetField(ref _remainingSeconds, value))
                {
                    UpdateCountdownText(); // 残り秒数が変わったらUIテキストも更新
                }
            }
        }

        private string _countdownText = "";
        /// <summary>
        /// UIに表示するカウントダウン文字列（例: "(4:59)"）。
        /// </summary>
        [JsonIgnore] // このプロパティは設定ファイルに保存しない
        public string CountdownText
        {
            get => _countdownText;
            private set => SetField(ref _countdownText, value);
        }

        /// <summary>
        /// このカラム専用のリロード（自動更新）用タイマー。
        /// </summary>
        [JsonIgnore]
        public DispatcherTimer? Timer { get; private set; }

        /// <summary>
        /// このデータに紐づくWebView2コントロールのインスタンス。
        /// </summary>
        [JsonIgnore]
        public Microsoft.Web.WebView2.Wpf.WebView2? AssociatedWebView { get; set; }

        #endregion

        #region --- Timer Control ---

        /// <summary>
        /// このカラム用のタイマーを初期化し、現在の設定に基づいて開始または停止します。
        /// </summary>
        public void InitializeTimer()
        {
            Timer = new DispatcherTimer();
            Timer.Tick += (sender, e) => ReloadWebView(); // タイマーのTickイベントでリロード処理を呼ぶ
            UpdateTimer();
        }

        /// <summary>
        /// 関連付けられたWebViewをリロード（F5）し、カウントダウンをリセットします。
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
            ResetCountdown(); // リロードしたらカウントダウンをリセット
        }

        /// <summary>
        /// 現在の設定（有効/無効、間隔）に基づき、タイマーを更新（再起動または停止）します。
        /// </summary>
        public void UpdateTimer()
        {
            if (Timer == null) return;

            Timer.Stop(); // いったんタイマーを停止
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
                RemainingSeconds = 0; // カウントダウンを0に
            }
        }

        /// <summary>
        /// カウントダウンの秒数をリセットします。
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
        /// UI用のカウントダウン文字列 (m:ss形式) を更新します。
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
        /// カラム削除時に、タイマーを安全に停止・破棄します。
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

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// プロパティが変更されたときに発生するイベント。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// プロパティの値を設定し、変更があればUIに通知します。
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
    /// 設定ファイル（settings.dat）に暗号化して保存するすべての設定データを保持するクラスです。
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
        /// <summary>
        /// ユーザーが通知をスキップした最新バージョン番号。
        /// </summary>
        public string SkippedVersion { get; set; } = "0.0.0";
    }


    /// <summary>
    /// メインウィンドウ (MainWindow.xaml) の分離コード（ロジック）。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region --- メンバー変数 ---

        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();
        private CoreWebView2Environment? _webViewEnvironment;
        private readonly DispatcherTimer _countdownTimer;
        private bool _isFocusMode = false;

        /// <summary>
        /// フォーカスモードに入ったときに、元のカラムを記憶するための変数。
        /// </summary>
        private ColumnData? _focusedColumnData = null;

        private readonly string _userDataFolder;
        private readonly string _settingsFilePath;

        /// <summary>
        /// 設定ファイルの暗号化・復号に使用する追加エントロピー。
        /// </summary>
        private static readonly byte[] _entropy = { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E };

        // デフォルトURLの定数
        private const string DefaultHomeUrl = "https://x.com/home";
        private const string DefaultNotifyUrl = "https://x.com/notifications";
        private const string SearchUrlFormat = "https://x.com/search?q={0}";
        private const string ListUrlFormat = "https://x.com/i/lists/{0}";

        #endregion

        #region --- コンストラクタとウィンドウイベント ---

        /// <summary>
        /// MainWindow のコンストラクタ。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            ColumnItemsControl.ItemsSource = Columns;

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            _settingsFilePath = Path.Combine(_userDataFolder, "settings.dat");

            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// ウィンドウの読み込みが完了したときに呼ばれるイベントハンドラ。
        /// </summary>
        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 設定ファイル (settings.dat) を復号して読み込み
                AppSettings settings = LoadSettings();

                // 2. WebViewの共有環境を非同期で初期化
                Directory.CreateDirectory(_userDataFolder);
                var options = new CoreWebView2EnvironmentOptions();
                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);

                // 3. フォーカスモード用のWebViewを初期化
                await InitializeFocusWebView();

                // 4. 起動時にフォーカスモードだったか確認
                if (settings.IsFocusMode && !string.IsNullOrEmpty(settings.FocusUrl))
                {
                    if (IsAllowedDomain(settings.FocusUrl, allowFocusRelatedLinks: true))
                    {
                        EnterFocusMode(settings.FocusUrl);
                    }
                }

                // 5. 保存されていたカラムを復元
                LoadColumnsFromSettings(settings);

                // 6. グローバルカウントダウンタイマーを開始
                _countdownTimer.Start();

                // 7. 起動時にアップデートを非同期で確認
                _ = CheckForUpdatesAsync(settings.SkippedVersion);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView環境の重大な初期化エラー: {ex.Message}\n\nWebView2ランタイムがインストールされているか確認してください。", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ウィンドウが閉じるときに呼ばれるイベントハンドラ。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveSettings();
            _countdownTimer.Stop();
            foreach (var col in Columns)
            {
                col.StopAndDisposeTimer();
            }
        }

        #endregion

        #region --- カラム管理 (追加・削除) ---

        /// <summary>
        /// データコレクション (ObservableCollection) に新しいカラムを追加します。
        /// </summary>
        private void AddNewColumn(string url)
        {
            if (IsAllowedDomain(url))
            {
                Columns.Add(new ColumnData { Url = url });
            }
            else
            {
                Debug.WriteLine($"[AddNewColumn] Blocked adding external URL: {url}");
                MessageBox.Show("許可されていないドメインのURLは追加できません。", "エラー");
            }
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
                if (IsAllowedDomain(input))
                {
                    AddNewColumn(input);
                }
                else
                {
                    MessageBox.Show("許可されていないドメインのURLです。\nx.com または twitter.com のURLのみ追加できます。", "入力エラー");
                }
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
        /// カラムヘッダーの「✖」（削除）ボタンが押された時のイベントハンドラ。
        /// </summary>
        private void DeleteColumn_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ColumnData columnData)
            {
                columnData.StopAndDisposeTimer();
                Columns.Remove(columnData);
            }
        }

        /// <summary>
        /// 検索/リスト追加用のシンプルな入力ダイアログ (InputWindow) を表示します。
        /// </summary>
        private string? ShowInputWindow(string title, string prompt)
        {
            var dialog = new InputWindow(title, prompt) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                return dialog.InputText?.Trim();
            }
            return null;
        }

        #endregion

        #region --- WebView 初期化 & フォーカス処理 ---

        /// <summary>
        /// XAMLのItemsControl内でWebView2コントロールがロードされたときに呼ばれます。
        /// </summary>
        private void WebView_Loaded(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;
            if (_webViewEnvironment == null)
            {
                Debug.WriteLine("[WebView_Loaded] WebView Environment is not ready. Aborting (will retry).");
                return;
            }
            if (webView.CoreWebView2 != null)
            {
                return; // 既に初期化済み
            }

            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            _ = webView.EnsureCoreWebView2Async(_webViewEnvironment);
        }

        /// <summary>
        /// WebViewのCoreWebView2プロパティの初期化が完了したときに呼ばれます。
        /// </summary>
        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;

            webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;

            if (!e.IsSuccess)
            {
                Debug.WriteLine($"[CoreWebView2Init] Initialization Failed: {e.InitializationException.Message}");
                return;
            }

            if (!(webView.DataContext is ColumnData columnData))
            {
                Debug.WriteLine("[CoreWebView2Init] DataContext is not ColumnData.");
                return;
            }

            Debug.WriteLine($"[CoreWebView2Init] Success for {columnData.Url}. Setting up Core...");

            try
            {
                if (webView.CoreWebView2 == null) return;

                // WebViewの各種設定（セキュリティ対策）
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                columnData.AssociatedWebView = webView;
                columnData.InitializeTimer();

                // ★ リンククリックで新しいウィンドウが開かれるリクエストをハンドル
                webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // WebView内でページ遷移（URL変更）が発生したときのイベント
                webView.CoreWebView2.SourceChanged += (coreSender, args) =>
                {
                    var coreWebView = coreSender as CoreWebView2;
                    if (coreWebView != null)
                    {
                        string newUrl = coreWebView.Source;

                        // ツイート詳細や返信画面か？ (フォーカス対象か)
                        if (IsAllowedDomain(newUrl, allowFocusRelatedLinks: true))
                        {
                            if (!_isFocusMode)
                            {
                                Debug.WriteLine($"[Column_SourceChanged] Entering Focus Mode for: {newUrl}");
                                _focusedColumnData = columnData; // フォーカス元のカラムを記憶
                                EnterFocusMode(newUrl);
                            }
                        }
                        // 許可されたドメイン（タイムラインなど）か？
                        else if (IsAllowedDomain(newUrl))
                        {
                            // カラムのURLとして保存する（フィッシング対策）
                            columnData.Url = newUrl;
                            Debug.WriteLine($"[Column_SourceChanged] Column URL updated to: {columnData.Url}");
                        }
                        else
                        {
                            // 許可されないドメイン（外部サイト）の場合はURLを保存しない
                            Debug.WriteLine($"[Column_SourceChanged] Blocked saving external URL: {newUrl}");
                        }
                    }
                };

                webView.CoreWebView2.Navigate(columnData.Url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreWebView2Init] Error during setup: {ex.Message}");
            }
        }


        /// <summary>
        /// フォーカスモード（単一表示）用のWebView (FocusWebView) を初期化します。
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            if (FocusWebView == null) return;
            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            if (FocusWebView.CoreWebView2 != null)
            {
                // ★ フォーカス用WebViewでもリンククリックをハンドル
                FocusWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // フォーカス用WebViewでURLが変更されたときのイベント
                FocusWebView.CoreWebView2.SourceChanged += (sender, args) =>
                {
                    var coreWebView = sender as CoreWebView2;
                    if (coreWebView == null) return;

                    string newUrl = coreWebView.Source;
                    Debug.WriteLine($"[Focus_SourceChanged] Focus URL updated to: {newUrl}");

                    // ツイート詳細・返信・インテント画面でなければフォーカス解除
                    if (_isFocusMode &&
                        !IsAllowedDomain(newUrl, allowFocusRelatedLinks: true) &&
                        !newUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[Focus_SourceChanged] Exiting Focus Mode (navigated away): {newUrl}");
                        ExitFocusMode();
                    }
                };
            }
        }

        /// <summary>
        /// WebView内で新しいウィンドウを開くリクエスト（target="_blank"など）をインターセプトします。
        /// 既定のブラウザで開くように変更します。
        /// </summary>
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // 1. WebView2が新しいウィンドウ（ポップアップ）を開くのをキャンセルする
            e.Handled = true;

            // 2. リンク先URLを取得
            string url = e.Uri;

            // 3. 既定のブラウザ（Chrome, Edgeなど）でURLを開く
            try
            {
                // UseShellExecute = true が既定のブラウザで開くための鍵
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NewWindowRequested] Failed to open URL in default browser: {ex.Message}");
                MessageBox.Show($"既定のブラウザでリンクを開けませんでした。\nURL: {url}", "エラー");
            }
        }

        /// <summary>
        /// フォーカスモードに入り、UI（Gridの表示/非表示）を切り替えます。
        /// </summary>
        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url);

            ColumnItemsControl.Visibility = Visibility.Collapsed;
            FocusViewGrid.Visibility = Visibility.Visible;

            foreach (var col in Columns) col.Timer?.Stop();
        }

        /// <summary>
        /// フォーカスモードを終了し、UIをカラム一覧に戻します。
        /// </summary>
        private void ExitFocusMode()
        {
            _isFocusMode = false;

            FocusViewGrid.Visibility = Visibility.Collapsed;
            ColumnItemsControl.Visibility = Visibility.Visible;

            FocusWebView?.CoreWebView2?.Navigate("about:blank");

            // フォーカスモードに入る前の元のカラムを、元のURLにナビゲートし直す
            if (_focusedColumnData != null && _focusedColumnData.AssociatedWebView?.CoreWebView2 != null)
            {
                try
                {
                    // Reload()ではなく、保存されている元のURL (例: /home) に Navigate() し直す
                    _focusedColumnData.AssociatedWebView.CoreWebView2.Navigate(_focusedColumnData.Url);
                    _focusedColumnData.ResetCountdown(); // カウントダウンもリセット
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ExitFocusMode] Failed to navigate original column: {ex.Message}");
                }
                _focusedColumnData = null; // 記憶をクリア
            }

            // 全カラムのタイマーを（設定に従って）再開
            foreach (var col in Columns) col.UpdateTimer();
        }

        /// <summary>
        /// 「カラム一覧に戻る」ボタンのクリックイベント。
        /// </summary>
        private void CloseFocusView_Click(object? sender, RoutedEventArgs e)
        {
            ExitFocusMode();
        }

        #endregion

        #region --- カウントダウン & 手動更新 ---

        /// <summary>
        /// グローバルタイマーにより1秒ごとに呼ばれ、自動更新が有効な全カラムの残り秒数を減らします。
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (_isFocusMode) return; // フォーカスモード中はカウントダウン停止

            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                {
                    column.RemainingSeconds--;
                }
            }
        }

        /// <summary>
        /// カラムヘッダーの「↻」（手動更新）ボタンが押された時のイベントハンドラ。
        /// </summary>
        private void ColumnManualRefresh_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ColumnData columnData)
            {
                columnData.ReloadWebView();
            }
        }

        #endregion

        #region --- 状態の保存と復元 (暗号化対応) ---

        /// <summary>
        /// ウィンドウの状態と、全カラムのデータ(URL, 更新設定)を
        /// DPAPIで暗号化してバイナリファイルに保存します。
        /// </summary>
        private void SaveSettings()
        {
            // まず現在の設定を読み込む（SkippedVersionを引き継ぐため）
            AppSettings settings = LoadSettings();

            // 現在のウィンドウ状態とカラムで設定を上書き
            settings.Columns = new List<ColumnData>(Columns);
            settings.IsFocusMode = _isFocusMode;

            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            if (this.WindowState == WindowState.Maximized)
            {
                settings.WindowState = WindowState.Maximized;
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowHeight = this.RestoreBounds.Height;
                settings.WindowWidth = this.RestoreBounds.Width;
            }
            else
            {
                settings.WindowState = WindowState.Normal;
                settings.WindowTop = this.Top;
                settings.WindowLeft = this.Left;
                settings.WindowHeight = this.Height;
                settings.WindowWidth = this.Width;
            }

            try
            {
                // 1. JSONにシリアライズ
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = false });

                // 2. JSON文字列をバイト配列に変換し、DPAPIで暗号化
                byte[] jsonData = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(jsonData, _entropy, DataProtectionScope.CurrentUser);

                // 3. 暗号化されたバイト配列をファイルに書き込み
                File.WriteAllBytes(_settingsFilePath, encryptedData);
                Debug.WriteLine($"Settings encrypted and saved to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 暗号化された設定ファイルを読み込み、DPAPIで復号してウィンドウの状態を復元します。
        /// </summary>
        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                Debug.WriteLine("No settings file found. Using default settings.");
                return new AppSettings();
            }

            try
            {
                byte[] encryptedData = File.ReadAllBytes(_settingsFilePath);
                byte[] jsonData = ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(jsonData);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    this.Top = settings.WindowTop;
                    this.Left = settings.WindowLeft;
                    this.Height = settings.WindowHeight;
                    this.Width = settings.WindowWidth;
                    this.WindowState = settings.WindowState;

                    ValidateWindowPosition();

                    Debug.WriteLine($"Settings loaded and decrypted from {_settingsFilePath}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load/decrypt settings: {ex.Message}");
                if (ex is JsonException || ex is CryptographicException)
                {
                    MessageBox.Show("設定ファイルの読み込みに失敗しました。ファイルが破損しているか、アクセス許可がありません。設定をリセットします。", "設定読み込みエラー");
                }
            }

            return new AppSettings(); // 失敗したらデフォルト設定を返す
        }

        /// <summary>
        /// 読み込まれた設定オブジェクトからカラムのリストを復元します。
        /// </summary>
        private void LoadColumnsFromSettings(AppSettings settings)
        {
            bool loadedColumn = false;
            try
            {
                if (settings.Columns.Any())
                {
                    foreach (var columnData in settings.Columns)
                    {
                        // (セキュリティ対策) 読み込んだカラムURLも検証してから追加する
                        if (IsAllowedDomain(columnData.Url))
                        {
                            Columns.Add(columnData);
                            loadedColumn = true;
                        }
                        else
                        {
                            Debug.WriteLine($"Skipping non-allowed column URL from settings: {columnData.Url}");
                        }
                    }
                    Debug.WriteLine($"Columns loaded from settings object.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load column URLs: {ex.Message}");
            }

            if (!loadedColumn)
            {
                Debug.WriteLine("No valid columns in settings. Loading default column.");
                AddNewColumn(DefaultHomeUrl);
            }
        }

        /// <summary>
        /// ウィンドウがモニターの表示領域外 (オフスクリーン) に復元されるのを防ぎます。
        /// </summary>
        private void ValidateWindowPosition()
        {
            var screen = System.Windows.SystemParameters.WorkArea;
            if (this.Left + this.Width < 0 || this.Left > screen.Width)
            {
                this.Left = 100;
            }
            if (this.Top + this.Height < 0 || this.Top > screen.Height)
            {
                this.Top = 100;
            }
        }

        #endregion

        #region --- ヘルパーメソッド ---

        /// <summary>
        /// 指定されたURLが許可されたドメイン（x.com または twitter.com）かどうかを検証します。
        /// </summary>
        /// <param name="url">検証するURL</param>
        /// <param name="allowFocusRelatedLinks">ツイート詳細 (/status/) や返信 (/compose/) へのリンクを許可するか</param>
        /// <returns>許可されたドメインの場合は true</returns>
        private bool IsAllowedDomain(string url, bool allowFocusRelatedLinks = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false; // スキーマがhttp/httpsでない
            }

            try
            {
                Uri uri = new Uri(url);

                bool isAllowedHost = uri.Host.EndsWith("x.com", StringComparison.OrdinalIgnoreCase) ||
                                     uri.Host.EndsWith("twitter.com", StringComparison.OrdinalIgnoreCase);

                if (!isAllowedHost)
                {
                    return false; // 許可されていないホスト
                }

                // --- ホスト名は許可されている ---

                // パスに "/status/", "/compose/", "/intent/" のいずれかが含まれるか
                bool isFocusRelated = uri.AbsolutePath.Contains("/status/") ||
                                        uri.AbsolutePath.Contains("/compose/") ||
                                        uri.AbsolutePath.Contains("/intent/");

                if (allowFocusRelatedLinks)
                {
                    // フォーカスモード中（または遷移時）は、これらのパスを許可
                    return isFocusRelated;
                }

                // 通常のカラムとして許可する場合（allowFocusRelatedLinks = false）
                return !isFocusRelated;
            }
            catch (UriFormatException ex)
            {
                Debug.WriteLine($"[IsAllowedDomain] Invalid URL format: {url}, {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// ビジュアルツリーを再帰的に探索して、指定した型(T)の最初の子要素を見つけます。
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

        #region --- アップデートチェック ---

        /// <summary>
        /// アプリ起動時に、GitHubリポジトリの最新リリースを非同期で確認します。
        /// </summary>
        /// <param name="skippedVersion">ユーザーが以前にスキップしたバージョン（settings.datから読込）</param>
        private async Task CheckForUpdatesAsync(string skippedVersion)
        {
            await Task.Delay(3000); // 起動処理を妨げないよう、少し待機

            try
            {
                // 1. 現在のアプリのバージョンを取得
                string currentVersionStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                Version currentVersion = new Version(currentVersionStr);
                Debug.WriteLine($"[UpdateCheck] Current version: {currentVersionStr}");

                // 2. GitHub APIで最新リリース情報を取得
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XColumn", currentVersionStr));
                    string apiUrl = "https://api.github.com/repos/mashersan/XColumn/releases/latest";
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[UpdateCheck] Failed to fetch releases: {response.StatusCode}");
                        return; // APIアクセス失敗時はサイレントに終了
                    }

                    string jsonString = await response.Content.ReadAsStringAsync();
                    JsonNode? releaseInfo = JsonNode.Parse(jsonString);

                    if (releaseInfo == null) return;

                    // 3. 最新バージョンタグ、リリースURL、更新内容(Body)を取得
                    string latestVersionTag = releaseInfo["tag_name"]?.GetValue<string>() ?? "v0.0.0";
                    string releaseHtmlUrl = releaseInfo["html_url"]?.GetValue<string>() ?? "";
                    string releaseBody = releaseInfo["body"]?.GetValue<string>() ?? "(更新内容なし)";

                    // "v1.3.0" -> "1.3.0" のように 'v' を取り除く
                    string latestVersionStr = latestVersionTag.StartsWith("v") ? latestVersionTag.Substring(1) : latestVersionTag;
                    Version latestVersion = new Version(latestVersionStr);
                    Debug.WriteLine($"[UpdateCheck] Latest version found: {latestVersionStr}");

                    // 4. バージョン比較
                    if (latestVersion > currentVersion)
                    {
                        // 最新版があり、かつスキップしたバージョンとも異なる場合
                        if (latestVersionStr != skippedVersion)
                        {
                            // メッセージに releaseBody を含める
                            string message = $"新しいバージョン {latestVersionTag} が利用可能です。\n\n" +
                                             $"【更新内容】\n{releaseBody}\n\n" +
                                             $"現在のバージョン: v{currentVersionStr}\n" +
                                             "リリースページに移動しますか？\n\n" +
                                             $"「いいえ」を選択すると、このバージョン ({latestVersionTag}) の通知をスキップします。";

                            // UIスレッドでMessageBoxを表示
                            MessageBoxResult result = MessageBox.Show(this, message, "アップデート通知", MessageBoxButton.YesNo, MessageBoxImage.Information);

                            if (result == MessageBoxResult.Yes)
                            {
                                // 5. 既定のブラウザでリリースページを開く
                                Process.Start(new ProcessStartInfo(releaseHtmlUrl) { UseShellExecute = true });
                            }
                            else if (result == MessageBoxResult.No)
                            {
                                // 6. このバージョンをスキップ設定に保存
                                AppSettings settings = LoadSettings();
                                settings.SkippedVersion = latestVersionStr;
                                SaveSettings(); // SkippedVersion を更新して保存
                                Debug.WriteLine($"[UpdateCheck] Skipped version: {latestVersionStr}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[UpdateCheck] Latest version {latestVersionStr} is already skipped.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[UpdateCheck] You are using the latest version.");
                    }
                }
            }
            catch (Exception ex)
            {
                // APIアクセス失敗、JSONパース失敗、バージョン形式エラーなど
                Debug.WriteLine($"[UpdateCheck] Error checking for updates: {ex.Message}");
            }
        }

        #endregion
    }
}