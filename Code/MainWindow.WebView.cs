using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
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
        private const string CssHideSocialContext = "div[data-testid='cellInnerDiv']:has([data-testid='socialContext']) { display: none !important; }";

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
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// Shift+ホイールによる横スクロール要求などを処理します。
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // 受信したメッセージをJSONとして解析
                string jsonString = e.TryGetWebMessageAsString();
                var json = JsonNode.Parse(jsonString);

                // 横スクロール要求 ("horizontalScroll") の場合
                if (json?["type"]?.GetValue<string>() == "horizontalScroll")
                {
                    int delta = json["delta"]?.GetValue<int>() ?? 0;
                    // MainWindow側のスクロール処理を呼び出す
                    PerformHorizontalScroll(delta);
                }
            }
            catch { }
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
                        Debug.WriteLine($"Extension loaded: {ext.Name} (ID: {ext.Id})");
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"拡張機能 '{ext.Name}' の読み込みに失敗しました。\n\n{ex.Message}", "拡張機能エラー");
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
        /// <param name="extensionFolder"></param>
        /// <returns></returns>
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
        /// <param name="ext"></param>
        public void OpenExtensionOptions(ExtensionItem ext)
        {
            // 拡張機能IDとオプションページが有効か確認
            if (string.IsNullOrEmpty(ext.Id) || string.IsNullOrEmpty(ext.OptionsPage))
            {
                // エラーメッセージを表示
                System.Windows.MessageBox.Show("この拡張機能には設定ページがないか、まだロードされていません。", "エラー");
                return;
            }

            // オプションページのURLを構築
            string url = (ext.OptionsPage.StartsWith("http://") || ext.OptionsPage.StartsWith("https://"))
                ? ext.OptionsPage
                : $"chrome-extension://{ext.Id}/{ext.OptionsPage}";

            EnterFocusMode(url);
        }

        /// <summary>
        /// カラム用WebViewの設定（スクリプト有効化、イベントハンドラなど）を行います。
        /// </summary>
        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            // WebViewの各種設定
            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // カラムデータとWebViewを関連付け
            col.AssociatedWebView = webView;
            col.InitializeTimer();

            // アプリがアクティブな場合、タイマーを停止
            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            // ナビゲーション完了時の処理登録
            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                // ナビゲーション成功時に各種スクリプトとCSSを適用
                if (args.IsSuccess)
                {
                    ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source, col);
                    ApplyVolumeScript(webView.CoreWebView2);
                    ApplyYouTubeClickScript(webView.CoreWebView2);

                    // スクロール同期スクリプトを適用（WebView内でのShift+Wheelを捕捉）
                    ApplyScrollSyncScript(webView.CoreWebView2);
                }
            };

            // ソースURL変更時の処理登録
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                // URLを取得
                string url = webView.CoreWebView2.Source;
                // 拡張機能のURLは無視
                if (url.StartsWith("chrome-extension://")) return;

                // 各種スクリプトとCSSを適用
                ApplyCustomCss(webView.CoreWebView2, url, col);
                ApplyYouTubeClickScript(webView.CoreWebView2);
                // 【追加】ページ遷移後もスクリプトを適用
                ApplyScrollSyncScript(webView.CoreWebView2);

                // ドメインの許可チェックとフォーカスモードの制御
                if (IsAllowedDomain(url, true))
                {
                    if (!_isFocusMode)
                    {
                        if (url == col.Url) return;
                        bool comingFromCompose = !string.IsNullOrEmpty(col.Url) &&
                                                 (col.Url.Contains("/compose/") || col.Url.Contains("/intent/"));

                        if (!comingFromCompose)
                        {
                            _focusedColumnData = col;
                            EnterFocusMode(url);
                        }
                        else
                        {
                            col.Url = url;
                        }
                    }
                }
                else if (IsAllowedDomain(url))
                {
                    col.Url = url;
                }
            };

            webView.CoreWebView2.Navigate(col.Url);
        }

        /// <summary>
        /// Shift+ホイール操作を検知してアプリ側にメッセージを送るJavaScriptを注入します。
        /// WebView2内のイベントは通常WPFに伝わらないため、このスクリプトでブリッジします。
        /// </summary>
        private async void ApplyScrollSyncScript(CoreWebView2 webView)
        {
            try
            {
                string script = @"
                    (function() {
                        // 二重登録防止
                        if (window.xColumnScrollHook) return;
                        window.xColumnScrollHook = true;

                        window.addEventListener('wheel', (e) => {
                            // Shiftキーが押されていて、かつ垂直スクロール操作（deltaY != 0）の場合
                            if (e.shiftKey && e.deltaY !== 0) {
                                // C#側 (CoreWebView2_WebMessageReceived) にメッセージを送信
                                window.chrome.webview.postMessage(JSON.stringify({ 
                                    type: 'horizontalScroll', 
                                    delta: e.deltaY 
                                }));
                                // ブラウザ本来のスクロール動作や戻る動作などをキャンセル
                                e.preventDefault();
                                e.stopPropagation();
                            }
                        }, { passive: false }); // preventDefaultを有効にするため passive: false を指定
                    })();
                ";
                await webView.ExecuteScriptAsync(script);
            }
            catch { }
        }

        /// <summary>
        /// 設定とURLに基づいて、WebViewにカスタムCSSを注入します。
        /// フォント変更、不要なUIの非表示、リポスト非表示などを行います。
        /// </summary>
        private async void ApplyCustomCss(CoreWebView2 webView, string url, ColumnData? col = null)
        {
            try
            {
                // 作成中や意図的なポップアップURLは無視
                if (url.Contains("/compose/") || url.Contains("/intent/")) return;

                // 注入するCSSを構築
                string cssToInject = "";

                if (!string.IsNullOrEmpty(_appFontFamily))
                {
                    cssToInject += $@"
                        body, div, span, p, a, h1, h2, h3, h4, h5, h6, input, textarea, button, select {{
                            font-family: '{_appFontFamily}', sans-serif !important;
                        }}
                    ";
                }

                if (_appFontSize > 0)
                {
                    cssToInject += $@"
                        html {{ font-size: {_appFontSize}px !important; }}
                        body {{ font-size: {_appFontSize}px !important; }}
                        div[dir='auto'], span, p, a, [data-testid='tweetText'] span, [data-testid='user-cell'] span {{ 
                             font-size: {_appFontSize}px !important; 
                             line-height: 1.4 !important;
                        }}
                    ";
                }

                // リポスト非表示設定が有効な場合、対応するCSSを追加
                if (col != null && col.IsRetweetHidden)
                {
                    cssToInject += CssHideSocialContext + "\n";
                }

                // カスタムCSSが設定されている場合、追加
                if (!string.IsNullOrEmpty(_customCss))
                {
                    cssToInject += _customCss + "\n";
                }

                // ドメイン許可チェックとUI非表示設定の適用
                if (IsAllowedDomain(url))
                {
                    bool isHome = url.Contains("/home");

                    // メニュー非表示設定の適用
                    if ((_hideMenuInHome && isHome) || (_hideMenuInNonHome && !isHome))
                    {
                        cssToInject += CssHideMenu;
                    }
                    // リストヘッダーと右サイドバー非表示設定の適用
                    if (_hideListHeader && url.Contains("/lists/"))
                    {
                        cssToInject += CssHideListHeader;
                    }
                    // 右サイドバー非表示設定の適用
                    if (_hideRightSidebar)
                    {
                        cssToInject += CssHideRightSidebar;
                    }
                }
                // CSSが空の場合は注入をスキップ
                if (string.IsNullOrEmpty(cssToInject)) return;

                // CSSをWebViewに注入
                string safeCss = cssToInject.Replace("\\", "\\\\").Replace("`", "\\`");
                string script = $@"
                    (function() {{
                        let style = document.getElementById('xcolumn-custom-style');
                        if (!style) {{
                            style = document.createElement('style');
                            style.id = 'xcolumn-custom-style';
                            document.head.appendChild(style);
                        }}
                        style.textContent = `{safeCss}`;
                    }})();
                ";

                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CSS Injection failed: {ex.Message}");
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
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, col.Url, col);
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
        /// <param name="webView"></param>
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
                                    // 再生キャンセル
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
            catch (Exception ex) { Debug.WriteLine($"YouTube script failed: {ex.Message}"); }
        }

        /// <summary>
        /// メディア拡大スクリプトを適用します。
        /// </summary>
        /// <param name="webView"></param>
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
            catch (Exception ex) { Debug.WriteLine($"Media expand script failed: {ex.Message}"); }
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
                        ApplyCustomCss(FocusWebView.CoreWebView2, FocusWebView.CoreWebView2.Source);
                        ApplyVolumeScript(FocusWebView.CoreWebView2);
                        ApplyMediaExpandScript(FocusWebView.CoreWebView2);
                        // 【追加】スクロール同期スクリプトを適用
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
                    ApplyCustomCss(FocusWebView.CoreWebView2, url);

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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (e.IsUserInitiated)
            {
                try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
                catch { System.Windows.MessageBox.Show($"リンクを開けませんでした: {e.Uri}"); }
            }
        }

        /// <summary>
        /// フォーカスモードに入ります。
        /// </summary>
        /// <param name="url"></param>
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
        }

        /// <summary>
        /// フォーカスビューの閉じるボタンクリック時の処理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseFocusView_Click(object sender, RoutedEventArgs e) => ExitFocusMode();

        /// <summary>
        /// ドメイン許可チェック。
        /// </summary>
        /// <param name="url"></param>
        /// <param name="focus"></param>
        /// <returns></returns>
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

                // フォーカスモード用URLの判定
                bool isFocusUrl = uri.AbsolutePath.Contains("/status/") ||
                                  uri.AbsolutePath.Contains("/settings");

                return focus ? isFocusUrl : !isFocusUrl;
            }
            catch { return false; }
        }
    }
}