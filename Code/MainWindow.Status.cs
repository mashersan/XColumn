using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers; // 追加
using System.Windows.Input;
using System.Windows.Threading;

// WPFとWinForms/Drawingの型名の競合を回避するためのエイリアス定義
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace XColumn
{
    /// <summary>
    /// サーバーの稼働状況（接続性）を監視し、UIに表示する機能を管理する分割クラス。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// サーバー監視用のタイマー。
        /// </summary>
        private DispatcherTimer? _statusTimer;

        /// <summary>
        /// 接続チェックに使用するHTTPクライアント。
        /// </summary>
        private readonly HttpClient _statusClient = new HttpClient();

        /// <summary>
        /// 監視対象のURL（Xのトップページ）。
        /// </summary>
        private const string TargetUrl = "https://x.com";

        /// <summary>
        /// 監視対象のAPIサーバー（バックエンドの生死確認用）。
        /// </summary>
        private const string ApiTargetUrl = "https://api.x.com";

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

            // キャッシュ等の影響を受けないよう、常に最新を取りに行く設定を追加
            _statusClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // 設定ファイルからチェック間隔を取得してタイマーを開始
            UpdateStatusCheckTimer();

            // 初回チェックを即時実行
            _ = CheckConnectionStatusAsync();
        }

        /// <summary>
        /// 設定に合わせて、サーバー監視タイマーの間隔を更新します。
        /// 引数を指定するとその値を使用し、省略すると設定ファイルから読み込みます。
        /// </summary>
        public void UpdateStatusCheckTimer(int? newInterval = null)
        {
            // 引数が指定された場合は設定値を更新
            if (newInterval.HasValue)
            {
                _serverCheckIntervalMinutes = newInterval.Value;
            }

            // 安全策: 0以下の場合は5分にする
            if (_serverCheckIntervalMinutes <= 0) _serverCheckIntervalMinutes = 5;

            // タイマーの初期化または再設定
            if (_statusTimer == null)
            {
                _statusTimer = new DispatcherTimer();
                _statusTimer.Tick += async (s, e) => await CheckConnectionStatusAsync();
            }
            else
            {
                _statusTimer.Stop();
            }

            // メモリ上の変数を使って間隔を設定
            _statusTimer.Interval = TimeSpan.FromMinutes(_serverCheckIntervalMinutes);
            _statusTimer.Start();

            Logger.Log($"Status check timer updated. Interval: {_serverCheckIntervalMinutes} min.");
        }

        /// <summary>
        /// 非同期でX.comへの接続を確認し、結果に応じてUIインジケーターを更新します。
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
                bool isMainPageOk = false;
                long totalMs = 0;

                // 1. メインページ (HTML) のチェック
                using (var request = new HttpRequestMessage(HttpMethod.Get, TargetUrl))
                {
                    // キャッシュ無効化を念のためリクエスト単位でも指定
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    using (var response = await _statusClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        totalMs = sw.ElapsedMilliseconds;
                        int code = (int)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                        {
                            isMainPageOk = true;
                            // ここではまだUI更新せず、APIチェックへ進む
                        }
                        else if (code == 403 || code == 429)
                        {
                            UpdateStatusUI(Brushes.Orange, Properties.Resources.Status_Unstable, $"応答あり (Code:{code}) - アクセス制限中");
                            return;
                        }
                        else if (code < 500)
                        {
                            UpdateStatusUI(Brushes.Gold, Properties.Resources.Status_Unstable, $"応答あり (Code:{code}) - クライアントエラー");
                            return;
                        }
                        else
                        {
                            UpdateStatusUI(Brushes.Red, Properties.Resources.Status_Error, $"サーバーエラー (Code:{code}) - X側で問題発生の可能性");
                            return;
                        }
                    }
                }

                // 2. APIサーバーの簡易チェック (メインページがOKの場合のみ)
                // ポスト取得可否はAPIサーバーの状態に依存するため、ここも確認する
                if (isMainPageOk)
                {
                    try
                    {
                        using (var apiRequest = new HttpRequestMessage(HttpMethod.Get, ApiTargetUrl))
                        {
                            apiRequest.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                            using (var apiResponse = await _statusClient.SendAsync(apiRequest, HttpCompletionOption.ResponseHeadersRead))
                            {
                                int apiCode = (int)apiResponse.StatusCode;

                                // APIルートは通常 404 Not Found を返すが、これは「サーバーが生きている」証拠。
                                // 500番台の場合はAPIサーバーダウンとみなす。
                                if (apiCode >= 500)
                                {
                                    sw.Stop();
                                    UpdateStatusUI(Brushes.Red, Properties.Resources.Status_Error,
                                        $"APIエラー (Code:{apiCode}) - ポスト取得不可の可能性");
                                    return;
                                }
                                // 429 Too Many Requests ならAPI制限中
                                else if (apiCode == 429)
                                {
                                    sw.Stop();
                                    UpdateStatusUI(Brushes.Orange, Properties.Resources.Status_Unstable,
                                        $"API制限中 (Code:{apiCode}) - ポスト取得に失敗する可能性があります");
                                    return;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // APIサーバーへの接続自体が失敗した場合
                        UpdateStatusUI(Brushes.Gold, Properties.Resources.Status_Unstable,
                            "APIサーバー接続不可 - タイムライン表示に不具合の可能性");
                        return;
                    }
                }

                sw.Stop();
                // 全てのチェックを通過
                UpdateStatusUI(Brushes.LimeGreen, Properties.Resources.Status_OK, $"接続良好 - {totalMs}ms");

            }
            catch (Exception ex)
            {
                // タイムアウト、DNS解決失敗など、通信自体が成立しなかった場合
                Logger.Log($"Status Check Failed: {ex.Message}");
                UpdateStatusUI(Brushes.Gray, Properties.Resources.Status_Error, "接続できません。ネットワーク障害の可能性があります。");
            }
        }

        /// <summary>
        /// ステータスインジケーター（色とテキスト）を更新します。
        /// </summary>
        /// <param name="color">インジケーターの色</param>
        /// <param name="shortText">表示する短いテキスト</param>
        /// <param name="tooltip">詳細なツールチップテキスト</param>
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

        /// <summary>
        /// ステータスパネル左クリック時の処理。
        /// Downdetectorのページを外部ブラウザで開きます。
        /// </summary>
        private void StatusPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://downdetector.jp/shougai/twitter/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageWindow.Show($"ページを開けませんでした。\n{ex.Message}", "エラー");
            }
        }

        /// <summary>
        /// ステータスパネル右クリック時の処理。
        /// 即座に接続チェックを実行します。
        /// </summary>
        private async void StatusPanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            StatusText.Text = "確認中...";
            await CheckConnectionStatusAsync();
        }
    }
}