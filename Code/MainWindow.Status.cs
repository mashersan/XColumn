using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

// WPFとWinForms/Drawingの型名の競合を回避するためのエイリアス定義
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;

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
        /// 接続監視機能を初期化し、定期チェックを開始します。
        /// アプリ起動時に一度だけ呼び出してください。
        /// </summary>
        private void InitializeStatusChecker()
        {
            // タイムアウトを15秒に設定
            _statusClient.Timeout = TimeSpan.FromSeconds(15);

            // 一般的なブラウザのUser-Agentを設定し、アクセス拒否される可能性を低減
            _statusClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

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
            int interval;

            // 引数が渡された場合はそれを使う（設定変更時）
            if (newInterval.HasValue)
            {
                interval = newInterval.Value;
            }
            // 引数がない場合はファイルから読む（起動時）
            else
            {
                var settings = ReadSettingsFromFile(_activeProfileName);
                interval = settings.ServerCheckIntervalMinutes;
            }

            if (interval <= 0) interval = 5; // 安全策

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

            _statusTimer.Interval = TimeSpan.FromMinutes(interval);
            _statusTimer.Start();

            Debug.WriteLine($"Status check timer updated. Interval: {interval} min.");
        }

        /// <summary>
        /// 非同期でX.comへの接続を確認し、結果に応じてUIインジケーターを更新します。
        /// </summary>
        private async Task CheckConnectionStatusAsync()
        {
            // UIパーツが未ロードの場合は処理しない
            if (StatusIndicator == null || StatusText == null) return;

            try
            {
                var sw = Stopwatch.StartNew();

                // HTTP GETリクエストを送信（ヘッダーのみ読み取り完了時点で制御を戻す）
                using (var request = new HttpRequestMessage(HttpMethod.Get, TargetUrl))
                {
                    var response = await _statusClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    sw.Stop();
                    long ms = sw.ElapsedMilliseconds;
                    int code = (int)response.StatusCode;

                    // ステータスコードに応じた判定ロジック
                    if (response.IsSuccessStatusCode)
                    {
                        // 200 OK系: 正常
                        UpdateStatusUI(Brushes.LimeGreen, "正常", $"接続良好 (Code:{code}) - {ms}ms");
                    }
                    else if (code == 403 || code == 429)
                    {
                        // 403 Forbidden / 429 Too Many Requests:
                        // サーバーは稼働しているが、Bot対策などでアクセスが制限されている状態
                        UpdateStatusUI(Brushes.Orange, "稼働中", $"応答あり (Code:{code}) - アクセス制限中");
                    }
                    else if (code < 500)
                    {
                        // その他の400番台: クライアントエラーだがサーバーは応答している
                        UpdateStatusUI(Brushes.Gold, "応答有", $"応答あり (Code:{code}) - クライアントエラー");
                    }
                    else
                    {
                        // 500番台: サーバー内部エラー（障害の可能性大）
                        UpdateStatusUI(Brushes.Red, "障害", $"サーバーエラー (Code:{code}) - X側で問題発生の可能性");
                    }
                }
            }
            catch (Exception ex)
            {
                // タイムアウト、DNS解決失敗など、通信自体が成立しなかった場合
                Debug.WriteLine($"Status Check Failed: {ex.Message}");
                UpdateStatusUI(Brushes.Gray, "不通", "接続できません。ネットワーク障害の可能性があります。");
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
                StatusPanel.ToolTip = $"{tooltip}\n\n[左クリック] Downdetectorで詳細を確認\n[右クリック] 今すぐ再チェック\n最終確認: {DateTime.Now:HH:mm:ss}";
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
                MessageBox.Show($"ページを開けませんでした。\n{ex.Message}", "エラー");
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