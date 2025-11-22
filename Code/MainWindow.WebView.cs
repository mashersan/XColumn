using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowのWebView関連のロジック（初期化、イベントハンドラ、CSS注入など）を管理します。
    /// </summary>
    public partial class MainWindow
    {
        private bool _extensionsLoaded = false;

        #region CSS Definitions

        // 左側メニュー (header role="banner") を非表示にし、メインコンテンツを左寄せにするCSS
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

        #endregion

        /// <summary>
        /// WebView2環境を初期化し、ブラウザデータフォルダを設定します。
        /// </summary>
        private async Task InitializeWebViewEnvironmentAsync()
        {
            string browserDataFolder = Path.Combine(_userDataFolder, "BrowserData", _activeProfileName);
            Directory.CreateDirectory(browserDataFolder);

            var options = new CoreWebView2EnvironmentOptions();
            options.AreBrowserExtensionsEnabled = true;

            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, browserDataFolder, options);

            await InitializeFocusWebView();
        }

        private void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is WebView2 webView && webView.CoreWebView2 == null && _webViewEnvironment != null)
            {
                webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                webView.EnsureCoreWebView2Async(_webViewEnvironment);
            }
        }

        private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                if (e.IsSuccess)
                {
                    // 最初のWebView初期化時に拡張機能をロードする
                    if (!_extensionsLoaded && webView.CoreWebView2 != null)
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
        /// 登録された拡張機能をWebView2プロファイルにロードします。
        /// </summary>
        private async Task LoadExtensionsAsync(CoreWebView2Profile profile)
        {
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
        }

        /// <summary>
        /// manifest.jsonから設定ページのパスを取得します。
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

                string? page = json["options_ui"]?["page"]?.GetValue<string>();

                if (string.IsNullOrEmpty(page))
                {
                    page = json["options_page"]?.GetValue<string>();
                }

                return page ?? "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// 拡張機能の設定画面（オプションページ）を開きます。
        /// </summary>
        public void OpenExtensionOptions(ExtensionItem ext)
        {
            if (string.IsNullOrEmpty(ext.Id) || string.IsNullOrEmpty(ext.OptionsPage))
            {
                System.Windows.MessageBox.Show("この拡張機能には設定ページがないか、まだロードされていません。", "エラー");
                return;
            }

            string url = $"chrome-extension://{ext.Id}/{ext.OptionsPage}";
            EnterFocusMode(url);
        }

        /// <summary>
        /// カラム用WebViewの設定（スクリプト有効化、イベントハンドラなど）を行います。
        /// </summary>
        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            col.AssociatedWebView = webView;
            col.InitializeTimer();

            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // ナビゲーション完了時にもCSSを適用（リロード対策）
            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                if (args.IsSuccess)
                {
                    ApplyCustomCss(webView.CoreWebView2, webView.CoreWebView2.Source);
                }
            };

            // URL変更時の処理（CSS適用およびフォーカスモード遷移判定）
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                string url = webView.CoreWebView2.Source;
                if (url.StartsWith("chrome-extension://")) return;

                ApplyCustomCss(webView.CoreWebView2, url);

                if (IsAllowedDomain(url, true))
                {
                    if (!_isFocusMode)
                    {
                        _focusedColumnData = col;
                        EnterFocusMode(url);
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
        /// 設定とURLに基づいて、WebViewにカスタムCSSを注入します。
        /// </summary>
        private async void ApplyCustomCss(CoreWebView2 webView, string url)
        {
            try
            {
                string cssToInject = "";

                if (IsAllowedDomain(url))
                {
                    bool isHome = url.Contains("/home");

                    // メニュー非表示設定の適用
                    if ((_hideMenuInHome && isHome) || (_hideMenuInNonHome && !isHome))
                    {
                        cssToInject += CssHideMenu;
                    }

                    // リストヘッダー非表示設定の適用
                    if (_hideListHeader && url.Contains("/lists/"))
                    {
                        cssToInject += CssHideListHeader;
                    }
                }

                if (string.IsNullOrEmpty(cssToInject)) return;

                // JavaScriptを使用してCSSをDOMに注入
                string script = $@"
                    (function() {{
                        let style = document.getElementById('xcolumn-custom-style');
                        if (!style) {{
                            style = document.createElement('style');
                            style.id = 'xcolumn-custom-style';
                            document.head.appendChild(style);
                        }}
                        style.innerHTML = `{cssToInject}`;
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
        /// 現在開いているすべてのカラムに対してCSSを再適用します。
        /// </summary>
        private void ApplyCssToAllColumns()
        {
            foreach (var col in Columns)
            {
                if (col.AssociatedWebView?.CoreWebView2 != null)
                {
                    ApplyCustomCss(col.AssociatedWebView.CoreWebView2, col.Url);
                }
            }
        }

        /// <summary>
        /// フォーカスモード用WebViewの初期化処理。
        /// </summary>
        private async Task InitializeFocusWebView()
        {
            if (FocusWebView == null || _webViewEnvironment == null) return;
            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            if (FocusWebView.CoreWebView2 != null)
            {
                if (!_extensionsLoaded)
                {
                    _extensionsLoaded = true;
                    await LoadExtensionsAsync(FocusWebView.CoreWebView2.Profile);
                }

                FocusWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                FocusWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess) ApplyCustomCss(FocusWebView.CoreWebView2, FocusWebView.CoreWebView2.Source);
                };

                FocusWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    string url = FocusWebView.CoreWebView2.Source;
                    if (url.StartsWith("chrome-extension://")) return;

                    ApplyCustomCss(FocusWebView.CoreWebView2, url);

                    // フォーカスモード維持判定
                    // status(詳細)、compose(作成)、intent(アクション)の場合はモードを維持
                    bool keepFocus = IsAllowedDomain(url, true) ||
                                     url.Contains("/compose/") ||
                                     url.Contains("/intent/");

                    if (_isFocusMode && !keepFocus && url != "about:blank")
                    {
                        ExitFocusMode();
                    }
                };
            }
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
            catch { System.Windows.MessageBox.Show($"リンクを開けませんでした: {e.Uri}"); }
        }

        /// <summary>
        /// フォーカスモード（単一ビュー）を開始します。
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
        /// フォーカスモードを終了し、カラム一覧に戻ります。
        /// </summary>
        private void ExitFocusMode()
        {
            _isFocusMode = false;
            FocusViewGrid.Visibility = Visibility.Collapsed;
            ColumnItemsControl.Visibility = Visibility.Visible;
            FocusWebView?.CoreWebView2?.Navigate("about:blank");

            if (_focusedColumnData?.AssociatedWebView?.CoreWebView2 != null)
            {
                _focusedColumnData.ResetCountdown();
            }
            _focusedColumnData = null;

            if (!_isAppActive)
            {
                foreach (var c in Columns) c.UpdateTimer();
                _countdownTimer.Start();
            }
        }

        private void CloseFocusView_Click(object sender, RoutedEventArgs e) => ExitFocusMode();

        /// <summary>
        /// アプリ内で表示を許可するドメインかどうかを検証します。
        /// </summary>
        /// <param name="focus">trueの場合、フォーカスモード対象のURLかどうかを判定します。</param>
        private bool IsAllowedDomain(string url, bool focus = false)
        {
            if (string.IsNullOrEmpty(url) || url == "about:blank") return true;
            if (url.StartsWith("chrome-extension://")) return true;

            if (!url.StartsWith("http")) return false;
            try
            {
                Uri uri = new Uri(url);
                if (!uri.Host.EndsWith("x.com") && !uri.Host.EndsWith("twitter.com")) return false;

                bool isFocusUrl = uri.AbsolutePath.Contains("/status/");

                return focus ? isFocusUrl : !isFocusUrl;
            }
            catch { return false; }
        }
    }
}