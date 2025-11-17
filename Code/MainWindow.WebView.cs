using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    public partial class MainWindow
    {
        /// <summary>
        /// プロファイルごとのデータフォルダを使用してWebView環境を初期化します。
        /// </summary>
        private async Task InitializeWebViewEnvironmentAsync()
        {
            string browserDataFolder = Path.Combine(_userDataFolder, "BrowserData", _activeProfileName);
            Directory.CreateDirectory(browserDataFolder);

            var options = new CoreWebView2EnvironmentOptions();
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

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                if (e.IsSuccess && webView.DataContext is ColumnData col)
                {
                    SetupWebView(webView, col);
                }
            }
        }

        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            col.AssociatedWebView = webView;

            // タイマーを初期化（必ず行う）
            col.InitializeTimer();

            // アプリがアクティブな場合はタイマーを一時停止
            if (_isAppActive) col.Timer?.Stop();

            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // ページ遷移イベント
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                string url = webView.CoreWebView2.Source;
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
                FocusWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                FocusWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    string url = FocusWebView.CoreWebView2.Source;
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

        private void EnterFocusMode(string url)
        {
            _isFocusMode = true;
            FocusWebView?.CoreWebView2?.Navigate(url);
            ColumnItemsControl.Visibility = Visibility.Collapsed;
            FocusViewGrid.Visibility = Visibility.Visible;

            _countdownTimer.Stop();
            foreach (var c in Columns) c.Timer?.Stop();
        }

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
        /// ドメインとパスの許可リスト検証。
        /// compose/intent などはフォーカスモードから除外してカラム内で処理させる。
        /// </summary>
        private bool IsAllowedDomain(string url, bool focus = false)
        {
            if (string.IsNullOrEmpty(url) || url == "about:blank") return true;
            if (!url.StartsWith("http")) return false;
            try
            {
                Uri uri = new Uri(url);
                if (!uri.Host.EndsWith("x.com") && !uri.Host.EndsWith("twitter.com")) return false;

                // ツイート詳細のみフォーカス対象とする
                bool isFocus = uri.AbsolutePath.Contains("/status/");

                return focus ? isFocus : !isFocus;
            }
            catch { return false; }
        }
    }
}