using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowのWebView関連のロジック（初期化、イベントハンドラ、CSS注入、スクリプト連携など）を管理します。
    /// </summary>
    public partial class MainWindow
    {
        // 拡張機能のロード状態
        private bool _extensionsLoaded = false;

        #region CSS Definitions

        // ヘッダーやメニュー非表示用のCSS
        private const string CssHideMenu = "header[role=\"banner\"] { display: none !important; } main[role=\"main\"] { align-items: flex-start !important; }";

        // リスト表示時のヘッダー周りを簡略化するCSS
        // 1. 詳細情報（メンバー数、編集ボタン等）を非表示（タイムライン自体は消さないよう :not(:has(...)) で除外）
        // 2. 上部固定バーの「戻るボタン」を非表示
        // 3. 上部余白を詰める
        private const string CssHideListHeader = @"
            [data-testid='primaryColumn'] div:has([data-testid='editListButton']):not(:has([data-testid='cellInnerDiv'])),
            [data-testid='primaryColumn'] div:has(a[href$='/members']):not(:has([data-testid='cellInnerDiv'])),
            [data-testid='primaryColumn'] div:has(a[href$='/followers']):not(:has([data-testid='cellInnerDiv'])) { 
                display: none !important; 
            }
            [data-testid='primaryColumn'] [data-testid='app-bar-back-button'] { display: none !important; }
            [data-testid='primaryColumn'] { padding-top: 0 !important; }
        ";

        // 右サイドバー非表示用のCSS
        private const string CssHideRightSidebar = "[data-testid='sidebarColumn'] { display: none !important; }";

        // リポスト（ソーシャルコンテキスト）非表示用のCSS
        private const string CssHideSocialContext = @"
            div[data-testid='cellInnerDiv']:has([data-testid='socialContext']),
            .tweet-context,
            .retweet-credit,
            .js-retweet-text { display: none !important; }
        ";

        // 0.5秒ごとに画面内のツイートをチェックし、「返信先」などの文字があればクラスを付与します
        private const string ScriptDetectReplies = @"
            (function() {
                if (window.xColumnReplyDetector) return;
                window.xColumnReplyDetector = true;

                // 検出するキーワード（日本語と英語に対応）
                const replyKeywords = ['返信先', 'Replying to'];

                function detect() {
                    // 未チェックのセルのみ対象にする（負荷軽減）
                    const cells = document.querySelectorAll('div[data-testid=""cellInnerDiv""]:not(.xcolumn-checked)');
                    cells.forEach(cell => {
                        cell.classList.add('xcolumn-checked');
                        const tweet = cell.querySelector('article[data-testid=""tweet""]');
                        if (!tweet) return;

                        // ツイート本文とユーザー名（ヘッダー）を取得
                        const body = tweet.querySelector('[data-testid=""tweetText""]');
                        const header = tweet.querySelector('[data-testid=""User-Name""]');

                        // ツイート内の全テキストノードを走査
                        const walker = document.createTreeWalker(tweet, NodeFilter.SHOW_TEXT, null, false);
                        let node;
                        while(node = walker.nextNode()) {
                            const text = node.textContent;
                            // キーワードが含まれているか
                            if (replyKeywords.some(kw => text.includes(kw))) {
                                // 本文やヘッダーの中に含まれている場合は除外（誤判定防止）
                                if (body && body.contains(node)) continue;
                                if (header && header.contains(node)) continue;

                                // リプライ確定 -> クラス付与
                                cell.classList.add('xcolumn-is-reply');
                                break;
                            }
                        }
                    });
                }
                // 定期実行
                setInterval(detect, 500);
            })();
        ";

        // 「返信先」を示す要素を含むツイートセルを非表示にするアプローチです。
        private const string CssHideReplies = @"
            div[data-testid='cellInnerDiv']:has(div > div > div > div > div > div > div > div > div > div > div > a[dir='ltr']) 
            { display: none !important; }
        ";

        // 入力フォーカス監視スクリプト
        private const string ScriptDetectInput = @"
            (function() {
                if (window.xColumnInputDetector) return;
                window.xColumnInputDetector = true;
                function notify() {
                    const el = document.activeElement;
                    const isInput = el && (['INPUT', 'TEXTAREA'].includes(el.tagName) || el.isContentEditable);
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'inputState', val: isInput }));
                }
                document.addEventListener('focus', notify, true);
                document.addEventListener('blur', notify, true);
                notify(); // 初期状態チェック
            })();
        ";
        #endregion

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
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            col.AssociatedWebView = webView;
            col.InitializeTimer();

            // アプリがアクティブな場合、タイマーを停止
            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            // 入力状態受信ハンドラ
            webView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string jsonString = e.TryGetWebMessageAsString();
                    var json = JsonNode.Parse(jsonString);
                    if (json?["type"]?.GetValue<string>() == "inputState")
                    {
                        col.IsInputActive = json["val"]?.GetValue<bool>() ?? false;
                    }
                }
                catch { }
            };

            webView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                if (args.IsSuccess)
                {
                    ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source, col);
                    ApplyVolumeScript(webView.CoreWebView2);
                    ApplyYouTubeClickScript(webView.CoreWebView2);

                    // スクロール同期スクリプトを適用（WebView内でのShift+Wheelを捕捉）
                    ApplyScrollSyncScript(webView.CoreWebView2);
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDetectReplies);
                    // 入力監視スクリプト注入
                    await webView.CoreWebView2.ExecuteScriptAsync(ScriptDetectInput);
                }
            };

            // ソースURL変更時の処理登録
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                // URLを取得
                string url = webView.CoreWebView2.Source;
                // 拡張機能のURLは無視
                if (url.StartsWith("chrome-extension://")) return;

                // URL変更時は即座にモデルを更新する（判定遅れを防ぐため）
                if (IsAllowedDomain(url) || IsAllowedDomain(url, true))
                {
                    col.Url = url;
                }

                ApplyCustomCss(webView.CoreWebView2, url, col);
                ApplyYouTubeClickScript(webView.CoreWebView2);
                // ページ遷移後もスクリプトを適用
                ApplyScrollSyncScript(webView.CoreWebView2);
                ApplyTrendingClickScript(webView.CoreWebView2);

                // ページ遷移時は入力状態を一旦リセットして監視再開
                col.IsInputActive = false;
                webView.CoreWebView2.ExecuteScriptAsync(ScriptDetectInput);

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
                        // ここにあった if (url == col.Url) return; を削除しました。
                        // 直前で col.Url を更新しているため、常にTrueとなり遷移がブロックされていました。

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
                string script = @"
                    (function() {
                        if (window.xColumnTrendingHook) return;
                        window.xColumnTrendingHook = true;
                        document.addEventListener('click', function(e) {
                            if (!window.location.href.includes('/explore/')) return;
                            const target = e.target;
                            
                            // ---------------------------------------------------------
                            // 1. まずは明確なリンク(aタグ)があり、かつ検索URLである場合をチェック
                            // ---------------------------------------------------------
                            const anchor = target.closest('a');
                            if (anchor) {
                                const href = anchor.getAttribute('href');
                                if (href && (href.includes('/search') || href.includes('q='))) {
                                    const fullUrl = new URL(href, window.location.origin).href;
                                    postNewColumn(fullUrl);
                                    e.preventDefault();
                                    e.stopPropagation();
                                    return;
                                }
                            }

                            // ---------------------------------------------------------
                            // 2. data-testid=""trend"" を持つ要素を探す
                            // ---------------------------------------------------------
                            const trendDiv = target.closest('div[data-testid=""trend""]');
                            if (trendDiv) {
                                const lines = trendDiv.innerText.split('\n');
                                let keyword = '';

                                // 除外ワード完全一致リスト
                                const ignoreWords = ['トレンド', 'おすすめ', 'さらに表示', 'Show more', 'Topic', 'Promoted'];
                                for (let line of lines) {
                                    line = line.trim();
                                    if (!line) continue;
                                    if (line.startsWith('#')) { 
                                        keyword = line;
                                        break;
                                    }

                                    // --- 除外ロジック ---

                                    // 1. 数字のみ (ランク表示 ""1"" や ""2"" など)
                                    if (/^\d+$/.test(line)) continue;
                                    
                                    // 2. カテゴリ行 (中黒点 ""·"" を含む行 例:""音楽 · トレンド"")
                                    if (line.includes('·')) continue;
                                    
                                    // 3. 件数行 (数字を含み、かつ""件""または""posts""を含む)
                                    if (/\d/.test(line) && (line.includes('件') || line.includes('posts'))) continue;
                                    
                                    // 4. システム文言（完全一致）
                                    if (ignoreWords.includes(line)) continue;

                                    // 5. 【追加】「〜のトレンド」で終わる行を除外
                                    // これで「近畿地方のトレンド」「シリーズ作品のトレンド」「日本のトレンド」をまとめて弾く
                                    if (line.endsWith('のトレンド')) continue;

                                    // 6. 意味のない記号のみの行
                                    if (line === '.' || line === ',') continue;

                                    // これらすべてを通過した最初の行をキーワードとみなす
                                    keyword = line;
                                    break;
                                }
                                if (keyword) {
                                    const searchUrl = 'https://x.com/search?q=' + encodeURIComponent(keyword);
                                    postNewColumn(searchUrl);
                                    e.preventDefault();
                                    e.stopPropagation();
                                }
                            }
                        }, true); 
                        function postNewColumn(url) {
                            window.chrome.webview.postMessage(JSON.stringify({ type: 'openNewColumn', url: url }));
                        }
                    })();
                ";
                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex) { Logger.Log($"ApplyTrendingClickScript Error: {ex.Message}"); }
        }

        private async void ApplyScrollSyncScript(CoreWebView2 webView)
        {
            try
            {
                string script = @"
                    (function() {
                        if (window.xColumnScrollHook) return;
                        window.xColumnScrollHook = true;
                        window.addEventListener('wheel', (e) => {
                            let delta = 0;
                            if (e.shiftKey && e.deltaY !== 0) delta = e.deltaY;
                            else if (e.deltaX !== 0) delta = e.deltaX;
                            if (delta !== 0) {
                                window.chrome.webview.postMessage(JSON.stringify({ type: 'horizontalScroll', delta: delta }));
                                e.preventDefault(); e.stopPropagation();
                            }
                        }, { passive: false });
                    })();
                ";
                await webView.ExecuteScriptAsync(script);
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
                    cssToInject += CssHideSocialContext + "\n";
                }

                // 4. カラムごとの設定 (Rep非表示)
                // JSで付与されたクラス(.xcolumn-is-reply)を持つ要素を非表示にするCSSを追加
                if (col != null && col.IsReplyHidden)
                {
                    cssToInject += ".xcolumn-is-reply { display: none !important; }\n";
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
                    bool isHome = url.Contains("/home");
                    // メニュー非表示
                    if ((_hideMenuInHome && isHome) || (_hideMenuInNonHome && !isHome))
                    {
                        cssToInject += CssHideMenu + "\n";
                    }
                    // リストヘッダー簡易表示
                    if (_hideListHeader && url.Contains("/lists/"))
                    {
                        cssToInject += CssHideListHeader + "\n";
                    }

                    // 右サイドバー非表示
                    if (_hideRightSidebar)
                    {
                        cssToInject += CssHideRightSidebar + "\n";
                    }
                }

                // 6. CSS注入実行
                if (string.IsNullOrEmpty(cssToInject))
                {
                    Logger.Log("[CSS] Result: Aborted (No CSS to inject)");
                    return;
                }

                Logger.Log($"[CSS] Injecting CSS... Length: {cssToInject.Length}");

                // エスケープ処理
                string safeCss = cssToInject.Replace("\\", "\\\\").Replace("`", "\\`").Replace("\r", "").Replace("\n", " ");

                // JavaScript注入 (再試行ロジック付き)
                // headタグが見つかるまで最大10回(1秒間)再試行します
                string script = $@"
                    (function() {{
                        let attempts = 0;
                        function injectXColumnStyle() {{
                            try {{
                                const head = document.head || document.getElementsByTagName('head')[0];
                                if (!head) {{
                                    attempts++;
                                    if (attempts < 10) setTimeout(injectXColumnStyle, 100);
                                    return;
                                }}
                                let style = document.getElementById('xcolumn-custom-style');
                                if (!style) {{
                                    style = document.createElement('style');
                                    style.id = 'xcolumn-custom-style';
                                    head.appendChild(style);
                                }}
                                style.textContent = `{safeCss}`;
                            }} catch(e) {{ console.error(e); }}
                        }}
                        injectXColumnStyle();
                    }})();
                ";
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
                string script = $@"
                    (function() {{
                        const vol = {_appVolume};
                        document.querySelectorAll('video, audio').forEach(m => m.volume = vol);
                        if (!window.xColumnVolHook) {{
                            window.xColumnVolHook = true;
                            window.addEventListener('play', (e) => {{
                                if(e.target && (e.target.tagName === 'VIDEO' || e.target.tagName === 'AUDIO')) {{
                                    e.target.volume = vol;
                                }}
                            }}, true);
                        }}
                    }})();
                ";
                await webView.ExecuteScriptAsync(script);
            }
            catch { }
        }

        /// <summary>
        /// XのYouTubeカードクリック時の挙動を制御するスクリプトを注入します。
        /// インライン再生を防ぎ、詳細ページ（フォーカスモード）への遷移を促します。
        /// </summary>
        private async void ApplyYouTubeClickScript(CoreWebView2 webView)
        {
            try
            {
                string script = @"
                    (function() {
                        if (window.xColumnYTHook) return;
                        window.xColumnYTHook = true;
                        document.addEventListener('click', function(e) {
                            const target = e.target;
                            if (!target || !target.closest) return;
                            // YouTubeカードのリンク検出
                            const card = target.closest('[data-testid=""card.wrapper""]');
                            if (card) {
                                const ytLink = card.querySelector('a[href*=""youtube.com""], a[href*=""youtu.be""]');
                                if (ytLink) {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    // 親ツイートを探し、詳細URLへ遷移
                                    const article = card.closest('article[data-testid=""tweet""]');
                                    if (article) {
                                        const statusLink = article.querySelector('a[href*=""/status/""]');
                                        if (statusLink) {
                                            window.location.href = statusLink.href;
                                        }
                                    }
                                    return;
                                }
                            }
                        }, true);
                    })();
                ";
                await webView.ExecuteScriptAsync(script);
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
                string script = @"
                    (function() {
                        const url = window.location.href;
                        if (!url.includes('/photo/') && !url.includes('/video/')) return;
                        const idMatch = url.match(/\/status\/(\d+)/);
                        const targetId = idMatch ? idMatch[1] : null;
                        let attempts = 0;
                        const maxAttempts = 20;
                        const interval = setInterval(() => {
                            attempts++;
                            if (attempts > maxAttempts) { clearInterval(interval); return; }
                            if (document.querySelector('div[role=""dialog""][aria-modal=""true""]')) {
                                clearInterval(interval);
                                return;
                            }
                            let targetTweet = null;
                            const tweets = document.querySelectorAll('article[data-testid=""tweet""]');
                            if (targetId) {
                                for (const t of tweets) {
                                    if (t.innerHTML.indexOf(targetId) !== -1) {
                                        targetTweet = t;
                                        break;
                                    }
                                }
                            }
                            if (!targetTweet && tweets.length > 0) targetTweet = tweets[0];
                            if (targetTweet) {
                                if (url.includes('/photo/')) {
                                    const photoMatch = url.match(/\/photo\/(\d+)/);
                                    if (photoMatch) {
                                        const index = parseInt(photoMatch[1]) - 1;
                                        const photos = targetTweet.querySelectorAll('div[data-testid=""tweetPhoto""]');
                                        if (photos[index]) {
                                            photos[index].click();
                                            clearInterval(interval);
                                        }
                                    }
                                }
                                else if (url.includes('/video/')) {
                                    const video = targetTweet.querySelector('div[data-testid=""videoPlayer""]');
                                    if (video) {
                                        video.click();
                                        clearInterval(interval);
                                    }
                                }
                            }
                        }, 200);
                    })();
                ";
                await webView.ExecuteScriptAsync(script);
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
                    MessageWindow.Show(this, $"リンクを開けませんでした: {e.Uri}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// フォーカスモードに入ります。
        /// </summary>
        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url);
            ColumnItemsControl.Visibility = Visibility.Collapsed;
            FocusViewGrid.Visibility = Visibility.Visible;
            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();
        }

        /// <summary>
        /// フォーカスモードを終了します。
        /// </summary>
        private void ExitFocusMode()
        {
            _isFocusMode = false;
            FocusViewGrid.Visibility = Visibility.Collapsed;
            ColumnItemsControl.Visibility = Visibility.Visible;
            FocusWebView?.CoreWebView2?.Navigate("about:blank");

            // フォーカスしていたカラムのメディア拡大状態をリセット
            if (_focusedColumnData?.AssociatedWebView?.CoreWebView2 != null)
            {
                // メディア拡大スクリプトを適用してリセット
                string colUrl = _focusedColumnData.AssociatedWebView.CoreWebView2.Source;

                // フォーカスビューでメディアを拡大表示していた場合、元のカラムに戻す
                if ((colUrl.Contains("/photo/") || colUrl.Contains("/video/")) && colUrl != _focusedColumnData.Url)
                {
                    if (_focusedColumnData.AssociatedWebView.CoreWebView2.CanGoBack)
                    {
                        _focusedColumnData.AssociatedWebView.CoreWebView2.GoBack();
                    }
                }
                _focusedColumnData.ResetCountdown();
            }
            _focusedColumnData = null;
            if (!_isAppActive)
            {
                foreach (var c in Columns) c.UpdateTimer();
                _countdownTimer.Start();
            }

            // Visibilityの切り替え直後はWebViewがまだ描画準備完了していない場合があるため、
            // 優先度を Loaded にしてUI描画後に実行します。
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
    }
}