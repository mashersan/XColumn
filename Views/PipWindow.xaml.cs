using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using XColumn.Helpers;
using XColumn.Scripts;

namespace XColumn.Views
{
    /// <summary>
    /// 動画などを最前面の小窓（Picture-in-Picture）で表示するウィンドウ。
    /// 独立したWebView2環境で指定URLを開きます。
    /// </summary>
    public partial class PipWindow : Window
    {
        // ===== Fields =====

        /// <summary>表示する対象URL。</summary>
        private readonly string _url;

        /// <summary>WebView2が使用するユーザーデータフォルダ。</summary>
        private readonly string _userDataFolder;

        // ===== Constructor =====

        /// <summary>
        /// 表示URLとユーザーデータフォルダを指定してウィンドウを初期化し、WebViewの初期化を開始します。
        /// </summary>
        /// <param name="url">表示する対象URL。</param>
        /// <param name="userDataFolder">WebView2が使用するユーザーデータフォルダ。</param>
        public PipWindow(string url, string userDataFolder)
        {
            InitializeComponent();
            _url = url;
            _userDataFolder = userDataFolder;
            InitializeWebViewAsync();
        }

        // ===== Private Methods =====

        /// <summary>
        /// WebView2環境を生成し、対象URLへナビゲートします。
        /// </summary>
        private async void InitializeWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
                await PipWebView.EnsureCoreWebView2Async(env);

                // ナビゲーション完了時、YouTube watchページならプレイヤーを全面化する
                PipWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (!args.IsSuccess) return;
                    string src = PipWebView.CoreWebView2.Source ?? "";
                    if (src.Contains("youtube.com") || src.Contains("youtu.be"))
                    {
                        PipWebView.CoreWebView2.ExecuteScriptAsync(ScriptDefinitions.YouTubeFullBleedScript);
                    }
                };

                PipWebView.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                Logger.Log($"PiP Initialize Error: {ex.Message}");
            }
        }

        // ===== Event Handlers =====

        /// <summary>
        /// ヘッダー部のドラッグでウィンドウを移動させます。
        /// </summary>
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        /// <summary>
        /// 「閉じる」ボタンクリック時の処理。WebViewを破棄してウィンドウを閉じます。
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            PipWebView.Dispose();
            this.Close();
        }
    }
}