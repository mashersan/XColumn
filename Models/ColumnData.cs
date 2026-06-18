using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using XColumn.Helpers;

namespace XColumn.Models
{
    /// <summary>
    /// 1つのカラム（WebView）に関連するデータと状態を管理するモデル兼ビューモデル。
    /// URL、更新間隔、タイマーの状態などを保持し、UIへの変更通知を行います。
    /// 永続化される項目はそのままシリアライズされ、実行時のみ有効な項目には [JsonIgnore] を付与しています。
    ///
    /// CommunityToolkit.Mvvm の ObservableObject を継承し、[ObservableProperty] / [RelayCommand] の
    /// ソースジェネレータを利用しています。
    /// WebViewコントロール固有の操作（戻る・休止HTML表示など）は、このクラスが発火する
    /// 委譲イベント(GoBackRequested / SuspendRequested)を View 側が購読して実行します。
    /// </summary>
    public partial class ColumnData : ObservableObject
    {
        /// <summary>
        /// カラムのレート制限ステータス。Normal=通常 / Caution=残数僅少の注意 / Stopped=更新停止中。
        /// </summary>
        public enum ColumnRateLimitStatus { Normal, Caution, Stopped }

        #region Identity

        /// <summary>
        /// カラムの一意識別子。
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        #endregion

        #region Observable Properties (バインド対象の状態)

        /// <summary>
        /// アクティブ状態（選択中かどうか）。
        /// </summary>
        [ObservableProperty]
        private bool isActive;

        /// <summary>
        /// カラム幅。初期値は 0 とし、作成時や読み込み時にグローバル設定値を適用します。
        /// </summary>
        [ObservableProperty]
        private double width = 0;

        /// <summary>
        /// Webページ内で入力フォームにフォーカスがあるかどうか（実行時のみ）。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isInputActive;

        partial void OnIsInputActiveChanged(bool value)
        {
            if (value) LastInputTime = DateTime.Now;
        }

        /// <summary>
        /// 現在スクロール位置がトップ（またはそれに近い）かどうか（実行時のみ）。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isAtTop = true;

        /// <summary>
        /// メモリ解放のための休止状態かどうか（実行時のみ）。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isSuspended = false;

        /// <summary>
        /// レート制限ステータス（実行時のみ）。アイコン表示の切り替えに使用。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        [NotifyPropertyChangedFor(nameof(IsRateLimitCaution))]
        [NotifyPropertyChangedFor(nameof(IsRateLimitStopped))]
        private ColumnRateLimitStatus rateLimitStatus = ColumnRateLimitStatus.Normal;

        /// <summary>注意モード（残数僅少）かどうか。XAMLトリガー用。</summary>
        [JsonIgnore] public bool IsRateLimitCaution => RateLimitStatus == ColumnRateLimitStatus.Caution;
        /// <summary>更新停止モードかどうか。XAMLトリガー用。</summary>
        [JsonIgnore] public bool IsRateLimitStopped => RateLimitStatus == ColumnRateLimitStatus.Stopped;

        /// <summary>
        /// レート制限(429)による休止中かどうか（実行時のみ）。手動休止(💤)と区別するため。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isRateLimited;

        /// <summary>
        /// 現在表示中のURL。
        /// </summary>
        [ObservableProperty]
        private string url = "";

        /// <summary>
        /// 自動更新の間隔（秒）。変更時にタイマーをリセットして再設定します。
        /// </summary>
        [ObservableProperty]
        private int refreshIntervalSeconds = 300;

        partial void OnRefreshIntervalSecondsChanged(int value) => UpdateTimer(true);

        /// <summary>
        /// 自動更新が有効かどうか。
        /// </summary>
        [ObservableProperty]
        private bool isAutoRefreshEnabled = false;

        partial void OnIsAutoRefreshEnabledChanged(bool value) => UpdateTimer(true);

        /// <summary>
        /// リポスト非表示設定。
        /// </summary>
        [ObservableProperty]
        private bool isRetweetHidden = false;

        /// <summary>
        /// リプライ（返信）非表示設定。
        /// </summary>
        [ObservableProperty]
        private bool isReplyHidden = false;

        /// <summary>
        /// カラムの削除や戻る操作をロックする設定。
        /// </summary>
        [ObservableProperty]
        private bool isLocked = false;

        /// <summary>
        /// WebViewのズーム倍率（1.0 = 100%）。変更時に ZoomPercentage の通知も発火します。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ZoomPercentage))]
        private double zoomFactor = 1.0;

        /// <summary>
        /// サムネイル画像・動画の表示倍率（％）。
        /// </summary>
        [ObservableProperty]
        private int mediaScalePercentage = 100;

        /// <summary>
        /// 次の更新までの残り秒数（実行時のみ）。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private int remainingSeconds;

        partial void OnRemainingSecondsChanged(int value) => UpdateCountdownText();

        /// <summary>
        /// UIに表示するカウントダウン文字列（例: "(4:59)"）（実行時のみ）。
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private string countdownText = "";

        /// <summary>
        /// このカラムが使用するプロファイル名。未指定(null)の場合はウィンドウのメインプロファイルを使用します。
        /// </summary>
        [ObservableProperty]
        private string? profileName;

        #endregion

        #region Other Properties (永続化・実行時参照)

        /// <summary>
        /// このカラムがX/Twitter以外の外部サイトを表示するカラムかどうか。
        /// 起動時復元・保存時のドメイン制限を免除するために保存する。
        /// </summary>
        public bool IsExternalSite { get; set; } = false;

        /// <summary>
        /// リスト自動遷移用のフラグ（UIバインドなし・永続化対象）。
        /// </summary>
        public bool IsListAutoNav { get; set; } = false;

        /// <summary>
        /// 設定ページや詳細ページから復帰する際に使用する直前の有効なURL。
        /// </summary>
        public string LastValidUrl { get; set; } = "https://x.com/home";

        /// <summary>
        /// 最終入力時刻（永続化対象）。設定時にUI通知は行いません。
        /// </summary>
        public DateTime LastInputTime
        {
            get => _lastInputTime;
            set { _lastInputTime = value; } // UI通知は不要
        }
        private DateTime _lastInputTime = DateTime.MinValue;

        /// <summary>
        /// ジッター用の乱数。
        /// UpdateTimer は UI スレッドからのみ呼ばれるため共有インスタンスで問題ない。
        // （new Random() を都度生成すると、短時間に多数生成した際に同一シードで同じ値が並ぶため static で共有） 
        /// </summary>
        private static readonly Random _jitterRandom = new Random();

        /// <summary>
        /// 自動更新の同時多発（バースト）を避けるための、更新間隔への上乗せ秒数を返します。
        /// 0〜約20%（最大60秒）の正方向ジッター。複数カラムの発火タイミングをばらけさせ、
        /// X の API 制限(429)に陥りにくくします。正方向のみ＝ユーザー設定より短くは更新しません。
        /// </summary>
        private int GetJitterSeconds()
        {
            if (!UseRefreshJitter) return 0;
            if (RefreshIntervalSeconds <= 0) return 0;
            int max = Math.Min((int)(RefreshIntervalSeconds * 0.2), 60);
            if (max <= 0) return 0;
            return _jitterRandom.Next(0, max + 1);
        }

        /// <summary>
        /// UI表示用のズーム率（％）。ZoomFactorと連動します（実行時のみ）。
        /// </summary>
        [JsonIgnore]
        public int ZoomPercentage
        {
            get => (int)Math.Round(ZoomFactor * 100);
            set
            {
                // 10%～500%の範囲に制限（安全策）
                int val = Math.Clamp(value, 10, 500);
                ZoomFactor = val / 100.0;
            }
        }

        /// <summary>
        /// ソフト更新（JSによる更新）を使用するかどうか（実行時のみ・設定から反映）。
        /// </summary>
        [JsonIgnore]
        public bool UseSoftRefresh { get; set; } = true;

        /// <summary>
        /// 自動更新タイミングを分散するか（実行時のみ・設定から反映）。
        /// </summary>
        [JsonIgnore]
        public bool UseRefreshJitter { get; set; } = false;

        /// <summary>
        /// 未読位置保持（スクロール中は更新しない）設定（実行時のみ・設定から反映）。
        /// </summary>
        [JsonIgnore]
        public bool KeepUnreadPosition { get; set; } = false;

        /// <summary>
        /// このカラムの自動更新を駆動するタイマー（実行時のみ）。
        /// </summary>
        [JsonIgnore]
        public DispatcherTimer? Timer { get; private set; }

        /// <summary>
        /// このカラムに紐づく実WebViewコントロール（実行時のみ）。
        /// </summary>
        [JsonIgnore]
        public Microsoft.Web.WebView2.Wpf.WebView2? AssociatedWebView { get; set; }

        #endregion

        #region View への委譲イベント（WebViewコントロール固有の操作を依頼する）

        /// <summary>
        /// 「戻る」操作を View(WebView) に依頼するイベント。
        /// View 側で CanGoBack を確認して GoBack() を実行します。
        /// </summary>
        public event Action<ColumnData>? GoBackRequested;

        /// <summary>
        /// 休止状態の切り替えに伴う表示更新を View に依頼するイベント。
        /// 引数 isSuspended が true なら休止HTMLの表示、false なら元URLへの復帰を行います。
        /// </summary>
        public event Action<ColumnData, bool>? SuspendRequested;

        /// <summary>
        /// レート制限(429)専用の休止画面表示を View に依頼するイベント。
        /// 復帰（元URL遷移）は通常の SuspendRequested(false) 経路を再利用します。
        /// </summary>
        public event Action<ColumnData, DateTimeOffset>? RateLimitSuspendRequested;

        #endregion

        #region Commands（XAMLのボタンから直接バインド）

        /// <summary>
        /// 手動更新（強制リロード）。
        /// </summary>
        [RelayCommand]
        private async Task ManualRefreshAsync()
        {
            // 手動更新は設定に関わらず強制リロード
            await ReloadWebViewAsync(forceReload: true);
        }

        /// <summary>
        /// メモリ解放のためのクリア（DOM破棄＆再ナビゲーション）。
        /// </summary>
        [RelayCommand]
        private async Task ClearAsync()
        {
            await ResetAndClearAsync();
        }

        /// <summary>
        /// 「戻る」操作。ロック中は無効。実際のWebView操作は View に委譲します。
        /// </summary>
        [RelayCommand]
        private void GoBack()
        {
            // ロック中は戻る操作を無効化
            if (IsLocked) return;
            GoBackRequested?.Invoke(this);
        }

        /// <summary>
        /// 休止状態のトグル。状態とタイマーはここで制御し、
        /// 実際の画面表示(休止HTML/復帰)は View に委譲します。
        /// </summary>
        [RelayCommand]
        private void Suspend()
        {
            // WebView がまだ無い場合は何もしない（View 側で再確認もする）
            if (AssociatedWebView?.CoreWebView2 == null) return;

            // 状態を反転
            IsSuspended = !IsSuspended;

            if (IsSuspended)
            {
                // 休止: タイマーを止める
                Timer?.Stop();
                RemainingSeconds = 0;
                // 表示更新（休止HTML）は View に委譲
                SuspendRequested?.Invoke(this, true);
                Logger.Log($"[ColumnData] Column suspended: {Url}");
            }
            else
            {
                IsRateLimited = false;
                _rateLimitResumeTimer?.Stop();

                // 復帰: 元URLへの遷移は View に委譲
                SuspendRequested?.Invoke(this, false);
                UpdateTimer(true);
                Logger.Log($"[ColumnData] Column resumed: {Url}");
            }
        }

        #endregion

        #region Public Methods（タイマー・ライフサイクル）

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
            // 休止中はリロード処理をブロックする
            if (IsSuspended) return;

            // レート制限による更新停止中は、手動更新も含めてリロードしない
            // DOM破棄や無駄な429を避ける。リセット時刻経過で自動復帰する）
            if (RateLimitStatus == ColumnRateLimitStatus.Stopped) return;

            if (AssociatedWebView?.CoreWebView2 != null)
            {
                // 非X/Twitterドメインのカラムはピリオドキーのソフト更新が効かないため、
                // 設定(UseSoftRefresh)に関わらず強制リロード(F5相当)にフォールバックする ▼
                if (!forceReload)
                {
                    // 表示中の実URLを優先（カラム内で別サイトに遷移している場合も拾うため）。
                    // NavigateToString等でSourceが空のときは設定URL(Url)にフォールバック。
                    string currentUrl = AssociatedWebView.CoreWebView2.Source;
                    if (string.IsNullOrEmpty(currentUrl)) currentUrl = Url;

                    if (!IsXDomain(currentUrl)) forceReload = true;
                }

                // ソフト更新（自動更新）の場合
                if (!forceReload)
                {
                    // IME/入力監視による更新ブロック
                    // 1. 現在入力中である (IsInputActive)
                    // 2. 最後の入力から30秒以内である (IME確定直後の誤作動防止)
                    bool isRecentlyInput = (DateTime.Now - LastInputTime).TotalSeconds < 30;

                    if (IsInputActive || isRecentlyInput)
                    {
                        // 更新せず、タイマーを少し延長して終了 (30秒後に再試行)
                        RemainingSeconds = 30;
                        UpdateTimer(false); // false = リセットせず現在のRemainingSecondsで再開
                        return;
                    }

                    try
                    {
                        // 1. マウスオーバー判定 (従来機能)
                        bool isMouseOver = IsCursorOverWebView();
                        if (isMouseOver)
                        {
                            Logger.Log($"[ColumnData] Skipped Refresh (MouseOver): {Url}");
                            UpdateTimer(true);
                            return;
                        }

                        // 2. 未読位置保持判定
                        if (KeepUnreadPosition)
                        {
                            // UIアイコンと同じ IsAtTop プロパティを利用して判定
                            if (!IsAtTop)
                            {
                                Logger.Log($"[ColumnData] Skipped Refresh (KeepUnreadPosition ON, Not at top): {Url}");
                                UpdateTimer(true);
                                return;
                            }
                        }

                        // --- 更新実行 (キー送信) ---

                        // フォーカス外し
                        await AssociatedWebView.ExecuteScriptAsync("if (document.activeElement instanceof HTMLElement) document.activeElement.blur();");

                        // ピリオドキー送信
                        string keyDownJson = @"{""type"": ""keyDown"", ""modifiers"": 0, ""text"": ""."", ""unmodifiedText"": ""."", ""key"": ""."", ""code"": ""Period"", ""windowsVirtualKeyCode"": 190}";
                        await AssociatedWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyDownJson);

                        string keyUpJson = @"{""type"": ""keyUp"", ""modifiers"": 0, ""key"": ""."", ""code"": ""Period"", ""windowsVirtualKeyCode"": 190}";
                        await AssociatedWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyUpJson);
                    }
                    catch (Exception ex) { Logger.Log($"Soft refresh failed: {ex.Message}"); }
                }
                // 手動更新(force)の場合
                else
                {
                    try
                    {
                        AssociatedWebView.CoreWebView2.Reload();
                    }
                    catch (Exception ex) { Logger.Log($"Reload failed: {ex.Message}"); }
                }
            }

            UpdateTimer(true);
        }

        /// <summary>
        /// 指定URLが X / Twitter のドメインかどうかを判定します。
        /// ソフト更新のピリオドキーは X 上でしか機能しないため、更新方式の振り分けに使います。
        /// </summary>
        private static bool IsXDomain(string? url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                string host = new Uri(url).Host;
                return host.EndsWith("x.com") || host.EndsWith("twitter.com");
            }
            catch { return false; }
        }

        /// <summary>
        /// 現在の設定に基づいてタイマーの状態（開始/停止/間隔）を更新します。
        /// </summary>
        /// <param name="reset">trueならカウントダウンを初期値にリセット。falseなら現在の残り時間で再開。</param>
        public void UpdateTimer(bool reset = true)
        {
            // 既存のタイマーを停止
            Timer?.Stop();

            // 休止中はいかなる場合もタイマーを再スタートさせない
            if (IsSuspended)
            {
                RemainingSeconds = 0;
                return;
            }

            // レート制限による更新停止中も再開しない（リセット時刻経過まで待つ）
            if (RateLimitStatus == ColumnRateLimitStatus.Stopped)
            {
                Timer?.Stop();
                return;
            }

            // 自動更新が有効な場合はタイマーを再設定
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                // カウントダウンをリセットまたは残り時間が0以下の場合は初期化
                // 【API制限対策】基準間隔にジッター(0〜約20%/最大60秒)を上乗せして、
                // 複数カラムの自動更新が同じ秒に発火する(バースト)のを防ぐ。
                // 表示(RemainingSeconds)と実発火(Timer.Interval)を同値にして整合させる。
                if (reset || RemainingSeconds <= 0)
                {
                    RemainingSeconds = RefreshIntervalSeconds + GetJitterSeconds();
                }
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
        /// タイマーを停止し、リソースを解放します（カラム削除時など）。
        /// </summary>
        public void StopAndDisposeTimer()
        {
            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }

            _rateLimitCountdownTimer?.Stop();
            _rateLimitCountdownTimer = null;

            RemainingSeconds = 0;
            AssociatedWebView = null;
        }

        /// <summary>
        /// カラムの状態を完全にリセットし、ベースURLへ再ナビゲーションします（メモリ解放用）。
        /// </summary>
        public async Task ResetAndClearAsync()
        {
            if (AssociatedWebView?.CoreWebView2 != null)
            {
                try
                {
                    // 履歴を保持せず、現在のURL（またはベースURL）へ直接遷移することでDOMを破棄
                    AssociatedWebView.CoreWebView2.Navigate(this.Url);
                    Logger.Log($"[ColumnData] Column reset and cleared: {Url}");
                }
                catch (Exception ex) { Logger.Log($"Clear failed: {ex.Message}"); }
            }

            UpdateTimer(true);
        }


        #endregion

        #region Rate Limit Proactive Handling（残数監視による更新停止／自動復帰）

        private const int RateLimitCautionThreshold = 10; // 残数がこれ未満で注意モード
        private const int RateLimitStopThreshold = 5;  // 残数がこれ未満で更新停止モード

        private DateTimeOffset _rateLimitResetTime;
        private DispatcherTimer? _rateLimitCountdownTimer; // 停止中のリセット待ちカウントダウン（1秒間隔）

        /// <summary>
        /// View(レスポンス監視)から、主タイムラインの残数とリセット時刻が観測されたときに呼ばれます。
        /// 残数に応じて 通常／注意／更新停止 を切り替えます。ページの破棄は行いません。
        /// </summary>
        public void UpdateRateLimitStatus(int remaining, DateTimeOffset resetTime)
        {
            // 手動休止中は介入しない
            if (IsSuspended) return;

            if (remaining < RateLimitStopThreshold)
            {
                _rateLimitResetTime = resetTime;
                EnterRateLimitStopped();
            }
            else if (remaining < RateLimitCautionThreshold)
            {
                // 停止中はそのまま維持。それ以外は注意モードへ。
                if (RateLimitStatus != ColumnRateLimitStatus.Stopped)
                    RateLimitStatus = ColumnRateLimitStatus.Caution;
            }
            else
            {
                // 残数が回復 → 注意モードからは通常へ戻す
                // （停止中は時間ベースで戻すため、ここでは触らない）
                if (RateLimitStatus == ColumnRateLimitStatus.Caution)
                    RateLimitStatus = ColumnRateLimitStatus.Normal;
            }
        }

        /// <summary>更新停止モードへ移行し、自動更新を止めてリセット待ちカウントダウンを開始します（DOMは保持）。</summary>
        private void EnterRateLimitStopped()
        {
            if (RateLimitStatus == ColumnRateLimitStatus.Stopped)
            {
                UpdateRateLimitCountdownText(); // 既に停止中ならリセット時刻の更新だけ反映
                return;
            }

            RateLimitStatus = ColumnRateLimitStatus.Stopped;
            Timer?.Stop();                 // 自動更新タイマーを停止（ページは破棄しない）
            StartRateLimitCountdown();
            Logger.Log($"[Rate Limit] Refresh paused until {_rateLimitResetTime.LocalDateTime:HH:mm:ss}: {Url}");
        }

        private void StartRateLimitCountdown()
        {
            _rateLimitCountdownTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _rateLimitCountdownTimer.Tick -= OnRateLimitCountdownTick; // 二重購読防止
            _rateLimitCountdownTimer.Tick += OnRateLimitCountdownTick;
            UpdateRateLimitCountdownText();
            _rateLimitCountdownTimer.Start();
        }

        private void OnRateLimitCountdownTick(object? sender, EventArgs e)
        {
            if (RateLimitStatus != ColumnRateLimitStatus.Stopped)
            {
                _rateLimitCountdownTimer?.Stop();
                return;
            }

            if (DateTimeOffset.Now >= _rateLimitResetTime)
            {
                // リセット時刻経過 → 通常へ復帰し自動更新を再開
                _rateLimitCountdownTimer?.Stop();
                RateLimitStatus = ColumnRateLimitStatus.Normal; // ※UpdateTimerのStopedガードより前に解除
                UpdateTimer(true);
                Logger.Log($"[Rate Limit] Refresh resumed: {Url}");
            }
            else
            {
                UpdateRateLimitCountdownText();
            }
        }

        /// <summary>停止中、タイマー表示部にリセットまでの残り時間を表示します。</summary>
        private void UpdateRateLimitCountdownText()
        {
            double sec = (_rateLimitResetTime - DateTimeOffset.Now).TotalSeconds;
            if (sec < 0) sec = 0;
            CountdownText = $"({TimeSpan.FromSeconds(sec):m\\:ss})";
        }

        #endregion

        #region Rate Limit (429) Handling

        // --- 調整用パラメータ ---
        private const int Consecutive429Threshold = 3;    // resetヘッダー無し単発を様子見する連続しきい値
        private const int Consecutive429WindowSeconds = 60;   // 連続とみなす時間窓
        private const int RateLimitResumeBufferSeconds = 5;    // reset時刻に上乗せする復帰バッファ（窓リセット直後の再429回避）
        private const int RateLimitFallbackCooldownSeconds = 900; // resetが取れない場合の固定クールダウン（X標準窓=15分）

        private readonly List<DateTime> _recent429Times = new();
        private DispatcherTimer? _rateLimitResumeTimer;

        /// <summary>
        /// 429検知時に View から呼ばれます（UIスレッド）。
        /// resetヘッダー付き＝バケット枯渇の確実な信号は即休止し reset時刻に自動復帰。
        /// ヘッダー無し（サブリソース等の単発の可能性）は、短時間に連続したときだけ休止します。
        /// </summary>
        public void NotifyRateLimited(DateTimeOffset? resetTime, bool hasRateLimitHeader)
        {
            // 手動/レート制限問わず既に休止中なら多重処理しない
            if (IsSuspended) return;
            // API制限の更新停止中は、さらに429を検知しても何もしない（既に止まっているため）。復帰はリセット時刻ベースで待つ。
            if (RateLimitStatus == ColumnRateLimitStatus.Stopped) return;

            var now = DateTime.Now;
            _recent429Times.Add(now);
            _recent429Times.RemoveAll(t => (now - t).TotalSeconds > Consecutive429WindowSeconds);

            bool confident = hasRateLimitHeader && resetTime.HasValue;
            if (!confident && _recent429Times.Count < Consecutive429Threshold) return;

            SuspendForRateLimit(resetTime);
        }

        private void SuspendForRateLimit(DateTimeOffset? resetTime)
        {
            _recent429Times.Clear();

            // 休止へ。※ IsAutoRefreshEnabled は変更しない（復帰後に自動更新を継続させるため）
            IsRateLimited = true;
            IsSuspended = true;
            Timer?.Stop();
            RemainingSeconds = 0;

            // 復帰予定時刻を先に確定（休止画面の表示にも使う）
            DateTimeOffset resumeAt = resetTime.HasValue
                ? resetTime.Value.AddSeconds(RateLimitResumeBufferSeconds)
                : DateTimeOffset.Now.AddSeconds(RateLimitFallbackCooldownSeconds);

            // 専用休止画面の表示は View に委譲（復帰予定時刻を渡す）
            RateLimitSuspendRequested?.Invoke(this, resumeAt);

            double waitSec = Math.Max((resumeAt - DateTimeOffset.Now).TotalSeconds, 5);

            _rateLimitResumeTimer ??= new DispatcherTimer();
            _rateLimitResumeTimer.Stop();
            _rateLimitResumeTimer.Tick -= OnRateLimitResumeTick; // 二重購読防止
            _rateLimitResumeTimer.Tick += OnRateLimitResumeTick;
            _rateLimitResumeTimer.Interval = TimeSpan.FromSeconds(waitSec);
            _rateLimitResumeTimer.Start();

            Logger.Log($"[Rate Limit] Suspended until {resumeAt.LocalDateTime:HH:mm:ss} (wait {waitSec:F0}s): {Url}");
        }

        private void OnRateLimitResumeTick(object? sender, EventArgs e)
        {
            _rateLimitResumeTimer?.Stop();
            if (!IsRateLimited) return; // 手動操作で状態が変わっていたら何もしない

            IsRateLimited = false;
            IsSuspended = false;
            SuspendRequested?.Invoke(this, false); // 元URLへ復帰（手動復帰と同じ経路）
            UpdateTimer(true);                      // 自動更新を再開

            Logger.Log($"[Rate Limit] Auto-resumed: {Url}");
        }

        #endregion

        #region Private Methods & Win32 Interop

        /// <summary>
        /// カウントダウン表示用テキストを更新します。
        /// RemainingSeconds の変更に伴い、UI上の残り時間表示をリフレッシュするために呼び出されます。
        /// </summary>
        private void UpdateCountdownText()
        {
            // 更新停止中はリセットまでの残り時間を表示
            if (RateLimitStatus == ColumnRateLimitStatus.Stopped)
            {
                UpdateRateLimitCountdownText();
                return;
            }

            if (!IsAutoRefreshEnabled || RemainingSeconds <= 0)
                CountdownText = "";
            else
                CountdownText = $"({TimeSpan.FromSeconds(RemainingSeconds):m\\:ss})";
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

        #endregion
    }
}