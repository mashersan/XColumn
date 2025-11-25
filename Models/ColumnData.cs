using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace XColumn.Models
{
    /// <summary>
    /// 1つのカラム（WebView）に関連するデータと状態を管理するモデルクラス。
    /// URL、更新間隔、タイマーの状態などを保持し、UIへの変更通知を行います。
    /// このクラスのインスタンスは設定ファイルにシリアライズされて保存されます。
    /// </summary>
    public class ColumnData : INotifyPropertyChanged
    {
        /// <summary>
        /// カラムの一意識別子。
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _url = "";
        /// <summary>
        /// 現在表示中のURL。
        /// </summary>
        public string Url
        {
            get => _url;
            set { SetField(ref _url, value); }
        }

        private int _refreshIntervalSeconds = 300;
        /// <summary>
        /// 自動更新の間隔（秒）。変更時にタイマーをリセットして再設定します。
        /// </summary>
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetField(ref _refreshIntervalSeconds, value)) UpdateTimer(true);
            }
        }

        private bool _isAutoRefreshEnabled = false;
        /// <summary>
        /// 自動更新が有効かどうか。
        /// </summary>
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetField(ref _isAutoRefreshEnabled, value)) UpdateTimer(true);
            }
        }

        private bool _isRetweetHidden = false;
        /// <summary>
        /// リポスト非表示設定。
        /// </summary>
        public bool IsRetweetHidden
        {
            get => _isRetweetHidden;
            set { _isRetweetHidden = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public bool UseSoftRefresh { get; set; } = true;

        private int _remainingSeconds;
        /// <summary>
        /// 次の更新までの残り秒数。
        /// </summary>
        [JsonIgnore]
        public int RemainingSeconds
        {
            get => _remainingSeconds;
            set
            {
                if (SetField(ref _remainingSeconds, value)) UpdateCountdownText();
            }
        }

        private string _countdownText = "";
        /// <summary>
        /// UIに表示するカウントダウン文字列（例: "(4:59)"）。
        /// </summary>
        [JsonIgnore]
        public string CountdownText
        {
            get => _countdownText;
            private set => SetField(ref _countdownText, value);
        }

        [JsonIgnore]
        public DispatcherTimer? Timer { get; private set; }

        [JsonIgnore]
        public Microsoft.Web.WebView2.Wpf.WebView2? AssociatedWebView { get; set; }

        /// <summary>
        /// タイマーを初期化します。
        /// </summary>
        public void InitializeTimer()
        {
            Timer = new DispatcherTimer();
            Timer.Tick += async (sender, e) => await ReloadWebViewAsync(forceReload: !UseSoftRefresh);
            UpdateTimer(true);
        }

        /// <summary>
        /// WebViewをリロードし、カウントダウンをリセットします。
        /// </summary>
        public async Task ReloadWebViewAsync(bool forceReload = false)
        {
            if (AssociatedWebView?.CoreWebView2 != null)
            {
                // ソフト更新（自動更新）の場合
                if (!forceReload)
                {
                    try
                    {
                        // カーソルがWebView上にあるか判定
                        bool isMouseOver = IsCursorOverWebView();

                        if (isMouseOver)
                        {
                            // マウスオーバー時はスクロール位置を確認
                            string scrollResult = await AssociatedWebView.ExecuteScriptAsync("window.scrollY");

                            if (double.TryParse(scrollResult, out double scrollY))
                            {
                                // 条件1.1.1: マウスオーバー中かつスクロール済みの場合は更新をスキップ
                                if (scrollY > 0)
                                {
                                    Debug.WriteLine($"[ColumnData] Skipped Refresh (MouseOver + Scrolled: {scrollY}px): {Url}");
                                    UpdateTimer(true);
                                    return;
                                }
                                // 条件1.2.1: スクロール0pxなら更新実行
                            }
                        }
                        // 条件2.1: マウスがない場合は無条件で更新実行

                        // --- 更新実行 (キー送信) ---

                        // フォーカス外し
                        await AssociatedWebView.ExecuteScriptAsync("if (document.activeElement instanceof HTMLElement) document.activeElement.blur();");

                        // ピリオドキー送信
                        string keyDownJson = @"{""type"": ""keyDown"", ""modifiers"": 0, ""text"": ""."", ""unmodifiedText"": ""."", ""key"": ""."", ""code"": ""Period"", ""windowsVirtualKeyCode"": 190}";
                        await AssociatedWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyDownJson);

                        string keyUpJson = @"{""type"": ""keyUp"", ""modifiers"": 0, ""key"": ""."", ""code"": ""Period"", ""windowsVirtualKeyCode"": 190}";
                        await AssociatedWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyUpJson);

                    }
                    catch (Exception ex) { Debug.WriteLine($"Soft refresh failed: {ex.Message}"); }
                }
                // 手動更新(force)の場合
                else
                {
                    try
                    {
                        AssociatedWebView.CoreWebView2.Reload();
                    }
                    catch (Exception ex) { Debug.WriteLine($"Reload failed: {ex.Message}"); }
                }
            }

            UpdateTimer(true);
        }

        /// <summary>
        /// 現在の設定に基づいてタイマーの状態（開始/停止/間隔）を更新します。
        /// </summary>
        /// <param name="reset">trueならカウントダウンを初期値にリセット。falseなら現在の残り時間で再開。</param>
        public void UpdateTimer(bool reset = true)
        {
            // 既存のタイマーを停止
            Timer?.Stop();

            // 自動更新が有効な場合はタイマーを再設定
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                // カウントダウンをリセットまたは残り時間が0以下の場合は初期化
                if (reset || RemainingSeconds <= 0) ResetCountdown();

                // タイマーを設定して開始
                if (Timer != null)
                {
                    int nextInterval = reset ? RefreshIntervalSeconds : RemainingSeconds;
                    if (nextInterval <= 0) nextInterval = 1;

                    Timer.Interval = TimeSpan.FromSeconds(nextInterval);
                    Timer.Start();
                }
            }
            else
            {
                RemainingSeconds = 0;
            }
        }

        /// <summary>
        /// カウントダウンを初期設定値に戻します。
        /// </summary>
        public void ResetCountdown()
        {
            // 残り時間を更新間隔にリセット
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
                RemainingSeconds = RefreshIntervalSeconds;
            else
                RemainingSeconds = 0;
        }
        /// <summary>
        /// カウントダウン表示用テキストの更新
        /// RemainingSeconds の変更に伴い、UI上の残り時間表示をリフレッシュするために呼び出されます。
        /// </summary>
        private void UpdateCountdownText()
        {
            if (!IsAutoRefreshEnabled || RemainingSeconds <= 0)
                CountdownText = "";
            else
                CountdownText = $"({TimeSpan.FromSeconds(RemainingSeconds):m\\:ss})";
        }

        /// <summary>
        /// タイマーを停止し、リソースを解放します（カラム削除時など）。
        /// </summary>
        public void StopAndDisposeTimer()
        {
            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }
            RemainingSeconds = 0;
            AssociatedWebView = null;
        }

        /// <summary>
        /// マウスカーソルが現在WebViewコントロール（またはその表示領域）の上に存在するかどうかを判定します。
        /// ユーザーがタイムラインを閲覧中（マウスオーバー時）に、自動更新によってスクロール位置がリセットされるのを防ぐための判定に使用されます。
        /// </summary>
        /// <returns>カーソルがWebView領域内にある場合は true、それ以外は false。</returns>
        private bool IsCursorOverWebView()
        {
            // WebViewが関連付けられていない、または非表示の場合はfalseを返す
            if (AssociatedWebView == null || !AssociatedWebView.IsVisible) return false;

            try
            {
                // WebViewのスクリーン座標とサイズを取得
                System.Windows.Point webViewScreenPos = AssociatedWebView.PointToScreen(new System.Windows.Point(0, 0));
                double width = AssociatedWebView.ActualWidth;
                double height = AssociatedWebView.ActualHeight;

                // 現在のカーソル位置を取得 (Win32 API)
                if (GetCursorPos(out POINT lpPoint))
                {
                    // カーソル位置がWebViewのスクリーン領域内にあるか判定
                    if (lpPoint.X >= webViewScreenPos.X &&
                        lpPoint.X <= webViewScreenPos.X + width &&
                        lpPoint.Y >= webViewScreenPos.Y &&
                        lpPoint.Y <= webViewScreenPos.Y + height)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]

        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}