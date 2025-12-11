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

            // 右クリックのコンテキストメニュー有効化
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // DevTools無効化
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

            // コンテキストメニューのカスタマイズ要求イベント
            webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

            webView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                if (args.IsSuccess)
                {
                    ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source, col);
                    ApplyVolumeScript(webView.CoreWebView2);
                    ApplyYouTubeClickScript(webView.CoreWebView2);

                    // リスト自動遷移ロジック
                    if (col.IsListAutoNav)
                    {
                        // ホーム画面(x.com または twitter.com)にいる場合のみ実行
                        string src = webView.CoreWebView2.Source;
                        if (src.Contains("x.com") || src.Contains("twitter.com"))
                        {
                            // 少し待ってからクリック（SPAの描画待ち: 1,0秒）
                            await Task.Delay(1000);

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
                webView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.ScriptDetectInput);

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
                    bool isHome = url.Contains("/home");
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

            // タイマーを再開
            foreach (var c in Columns) c.UpdateTimer();
            _countdownTimer.Start();
            // アクティブじゃなくてもタイマーを再開なのでコメントアウト
            /*
            if (!_isAppActive)
            {
                foreach (var c in Columns) c.UpdateTimer();
                _countdownTimer.Start();
            }
            */

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
                    "名前を付けて画像を保存", null, CoreWebView2ContextMenuItemKind.Command);

                string srcUrl = e.ContextMenuTarget.SourceUri;
                saveImageItem.CustomItemSelected += async (s, args) =>
                {
                    await DownloadAndSaveImageAsync(srcUrl);
                };
                e.MenuItems.Add(saveImageItem);
            }

            // B. リンクがある場合のコピーメニュー
            if (!string.IsNullOrEmpty(e.ContextMenuTarget.LinkUri) && _webViewEnvironment != null)
            {
                // 画像メニューがなく、かつ上に項目がある場合のみセパレータ追加 (重複防止)
                if (e.ContextMenuTarget.Kind != CoreWebView2ContextMenuTargetKind.Image && e.MenuItems.Count > 0)
                {
                    e.MenuItems.Add(_webViewEnvironment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
                }

                // リンクコピー項目作成
                var linkCopyItem = _webViewEnvironment.CreateContextMenuItem(
                    "リンクのアドレスをコピー", null, CoreWebView2ContextMenuItemKind.Command);

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
                // 1. ファイル名の推定
                string fileName = "image.jpg";
                try
                {
                    Uri uri = new Uri(url);
                    fileName = System.IO.Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName)) fileName = "image.jpg";
                }
                catch { }

                // 2. SaveFileDialog の表示
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.FileName = fileName;
                dialog.Filter = "画像ファイル|*.*";

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
                MessageWindow.Show(this, $"画像の保存に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}