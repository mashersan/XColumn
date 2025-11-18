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
        /// 拡張機能がWebView2プロファイルにロード済みかどうかを管理するフラグ。
        /// </summary>
        private bool _extensionsLoaded = false;

        /// <summary>
        /// WebView2環境を初期化します。
        /// プロファイルごとのデータフォルダを指定し、拡張機能の有効化オプションを設定します。
        /// </summary>
        private async Task InitializeWebViewEnvironmentAsync()
        {
            // プロファイルごとに異なるブラウザデータフォルダを使用（Cookie分離のため）
            string browserDataFolder = Path.Combine(_userDataFolder, "BrowserData", _activeProfileName);
            Directory.CreateDirectory(browserDataFolder);

            var options = new CoreWebView2EnvironmentOptions();

            // ★重要: 拡張機能を使用するために必須の設定
            options.AreBrowserExtensionsEnabled = true;

            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, browserDataFolder, options);

            // フォーカスモード用のWebViewも初期化しておく
            await InitializeFocusWebView();
        }

        /// <summary>
        /// WebViewがXAML上でロードされた時のイベントハンドラ。
        /// 環境設定(_webViewEnvironment)を適用してCoreWebView2を初期化します。
        /// </summary>
        private void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is WebView2 webView && webView.CoreWebView2 == null && _webViewEnvironment != null)
            {
                webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                webView.EnsureCoreWebView2Async(_webViewEnvironment);
            }
        }

        /// <summary>
        /// CoreWebView2の初期化完了イベント。
        /// 拡張機能のロード（初回のみ）と、個別のWebView設定を行います。
        /// </summary>
        private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                if (e.IsSuccess)
                {
                    // 最初のWebViewが初期化されたタイミングで拡張機能を一括ロードする
                    // (Profileは全WebViewで共有されているため、1回行えば全てに反映される)
                    if (!_extensionsLoaded && webView.CoreWebView2 != null)
                    {
                        _extensionsLoaded = true;
                        await LoadExtensionsAsync(webView.CoreWebView2.Profile);
                    }

                    // カラム固有の設定（タイマーなど）
                    if (webView.DataContext is ColumnData col)
                    {
                        SetupWebView(webView, col);
                    }
                }
            }
        }

        /// <summary>
        /// 登録された拡張機能フォルダをWebView2プロファイルに追加します。
        /// </summary>
        /// <param name="profile">対象のCoreWebView2Profile</param>
        private async Task LoadExtensionsAsync(CoreWebView2Profile profile)
        {
            foreach (var ext in _extensionList)
            {
                if (ext.IsEnabled && Directory.Exists(ext.Path))
                {
                    try
                    {
                        await profile.AddBrowserExtensionAsync(ext.Path);
                        Debug.WriteLine($"Extension loaded: {ext.Name}");
                    }
                    catch (Exception ex)
                    {
                        // 読み込み失敗時はユーザーに通知する
                        System.Windows.MessageBox.Show($"拡張機能 '{ext.Name}' の読み込みに失敗しました。\n\n{ex.Message}", "拡張機能エラー");
                    }
                }
            }
        }

        /// <summary>
        /// 個々のWebViewに対する設定（スクリプト有効化、イベントハンドラ登録など）を行います。
        /// </summary>
        private void SetupWebView(WebView2 webView, ColumnData col)
        {
            if (webView.CoreWebView2 == null) return;

            webView.CoreWebView2.Settings.IsScriptEnabled = true;

            // 右クリックメニューと開発者ツールを無効化（アプリらしい挙動にするため）
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            col.AssociatedWebView = webView;

            // 自動更新タイマーを初期化
            col.InitializeTimer();

            // アプリがアクティブで、かつ「アクティブ時停止」が有効ならタイマーを止める
            if (_isAppActive && StopTimerWhenActive)
            {
                col.Timer?.Stop();
            }

            // 外部リンクを既定のブラウザで開く処理
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // ページ遷移イベント（フォーカスモードへの移行判定など）
            webView.CoreWebView2.SourceChanged += (s, args) =>
            {
                string url = webView.CoreWebView2.Source;
                if (IsAllowedDomain(url, true))
                {
                    // 特定URL（ツイート詳細など）ならフォーカスモードへ
                    if (!_isFocusMode)
                    {
                        _focusedColumnData = col;
                        EnterFocusMode(url);
                    }
                }
                else if (IsAllowedDomain(url))
                {
                    // 通常の許可URLならカラムのURL情報を更新
                    col.Url = url;
                }
            };
            webView.CoreWebView2.Navigate(col.Url);
        }

        private async Task InitializeFocusWebView()
        {
            if (FocusWebView == null || _webViewEnvironment == null) return;
            await FocusWebView.EnsureCoreWebView2Async(_webViewEnvironment);

            // FocusWebView初期化時も念のため拡張機能ロードを試みる
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
        /// フォーカスモード（単一ビュー表示）を開始します。
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
        /// フォーカスモードを終了し、マルチカラム表示に戻ります。
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
        /// 指定されたURLがアプリ内で表示を許可されているドメインか検証します。
        /// </summary>
        /// <param name="focus">trueの場合、フォーカスモード対象（ツイート詳細など）かどうかを判定します。</param>
        private bool IsAllowedDomain(string url, bool focus = false)
        {
            if (string.IsNullOrEmpty(url) || url == "about:blank") return true;
            if (!url.StartsWith("http")) return false;
            try
            {
                Uri uri = new Uri(url);
                if (!uri.Host.EndsWith("x.com") && !uri.Host.EndsWith("twitter.com")) return false;
                bool isFocus = uri.AbsolutePath.Contains("/status/");
                return focus ? isFocus : !isFocus;
            }
            catch { return false; }
        }
    }
}