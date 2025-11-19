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
    public partial class MainWindow
    {
        private bool _extensionsLoaded = false;

        /// <summary>
        /// WebView2環境（CoreWebView2Environment）を初期化します。
        /// 拡張機能の有効化もここで行います。
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

                // Manifest V3
                string? page = json["options_ui"]?["page"]?.GetValue<string>();

                // Manifest V2
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

            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                string url = webView.CoreWebView2.Source;
                if (url.StartsWith("chrome-extension://")) return;

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
                FocusWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    string url = FocusWebView.CoreWebView2.Source;
                    if (url.StartsWith("chrome-extension://")) return;

                    if (_isFocusMode && !IsAllowedDomain(url, true) && url != "about:blank")
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
                _focusedColumnData.AssociatedWebView.CoreWebView2.Navigate(_focusedColumnData.Url);
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

                // ツイート詳細、リポスト、ツイート作成などはフォーカスモード対象
                bool isFocusUrl = uri.AbsolutePath.Contains("/status/") ||
                                  uri.AbsolutePath.Contains("/compose/") ||
                                  uri.AbsolutePath.Contains("/intent/");

                return focus ? isFocusUrl : !isFocusUrl;
            }
            catch { return false; }
        }
    }
}