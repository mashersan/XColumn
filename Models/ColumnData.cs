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
        /// 自動更新の間隔（秒）。変更時にタイマーを再設定します。
        /// </summary>
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetField(ref _refreshIntervalSeconds, value)) UpdateTimer();
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
                if (SetField(ref _isAutoRefreshEnabled, value)) UpdateTimer();
            }
        }

        private int _remainingSeconds;
        /// <summary>
        /// 次の更新までの残り秒数。UIバインディング用。
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
            Timer.Tick += (sender, e) => ReloadWebView();
            UpdateTimer();
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
            ResetCountdown();
        }

        /// <summary>
        /// 現在の設定に基づいてタイマーの状態（開始/停止/間隔）を更新します。
        /// </summary>
        public void UpdateTimer()
        {
            // 既存タイマーがあれば停止
            Timer?.Stop();

            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                // タイマー停止中でもカウントダウン表示はセットする
                ResetCountdown();

                // タイマーインスタンスが存在すれば開始
                if (Timer != null)
                {
                    Timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
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

        /// <summary>
        /// タイマーを破棄します。
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