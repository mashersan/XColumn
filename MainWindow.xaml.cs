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
// 設定ファイルの暗号化・復号（DPAPI）のために追加
using System.Security.Cryptography;
using System.Text;

namespace TweetDesk
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
        /// カラムがロードされた際に MainWindow.xaml.cs によって設定されます。
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
        /// 関連付けられたWebViewをリロードし、カウントダウンをリセットします。
        /// （手動更新・自動更新の両方から呼ばれます）
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
                    // WebViewが破棄された後などにリロードしようとすると例外が起きる可能性がある
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
                // 自動更新が有効で、間隔が0秒より大きい場合
                Timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
                Timer.Start();
                ResetCountdown();
                Debug.WriteLine($"[ColumnData] Timer started for {Url}. Interval: {RefreshIntervalSeconds}s");
            }
            else
            {
                // 自動更新が無効な場合
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
                CountdownText = ""; // 自動更新が無効、または残り0秒なら非表示
            }
            else
            {
                var timeSpan = TimeSpan.FromSeconds(RemainingSeconds);
                CountdownText = $"({timeSpan:m\\:ss})"; // (4:59) のような形式
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
            AssociatedWebView = null; // WebViewとの関連付けも解除
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
        /// <typeparam name="T">プロパティの型</typeparam>
        /// <param name="field">変更対象のフィールド（参照渡し）</param>
        /// <param name="value">新しい値</param>
        /// <param name="propertyName">プロパティ名 (自動補完)</param>
        /// <returns>値が変更された場合は true、されなかった場合は false</returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false; // 同じ値なら何もしない
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); // 変更通知
            return true;
        }

        #endregion
    }

    /// <summary>
    /// 設定ファイル（settings.dat）に暗号化して保存するすべての設定データを保持するクラスです。
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// カラムのリスト（URLや更新設定など）。
        /// </summary>
        public List<ColumnData> Columns { get; set; } = new List<ColumnData>();

        /// <summary>
        /// 終了時のウィンドウの上端位置。
        /// </summary>
        public double WindowTop { get; set; } = 100;

        /// <summary>
        /// 終了時のウィンドウの左端位置。
        /// </summary>
        public double WindowLeft { get; set; } = 100;

        /// <summary>
        /// 終了時のウィンドウの高さ。
        /// </summary>
        public double WindowHeight { get; set; } = 800;

        /// <summary>
        /// 終了時のウィンドウの幅。
        /// </summary>
        public double WindowWidth { get; set; } = 1200;

        /// <summary>
        /// 終了時のウィンドウの状態（最大化、通常など）。
        /// </summary>
        public WindowState WindowState { get; set; } = WindowState.Normal;

        /// <summary>
        /// 終了時にフォーカスモードだったかどうか。
        /// </summary>
        public bool IsFocusMode { get; set; } = false;

        /// <summary>
        /// 終了時にフォーカスモードで表示していたURL。
        /// </summary>
        public string? FocusUrl { get; set; } = null;
    }


    /// <summary>
    /// メインウィンドウ (MainWindow.xaml) の分離コード（ロジック）。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region --- メンバー変数 ---

        /// <summary>
        /// UI (ItemsControl) にバインドされているカラムのコレクション。
        /// </summary>
        public ObservableCollection<ColumnData> Columns { get; } = new ObservableCollection<ColumnData>();

        /// <summary>
        /// 全てのWebViewで共有されるブラウザ環境（Cookie, ログイン情報などを保持）。
        /// </summary>
        private CoreWebView2Environment? _webViewEnvironment;

        /// <summary>
        /// 全カラムのカウントダウンを1秒ずつ進めるためのグローバルタイマー。
        /// </summary>
        private readonly DispatcherTimer _countdownTimer;

        /// <summary>
        /// 現在フォーカスモード（単一カラム表示）かどうか。
        /// </summary>
        private bool _isFocusMode = false;

        /// <summary>
        /// WebView2のユーザーデータ（Cookieなど）を保存するフォルダパス。
        /// </summary>
        private readonly string _userDataFolder;

        /// <summary>
        /// アプリ設定（ウィンドウサイズやカラム情報）を暗号化して保存するファイルパス。
        /// </summary>
        private readonly string _settingsFilePath;

        /// <summary>
        /// 設定ファイルの暗号化・復号に使用する追加エントロピー（固定値でOK）。
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
            // XAMLで定義されたコンポーネントを初期化
            InitializeComponent();

            // ItemsControl (ColumnItemsControl) のデータソースを、このクラスの 'Columns' プロパティに設定
            ColumnItemsControl.ItemsSource = Columns;

            // 1秒ごとのグローバルカウントダウンタイマーを初期化
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick; // 1秒ごとに CountdownTimer_Tick メソッドを呼ぶ

            // ユーザー設定（Cookieや設定ファイル）の保存先パスを決定
            // (%APPDATA%\TweetDesk)
            _userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TweetDesk");
            // (%APPDATA%\TweetDesk\settings.dat) - 暗号化バイナリファイル
            _settingsFilePath = Path.Combine(_userDataFolder, "settings.dat");

            // ウィンドウが閉じるときに設定を保存するイベント (MainWindow_Closing) を登録
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// ウィンドウの読み込みが完了したときに呼ばれるイベントハンドラ。
        /// 設定を復元し、WebView環境を非同期で準備します。
        /// </summary>
        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 設定ファイル (settings.dat) を復号して読み込み、ウィンドウサイズと位置を復元
                AppSettings settings = LoadSettings();

                // 2. WebViewの共有環境を非同期で初期化 (Cookie保存フォルダとして _userDataFolder を指定)
                Directory.CreateDirectory(_userDataFolder); // フォルダがなければ作成
                var options = new CoreWebView2EnvironmentOptions();
                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);

                // 3. フォーカスモード（単一表示）用のWebViewを初期化
                await InitializeFocusWebView();

                // 4. 起動時にフォーカスモードだったか確認
                if (settings.IsFocusMode && !string.IsNullOrEmpty(settings.FocusUrl))
                {
                    // 復元するフォーカスURLも、念のためドメインを検証する
                    if (IsAllowedDomain(settings.FocusUrl, allowStatusLinks: true))
                    {
                        EnterFocusMode(settings.FocusUrl);
                    }
                }

                // 5. 保存されていたカラムを（WebView環境の準備ができた後で）復元
                LoadColumnsFromSettings(settings);

                // 6. グローバルカウントダウンタイマーを開始
                _countdownTimer.Start();
            }
            catch (Exception ex)
            {
                // WebView2ランタイムが見つからないなど、致命的なエラー
                MessageBox.Show($"WebView環境の重大な初期化エラー: {ex.Message}\n\nWebView2ランタイムがインストールされているか確認してください。", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ウィンドウが閉じるときに呼ばれるイベントハンドラ。
        /// 現在の状態を暗号化してファイルに保存し、タイマーを破棄します。
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 現在のカラム、ウィンドウサイズ、フォーカス状態などを settings.dat に暗号化して保存
            SaveSettings();

            // タイマーを停止
            _countdownTimer.Stop();
            foreach (var col in Columns)
            {
                col.StopAndDisposeTimer(); // 各カラムのタイマーも破棄
            }
        }

        #endregion

        #region --- カラム管理 (追加・削除) ---

        /// <summary>
        /// データコレクション (ObservableCollection) に新しいカラムを追加します。
        /// UIは自動的に更新されます。
        /// </summary>
        /// <param name="url">追加するカラムのURL</param>
        private void AddNewColumn(string url)
        {
            // 追加するURLが許可ドメイン（x.com/twitter.com）か常に確認する
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

        /// <summary>
        /// 「ホーム追加」ボタンのクリックイベント。
        /// </summary>
        private void AddHome_Click(object? sender, RoutedEventArgs e)
        {
            AddNewColumn(DefaultHomeUrl);
        }

        /// <summary>
        /// 「通知追加」ボタンのクリックイベント。
        /// </summary>
        private void AddNotify_Click(object? sender, RoutedEventArgs e)
        {
            AddNewColumn(DefaultNotifyUrl);
        }

        /// <summary>
        /// 「検索追加」ボタンのクリックイベント。
        /// 入力ダイアログを表示し、キーワードから検索URLを生成して追加します。
        /// </summary>
        private void AddSearch_Click(object? sender, RoutedEventArgs e)
        {
            string? keyword = ShowInputWindow("検索", "検索キーワードを入力してください:");
            if (!string.IsNullOrEmpty(keyword))
            {
                // URLとして安全な形式にエンコード (URLインジェクション対策)
                string encodedKeyword = WebUtility.UrlEncode(keyword);
                AddNewColumn(string.Format(SearchUrlFormat, encodedKeyword));
            }
        }

        /// <summary>
        /// 「リスト追加」ボタンのクリックイベント。
        /// 入力ダイアログを表示し、リストIDまたはURLからカラムを追加します。
        /// </summary>
        private void AddList_Click(object? sender, RoutedEventArgs e)
        {
            string? input = ShowInputWindow("リストの追加", "リストID (数字のみ) または リストの完全なURLを入力してください:");
            if (string.IsNullOrEmpty(input)) return; // キャンセルされた

            // URLが直接入力された場合
            if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // ドメインを検証してから追加（フィッシング対策）
                if (IsAllowedDomain(input))
                {
                    AddNewColumn(input);
                }
                else
                {
                    MessageBox.Show("許可されていないドメインのURLです。\nx.com または twitter.com のURLのみ追加できます。", "入力エラー");
                }
            }
            // リストID（数字のみ）が入力された場合
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
            // sender (Button) の Tag プロパティに紐づけられた ColumnData を取得
            if (sender is Button button && button.Tag is ColumnData columnData)
            {
                columnData.StopAndDisposeTimer(); // 削除前にタイマーを安全に停止
                Columns.Remove(columnData); // コレクションから削除 (UIも自動更新)
            }
        }

        /// <summary>
        /// 検索/リスト追加用のシンプルな入力ダイアログ (InputWindow) を表示します。
        /// </summary>
        /// <param name="title">ウィンドウのタイトル</param>
        /// <param name="prompt">ユーザーへの指示テキスト</param>
        /// <returns>OKが押された場合は入力された文字列、キャンセルの場合は null</returns>
        private string? ShowInputWindow(string title, string prompt)
        {
            var dialog = new InputWindow(title, prompt)
            {
                Owner = this // このMainWindowを親として、中央に表示
            };

            if (dialog.ShowDialog() == true) // ShowDialog() は OK が押されると true を返す
            {
                return dialog.InputText?.Trim(); // 入力テキストを取得
            }
            return null; // キャンセルされた
        }

        #endregion

        #region --- WebView 初期化 & フォーカス処理 ---

        /// <summary>
        /// XAMLのItemsControl内でWebView2コントロールがロードされたときに呼ばれます。
        /// (カラム追加時、ドラッグドロップでの移動時、アプリ起動時など)
        /// </summary>
        private void WebView_Loaded(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;

            if (_webViewEnvironment == null)
            {
                // Window_Loaded での環境初期化がまだ終わっていない場合。
                // 初期化が完了すれば再度このイベントが呼ばれるはずなので、今回は何もしない。
                Debug.WriteLine("[WebView_Loaded] WebView Environment is not ready. Aborting (will retry).");
                return;
            }

            if (webView.CoreWebView2 != null)
            {
                // 既に初期化済み（例: ドラッグ＆ドロップでカラムが再描画されただけ）
                return;
            }

            // CoreWebView2 の初期化が完了したときのイベントハンドラを登録
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

            // CoreWebView2 の初期化を非同期で開始
            // （awaitせずバックグラウンドで実行させる）
            // ★必ず共有環境 (_webViewEnvironment) を指定する
            _ = webView.EnsureCoreWebView2Async(_webViewEnvironment);
        }

        /// <summary>
        /// WebViewのCoreWebView2プロパティの初期化が「本当に」完了したときに呼ばれます。
        /// WebViewの各種設定やイベントハンドラの登録を行います。
        /// </summary>
        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!(sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)) return;

            // イベントハンドラを解除 (二重実行防止のため)
            webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;

            if (!e.IsSuccess)
            {
                Debug.WriteLine($"[CoreWebView2Init] Initialization Failed: {e.InitializationException.Message}");
                return;
            }

            // このUI (WebView) に紐づくデータコンテキスト (ColumnData) を取得
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
                webView.CoreWebView2.Settings.IsScriptEnabled = true;        // JavaScriptは有効に
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; // 標準の右クリックメニューを無効に
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;      // 開発者ツール(F12)を無効に

                // ColumnData (データ) に、この WebView (UI) のインスタンスを関連付ける
                // (これにより、ColumnData側からリロード操作が可能になる)
                columnData.AssociatedWebView = webView;

                // このカラム専用の自動更新タイマーを初期化（＆必要なら開始）
                columnData.InitializeTimer();

                // WebView内でページ遷移（URL変更）が発生したときのイベント
                webView.CoreWebView2.SourceChanged += (coreSender, args) =>
                {
                    var coreWebView = coreSender as CoreWebView2;
                    if (coreWebView != null)
                    {
                        string newUrl = coreWebView.Source; // 新しいURL

                        // 「/status/」（ツイート詳細ページ）でない場合
                        if (!IsAllowedDomain(newUrl, allowStatusLinks: true)) // statusリンクも許可
                        {
                            // 許可されたドメインのURLのみをカラムのURLとして保存する（フィッシング対策）
                            if (IsAllowedDomain(newUrl))
                            {
                                columnData.Url = newUrl;
                                Debug.WriteLine($"[Column_SourceChanged] Column URL updated to: {columnData.Url}");
                            }
                            else
                            {
                                Debug.WriteLine($"[Column_SourceChanged] Blocked saving external URL: {newUrl}");
                            }
                        }
                        else
                        {
                            // 「/status/」URLの場合は、フォーカスモードに入る
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
                // CoreWebView2 が null になった場合など
                Debug.WriteLine($"[CoreWebView2Init] Error during setup: {ex.Message}");
            }
        }


        /// <summary>
        /// フォーカスモード（単一表示）用のWebView (FocusWebView) を初期化します。
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            if (FocusWebView == null) return;

            // 共有環境で初期化
            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            if (FocusWebView.CoreWebView2 != null)
            {
                // フォーカス用WebViewでURLが変更されたときのイベント
                FocusWebView.CoreWebView2.SourceChanged += (sender, args) =>
                {
                    var coreWebView = sender as CoreWebView2;
                    if (coreWebView == null) return;

                    string newUrl = coreWebView.Source;
                    Debug.WriteLine($"[Focus_SourceChanged] Focus URL updated to: {newUrl}");

                    // 「status」URL *以外* に移動したら（例: 戻るボタン、ホームへ移動など）、
                    // または許可されていないドメインに移動したら、自動的にカラム一覧に戻る
                    if (_isFocusMode &&
                        !IsAllowedDomain(newUrl, allowStatusLinks: true) &&
                        !newUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[Focus_SourceChanged] Exiting Focus Mode (navigated away): {newUrl}");
                        ExitFocusMode();
                    }
                };
            }
        }

        /// <summary>
        /// フォーカスモードに入り、UI（Gridの表示/非表示）を切り替えます。
        /// </summary>
        /// <param name="url">ツイート詳細 (status) のURL</param>
        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url); // フォーカス用WebViewでURLを開く

            // UIを切り替え
            ColumnItemsControl.Visibility = Visibility.Collapsed; // カラム一覧を非表示
            FocusViewGrid.Visibility = Visibility.Visible;      // フォーカス用Gridを表示

            // カウントダウンが進まないよう、全カラムの自動更新タイマーを一時停止
            foreach (var col in Columns) col.Timer?.Stop();
        }

        /// <summary>
        /// フォーカスモードを終了し、UIをカラム一覧に戻します。
        /// </summary>
        private void ExitFocusMode()
        {
            _isFocusMode = false;

            // UIを切り替え
            FocusViewGrid.Visibility = Visibility.Collapsed;    // フォーカス用Gridを非表示
            ColumnItemsControl.Visibility = Visibility.Visible;   // カラム一覧を表示

            FocusWebView?.CoreWebView2?.Navigate("about:blank"); // フォーカス用WebViewを空にする

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
            // フォーカスモード中はカウントダウンを停止
            if (_isFocusMode) return;

            foreach (var column in Columns)
            {
                if (column.IsAutoRefreshEnabled && column.RemainingSeconds > 0)
                {
                    column.RemainingSeconds--; // 1秒減らす (UIはColumnData側のINotifyPropertyChangedで自動更新)
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
                columnData.ReloadWebView(); // 紐づくColumnDataのリロード処理を呼ぶ
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
            // 保存する設定オブジェクトを作成
            var settings = new AppSettings
            {
                Columns = new List<ColumnData>(Columns), // 現在のカラムリスト
                IsFocusMode = _isFocusMode
            };

            // フォーカスモードだった場合、そのURLも保存
            if (_isFocusMode && FocusWebView?.CoreWebView2 != null)
            {
                settings.FocusUrl = FocusWebView.CoreWebView2.Source;
            }
            else
            {
                // フォーカスモードでない場合
                settings.IsFocusMode = false;
                settings.FocusUrl = null;
            }

            // ウィンドウの状態を保存
            if (this.WindowState == WindowState.Maximized)
            {
                // 最大化時は、復元後（通常サイズ）の情報を保存
                settings.WindowState = WindowState.Maximized;
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowHeight = this.RestoreBounds.Height;
                settings.WindowWidth = this.RestoreBounds.Width;
            }
            else
            {
                // 通常時（または最小化時）は、現在のサイズと位置を保存
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
                //    (DataProtectionScope.CurrentUser: 現在のWindowsユーザーのみが復号可能)
                byte[] jsonData = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(jsonData, _entropy, DataProtectionScope.CurrentUser);

                // 3. 暗号化されたバイト配列をファイルに書き込み
                File.WriteAllBytes(_settingsFilePath, encryptedData);
                Debug.WriteLine($"Settings encrypted and saved to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                // （例: %APPDATA% への書き込み権限がない）
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 暗号化された設定ファイルを読み込み、DPAPIで復号してウィンドウの状態を復元します。
        /// </summary>
        /// <returns>読み込んだ設定オブジェクト (カラム復元用)</returns>
        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                Debug.WriteLine("No settings file found. Using default settings.");
                return new AppSettings(); // 設定ファイルがなければ、デフォルト設定を返す
            }

            try
            {
                // 1. 暗号化されたデータをバイト配列として読み込む
                byte[] encryptedData = File.ReadAllBytes(_settingsFilePath);

                // 2. DPAPIで復号
                byte[] jsonData = ProtectedData.Unprotect(encryptedData, _entropy, DataProtectionScope.CurrentUser);

                // 3. バイト配列をJSON文字列に戻す
                string json = Encoding.UTF8.GetString(jsonData);

                // 4. JSONから設定オブジェクトにデシリアライズ
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    // ウィンドウのサイズと位置を復元
                    this.Top = settings.WindowTop;
                    this.Left = settings.WindowLeft;
                    this.Height = settings.WindowHeight;
                    this.Width = settings.WindowWidth;
                    this.WindowState = settings.WindowState;

                    // ウィンドウが画面外に表示されるのを防ぐ
                    ValidateWindowPosition();

                    Debug.WriteLine($"Settings loaded and decrypted from {_settingsFilePath}");
                    return settings; // 読み込んだ設定を返す
                }
            }
            catch (JsonException jsonEx)
            {
                // JSONパース失敗（ファイル破損の可能性）
                Debug.WriteLine($"Failed to parse settings (file might be corrupt): {jsonEx.Message}");
            }
            catch (CryptographicException cryptoEx)
            {
                // 復号失敗（別ユーザーや別PCで作成されたか、ファイル破損）
                Debug.WriteLine($"Failed to decrypt settings (file might be from another user/PC or corrupt): {cryptoEx.Message}");
                MessageBox.Show("設定ファイルの読み込みに失敗しました。ファイルが破損しているか、アクセス許可がありません。設定をリセットします。", "設定読み込みエラー");
            }
            catch (Exception ex)
            {
                // その他の予期せぬエラー
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            // 読み込みに失敗したら、デフォルト設定を返す
            return new AppSettings();
        }

        /// <summary>
        /// 読み込まれた設定オブジェクトからカラムのリストを復元します。
        /// (WebView環境の初期化 *後* に呼ばれます)
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
                        // 読み込んだカラムURLも検証してから追加する（フィッシング対策）
                        if (IsAllowedDomain(columnData.Url))
                        {
                            Columns.Add(columnData);
                            loadedColumn = true;
                        }
                        else
                        {
                            // 許可されていないドメインのURLは読み飛ばす
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

            // 復元するカラムが一つもなかった場合、デフォルト（ホーム）を追加
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
            // プライマリスクリーン（メインモニター）の作業領域（タスクバーを除く）を取得
            var screen = System.Windows.SystemParameters.WorkArea;

            // ウィンドウが画面外（左右）に行っていないか？
            if (this.Left + this.Width < 0 || this.Left > screen.Width)
            {
                this.Left = 100; // デフォルト位置に戻す
            }

            // ウィンドウが画面外（上下）に行っていないか？
            if (this.Top + this.Height < 0 || this.Top > screen.Height)
            {
                this.Top = 100; // デフォルト位置に戻す
            }
        }

        #endregion

        #region --- ヘルパーメソッド ---

        /// <summary>
        /// 指定されたURLが許可されたドメイン（x.com または twitter.com）かどうかを検証します。
        /// (フィッシングサイトへの永続的な遷移を防ぐためのセキュリティ対策)
        /// </summary>
        /// <param name="url">検証するURL</param>
        /// <param name="allowStatusLinks">ツイート詳細 (/status/) へのリンクを許可するかどうか</param>
        /// <returns>許可されたドメインの場合は true</returns>
        private bool IsAllowedDomain(string url, bool allowStatusLinks = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // about:blank はWebView内部のナビゲーションとして許可する
            if (url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // http/https で始まらないURLは無効 (例: "javascript:...")
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                Uri uri = new Uri(url);

                // ホスト名が "x.com" または "twitter.com" で終わるか（サブドメインを許可するため）
                bool isAllowedHost = uri.Host.EndsWith("x.com", StringComparison.OrdinalIgnoreCase) ||
                                     uri.Host.EndsWith("twitter.com", StringComparison.OrdinalIgnoreCase);

                if (isAllowedHost)
                {
                    if (allowStatusLinks)
                    {
                        // /status/ リンク（ツイート詳細）も許可する
                        return true;
                    }

                    // /status/ リンクを許可しない場合（カラムURLの保存時など）
                    // パスに "/status/" が含まれていないことを確認
                    return !uri.AbsolutePath.Contains("/status/");
                }

                // ホスト名が許可されていない
                return false;
            }
            catch (UriFormatException ex)
            {
                // 不正な形式のURL
                Debug.WriteLine($"[IsAllowedDomain] Invalid URL format: {url}, {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// ビジュアルツリーを再帰的に探索して、指定した型(T)の最初の子要素を見つけます。
        /// （WPFの内部的なUI要素を取得するために使用）
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    // 型 T に一致する子要素が見つかった
                    return typedChild;
                }

                // 見つからなければ、さらにその子要素を再帰的に探索
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