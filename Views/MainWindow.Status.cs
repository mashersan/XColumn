using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Input;
using System.Windows.Threading;
using XColumn.Helpers;

// WPFとWinForms/Drawingの型名の競合を回避するためのエイリアス定義
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace XColumn.Views
{
    /// <summary>
    /// サーバーの稼働状況（接続性）を監視し、UIに表示する機能を管理する分割クラス。
    /// X.com本体とAPIサーバーの双方を定期的にチェックし、ステータスインジケーターを更新します。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Constants =====

        /// <summary>監視対象のURL（Xのトップページ）。</summary>
        private const string TargetUrl = "https://x.com";

        /// <summary>監視対象のAPIサーバー（バックエンドの生死確認用）。</summary>
        private const string ApiTargetUrl = "https://api.x.com";

        // ===== Fields =====

        /// <summary>サーバー監視用のタイマー。</summary>
        private DispatcherTimer? _statusTimer;

        /// <summary>接続チェックに使用するHTTPクライアント（アプリ生存中は使い回す）。</summary>
        private readonly HttpClient _statusClient = new HttpClient();

        /// <summary>最後にネットワークエラー（API制限など）を検知した時刻。</summary>
        private DateTime _lastNetworkErrorTime = DateTime.MinValue;

        // ===== Public Methods =====

        /// <summary>
        /// WebViewから報告されたネットワークエラーを記録します。
        /// ここでは時刻を控えるのみで、UIの即時上書きは行いません
        /// （本当に落ちているかの判断は定期チェックに委ねます）。
        /// </summary>
        /// <param name="statusCode">HTTPステータスコード。</param>
        public void ReportNetworkError(int statusCode)
        {
            Dispatcher.Invoke(() =>
            {
                _lastNetworkErrorTime = DateTime.Now;
                Logger.Log($"[Status] WebView reported background error: {statusCode}");
            });
        }

        /// <summary>
        /// サーバー監視タイマーの間隔を更新します。
        /// 引数を指定するとその値を使用し、省略すると現在のメモリ上の設定値を使用します。
        /// </summary>
        /// <param name="newInterval">新しいチェック間隔（分）。null の場合は現在値を維持。</param>
        public void UpdateStatusCheckTimer(int? newInterval = null)
        {
            // 引数が指定された場合は設定値を更新
            if (newInterval.HasValue)
            {
                _serverCheckIntervalMinutes = newInterval.Value;
            }

            // 安全策: 0以下の場合は5分にフォールバック
            if (_serverCheckIntervalMinutes <= 0) _serverCheckIntervalMinutes = 5;

            // タイマーの初期化、または既存タイマーの停止
            if (_statusTimer == null)
            {
                _statusTimer = new DispatcherTimer();
                _statusTimer.Tick += async (s, e) => await CheckConnectionStatusAsync();
            }
            else
            {
                _statusTimer.Stop();
            }

            _statusTimer.Interval = TimeSpan.FromMinutes(_serverCheckIntervalMinutes);
            _statusTimer.Start();

            Logger.Log($"Status check timer updated. Interval: {_serverCheckIntervalMinutes} min.");
        }

        // ===== Private Methods (Setup) =====

        /// <summary>
        /// 接続監視機能を初期化し、定期チェックを開始します。
        /// アプリ起動時に一度だけ呼び出してください。
        /// </summary>
        private void InitializeStatusChecker()
        {
            // タイムアウトを15秒に設定
            _statusClient.Timeout = TimeSpan.FromSeconds(15);

            // 一般的なブラウザのUser-Agentを設定し、アクセス拒否される可能性を低減
            _statusClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // キャッシュの影響を受けないよう、常に最新を取得する設定
            _statusClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // 設定値に基づきタイマーを開始
            UpdateStatusCheckTimer();

            // 初回チェックを即時実行（戻り値は意図的に破棄）
            _ = CheckConnectionStatusAsync();
        }

        // ===== Private Methods (Connection Check) =====

        /// <summary>
        /// 非同期でX.comおよびAPIサーバーへの接続を確認し、結果に応じてUIインジケーターを更新します。
        /// </summary>
        private async Task CheckConnectionStatusAsync()
        {
            // UIパーツが未ロードの場合は処理しない
            if (StatusIndicator == null || StatusText == null) return;

            Dispatcher.Invoke(() =>
            {
                StatusIndicator.Fill = System.Windows.Media.Brushes.Yellow;
                StatusText.Text = Properties.Resources.Status_Checking;
            });

            try
            {
                var sw = Stopwatch.StartNew();
                long totalMs = 0;
                bool isApiOk = true;
                int apiErrorCode = 0;

                // 1. メインページ (HTML) のチェック
                using (var request = new HttpRequestMessage(HttpMethod.Get, TargetUrl))
                {
                    // キャッシュ無効化をリクエスト単位でも念のため指定
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    using (var response = await _statusClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        totalMs = sw.ElapsedMilliseconds;
                        int code = (int)response.StatusCode;

                        // 200系に限らず、リダイレクト(300系)や未認証(400/401/403)も「サーバーは生存」と判断する。
                        // 500系のみを本体障害とみなす。
                        if (code >= 500)
                        {
                            UpdateStatusUI(Brushes.Red, Properties.Resources.Status_Error,
                                string.Format(Properties.Resources.Msg_Status_ServerError, code));
                            return;
                        }
                    }
                }

                // 2. APIサーバーの簡易チェック
                try
                {
                    using (var apiRequest = new HttpRequestMessage(HttpMethod.Get, ApiTargetUrl))
                    {
                        apiRequest.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                        using (var apiResponse = await _statusClient.SendAsync(apiRequest, HttpCompletionOption.ResponseHeadersRead))
                        {
                            int apiCode = (int)apiResponse.StatusCode;

                            // 500番台のみをAPI障害とみなす（認証なしのため400系が返るのは正常）
                            if (apiCode >= 500)
                            {
                                isApiOk = false;
                                apiErrorCode = apiCode;
                            }
                            else if (apiCode == 429)
                            {
                                // レート制限中は即座に「不安定」表示して終了
                                sw.Stop();
                                UpdateStatusUI(Brushes.Orange, Properties.Resources.Status_Unstable,
                                    string.Format(Properties.Resources.Msg_Status_ApiLimited, apiCode));
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    // APIサーバーのDNS解決失敗などは障害とみなす
                    isApiOk = false;
                }

                sw.Stop();

                // 最終判定
                if (!isApiOk)
                {
                    UpdateStatusUI(Brushes.Orange, Properties.Resources.Status_Unstable,
                        string.Format(Properties.Resources.Msg_Status_ApiUnstable, apiErrorCode));
                }
                else
                {
                    UpdateStatusUI(Brushes.LimeGreen, Properties.Resources.Status_OK,
                        string.Format(Properties.Resources.Msg_Status_Connected, totalMs));
                }
            }
            catch (Exception ex)
            {
                // タイムアウト、DNS解決失敗など、メインサーバーとの通信自体が成立しなかった場合
                Logger.Log($"Status Check Failed: {ex.Message}");
                UpdateStatusUI(Brushes.Gray, Properties.Resources.Status_Error, Properties.Resources.Status_ConnectionFailed);
            }
        }

        /// <summary>
        /// ステータスインジケーター（色・テキスト・ツールチップ）を更新します。
        /// </summary>
        /// <param name="color">インジケーターの色。</param>
        /// <param name="shortText">表示する短いテキスト。</param>
        /// <param name="tooltip">詳細なツールチップテキスト。</param>
        private void UpdateStatusUI(Brush color, string shortText, string tooltip)
        {
            if (StatusIndicator == null || StatusText == null) return;

            StatusIndicator.Fill = color;
            StatusText.Text = shortText;

            if (StatusPanel != null)
            {
                StatusPanel.ToolTip = $"{tooltip}\n\n{Properties.Resources.Tooltip_ServerStatus2}{DateTime.Now:HH:mm:ss}";
            }
        }

        // ===== Private Methods (Event Handlers) =====

        /// <summary>
        /// ステータスパネル左クリック時の処理。Downdetectorの障害情報ページを外部ブラウザで開きます。
        /// </summary>
        private void StatusPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://downdetector.jp/shougai/twitter/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageWindow.Show(string.Format(Properties.Resources.Msg_Err_OpenPageFailed, ex.Message),
                    Properties.Resources.Error);
            }
        }

        /// <summary>
        /// ステータスパネル右クリック時の処理。即座に接続チェックを実行します。
        /// </summary>
        private async void StatusPanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            StatusText.Text = Properties.Resources.Status_Checking;
            await CheckConnectionStatusAsync();
        }
    }
}