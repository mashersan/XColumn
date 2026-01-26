using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using XColumn.Models;

// 曖昧さ回避
using Clipboard = System.Windows.Clipboard;

namespace XColumn
{
    /// <summary>
    /// MainWindowのWebView関連のロジック（初期化、イベントハンドラ、CSS注入、スクリプト連携など）を管理します。
    /// </summary>
    public partial class MainWindow
    {
        // 拡張機能のロード状態
        private bool _extensionsLoaded = false;

        private bool _isMediaFocusIntent = false;

        /// <summary>
        /// WebView2環境を初期化し、ブラウザデータフォルダを設定します。
        /// </summary>
        private async Task InitializeWebViewEnvironmentAsync()
        {
            // プロファイルごとのブラウザデータフォルダを作成
            string browserDataFolder = Path.Combine(_userDataFolder, "BrowserData", _activeProfileName);
            Directory.CreateDirectory(browserDataFolder);

            // WebView2環境オプションの設定
            var options = new CoreWebView2EnvironmentOptions();
            options.AreBrowserExtensionsEnabled = true;

            // user-gesture-required: ユーザーがクリック等の操作をするまで動画を自動再生しない
            if (_forceDisableAutoPlay)
            {
                string args = options.AdditionalBrowserArguments ?? "";
                options.AdditionalBrowserArguments = $"{args} --autoplay-policy=user-gesture-required".Trim();
            }

            // GPU無効化設定
            if (_disableGpu)
            {
                // 既存の引数がある場合はスペースで区切って追記、なければそのまま設定
                string currentArgs = options.AdditionalBrowserArguments ?? "";
                options.AdditionalBrowserArguments = $"{currentArgs} --disable-gpu".Trim();

                Logger.Log("WebView2 initialized with --disable-gpu");
            }


            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, browserDataFolder, options);
            await InitializeFocusWebView();
        }

        /// <summary>
        /// WebView2コントロールがロードされたときの処理。
        /// </summary>
        private void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            // CoreWebView2が未初期化の場合、環境を指定して初期化を開始
            if (sender is WebView2 webView && webView.CoreWebView2 == null && _webViewEnvironment != null)
            {
                webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                webView.EnsureCoreWebView2Async(_webViewEnvironment);
            }
        }

        /// <summary>
        /// CoreWebView2の初期化完了時の処理。
        /// </summary>
        private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            // 初期化成功時に各種設定とイベントハンドラを登録
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

                    // 拡張機能のロード
                    if (!_extensionsLoaded)
                    {
                        _extensionsLoaded = true;
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
        /// ブラウザ(JavaScript)から送信されたメッセージを受け取り処理します。
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // 受信したメッセージをJSONとして解析
                string jsonString = e.TryGetWebMessageAsString();
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
                            // 送信元のWebViewを探す（Columnsから）
                            // senderはCoreWebView2なので、それを持つWebView2コントロールを探す必要はない
                            // CoreWebView2自体にメソッドがある
                            if (coreWebView.CanGoBack)
                            {
                                coreWebView.GoBack();
                            }
                        }
                    }
                }
                else if (type == "openNewColumn")
                {
                    string? url = json?["url"]?.GetValue<string>();
                    Logger.Log($"[WebView Message] openNewColumn received. URL: {url}");

                    if (!string.IsNullOrEmpty(url) && IsAllowedDomain(url))
                    {
                        // UIスレッドで実行
                        Dispatcher.InvokeAsync(() => AddNewColumn(url));
                    }
                }
                // デバッグログ受信
                else if (type == "debugLog")
                {
                    string? message = json?["message"]?.GetValue<string>();
                    Logger.Log($"[JS Log] {message}");
                }

                // フォーカスモードを直接開くメッセージの処理
                if (type == "openFocusMode")
                {
                    string? url = json?["url"]?.GetValue<string>();
                    // Logger.Log($"[C#_FocusMode] Triggering OpenFocusMode for: {url}");
                    if (!string.IsNullOrEmpty(url) && IsAllowedDomain(url, true))
                    {
                        _isMediaFocusIntent = url.Contains("/photo/") || url.Contains("/video/");

                        if (_isMediaFocusIntent && !_disableFocusModeOnMediaClick)
                        {
                            if (sender is CoreWebView2 coreWebView)
                            {
                                _focusedColumnData = Columns.FirstOrDefault(c => c.AssociatedWebView?.CoreWebView2 == coreWebView);
                                coreWebView.Navigate(url);
                            }
                            else if (IsAllowedDomain(url, true))
                            {
                                Dispatcher.InvokeAsync(() => OpenFocusMode(url));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"WebMessageReceived Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 登録された拡張機能をWebView2プロファイルにロードします。
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
                        MessageWindow.Show(this, $"拡張機能 '{ext.Name}' の読み込みに失敗しました。\n\n{ex.Message}", "拡張機能エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        /// オプションページのパスを取得します。
        /// </summary>
        private string GetOptionsPagePath(string extensionFolder)
        {
            try
            {
                // manifest.jsonを読み込み
                string manifestPath = Path.Combine(extensionFolder, "manifest.json");
                if (!File.Exists(manifestPath)) return "";

                // JSONを解析してオプションページのパスを取得
                string jsonString = File.ReadAllText(manifestPath);
                var json = JsonNode.Parse(jsonString);

                // Old Twitter Layout拡張機能の特別対応
                if (json == null) return "";

                string? name = json["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(name) && name.Contains("Old Twitter Layout"))
                {
                    return "https://x.com/old/settings";
                }

                // 通常のオプションページパスの取得
                string? page = json["options_ui"]?["page"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["options_page"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["action"]?["default_popup"]?.GetValue<string>();
                if (string.IsNullOrEmpty(page)) page = json["browser_action"]?["default_popup"]?.GetValue<string>();

                return page ?? "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// 拡張機能のオプションページを開きます。
        /// </summary>
        public void OpenExtensionOptions(ExtensionItem ext)
        {
            // 拡張機能IDとオプションページが有効か確認
            if (string.IsNullOrEmpty(ext.Id) || string.IsNullOrEmpty(ext.OptionsPage))
            {
                MessageWindow.Show(this, "この拡張機能には設定ページがないか、まだロードされていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // オプションページのURLを構築
            string url = (ext.OptionsPage.StartsWith("http://") || ext.OptionsPage.StartsWith("https://"))
                ? ext.OptionsPage
                : $"chrome-extension://{ext.Id}/{ext.OptionsPage}";

            EnterFocusMode(url);
        }

        /// <summary>
        /// カラム用WebViewの設定を行います。
        /// </summary>
        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            // WebViewの各種設定
            webView.CoreWebView2.Settings.IsScriptEnabled = true;

            // 右クリックのコンテキストメニュー有効化
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // 最新のChromeとして認識させるためのUser Agent設定
            //webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

            // DevTools無効化
            webView.CoreWebView2.Settings.AreDevToolsEnabled = _enableDevTools;

            col.AssociatedWebView = webView;
            col.InitializeTimer();

            // ズーム倍率の初期適用
            webView.ZoomFactor = col.ZoomFactor;

            // ZoomFactorプロパティの変更を監視してWebViewに適用
            col.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ColumnData.ZoomFactor))
                {
                    try
                    {
                        // UIスレッドで実行
                        webView.Dispatcher.Invoke(() =>
                        {
                            webView.ZoomFactor = col.ZoomFactor;
                        });
                    }
                    catch { /* WebViewが破棄されている場合などは無視 */ }
                }
            };

            // アプリがアクティブな場合、タイマーを停止
            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            webView.CoreWebView2.NavigationStarting += (s, args) =>
            {
                string url = args.Uri;
                if (url.StartsWith("chrome-extension://") || url == "about:blank") return;

                // 詳細ページへの遷移を検知した場合
                if (IsAllowedDomain(url, true) && !_isFocusMode)
                {
                    // カラムの遷移は許可する（args.Cancel = true はしない）

                    _focusedColumnData = col; // どのカラムが遷移したか記録
                    _isMediaFocusIntent = url.Contains("/photo/") || url.Contains("/video/");

                    // モーダルを起動
                    EnterFocusMode(url);
                }
            };

            // 入力状態受信ハンドラ
            webView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string jsonString = e.TryGetWebMessageAsString();
                    var json = JsonNode.Parse(jsonString);
                    if (json?["type"]?.GetValue<string>() == "inputState")
                    {
                        bool isActive = json["val"]?.GetValue<bool>() ?? false;
                        col.IsInputActive = json["val"]?.GetValue<bool>() ?? false;

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

            webView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                if (args.IsSuccess)
                {
                    ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source, col);
                    ApplyVolumeScript(webView.CoreWebView2);
                    ApplyYouTubeClickScript(webView.CoreWebView2);

                    // 自動再生無効化スクリプト注入
                    if (_forceDisableAutoPlay)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                    }
                    // リスト自動遷移ロジック
                    if (col.IsListAutoNav)
                    {
                        // ホーム画面(x.com または twitter.com)にいる場合のみ実行
                        string src = webView.CoreWebView2.Source;
                        if (src.Contains("x.com") || src.Contains("twitter.com"))
                        {
                            // 設定画面で指定された待機時間（ミリ秒）を使用
                            await Task.Delay(_listAutoNavDelay);

                            // ScriptDefinitionsからスクリプトを取得して実行
                            string result = await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptClickListButton);

                            // クリックに成功したらフラグをオフにする
                            if (result.Contains("clicked"))
                            {
                                col.IsListAutoNav = false;
                            }
                        }
                    }

                    // スクロール同期スクリプトを適用（WebView内でのShift+Wheelを捕捉）
                    ApplyScrollSyncScript(webView.CoreWebView2);
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectReplies);
                    // 入力監視スクリプト注入 
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectInput);

                    // NGワードフィルタースクリプト注入
                    ApplyNgWordsScript(webView.CoreWebView2);

                    // キー入力監視スクリプト注入 (ESCキー対応)
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                    // スクロール位置保持スクリプト注入
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptPreserveScrollPosition);

                    // メディアクリックのインターセプトスクリプトを注入
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptInterceptClick);
                }
            };

            // ソースURL変更時の処理登録
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                // URLを取得
                string url = webView.CoreWebView2.Source;
                // 拡張機能のURLは無視
                if (url.StartsWith("chrome-extension://")) return;

                // URL変更時のモデル更新
                // 修正: 設定ページ(/settings)や詳細ページ(/status/)などの「Focus対象」URLは、
                // カラムの基点URLとして保存したくないため、モデル(col.Url)には反映しない。
                // これにより、再起動時は直前のタイムライン（ホームなど）が復元される。
                if (IsAllowedDomain(url, false))
                {
                    col.Url = url;
                }
                /*
                // URL変更時は即座にモデルを更新する（判定遅れを防ぐため）
                if (IsAllowedDomain(url) || IsAllowedDomain(url, true))
                {
                    col.Url = url;
                }
                */
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

                // フォーカスモード判定
                // IsAllowedDomain(url, true) は /status/ や /settings などの詳細ページの場合に true を返す
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

                if (isFocusTarget)
                {
                    if (!_isFocusMode)
                    {
                        bool comingFromCompose = !string.IsNullOrEmpty(col.Url) &&
                                                 (col.Url.Contains("/compose/") || col.Url.Contains("/intent/"));

                        if (!comingFromCompose)
                        {
                            _focusedColumnData = col;
                            EnterFocusMode(url);
                        }
                    }
                }
            };

            webView.CoreWebView2.Navigate(col.Url);
        }

        /// <summary>
        /// トレンドカラム（/explore/配下）でのリンククリックを検知し、
        /// テキスト解析によってキーワードを特定し、新規カラムで検索を開くスクリプトを注入します。
        /// </summary>
        private async void ApplyTrendingClickScript(CoreWebView2 webView)
        {
            try
            {
                // ScriptDefinitionsから取得
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptTrendingClick);
            }
            catch (Exception ex) { Logger.Log($"ApplyTrendingClickScript Error: {ex.Message}"); }
        }

        private async void ApplyScrollSyncScript(CoreWebView2 webView)
        {
            try
            {
                // ScriptDefinitionsから取得
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptScrollSync);
            }
            catch { }
        }

        /// <summary>
        /// ユーザー設定やカラム設定に基づいて、カスタムCSSをWebViewに注入します。
        /// </summary>
        private async void ApplyCustomCss(CoreWebView2 webView, string url, ColumnData? col = null)
        {
            try
            {
                Logger.Log($"[CSS] ApplyCustomCss Start. URL: {url}");

                // 1. 除外URLチェック
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

                // 5. ユーザー定義のカスタムCSS
                if (!string.IsNullOrEmpty(_customCss))
                {
                    cssToInject += _customCss + "\n";
                }

                // 6. 表示オプション (ヘッダー、サイドバーなどの非表示)
                // 許可ドメイン
                bool isXDomain = url.Contains("twitter.com") || url.Contains("x.com");
                if (isXDomain)
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

                // 6. CSS注入実行
                if (string.IsNullOrEmpty(cssToInject))
                {
                    Logger.Log("[CSS] Result: Aborted (No CSS to inject)");
                    return;
                }

                Logger.Log($"[CSS] Injecting CSS... Length: {cssToInject.Length}");

                // ScriptDefinitionsのヘルパーメソッドを使って注入用スクリプトを生成
                string script = ScriptDefinitions.GetCssInjectionScript(cssToInject);

                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.Log($"[CSS] Fatal Error: {ex.Message}");
            }
        }

        /// <summary>
        /// CSS設定をすべてのカラムに適用します。
        /// </summary>
        private void ApplyCssToAllColumns()
        {
            foreach (var col in Columns)
            {
                // 各カラムのWebViewにCSSを適用
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
        /// 音量設定をすべてのカラムに適用します。
        /// </summary>
        private void ApplyVolumeToAllWebViews()
        {
            foreach (var col in Columns)
            {
                // 各カラムのWebViewに音量スクリプトを適用
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    ApplyVolumeScript(col.AssociatedWebView.CoreWebView2);
                }
            }
            // フォーカスビューにも適用
            if (FocusWebView?.CoreWebView2 != null)
            {
                ApplyVolumeScript(FocusWebView.CoreWebView2);
            }
        }

        /// <summary>
        /// 音量制御スクリプトを適用します。
        /// </summary>
        private async void ApplyVolumeScript(CoreWebView2 webView)
        {
            try
            {
                // ScriptDefinitionsから生成メソッドを使用
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
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptYouTubeClick);
            }
            catch (Exception ex) { Logger.Log($"YouTube script failed: {ex.Message}"); }
        }

        /// <summary>
        /// メディア拡大スクリプトを適用します。
        /// </summary>
        private async void ApplyMediaExpandScript(CoreWebView2 webView)
        {
            try
            {
                // 1. フラグを確認
                string jsFlag = _isMediaFocusIntent ? "true" : "false";
                await webView.ExecuteScriptAsync($"window.xColumnForceExpand = {jsFlag};");
                await webView.ExecuteScriptAsync(ScriptDefinitions.ScriptMediaExpand);
            }
            catch (Exception ex) { Logger.Log($"Media expand script failed: {ex.Message}"); }
        }

        /// <summary>
        /// フォーカスモード用のWebViewを初期化します。
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            // フォーカスWebViewと環境が有効でない場合は処理を中止
            if (FocusWebView == null || _webViewEnvironment == null) return;
            // CoreWebView2の初期化を待機
            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            // 初期化完了後の設定
            if (FocusWebView.CoreWebView2 != null)
            {

                // --- ブラウザ標準ダイアログを無効化し、フリーズを根本的に防ぐ ---
                // これにより alert や confirm、beforeunload ダイアログでアプリが止まらなくなります
                FocusWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                // --- スクリプトダイアログの制御 ---
                FocusWebView.CoreWebView2.ScriptDialogOpening += (s, args) =>
                {
                    // フォーカスモードを終了している最中にダイアログが出た場合、
                    // ユーザーには見えないため、自動的に承認して続行させる
                    if (!_isFocusMode)
                    {
                        args.Accept();
                    }
                };


                FocusWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                // フォーカスビューでもスクロール同期メッセージを受信
                FocusWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // 拡張機能のロード
                if (!_extensionsLoaded)
                {
                    _extensionsLoaded = true;
                    await LoadExtensionsAsync(FocusWebView.CoreWebView2.Profile);
                }

                // ナビゲーション完了時の処理登録
                FocusWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    // ナビゲーション成功時に各種スクリプトとCSSを適用
                    if (args.IsSuccess)
                    {
                        ApplyCustomCss(FocusWebView.CoreWebView2, FocusWebView.CoreWebView2.Source, _focusedColumnData);
                        ApplyVolumeScript(FocusWebView.CoreWebView2);
                        ApplyMediaExpandScript(FocusWebView.CoreWebView2);
                        ApplyScrollSyncScript(FocusWebView.CoreWebView2);
                        // 自動再生無効化スクリプト注入
                        if (_forceDisableAutoPlay)
                        {
                            FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDisableVideoAutoplay);
                        }
                        // --
                        // ESCキー監視
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                        // スクロール位置保持スクリプト注入
                        FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptPreserveScrollPosition);
                    }
                };

                // ソースURL変更時の処理登録
                FocusWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    // URLを取得
                    string url = FocusWebView.CoreWebView2.Source;
                    // 拡張機能のURLは無視
                    if (url.StartsWith("chrome-extension://")) return;

                    // 各種スクリプトとCSSを適用
                    ApplyCustomCss(FocusWebView.CoreWebView2, url, _focusedColumnData);

                    // ESCキー監視
                    FocusWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectKeyInput);

                    // ドメイン許可チェックとフォーカスモードの制御
                    bool keepFocus = IsAllowedDomain(url, true) ||
                                     url.Contains("/compose/") ||
                                     url.Contains("/intent/") ||
                                     url.Contains("/settings");

                    // フォーカスモード終了条件の判定
                    if (_isFocusMode && !keepFocus && url != "about:blank")
                    {
                        ExitFocusMode();
                    }
                };
            }
        }

        /// <summary>
        /// 新規ウィンドウ要求時の処理。
        /// </summary>
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (e.IsUserInitiated)
            {
                try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
                catch
                {
                    string msg = string.Format(Properties.Resources.Err_LinkOpenFailed, e.Uri);
                    MessageWindow.Show(this, msg, Properties.Resources.Common_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // MainWindow.WebView.cs

        /// <summary>
        /// フォーカスモードに入ります。
        /// </summary>
        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url);
            FocusViewGrid.Visibility = Visibility.Visible;
            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();
        }

        /// <summary>
        /// フォーカスモードを終了します。
        /// </summary>
        private void ExitFocusMode()
        {
            if (!_isFocusMode) return;
            _isFocusMode = false;
            _isMediaFocusIntent = false;

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

                    Dispatcher.InvokeAsync(() => {
                        col.AssociatedWebView.Focus();
                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            else
            {
                this.Focus();
            }
            _focusedColumnData = null;

            // 4.タイマーを再開
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
        /// ドメイン許可チェック。
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

        /// <summary>
        /// コンテキストメニュー表示時の処理。
        /// </summary>
        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            // 1. 標準メニューから必要な項目を退避
            // 順序: コピー -> 貼り付け
            var copyItem = e.MenuItems.FirstOrDefault(i => i.Name == "copy");
            var cutItem = e.MenuItems.FirstOrDefault(i => i.Name == "cut");
            var pasteItem = e.MenuItems.FirstOrDefault(i => i.Name == "paste");

            // 2. メニューを一度すべてクリア
            e.MenuItems.Clear();

            // 3. 標準メニューを追加 (コピー -> 貼り付け -> 切り取り)
            if (copyItem != null) e.MenuItems.Add(copyItem);
            if (cutItem != null) e.MenuItems.Add(cutItem);
            if (pasteItem != null) e.MenuItems.Add(pasteItem);


            // A. 画像の場合の保存メニュー
            if (e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.Image && _webViewEnvironment != null)
            {
                // セパレータ
                if (e.MenuItems.Count > 0)
                {
                    e.MenuItems.Add(_webViewEnvironment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                }

                var saveImageItem = _webViewEnvironment.CreateContextMenuItem(
                    Properties.Resources.Ctx_SaveImageAs, null, CoreWebView2ContextMenuItemKind.Command);

                string srcUrl = e.ContextMenuTarget.SourceUri;
                saveImageItem.CustomItemSelected += async (s, args) =>
                {
                    await DownloadAndSaveImageAsync(srcUrl);
                };
                e.MenuItems.Add(saveImageItem);
            }

            // B. リンクがある場合のコピーメニュー
            if (!(e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.SelectedText) && !string.IsNullOrEmpty(e.ContextMenuTarget.LinkUri) && _webViewEnvironment != null)
            {
                // 画像メニューがなく、かつ上に項目がある場合のみセパレータ追加 (重複防止)
                if (e.ContextMenuTarget.Kind != CoreWebView2ContextMenuTargetKind.Image && e.MenuItems.Count > 0)
                {
                    e.MenuItems.Add(_webViewEnvironment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                }

                // リンクコピー項目作成
                var linkCopyItem = _webViewEnvironment.CreateContextMenuItem(
                    Properties.Resources.Ctx_CopyLinkAddress, null, CoreWebView2ContextMenuItemKind.Command);

                string targetUrl = e.ContextMenuTarget.LinkUri;
                linkCopyItem.CustomItemSelected += (s, args) =>
                {
                    try { Clipboard.SetText(targetUrl); } catch { }
                };

                e.MenuItems.Add(linkCopyItem);
            }

            // テキスト選択内容の取得（エラー対策済み）
            string selectedText = "";
            try
            {
                if (e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.SelectedText)
                {
                    selectedText = e.ContextMenuTarget.SelectionText;
                }
            }
            catch { /* 無視 */ }

            // 4. 独自メニューの追加 (テキスト選択時のみ)
            if (!string.IsNullOrEmpty(selectedText) && _webViewEnvironment != null)
            {
                // メニュー表示用に長いテキストは省略
                string displayLabel = selectedText.Length > 15 ? selectedText.Substring(0, 15) + "..." : selectedText;

                // --- 区切り線 ---
                e.MenuItems.Add(_webViewEnvironment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));

                // --- Google検索 ---
                string searchLabel = string.Format(Properties.Resources.Ctx_GoogleSearch, displayLabel);
                var searchItem = _webViewEnvironment.CreateContextMenuItem(
                    searchLabel, null, CoreWebView2ContextMenuItemKind.Command);

                searchItem.CustomItemSelected += (s, args) => PerformGoogleSearch(selectedText);
                e.MenuItems.Add(searchItem);

                // --- 区切り線 ---
                e.MenuItems.Add(_webViewEnvironment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));

                // --- NGワード登録 ---
                string ngLabel = string.Format(Properties.Resources.Ctx_AddNgWord, displayLabel);
                var ngItem = _webViewEnvironment.CreateContextMenuItem(
                    ngLabel, null, CoreWebView2ContextMenuItemKind.Command);

                ngItem.CustomItemSelected += (s, args) => AddNgWord(selectedText);
                e.MenuItems.Add(ngItem);
            }
        }

        /// <summary>
        /// 画像をダウンロードして保存します。
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
                        // パス部分のファイル名を取得し、クエリで指定された拡張子を付与
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
                        await System.IO.File.WriteAllBytesAsync(dialog.FileName, data);
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
        /// 指定されたテキストでGoogle検索を行います。
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

        /// NGワードを追加し、保存して即時反映させます。
        /// </summary>
        private void AddNgWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            // ファイル読み込みではなく、メモリ上の _ngWords を操作して SaveSettings を呼ぶ
            if (!_ngWords.Contains(word))
            {
                _ngWords.Add(word);

                // メインの保存メソッドを使って一括保存
                SaveSettings(_activeProfileName);

                // 全カラムに反映
                ApplyNgWordsToAllColumns(_ngWords);

                // リソースを使ってメッセージ表示 (前回の多言語対応済みの場合)
                string msg = string.Format(Properties.Resources.Msg_NgWordAdded, word);
                MessageWindow.Show(this, msg, Properties.Resources.Title_Registered, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        /// <summary>
        /// NGワードスクリプトを単一のWebViewに適用します。
        /// </summary>
        private async void ApplyNgWordsScript(CoreWebView2 webView)
        {
            try
            {
                //  _ngWords を使用して適用
                if (_ngWords != null && _ngWords.Count > 0)
                {
                    string script = ScriptDefinitions.GetNgWordScript(_ngWords);
                    await webView.ExecuteScriptAsync(script);
                }
            }
            catch { }
        }

        /// <summary>
        /// NGワードスクリプトをすべてのカラムに再適用します（設定変更時用）。
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

        // MainWindow.xaml.cs に追加

        /// <summary>
        /// フォーカスモードの背景クリック時の処理。
        /// 背景の半透明部分をクリックしたときだけ詳細を閉じます。
        /// </summary>
        private void FocusViewGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // クリックされた要素が FocusViewGrid 自体（背景部分）であるか確認
            if (e.OriginalSource == FocusViewGrid)
            {
                ExitFocusMode();
            }
        }
    }
}