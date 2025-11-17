using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace XColumn.Models
{
    /// <summary>
    /// 1つのカラム（WebView）の状態と設定を管理するクラス。
    /// </summary>
    public class ColumnData : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        private string _url = "";
        public string Url
        {
            get => _url;
            set { SetField(ref _url, value); }
        }

        private int _refreshIntervalSeconds = 300;
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                // 設定変更時は必ずリセットして反映
                if (SetField(ref _refreshIntervalSeconds, value)) UpdateTimer(true);
            }
        }

        private bool _isAutoRefreshEnabled = false;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                // 有効/無効切り替え時もリセットして反映
                if (SetField(ref _isAutoRefreshEnabled, value)) UpdateTimer(true);
            }
        }

        private int _remainingSeconds;
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

        public void InitializeTimer()
        {
            Timer = new DispatcherTimer();
            Timer.Tick += (sender, e) => ReloadWebView();
            UpdateTimer(true);
        }

        /// <summary>
        /// WebViewをリロードし、カウントダウンをリセットします。
        /// </summary>
        public void ReloadWebView()
        {
            if (AssociatedWebView?.CoreWebView2 != null)
            {
                try
                {
                    AssociatedWebView.CoreWebView2.Reload();
                    Debug.WriteLine($"[ColumnData] Reloaded: {Url}");
                }
                catch (Exception ex) { Debug.WriteLine($"Reload failed: {ex.Message}"); }
            }

            // リロード後は必ずカウントダウンを初期値にリセットし、
            // タイマー間隔も正規の設定値（RefreshIntervalSeconds）に戻して再スタートする
            UpdateTimer(true);
        }

        /// <summary>
        /// 現在の設定に基づいてタイマーの状態（開始/停止/間隔）を更新します。
        /// </summary>
        /// <param name="reset">trueならカウントダウンを初期値にリセット。falseなら現在の残り時間で再開。</param>
        public void UpdateTimer(bool reset = true)
        {
            // 既存タイマーがあれば停止
            Timer?.Stop();

            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                // リセット要求がある場合、または残り時間が不正(0以下)な場合はリセット
                if (reset || RemainingSeconds <= 0)
                {
                    ResetCountdown();
                }
                // reset = false の場合は、現在の RemainingSeconds を維持する（続きから再開）

                // タイマーインスタンスが存在すれば開始
                if (Timer != null)
                {
                    // 次に発火するまでの時間を決定
                    // リセット時や通常時は「設定秒数」
                    // 中断からの再開時は「残り秒数」をセットする
                    int nextInterval = reset ? RefreshIntervalSeconds : RemainingSeconds;

                    // 念のため最小値を1秒に制限
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
        /// カウントダウンを初期値に戻します。
        /// </summary>
        public void ResetCountdown()
        {
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
                RemainingSeconds = RefreshIntervalSeconds;
            else
                RemainingSeconds = 0;
        }

        private void UpdateCountdownText()
        {
            if (!IsAutoRefreshEnabled || RemainingSeconds <= 0)
                CountdownText = "";
            else
                CountdownText = $"({TimeSpan.FromSeconds(RemainingSeconds):m\\:ss})";
        }

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}