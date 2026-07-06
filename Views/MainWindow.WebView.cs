using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using XColumn.Helpers;
using XColumn.Models;
using XColumn.Scripts;

// WPFとWinFormsのClipboard型の曖昧さ回避
using Clipboard = System.Windows.Clipboard;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindowのWebView関連ロジック（環境初期化、イベントハンドラ、CSS/スクリプト注入、
    /// フォーカスモード連携、コンテキストメニュー、エラー監視など）を管理する分割クラス。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Fields =====

        /// <summary>プロファイル名ごとに保持するWebView2環境（プロファイル単位でデータを分離する）。</summary>
        private readonly Dictionary<string, CoreWebView2Environment> _webViewEnvironments = new Dictionary<string, CoreWebView2Environment>();

        /// <summary>拡張機能を既にロード済みのプロファイル名の集合（プロファイル単位で二重ロードを防ぐ）。</summary>
        private readonly HashSet<string> _loadedProfilesExtensions = new HashSet<string>();

        /// <summary>メディア（画像/動画）クリック起因のフォーカス遷移であるかを示すフラグ。</summary>
        private bool _isMediaFocusIntent = false;

        /// <summary>最後にネットワークエラーを検知した時刻（短時間の大量通知を抑止するデバウンス用）。</summary>
        private DateTime _lastDetectedErrorTime = DateTime.MinValue;

        // ===== Public Methods =====

        /// <summary>
        /// 拡張機能のオプションページをフォーカスモードで開きます。
        /// </summary>
        public void OpenExtensionOptions(ExtensionItem ext)
        {
            // 拡張機能IDとオプションページが有効か確認
            if (string.IsNullOrEmpty(ext.Id) || string.IsNullOrEmpty(ext.OptionsPage))
            {
                MessageWindow.Show(this, Properties.Resources.Extension_NoOptionsPage, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // オプションページのURLを構築（http(s)はそのまま、それ以外は chrome-extension スキームで構築）
            string url = (ext.OptionsPage.StartsWith("http://") || ext.OptionsPage.StartsWith("https://"))
                ? ext.OptionsPage
                : $"chrome-extension://{ext.Id}/{ext.OptionsPage}";

            EnterFocusMode(url);
        }

        // ===== Private Methods (Environment & Initialization) =====

        /// <summary>
        /// 指定されたプロファイルのWebView2環境を取得、なければ生成してキャッシュします。
        /// </summary>
        private async Task<CoreWebView2Environment?> GetOrCreateEnvironmentAsync(string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) profileName = _activeProfileName;

            if (_webViewEnvironments.TryGetValue(profileName, out var cachedEnv))
            {
                return cachedEnv;
            }

            try
            {
                string browserDataFolder = Path.Combine(_userDataFolder, "BrowserData", profileName);
                Directory.CreateDirectory(browserDataFolder);

                // var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true };
                var options = new CoreWebView2EnvironmentOptions
                {
                    AreBrowserExtensionsEnabled = true,

                    // 【試験的】画像原寸ボタン等が1クリックで複数のwindow.open()を呼べるように
                    // Chromiumのポップアップブロッカーを無効化する（設定ON時のみ・要再起動）。
                    // 実際のポップアップ制御はNewWindowRequestedハンドラ側で行う。
                    AdditionalBrowserArguments = _disablePopupBlocking ? "--disable-popup-blocking" : ""

                };

                var env = await CoreWebView2Environment.CreateAsync(null, browserDataFolder, options);
                _webViewEnvironments[profileName] = env;
                return env;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create environment for {profileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// メインプロファイルのWebView2環境を初期化し、フォーカス用WebViewをセットアップします。
        /// </summary>
        private async Task InitializeWebViewEnvironmentAsync()
        {
            if (string.IsNullOrEmpty(_userDataFolder) || string.IsNullOrEmpty(_activeProfileName)) return;

            // メイン環境の初期化とFocusWebViewのセットアップ
            var mainEnv = await GetOrCreateEnvironmentAsync(_activeProfileName);
            if (mainEnv != null)
            {
                await InitializeFocusWebView();
            }
        }

        /// <summary>
        /// フォーカスモード用のWebViewを初期化し、各種イベントハンドラを登録します。
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            var mainEnv = await GetOrCreateEnvironmentAsync(_activeProfileName);

            // フォーカスWebViewと環境が有効でない場合は処理を中止
            if (FocusWebView == null || mainEnv == null) return;

            // CoreWebView2の初期化を待機
            await FocusWebView.EnsureCoreWebView2Async(mainEnv);

            // 初期化完了後の設定
            if (FocusWebView.CoreWebView2 != null)
            {
                // ブラウザ標準ダイアログを無効化し、フリーズを根本的に防ぐ
                FocusWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                // フォーカスビューでもエラーを監視する
                FocusWebView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;

                // スクリプトダイアログの制御（フォーカスモード外なら受理）
                FocusWebView.CoreWebView2.ScriptDialogOpening += (s, args) =>
                {
                    if (!_isFocusMode) args.Accept();
                };

                FocusWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                FocusWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // フォーカスビューが使うメインプロファイルに拡張機能が未ロードならロードする
                if (!_loadedProfilesExtensions.Contains(_activeProfileName))
                {
                    _loadedProfilesExtensions.Add(_activeProfileName);
                    await LoadExtensionsAsync(FocusWebView.CoreWebView2.Profile);
                }

                // ナビゲーション完了時の処理登録
                FocusWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        string src = FocusWebView.CoreWebView2.Source ?? "";
                        // YouTube動画ページなら全面化CSSを適用（PiPと同じ見た目にする）
                        if (src.Contains("youtube.com") || src.Contains("youtu.be"))
                        {
                            FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.YouTubeFocusFullBleedScript);
                            return; // YouTubeページにはX用スクリプトを当てない
                        }

                        // 外部URL（x.com / twitter.com 以外）ではフォーカス用スクリプトを当てない
                        bool isXHost = false;
                        try
                        {
                            string host = new Uri(src).Host;
                            isXHost = host.EndsWith("x.com") || host.EndsWith("twitter.com");
                        }
                        catch { /* 解析失敗時はXではない扱い */ }
                        if (!isXHost) return;

                        ApplyCustomCss(FocusWebView.CoreWebView2, src, _focusedColumnData);
                        ApplyVolumeScript(FocusWebView.CoreWebView2);
                        ApplyMediaExpandScript(FocusWebView.CoreWebView2);
                        ApplyScrollSyncScript(FocusWebView.CoreWebView2);
                        if (_forceDisableAutoPlay) FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptPreserveScrollPosition);
                        ApplyAbsoluteTimeScript(FocusWebView.CoreWebView2);
                        FocusWebView.CoreWebView2.ExecuteScriptAsync($"window.xColumnShowAbsoluteTime = {(_showAbsoluteTime ? "true" : "false")};");
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptAbsoluteTime);
                    }
                };

                // ソースURL変更時の処理登録
                FocusWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    string url = FocusWebView.CoreWebView2.Source;
                    if (url.StartsWith("chrome-extension://")) return;

                    // YouTube動画をフォーカス表示中は、x.com外URLでもフォーカスを維持する
                    if (IsYouTubeUrl(url))
                    {
                        return;
                    }

                    // 外部リンクをフォーカス表示中も維持する
                    if (_isExternalUrlFocus)
                    {
                        // 外部サイトにはX用CSS/スクリプトを当てない。
                        // ESCでフォーカスモードを抜けられるよう、キー監視だけ注入する。
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);
                        return;
                    }

                    ApplyCustomCss(FocusWebView.CoreWebView2, url, _focusedColumnData);
                    FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                    _isMediaFocusIntent = url.Contains("/photo/") || url.Contains("/video/");
                    ApplyMediaExpandScript(FocusWebView.CoreWebView2);

                    // フォーカス維持対象でないURLへ遷移したらフォーカスモードを抜ける

                    // フォーカスモードは「カラム一覧へ戻る」ボタン（CloseFocusView_Click）でのみ解除する。
                    // URLベースの自動解除（プロフィール/ハッシュタグ等で抜ける挙動）は廃止。
                    // bool keepFocus = IsAllowedDomain(url, true) || url.Contains("/compose/") || url.Contains("/intent/") || url.Contains("/settings");
                    // if (_isFocusMode && !keepFocus && url != "about:blank") ExitFocusMode();
                };
            }
        }

        // ===== Private Methods (WebView Setup & Lifecycle) =====

        /// <summary>
        /// カラム用WebView2コントロールがロードされたときに、対象プロファイルの環境で初期化を開始します。
        /// </summary>
        private async void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is WebView2 webView && webView.CoreWebView2 == null && webView.DataContext is ColumnData col)
            {
                // カラムに設定されたプロファイル名を取得（なければメインプロファイル）
                string targetProfile = string.IsNullOrEmpty(col.ProfileName) ? _activeProfileName : col.ProfileName;

                var env = await GetOrCreateEnvironmentAsync(targetProfile);
                if (env != null)
                {
                    webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                    await webView.EnsureCoreWebView2Async(env);
                }
            }
        }

        /// <summary>
        /// CoreWebView2の初期化完了時の処理。各種設定とイベントハンドラの登録、拡張機能ロードを行います。
        /// </summary>
        private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                // 初期化完了イベントハンドラの解除
                webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                if (e.IsSuccess && webView.CoreWebView2 != null)
                {
                    // 新規ウィンドウ要求時の処理を登録
                    webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                    // ブラウザからのメッセージ（スクロール要求など）を受信するハンドラを登録
                    webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                    // データコンテキストから対象のプロファイル名を特定
                    string targetProfile = _activeProfileName;
                    if (webView.DataContext is ColumnData colData && !string.IsNullOrEmpty(colData.ProfileName))
                    {
                        targetProfile = colData.ProfileName;
                    }

                    // 対象プロファイルに拡張機能が未ロードならロードする
                    if (!_loadedProfilesExtensions.Contains(targetProfile))
                    {
                        _loadedProfilesExtensions.Add(targetProfile);
                        await LoadExtensionsAsync(webView.CoreWebView2.Profile);
                    }

                    if (webView.DataContext is ColumnData col)
                    {
                        SetupWebView(webView, col);
                    }
                }
            }
        }

        /// <summary>
        /// カラム用WebViewの各種設定・イベントハンドラの登録・初期ナビゲーションを行います。
        /// </summary>
        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            // WebViewの各種設定
            webView.CoreWebView2.Settings.IsScriptEnabled = true;

            // 右クリックのコンテキストメニュー有効化
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // DevToolsの有効/無効（設定に従う）
            webView.CoreWebView2.Settings.AreDevToolsEnabled = _enableDevTools;

            // ネットワークレスポンスを監視してエラーを検知するイベントを登録
            webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;

            col.AssociatedWebView = webView;
            col.InitializeTimer();

            // ズーム倍率の初期適用
            webView.ZoomFactor = col.ZoomFactor;

            // ColumnData からの委譲イベントを購読（WebView固有操作を View 側で実行）。
            // 二重購読防止のため一旦解除してから登録する。
            col.GoBackRequested -= OnColumnGoBackRequested;
            col.GoBackRequested += OnColumnGoBackRequested;
            col.SuspendRequested -= OnColumnSuspendRequested;
            col.SuspendRequested += OnColumnSuspendRequested;
            col.RateLimitSuspendRequested -= OnColumnRateLimitSuspendRequested;
            col.RateLimitSuspendRequested += OnColumnRateLimitSuspendRequested;

            // ZoomFactor / MediaScalePercentage の変更を監視してWebViewへ反映
            col.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ColumnData.ZoomFactor))
                {
                    try
                    {
                        webView.Dispatcher.Invoke(() =>
                        {
                            webView.ZoomFactor = col.ZoomFactor;
                        });
                    }
                    catch { /* WebViewが破棄されている場合などは無視 */ }
                }
                else if (e.PropertyName == nameof(ColumnData.MediaScalePercentage))
                {
                    // 画像サイズ倍率が変更されたらCSSを再適用
                    try
                    {
                        webView.Dispatcher.Invoke(() =>
                        {
                            if (webView.CoreWebView2 != null)
                            {
                                ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source, col);
                            }
                        });
                    }
                    catch { }
                }
            };

            // アプリがアクティブな場合、（設定により）自動更新タイマーを停止
            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            // ナビゲーション開始時のフォーカスモード遷移判定
            webView.CoreWebView2.NavigationStarting += (s, args) =>
            {
                if (col.IsSuspended) return;

                string url = args.Uri;
                if (url.StartsWith("chrome-extension://") || url == "about:blank") return;

                if (IsAllowedDomain(url, true) && !_isFocusMode)
                {
                    bool isFocusTarget = true;
                    bool isMedia = url.Contains("/photo/") || url.Contains("/video/");

                    if (isFocusTarget)
                    {
                        // 安全対策: アクティブと異なるプロファイルのカラムでは、
                        // モーダルを起動せず、そのままカラム内(インライン)でブラウジングさせる。
                        bool isOtherProfile = !string.IsNullOrEmpty(col.ProfileName) && col.ProfileName != _activeProfileName;

                        if (!isOtherProfile)
                        {
                            // フォーカスモードに入らず、通常のブラウザ遷移として処理（誤爆防止）
                            return;
                        }

                        _focusedColumnData = col;
                        _isMediaFocusIntent = isMedia;
                        EnterFocusMode(url);
                    }
                }
            };

            // 入力状態(inputState)受信ハンドラ
            webView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string jsonString = e.TryGetWebMessageAsString();
                    var json = JsonNode.Parse(jsonString);
                    if (json?["type"]?.GetValue<string>() == "inputState")
                    {
                        bool isActive = json["val"]?.GetValue<bool>() ?? false;
                        col.IsInputActive = isActive;

                        if (isActive)
                        {
                            col.LastInputTime = DateTime.Now;
                        }
                    }
                }
                catch { }
            };

            // コンテキストメニューのカスタマイズ要求イベント
            webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

            // ナビゲーション完了時：各種スクリプト/CSSを注入
            webView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                // 破棄チェック（NavigationCompleted時はCoreWebView2がnullでないことを保証）
                var wv2 = webView.CoreWebView2;
                if (wv2 == null) return;

                // 休止画面の読み込み完了時はスクリプト等を注入しない
                if (col.IsSuspended) return;

                if (args.IsSuccess)
                {
                    ApplyCustomCss(wv2, wv2.Source, col);
                    ApplyVolumeScript(wv2);
                    ApplyYouTubeClickScript(wv2);

                    int tolerance = _scrollTopTolerance;
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.GetScrollStateNotifierScript(tolerance));

                    // 自動再生無効化スクリプト注入
                    if (_forceDisableAutoPlay)
                    {
                        await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                    }

                    // リスト自動遷移ロジック
                    if (col.IsListAutoNav)
                    {
                        // ホーム画面(x.com または twitter.com)にいる場合のみ実行
                        string src = wv2.Source;
                        if (src.Contains("x.com") || src.Contains("twitter.com"))
                        {
                            // 設定画面で指定された待機時間（ミリ秒）を使用
                            await Task.Delay(_listAutoNavDelay);

                            // ScriptDefinitionsからスクリプトを取得して実行
                            string result = await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptClickListButton);

                            // クリックに成功したらフラグをオフにする
                            if (result.Contains("clicked"))
                            {
                                col.IsListAutoNav = false;
                            }
                        }
                    }

                    // スクロール同期スクリプト（WebView内でのShift+Wheelを捕捉）
                    ApplyScrollSyncScript(wv2);
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectReplies);

                    // 表示オプション（絶対時間表示）をJS変数に渡す
                    await wv2.ExecuteScriptAsync($"window.xColumnShowAbsoluteTime = {(_showAbsoluteTime ? "true" : "false")};");
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptAbsoluteTime);

                    // 入力監視スクリプト注入
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectInput);

                    // NGワードフィルタースクリプト注入
                    ApplyNgWordsScript(wv2);

                    // 設定値(メディアクリック時の遷移無効化)をJS変数に渡す
                    await wv2.ExecuteScriptAsync($"window.xColumnDisableMediaFocus = {_disableFocusModeOnMediaClick.ToString().ToLower()};");

                    // 設定値(ポストクリック時の遷移無効化)もJS変数に渡す
                    await wv2.ExecuteScriptAsync($"window.xColumnDisableTweetFocus = {_disableFocusModeOnTweetClick.ToString().ToLower()};");

                    // キー入力監視スクリプト注入 (ESCキー対応)
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                    // スクロール位置保持スクリプト注入
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptPreserveScrollPosition);

                    // メディアクリックのインターセプトスクリプトを注入
                    await wv2.ExecuteScriptAsync(ScriptDefinitions.ScriptInterceptClick);

                    // 絶対時刻表示スクリプトの適用
                    ApplyAbsoluteTimeScript(wv2);
                }
            };

            // ソースURL変更時の処理
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                // 休止中はURLの更新（about:blankでの上書き等）を防止する
                if (col.IsSuspended) return;

                string url = webView.CoreWebView2.Source;

                // 拡張機能のURLは無視
                if (url.StartsWith("chrome-extension://")) return;

                // URL変更時のモデル更新。
                // 設定ページ(/settings)や詳細ページ(/status/)などの「Focus対象」URLは、
                // カラムの基点URLとして保存したくないため col.Url には反映しない。
                // これにより再起動時は直前のタイムライン（ホームなど）が復元される。
                if (IsAllowedDomain(url, false))
                {
                    col.Url = url;
                }

                ApplyCustomCss(webView.CoreWebView2, url, col);
                ApplyYouTubeClickScript(webView.CoreWebView2);

                // 自動再生無効化スクリプト注入
                if (_forceDisableAutoPlay)
                {
                    webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                }

                // ページ遷移後もスクリプトを適用
                ApplyScrollSyncScript(webView.CoreWebView2);
                ApplyTrendingClickScript(webView.CoreWebView2);

                // ページ遷移時は入力状態を一旦リセットして監視再開
                col.IsInputActive = false;
                webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectInput);

                // ページ遷移後もキー監視を有効化
                webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                // ページ遷移後もスクロール保持スクリプトを有効化
                webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptPreserveScrollPosition);

                if (!IsAllowedDomain(url) && !IsAllowedDomain(url, true)) return;

                // フォーカスモード判定。
                // IsAllowedDomain(url, true) は /status/ や /settings などの詳細ページで true を返す。
                bool isFocusTarget = IsAllowedDomain(url, true);

                bool isMedia = url.Contains("/photo/") || url.Contains("/video/");

                if (isFocusTarget && _disableFocusModeOnMediaClick && isMedia)
                {
                    isFocusTarget = false;
                }
                if (isFocusTarget && _disableFocusModeOnTweetClick && !isMedia)
                {
                    isFocusTarget = false;
                }

                // 投稿画面(/compose/, /intent/)はカラム内（Web標準モーダル）で表示させるため、
                // フォーカスモードの対象から除外する。
                if (url.Contains("/compose/") || url.Contains("/intent/"))
                {
                    isFocusTarget = false;
                }

                if (isFocusTarget)
                {
                    if (!_isFocusMode)
                    {
                        bool comingFromCompose = !string.IsNullOrEmpty(col.Url) &&
                                                 (col.Url.Contains("/compose/") || col.Url.Contains("/intent/"));

                        if (!comingFromCompose)
                        {
                            // 別プロファイルのカラムの場合はモーダル(FocusMode)を起動しない
                            bool isOtherProfile = !string.IsNullOrEmpty(col.ProfileName) && col.ProfileName != _activeProfileName;
                            if (isOtherProfile)
                            {
                                return; // そのままカラム内で遷移させる
                            }
                            _focusedColumnData = col;

                            // 非同期URL書き換えルートでもメディアフラグをセットする
                            _isMediaFocusIntent = isMedia;

                            EnterFocusMode(url);
                        }
                    }
                }
            };

            webView.CoreWebView2.Navigate(col.Url);
        }

        // ===== Private Methods (Web Message Handling) =====

        /// <summary>
        /// ブラウザ(JavaScript)から送信されたメッセージを受け取り、種別に応じて処理します。
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // 受信したメッセージをJSONとして解析
                string jsonString = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(jsonString)) return;
                var json = JsonNode.Parse(jsonString);
                string? type = json?["type"]?.GetValue<string>();

                // 横スクロール要求
                if (type == "horizontalScroll")
                {
                    double delta = json?["delta"]?.GetValue<double>() ?? 0;
                    PerformHorizontalScroll((int)delta);
                }
                // キー入力（ESCキー）
                else if (type == "keyInput")
                {
                    string? key = json?["key"]?.GetValue<string>();
                    if (key == "Escape")
                    {
                        // フォーカスモード中なら終了
                        if (_isFocusMode)
                        {
                            ExitFocusMode();
                        }
                        // そうでなく、通常のカラムで戻れる場合は戻る
                        else if (sender is CoreWebView2 coreWebView)
                        {
                            if (coreWebView.CanGoBack)
                            {
                                coreWebView.GoBack();
                            }
                        }
                    }
                }
                // 新規カラムで開く要求
                else if (type == "openNewColumn")
                {
                    string? url = json?["url"]?.GetValue<string>();
                    Logger.Log($"[WebView Message] openNewColumn received. URL: {url}");

                    if (!string.IsNullOrEmpty(url) && IsAllowedDomain(url))
                    {
                        Dispatcher.InvokeAsync(() => AddNewColumn(url));
                    }
                }
                // デバッグログ受信
                else if (type == "debugLog")
                {
                    string? message = json?["message"]?.GetValue<string>();
                    Logger.Log($"[JS Log] {message}");
                }
                // YouTube等の外部動画をPiPで開く要求
                else if (type == "openPipVideo")
                {
                    string ? url = json?["url"]?.GetValue<string>();
                    // PiP設定がONで、かつYouTubeのURLの場合のみPiPで開く（任意URL流入を防止）
                    if (_autoPipForVideo && !string.IsNullOrEmpty(url) && IsYouTubeUrl(url))
                    {
                        Dispatcher.InvokeAsync(() => OpenFocusMode(url, true));
                    }
                }
                // YouTube動画をフォーカスモードで全面表示する要求
                else if (type == "openFocusVideo")
                {
                    string? url = json?["url"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(url) && IsYouTubeUrl(url))
                    {
                        if (sender is CoreWebView2 coreWebView)
                        {
                            _focusedColumnData = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);
                        }
                        string targetUrl = url;
                        Dispatcher.InvokeAsync(() => EnterFocusModeWithUrl(targetUrl));
                    }
                }

                // フォーカスモードを直接開くメッセージの処理
                if (type == "openFocusMode")
                {
                    string? url = json?["url"]?.GetValue<string>();

                    // JSから送られた isVideo フラグを安全に取得（存在しない場合は false）
                    bool isVideo = false;
                    var isVideoNode = json?["isVideo"];
                    if (isVideoNode != null)
                    {
                        isVideo = isVideoNode.GetValue<bool>();
                    }

                    if (!string.IsNullOrEmpty(url) && IsAllowedDomain(url, true))
                    {
                        _isMediaFocusIntent = url.Contains("/photo/") || url.Contains("/video/");

                        if (sender is CoreWebView2 coreWebView)
                        {
                            var targetCol = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);
                            // 別プロファイルの場合はモーダルを起動せず、そのままカラム内で遷移させる
                            if (targetCol != null && !string.IsNullOrEmpty(targetCol.ProfileName) && targetCol.ProfileName != _activeProfileName)
                            {
                                coreWebView.Navigate(url);
                                return;
                            }

                            // カラムのWebViewからの要求時は対象カラムを記録してフォーカスモードを起動する
                            _focusedColumnData = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);

                            // OpenFocusMode に isVideo フラグも一緒に渡す
                            Dispatcher.InvokeAsync(() => OpenFocusMode(url, isVideo));
                        }
                    }
                }
                // スクロール位置（最上部かどうか）の通知
                else if (type == "scrollState")
                {
                    bool isTop = json?["isTop"]?.GetValue<bool>() ?? true;
                    if (sender is CoreWebView2 coreWebView)
                    {
                        var targetCol = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);
                        if (targetCol != null)
                        {
                            targetCol.IsAtTop = isTop;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"WebMessageReceived Error: {ex.Message}");
            }
        }

        // ===== Private Methods (Extensions) =====

        /// <summary>
        /// 有効な拡張機能をWebView2プロファイルにロードし、無効/不要な拡張機能を削除します。
        /// </summary>
        private async Task LoadExtensionsAsync(CoreWebView2Profile profile)
        {
            // 有効な拡張機能を順にロード
            foreach (var ext in _extensionList)
            {
                if (ext.IsEnabled && Directory.Exists(ext.Path))
                {
                    try
                    {
                        var loadedExt = await profile.AddBrowserExtensionAsync(ext.Path);
                        ext.Id = loadedExt.Id;
                        ext.OptionsPage = GetOptionsPagePath(ext.Path);
                        Logger.Log($"Extension loaded: {ext.Name} (ID: {ext.Id})");
                    }
                    catch (Exception ex)
                    {
                        MessageWindow.Show(this,
                            string.Format(Properties.Resources.Msg_Err_ExtensionLoadFailed, ext.Name, ex.Message),
                            Properties.Resources.ExtensionError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            try
            {
                // 不要な拡張機能をプロファイルから削除
                var installedExtensions = await profile.GetBrowserExtensionsAsync();
                foreach (var installedExt in installedExtensions)
                {
                    bool shouldKeep = _extensionList.Any(e => e.Id == installedExt.Id && e.IsEnabled);
                    if (!shouldKeep) { try { await installedExt.RemoveAsync(); } catch { } }
                }
            }
            catch { }
        }

        /// <summary>
        /// 拡張機能フォルダの manifest.json を解析し、オプションページのパスを取得します。
        /// </summary>
        private string GetOptionsPagePath(string extensionFolder)
        {
            try
            {
                string manifestPath = Path.Combine(extensionFolder, "manifest.json");
                if (!File.Exists(manifestPath)) return "";

                string jsonString = File.ReadAllText(manifestPath);
                var json = JsonNode.Parse(jsonString);
                if (json == null) return "";

                // Old Twitter Layout拡張機能の特別対応（専用の設定URLを返す）
                string? name = json["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(name) && name.Contains("Old Twitter Layout"))
                {
                    return "https://x.com/old/settings";
                }

                // 通常のオプションページパスの取得（複数のmanifestキーをフォールバックで探索）
                string? page = json["options_ui"]?["page"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["options_page"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["action"]?["default_popup"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["browser_action"]?["default_popup"]?.GetValue<string>();

                return page ?? "";
            }
            catch { return ""; }
        }

        // ===== Private Methods (Error Monitoring) =====

        /// <summary>
        /// 【デバッグ用】レート制限ヘッダー(limit/remaining/reset)をログ＆VS出力ウィンドウに出力します。
        /// 早期リターン前に呼ぶことで、429時だけでなく通常の200応答での残量推移も観測できます。
        /// x-rate-limit-limit を持つ応答（=制限枠のあるGraphQL等）のみを対象とし、画像等のノイズは出しません。
        /// </summary>
        private void LogRateLimitDebug(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                var headers = e.Response.Headers;
                // レート制限枠を持たない応答（画像・CSS・JS等）は無視
                if (!headers.Contains("x-rate-limit-limit")) return;

                string url = e.Request.Uri;
                if (!IsTargetDomain(url)) return;

                string limit = headers.Contains("x-rate-limit-limit") ? headers.GetHeader("x-rate-limit-limit") : "-";
                string remaining = headers.Contains("x-rate-limit-remaining") ? headers.GetHeader("x-rate-limit-remaining") : "-";
                string resetRaw = headers.Contains("x-rate-limit-reset") ? headers.GetHeader("x-rate-limit-reset") : "";

                string resetInfo = "-";
                if (long.TryParse(resetRaw, out long unix))
                {
                    var reset = DateTimeOffset.FromUnixTimeSeconds(unix);
                    double sec = (reset - DateTimeOffset.Now).TotalSeconds;
                    resetInfo = $"{reset.LocalDateTime:HH:mm:ss}(あと{sec:F0}秒)";
                }

                // GraphQLのOperation名を抜き出してログを見やすくする
                string op = ExtractGraphqlOperation(url);

                // どのアカウント（バケット）かを把握するためプロファイル名も付与
                string profile;
                ColumnData? col = (sender is CoreWebView2 cw)
                    ? Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == cw)
                    : null;
                if (col == null)
                    profile = "(no-column)";
                else if (string.IsNullOrEmpty(col.ProfileName))
                    profile = "(default)";
                else
                    profile = col.ProfileName;

                string line = $"[RateLimit] {op} status={e.Response.StatusCode} " +
                              $"limit={limit} remaining={remaining} reset={resetInfo} profile={profile}";

                Logger.Log(line);                          // 通常のログファイルへ
            }
            catch { /* デバッグ用途のため失敗は無視 */ }
        }

        /// <summary>
        /// GraphQLのURLから Operation 名（例: ListLatestTweetsTimeline）を抽出します。
        /// </summary>
        private static string ExtractGraphqlOperation(string url)
        {
            try
            {
                int q = url.IndexOf('?');
                string path = q >= 0 ? url.Substring(0, q) : url;
                int slash = path.LastIndexOf('/');
                return slash >= 0 ? path.Substring(slash + 1) : path;
            }
            catch { return url; }
        }

        /// <summary>
        /// WebView内の通信レスポンスを監視し、API制限(429)やサーバーエラー(500番台)を検知します。
        /// JavaScriptに依存せず確実にエラーを捕捉できます。
        /// </summary>
        private void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            // デバッグ用レート制限ヘッダー可視化
            if (Logger.EnableDebugLog) LogRateLimitDebug(sender, e);

            // レート制限の監視とカラムへの通知を行う（200応答も含めて常に呼ぶことで、通常時の残量推移も観察できる）
            TrackRateLimit(sender, e);

            // 200番台～300番台は正常なので高速にスキップ
            if (e.Response.StatusCode >= 200 && e.Response.StatusCode < 400) return;

            int code = e.Response.StatusCode;

            // 429 (Too Many Requests) または 500番台 (Server Error) を監視
            if (code == 429 || code >= 500)
            {
                string url = e.Request.Uri;
                if (IsTargetDomain(url))
                {
                    // 429エラーを出しているカラムを特定し、APIの暴走を止める
                    if (code == 429 && !_ignoreRateLimit429 && sender is CoreWebView2 coreWebView)
                    {
                        bool hasReset = TryGetRateLimitReset(e, out DateTimeOffset resetTime);

                        Dispatcher.Invoke(() =>
                        {
                            var targetCol = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);
                            // 即休止＆永久停止をやめ、休止/自動復帰の判断は ColumnData に委譲
                            targetCol?.NotifyRateLimited(hasReset ? resetTime : (DateTimeOffset?)null, hasReset);
                        });
                    }

                    // UIスレッドへの負荷軽減のため、前回の検知から数秒間はスキップ
                    if ((DateTime.Now - _lastDetectedErrorTime).TotalSeconds < 5) return;

                    _lastDetectedErrorTime = DateTime.Now;
                    ReportNetworkError(code); // MainWindow.Status.cs のメソッドを呼び出し
                }
            }
        }

        /// <summary>
        /// ネットワークエラー監視の対象とするドメインかどうかを判定します。
        /// </summary>
        private bool IsTargetDomain(string url)
        {
            return url.Contains("x.com") ||
                   url.Contains("twitter.com") ||
                   url.Contains("api.twitter.com");
        }

        /// <summary>
        /// レート制限の残数を監視し、対象カラムの主タイムラインなら ColumnData へ通知します。
        /// </summary>
        private void TrackRateLimit(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                var headers = e.Response.Headers;
                if (!headers.Contains("x-rate-limit-remaining")) return; // 枠を持つ応答のみ
                if (sender is not CoreWebView2 cw) return;

                var col = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == cw);
                if (col == null) return;

                string op = ExtractGraphqlOperation(e.Request.Uri);
                if (!IsPrimaryTimelineOperation(col, op)) return; // badge_count等の背景通信は対象外

                if (!int.TryParse(headers.GetHeader("x-rate-limit-remaining"), out int remaining)) return;
                DateTimeOffset reset = TryGetRateLimitReset(e, out var r) ? r : DateTimeOffset.Now.AddMinutes(15);

                Dispatcher.Invoke(() => col.UpdateRateLimitStatus(remaining, reset, _ignoreRateLimit429));
            }
            catch { /* 監視失敗は無視 */ }
        }

        /// <summary>
        /// 観測したGraphQL Operationが、そのカラムの「主タイムライン」エンドポイントかを判定します。
        /// （カラムのURL種別に対応するもののみを残数監視の対象にする）
        /// </summary>
        private static bool IsPrimaryTimelineOperation(ColumnData col, string op)
        {
            if (col.IsExternalSite) return false; // X以外のサイトは対象外
            string url = col.Url ?? "";

            if (url.Contains("/search")) return op == "SearchTimeline";
            if (url.Contains("/lists/") || url.Contains("/i/lists/")) return op == "ListLatestTweetsTimeline";
            if (url.Contains("/notifications")) return op == "NotificationsTimeline";
            if (url.Contains("/home")) return op == "HomeTimeline" || op == "HomeLatestTimeline";
            // ユーザーカラム等
            return op == "UserTweets" || op == "UserTweetsAndReplies" || op == "UserMedia";
        }

        /// <summary>
        /// レスポンスから x-rate-limit-reset（Unix秒）を読み取り、復帰予定時刻に変換します。
        /// </summary>
        private static bool TryGetRateLimitReset(CoreWebView2WebResourceResponseReceivedEventArgs e, out DateTimeOffset resetTime)
        {
            resetTime = default;
            try
            {
                var headers = e.Response.Headers;
                if (headers.Contains("x-rate-limit-reset") &&
                    long.TryParse(headers.GetHeader("x-rate-limit-reset"), out long unix))
                {
                    resetTime = DateTimeOffset.FromUnixTimeSeconds(unix);
                    return true;
                }
            }
            catch { /* ヘッダー未提供などは無視 */ }
            return false;
        }

        /// <summary>
        /// URLがYouTube系ドメインかどうかを判定します（PiPで開く対象の検証用）。
        /// </summary>
        private static bool IsYouTubeUrl(string url)
        {
            try
            {
                string host = new Uri(url).Host;
                return host.EndsWith("youtube.com") || host.EndsWith("youtu.be") || host.EndsWith("youtube-nocookie.com");
            }
            catch { return false; }
        }


        // ===== Private Methods (CSS & Script Injection) =====

        /// <summary>
        /// ユーザー設定やカラム設定に基づいて、カスタムCSSをWebViewに注入します。
        /// </summary>
        private async void ApplyCustomCss(CoreWebView2 webView, string url, ColumnData? col = null)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;
                Logger.Log($"[CSS] ApplyCustomCss Start. URL: {url}");

                // 1. 除外URLチェック（投稿/インテント画面にはCSSを当てない）
                if (url.Contains("/compose/") || url.Contains("/intent/"))
                {
                    Logger.Log("[CSS] Skipped (Compose/Intent URL)");
                    return;
                }

                string cssToInject = "";

                // 2. フォント設定（無条件で適用）
                if (!string.IsNullOrEmpty(_appFontFamily))
                {
                    cssToInject += $@"body, div, span, p, a, h1, h2, h3, h4, h5, h6, input, textarea, button, select {{ font-family: '{_appFontFamily}', sans-serif !important; }} ";
                }

                // 2. フォントサイズ設定
                if (_appFontSize > 0)
                {
                    cssToInject += $@"html {{ font-size: {_appFontSize}px !important; }} body {{ font-size: {_appFontSize}px !important; }} div[dir='auto'], span, p, a, [data-testid='tweetText'] span, [data-testid='user-cell'] span {{ font-size: {_appFontSize}px !important; line-height: 1.4 !important; }} ";
                }

                // 3. カラムごとの設定 (RT非表示)
                if (col != null && col.IsRetweetHidden)
                {
                    cssToInject += ScriptDefinitions.CssHideSocialContext + "\n";
                }

                // 4. カラムごとの設定 (Rep非表示)
                if (col != null && col.IsReplyHidden)
                {
                    cssToInject += ScriptDefinitions.CssHideRepliesClass + "\n";
                }

                // 5. カラムごとの設定 (画像サイズ倍率)
                bool isFocusMode = false;
                if (url != null && url.Contains("/status/"))
                {
                    isFocusMode = true;
                }

                if (col != null && col.MediaScalePercentage != 100 && col.MediaScalePercentage > 0 && !isFocusMode)
                {
                    double scale = col.MediaScalePercentage / 100.0;
                    cssToInject += ScriptDefinitions.GetMediaScaleCss(scale) + "\n";
                }

                // 6. ユーザー定義のカスタムCSS
                if (!string.IsNullOrEmpty(_customCss))
                {
                    cssToInject += _customCss + "\n";
                }

                // 7. 表示オプション (ヘッダー、サイドバーなどの非表示)
                bool isXDomain = !string.IsNullOrEmpty(url) && (url.Contains("twitter.com") || url.Contains("x.com"));
                if (isXDomain && !string.IsNullOrEmpty(url))
                {
                    // ホーム画面判定（グローバルトレンドをホーム判定からはじく）
                    bool isHome = url.TrimEnd('/').Equals("https://x.com/home", StringComparison.OrdinalIgnoreCase) ||
                                  url.TrimEnd('/').Equals("https://twitter.com/home", StringComparison.OrdinalIgnoreCase);

                    // メニュー非表示
                    if ((_hideMenuInHome && isHome) || (_hideMenuInNonHome && !isHome))
                    {
                        cssToInject += ScriptDefinitions.CssHideMenu + "\n";
                    }
                    // リストヘッダー簡易表示
                    if (_hideListHeader && url.Contains("/lists/"))
                    {
                        cssToInject += ScriptDefinitions.CssHideListHeader + "\n";
                    }
                    // 右サイドバー非表示
                    if (_hideRightSidebar)
                    {
                        cssToInject += ScriptDefinitions.CssHideRightSidebar + "\n";
                    }
                }

                // 8. 注入するCSSがない場合は処理を中止
                if (string.IsNullOrEmpty(cssToInject))
                {
                    Logger.Log("[CSS] Result: Aborted (No CSS to inject)");
                    return;
                }

                Logger.Log($"[CSS] Injecting CSS... Length: {cssToInject.Length}");

                // ScriptDefinitionsのヘルパーで注入用スクリプトを生成して実行
                string script = ScriptDefinitions.GetCssInjectionScript(cssToInject);
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.Log($"[CSS] Fatal Error: {ex.Message}");
            }
        }

        /// <summary>
        /// CSS設定をすべてのカラムおよびフォーカスビューに適用します。
        /// </summary>
        private void ApplyCssToAllColumns()
        {
            foreach (var col in Columns)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    string currentUrl = col.AssociatedWebView.CoreWebView2.Source;
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, currentUrl, col);
                }
            }
            // フォーカスビューにも適用
            if (FocusWebView?.CoreWebView2 != null)
            {
                ApplyCustomCss(FocusWebView.CoreWebView2, FocusWebView.CoreWebView2.Source, _focusedColumnData);
            }
        }

        /// <summary>
        /// 音量設定をすべてのWebViewへ一括適用します（プロセス音量として設定）。
        /// </summary>
        private void ApplyVolumeToAllWebViews()
        {
            SetWebView2Volume(_appVolume);
        }

        /// <summary>
        /// 音量制御スクリプトを単一のWebViewに適用します。
        /// </summary>
        private async void ApplyVolumeScript(CoreWebView2 webView)
        {
            try
            {
                string script = ScriptDefinitions.GetVolumeScript(_appVolume);
                await webView.ExecuteScriptAsync(script);
            }
            catch { }
        }

        /// <summary>
        /// XのYouTubeカードクリック時の挙動を制御するスクリプトを注入します。
        /// </summary>
        private async void ApplyYouTubeClickScript(CoreWebView2 webView)
        {
            try
            {
                // YouTubeカードクリック時にPiPへ振り替えるか判定するためのフラグを渡す
                await webView.ExecuteScriptAsync($"window.xColumnAutoPipForVideo = {_autoPipForVideo.ToString().ToLower()};");
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptYouTubeClick);
            }
            catch (Exception ex) { Logger.Log($"YouTube script failed: {ex.Message}"); }
        }

        /// <summary>
        /// メディア拡大スクリプトを適用します（フォーカス意図フラグをJSへ渡してから実行）。
        /// </summary>
        private async void ApplyMediaExpandScript(CoreWebView2 webView)
        {
            try
            {
                string jsFlag = _isMediaFocusIntent ? "true" : "false";
                await webView.ExecuteScriptAsync($"window.xColumnForceExpand = {jsFlag};");
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptMediaExpand);
            }
            catch (Exception ex) { Logger.Log($"Media expand script failed: {ex.Message}"); }
        }

        /// <summary>
        /// トレンドカラム（/explore/配下）でのリンククリックを検知し、
        /// キーワードを特定して新規カラムで検索を開くスクリプトを注入します。
        /// </summary>
        private async void ApplyTrendingClickScript(CoreWebView2 webView)
        {
            try
            {
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptTrendingClick);
            }
            catch (Exception ex) { Logger.Log($"ApplyTrendingClickScript Error: {ex.Message}"); }
        }

        /// <summary>
        /// スクロール同期スクリプト（WebView内でのShift+Wheel捕捉）を注入します。
        /// </summary>
        private async void ApplyScrollSyncScript(CoreWebView2 webView)
        {
            try
            {
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptScrollSync);
            }
            catch { }
        }

        /// <summary>
        /// NGワードフィルタースクリプトを単一のWebViewに適用します。
        /// </summary>
        private async void ApplyNgWordsScript(CoreWebView2 webView)
        {
            try
            {
                if (_ngWords != null && _ngWords.Count > 0)
                {
                    string script = ScriptDefinitions.GetNgWordScript(_ngWords);
                    await webView.ExecuteScriptAsync(script);
                }
            }
            catch { }
        }

        /// <summary>
        /// NGワードスクリプトをすべてのカラムおよびフォーカスビューに再適用します（設定変更時用）。
        /// </summary>
        private void ApplyNgWordsToAllColumns(List<string> ngWords)
        {
            string script = ScriptDefinitions.GetNgWordScript(ngWords);

            foreach (var col in Columns)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            if (FocusWebView?.CoreWebView2 != null)
            {
                FocusWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        /// <summary>
        /// 絶対時刻表示用のスクリプトと設定値を単一のWebViewに適用します（WebView初期化時に使用）。
        /// </summary>
        private async void ApplyAbsoluteTimeScript(CoreWebView2 webView)
        {
            try
            {
                // 現在の設定値(true/false)をJSの変数として渡す
                string flagScript = $"window.xColumnShowAbsoluteTime = {(_showAbsoluteTime ? "true" : "false")};";
                await webView.ExecuteScriptAsync(flagScript);

                // メインの変換ロジックを注入
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptAbsoluteTime);
            }
            catch { }
        }

        /// <summary>
        /// 設定変更時に、すべてのWebViewへ絶対時刻設定の有効/無効を即時反映させます。
        /// </summary>
        private async void ApplyAbsoluteTimeSettingsToAll()
        {
            // 1. 設定フラグ更新用スクリプト
            string flagScript = $"window.xColumnShowAbsoluteTime = {(_showAbsoluteTime ? "true" : "false")};";

            // 2. スクリプト本体（再実行で即座に updateAbsTime() が呼ばれる）
            string mainScript = ScriptDefinitions.ScriptAbsoluteTime;

            foreach (var col in Columns)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    try
                    {
                        await col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(flagScript);
                        // 未注入時や即時反映のために本体も再実行
                        await col.AssociatedWebView.CoreWebView2.ExecuteScriptAsync(mainScript);
                    }
                    catch { /* 無視 */ }
                }
            }

            if (FocusWebView?.CoreWebView2 != null)
            {
                try
                {
                    await FocusWebView.CoreWebView2.ExecuteScriptAsync(flagScript);
                    await FocusWebView.CoreWebView2.ExecuteScriptAsync(mainScript);
                }
                catch { /* 無視 */ }
            }
        }

        // ===== Private Methods (Focus Mode) =====

        /// <summary>
        /// 新規ウィンドウ要求時の処理。拡張機能URLはアプリ内のフォーカスモードで開き、
        /// それ以外は外部ブラウザで開きます。
        /// </summary>
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            // if (!e.IsUserInitiated) return;

            string uri = e.Uri;

            // pbs.twimg.com の画像は「画像原寸ボタン」等が1クリックで複数開くため、
            // ポップアップブロッカー無効化設定がONのときに限り、
            // 2枚目以降でユーザー操作フラグが消費されていても許可する
            bool isTwimgImage = uri.StartsWith("https://pbs.twimg.com/", StringComparison.OrdinalIgnoreCase);
            if (!e.IsUserInitiated && !(_disablePopupBlocking && isTwimgImage)) return;

            // 拡張機能の画面は従来どおりアプリ内フォーカスモードで開く
            if (uri.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.InvokeAsync(() => EnterFocusMode(uri));
                return;
            }

            // 外部サイトの開き方を設定に従って振り分ける
            switch (_externalLinkOpenMode)
            {
                case "Pip":
                    Dispatcher.InvokeAsync(() => OpenInPip(uri));
                    break;
                case "Focus":
                    Dispatcher.InvokeAsync(() => EnterFocusModeWithUrl(uri));
                    break;
                default: // "Default" = 既定のブラウザ
                    OpenInDefaultBrowser(uri);
                    break;
            }
        }

        /// <summary>
        /// 指定URLを既定のブラウザで開きます。失敗時はダイアログを表示します。
        /// </summary>
        private void OpenInDefaultBrowser(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch
            {
                string msg = string.Format(Properties.Resources.Err_LinkOpenFailed, url);
                MessageWindow.Show(this, msg, Properties.Resources.Common_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// フォーカスモードに入ります。別プロファイルのカラムではモーダル化せずインライン遷移させます。
        /// </summary>
        private void EnterFocusMode(string url)
        {
            // 対象カラムが「別プロファイル」のものである場合、フォーカスモードには絶対に入らない
            if (_focusedColumnData != null &&
                !string.IsNullOrEmpty(_focusedColumnData.ProfileName) &&
                _focusedColumnData.ProfileName != _activeProfileName)
            {
                // 画像クリック等でJS側の画面遷移が一時停止しているケースがあるため、
                // そのカラム自身（インライン）の中で強制的に対象URLへ遷移させる。
                try
                {
                    _focusedColumnData.AssociatedWebView?.CoreWebView2?.Navigate(url);
                }
                catch { }

                return; // 全画面化(FocusMode)を未然に防ぐ
            }

            _isFocusMode = true;
            _isExternalUrlFocus = false;
            FocusWebView?.CoreWebView2?.Navigate(url);

            ColumnItemsControl.Visibility = Visibility.Hidden;

            FocusViewGrid.Visibility = Visibility.Visible;
            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();
        }

        /// <summary>
        /// YouTube等の動画URLを、フォーカスモードのWebViewで全面表示します。
        /// 既存のEnterFocusModeはx.com前提のため、ドメインチェックを通らない動画URL用に分離しています。
        /// </summary>
        private void EnterFocusModeWithUrl(string url)
        {
            _isFocusMode = true;
            _isExternalUrlFocus = true;

            FocusViewGrid.Visibility = Visibility.Visible;
            ColumnItemsControl.Visibility = Visibility.Hidden;

            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();

            FocusWebView?.CoreWebView2?.Navigate(url);
        }

        /// <summary>
        /// フォーカスモードを終了し、カラム表示・タイマー・CSSを復帰させます。
        /// </summary>
        private void ExitFocusMode()
        {
            if (!_isFocusMode) return;
            _isFocusMode = false;
            _isMediaFocusIntent = false;
            _isExternalUrlFocus = false;

            // 1. スクリプトの実行を強制停止してから白紙へ遷移
            if (FocusWebView?.CoreWebView2 != null)
            {
                try
                {
                    FocusWebView.CoreWebView2.Stop();
                    FocusWebView.CoreWebView2.Navigate("about:blank");
                }
                catch { }
            }

            // 2. フォーカスビューを非表示にする
            FocusViewGrid.Visibility = Visibility.Collapsed;

            ColumnItemsControl.Visibility = Visibility.Visible;

            // 3. 状態のリセットとフォーカス復帰
            if (_focusedColumnData != null)
            {
                var col = _focusedColumnData;
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    string colUrl = col.AssociatedWebView.CoreWebView2.Source;
                    if (IsAllowedDomain(colUrl, true) && colUrl != col.Url)
                    {
                        if (col.AssociatedWebView.CoreWebView2.CanGoBack)
                        {
                            col.AssociatedWebView.CoreWebView2.GoBack();
                        }
                    }
                    col.ResetCountdown();

                    Dispatcher.InvokeAsync(() =>
                    {
                        col.AssociatedWebView.Focus();
                    }, DispatcherPriority.Render);
                }
            }
            else
            {
                this.Focus();
            }
            _focusedColumnData = null;

            // 4. タイマーを再開
            foreach (var c in Columns) c.UpdateTimer();
            _countdownTimer.Start();

            // 5. CSSを再適用
            Dispatcher.InvokeAsync(() =>
            {
                ApplyCssToAllColumns();
            }, DispatcherPriority.Loaded);
        }

        /// <summary>
        /// フォーカスビューの閉じるボタンクリック時の処理。
        /// </summary>
        private void CloseFocusView_Click(object sender, RoutedEventArgs e) => ExitFocusMode();

        /// <summary>
        /// フォーカスモードの背景（半透明部分）クリック時の処理。背景クリック時のみ詳細を閉じます。
        /// </summary>
        private void FocusViewGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // クリックされた要素が FocusViewGrid 自体（背景部分）であるか確認
            if (e.OriginalSource == FocusViewGrid)
            {
                ExitFocusMode();
            }
        }

        // ===== Private Methods (Domain Check) =====

        /// <summary>
        /// URLが許可ドメイン(x.com / twitter.com)かを判定します。
        /// focus=true の場合は /status/ や /settings などの「フォーカス対象URL」かどうかを返します。
        /// </summary>
        private bool IsAllowedDomain(string url, bool focus = false)
        {
            // 空URLやabout:blankは許可
            if (string.IsNullOrEmpty(url) || url == "about:blank") return true;
            // 拡張機能のURLは許可
            if (url.StartsWith("chrome-extension://")) return true;
            // HTTP/HTTPS以外は拒否
            if (!url.StartsWith("http")) return false;
            try
            {
                Uri uri = new Uri(url);
                // ホスト名がx.comまたはtwitter.comでない場合は拒否
                if (!uri.Host.EndsWith("x.com") && !uri.Host.EndsWith("twitter.com")) return false;
                bool isFocusUrl = uri.AbsolutePath.Contains("/status/") || uri.AbsolutePath.Contains("/settings");
                return focus ? isFocusUrl : !isFocusUrl;
            }
            catch { return false; }
        }

        // ===== Private Methods (Context Menu & Actions) =====

        /// <summary>
        /// コンテキストメニュー表示時の処理。標準項目を整理し、画像保存・リンクコピー・
        /// 選択テキストのGoogle検索/NGワード追加などの独自項目を追加します。
        /// </summary>
        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (sender is not CoreWebView2 coreWebView) return;
            var currentEnv = coreWebView.Environment;

            CoreWebView2ContextMenuTargetKind kind = CoreWebView2ContextMenuTargetKind.Page;
            string srcUri = "", linkUri = "", selectionText = "";
            bool hasSelection = false;
            try
            {
                var target = e.ContextMenuTarget;
                try { kind = target.Kind; } catch (Exception ex) { Logger.Log($"[ContextMenu build error] Kind ex: {ex.Message}"); }
                try { srcUri = target.SourceUri ?? ""; } catch (Exception ex) { Logger.Log($"[ContextMenu build error] SourceUri ex: {ex.Message}"); }
                try { linkUri = target.LinkUri ?? ""; } catch (Exception ex) { Logger.Log($"[ContextMenu build error] LinkUri ex: {ex.Message}"); }
                try { hasSelection = (kind == CoreWebView2ContextMenuTargetKind.SelectedText); } catch { }
                try { if (hasSelection) selectionText = target.SelectionText ?? ""; } catch (Exception ex) { Logger.Log($"[Ctx] SelText ex: {ex.Message}"); }
            }
            catch (Exception ex) { Logger.Log($"[ContextMenu build error] target read fatal: {ex.Message}"); }

            Logger.Log($"[ContextMenu build error] read done. kind={kind}, initialMenuCount={e.MenuItems.Count}");

            try
            {
                // 独自項目を出す対象か判定（画像・リンク・選択テキストのいずれか）
                bool isImage = (kind == CoreWebView2ContextMenuTargetKind.Image);
                bool hasLink = !string.IsNullOrEmpty(linkUri) && kind != CoreWebView2ContextMenuTargetKind.SelectedText;
                bool hasSelected = !string.IsNullOrEmpty(selectionText);

                // どれにも該当しないなら、WebView2標準メニューをそのまま表示（Clearしない）
                if (!isImage && !hasLink && !hasSelected)
                {
                    Logger.Log($"[ContextMenu build error] no custom items. keep default. count={e.MenuItems.Count}");
                    return;
                }

                // ここからは独自項目を出すケース。標準のcopy/cut/pasteだけ残して整理する
                var copyItem = e.MenuItems.FirstOrDefault(i => i.Name == "copy");
                var cutItem = e.MenuItems.FirstOrDefault(i => i.Name == "cut");
                var pasteItem = e.MenuItems.FirstOrDefault(i => i.Name == "paste");

                e.MenuItems.Clear();

                if (copyItem != null) e.MenuItems.Add(copyItem);
                if (cutItem != null) e.MenuItems.Add(cutItem);
                if (pasteItem != null) e.MenuItems.Add(pasteItem);

                // A. 画像の保存
                if (isImage && currentEnv != null)
                {
                    if (e.MenuItems.Count > 0)
                        e.MenuItems.Add(currentEnv.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                    var saveImageItem = currentEnv.CreateContextMenuItem(Properties.Resources.Ctx_SaveImageAs, null, CoreWebView2ContextMenuItemKind.Command);
                    string srcUrl = srcUri;
                    saveImageItem.CustomItemSelected += async (s, args) => await DownloadAndSaveImageAsync(srcUrl);
                    e.MenuItems.Add(saveImageItem);
                }

                // B. リンクのコピー
                if (hasLink && currentEnv != null)
                {
                    if (!isImage && e.MenuItems.Count > 0)
                        e.MenuItems.Add(currentEnv.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                    var linkCopyItem = currentEnv.CreateContextMenuItem(Properties.Resources.Ctx_CopyLinkAddress, null, CoreWebView2ContextMenuItemKind.Command);
                    string targetUrl = linkUri;
                    linkCopyItem.CustomItemSelected += (s, args) => { try { Clipboard.SetText(targetUrl); } catch { } };
                    e.MenuItems.Add(linkCopyItem);
                }

                // C. 選択テキスト（Google検索・NGワード追加）
                if (hasSelected && currentEnv != null)
                {
                    string displayLabel = selectionText.Length > 15 ? selectionText.Substring(0, 15) + "..." : selectionText;
                    e.MenuItems.Add(currentEnv.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                    string searchLabel = string.Format(Properties.Resources.Ctx_GoogleSearch, displayLabel);
                    var searchItem = currentEnv.CreateContextMenuItem(searchLabel, null, CoreWebView2ContextMenuItemKind.Command);
                    searchItem.CustomItemSelected += (s, args) => PerformGoogleSearch(selectionText);
                    e.MenuItems.Add(searchItem);
                    e.MenuItems.Add(currentEnv.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                    string ngLabel = string.Format(Properties.Resources.Ctx_AddNgWord, displayLabel);
                    var ngItem = currentEnv.CreateContextMenuItem(ngLabel, null, CoreWebView2ContextMenuItemKind.Command);
                    ngItem.CustomItemSelected += (s, args) => AddNgWord(selectionText);
                    e.MenuItems.Add(ngItem);
                }

                Logger.Log($"[ContextMenu build error] DONE. finalCount={e.MenuItems.Count}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[ContextMenu build error] build error: {ex.Message}");
            }
        }

        /// <summary>
        /// 画像をダウンロードして保存します。X(Twitter)のformatクエリにも対応します。
        /// </summary>
        private async Task DownloadAndSaveImageAsync(string url)
        {
            try
            {
                // 1. ファイル名の推定と拡張子の決定
                string fileName = "image.jpg";
                string extension = ".jpg"; // デフォルト拡張子

                try
                {
                    Uri uri = new Uri(url);

                    // X(Twitter)の画像URL対応: クエリパラメータ "format" を確認
                    var queryDictionary = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    string? format = queryDictionary["format"];

                    if (!string.IsNullOrEmpty(format))
                    {
                        extension = "." + format;
                        // パス部分のファイル名にクエリ指定の拡張子を付与
                        fileName = Path.GetFileName(uri.LocalPath) + extension;
                    }
                    else
                    {
                        // 通常のURL（パスに拡張子が含まれる場合）
                        string path = uri.LocalPath;
                        string ext = Path.GetExtension(path);
                        if (!string.IsNullOrEmpty(ext))
                        {
                            extension = ext;
                            fileName = Path.GetFileName(path);
                        }
                        else
                        {
                            // 拡張子がない場合はデフォルトを使用
                            fileName = Path.GetFileName(path) + extension;
                        }
                    }
                }
                catch { }

                // 2. SaveFileDialog の表示
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.FileName = fileName;

                // 拡張子フィルタを動的に設定（該当拡張子を優先）
                dialog.DefaultExt = extension;
                dialog.Filter = $"{Properties.Resources.Filter_ImageFiles} (*{extension})|*{extension}|{Properties.Resources.Filter_AllFiles} (*.*)|*.*";

                if (dialog.ShowDialog() == true)
                {
                    // 3. ダウンロードと保存
                    using (var client = new HttpClient())
                    {
                        var data = await client.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(dialog.FileName, data);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format(Properties.Resources.Err_SaveImageFailed, ex.Message);
                MessageWindow.Show(this, msg, Properties.Resources.Common_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 指定されたテキストで既定ブラウザのGoogle検索を開きます。
        /// </summary>
        private void PerformGoogleSearch(string text)
        {
            try
            {
                string query = Uri.EscapeDataString(text);
                string url = $"https://www.google.com/search?q={query}";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log($"Google Search Error: {ex.Message}");
            }
        }

        /// <summary>
        /// NGワードを追加し、保存して全カラムへ即時反映させます。
        /// </summary>
        private void AddNgWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            // ファイル読み込みではなく、メモリ上の _ngWords を操作して SaveSettings を呼ぶ
            if (!_ngWords.Contains(word))
            {
                _ngWords.Add(word);

                // メインの保存メソッドで一括保存
                SaveSettings(_activeProfileName);

                // 全カラムに反映
                ApplyNgWordsToAllColumns(_ngWords);

                string msg = string.Format(Properties.Resources.Msg_NgWordAdded, word);
                MessageWindow.Show(this, msg, Properties.Resources.Title_Registered, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ===== Private Methods (Column Delegation & Helpers) =====

        /// <summary>
        /// ColumnData からの「戻る」要求を受けて、実WebViewで履歴を戻します。
        /// </summary>
        private void OnColumnGoBackRequested(ColumnData col)
        {
            if (col.AssociatedWebView?.CoreWebView2 != null && col.AssociatedWebView.CoreWebView2.CanGoBack)
            {
                col.AssociatedWebView.CoreWebView2.GoBack();
            }
        }

        /// <summary>
        /// ColumnData からの休止/復帰要求を受けて、休止HTMLの表示または元URLへの復帰を行います。
        /// </summary>
        /// <param name="col">対象カラム。</param>
        /// <param name="suspend">true=休止HTML表示, false=元URLへ復帰。</param>
        private void OnColumnSuspendRequested(ColumnData col, bool suspend)
        {
            var core = col.AssociatedWebView?.CoreWebView2;
            if (core == null) return;

            if (suspend)
            {
                // メモリ解放のための軽量HTML（ダークモード風）を表示
                core.NavigateToString(BuildSuspendScreenHtml(Properties.Resources.Suspend_PausedTitle,
                                                             Properties.Resources.Suspend_PausedBody));
            }
            else
            {
                // 元のURLに遷移して再描画
                core.Navigate(col.Url);
            }
        }

        /// <summary>
        /// レート制限(429)による休止表示要求。専用画面に復帰予定時刻を添えて表示します。
        /// </summary>
        private void OnColumnRateLimitSuspendRequested(ColumnData col, DateTimeOffset resumeAt)
        {
            // 既定では429休止画面を出さず、タイムライン表示を保ったまま静かに休止・自動復帰する
            if (!_showRateLimit429Screen) return;

            try
            {
                // 本文に復帰予定時刻の行を改行で追記（BuildSuspendScreenHtml側で \r\n → <br> 変換される）
                string body = Properties.Resources.Suspend_RateLimitBody
                            + "\r\n"
                            + string.Format(Properties.Resources.Suspend_RateLimitResumeAt,
                                            resumeAt.LocalDateTime.ToString("HH:mm:ss"));

                col.AssociatedWebView?.CoreWebView2?.NavigateToString(
                    BuildSuspendScreenHtml(Properties.Resources.Suspend_RateLimitTitle, body));
            }
            catch { /* WebView破棄時のエラー回避 */ }
        }

        /// <summary>
        /// 休止/エラー時に表示する軽量なHTML（ダークモード風）を生成します。
        /// </summary>
        /// <param name="title">見出し（例: 休止中、API制限）。</param>
        /// <param name="bodyHtml">本文（&lt;br&gt; を含むHTML断片を許容）。</param>
        private static string BuildSuspendScreenHtml(string title, string bodyHtml)
        {
            // リソース文字列中の改行を<br>に変換（改行コードのゆらぎに対応）
            string body = (bodyHtml ?? "")
                .Replace("\r\n", "<br>")
                .Replace("\n", "<br>")
                .Replace("\r", "<br>");

            return $@"
                <html>
                <head><meta charset='utf-8'></head>
                <body style='background-color:#15202B; color:#8899A6; display:flex; justify-content:center; align-items:center; height:100vh; margin:0; font-family:sans-serif;'>
                    <div style='text-align:center;'>
                        <h2 style='margin-bottom:10px;'>{title}</h2>
                        <p style='font-size:14px;'>{body}</p>
                    </div>
                </body>
                </html>";
        }
    }
}