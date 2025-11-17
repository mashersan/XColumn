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
    /// 1つのカラム（WebView）のデータと状態を管理するクラス。
    /// URL、更新設定、タイマー制御、UI通知（INotifyPropertyChanged）を担当します。
    /// </summary>
    public class ColumnData : INotifyPropertyChanged
    {
        /// <summary>
        /// カラムを一意に識別するためのID。
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        private string _url = "";
        /// <summary>
        /// 現在表示しているページのURL。
        /// </summary>
        public string Url
        {
            get => _url;
            set { SetField(ref _url, value); }
        }

        private int _refreshIntervalSeconds = 300;
        /// <summary>
        /// 自動更新の間隔（秒単位）。
        /// 変更されるとタイマーが再設定されます。
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
        /// 次の更新までの残り秒数（カウントダウン用）。
        /// 設定ファイルには保存しません。
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

        /// <summary>
        /// このカラム専用の更新タイマー。
        /// </summary>
        [JsonIgnore]
        public DispatcherTimer? Timer { get; private set; }

        /// <summary>
        /// このデータに関連付けられたWebView2コントロールの実体。
        /// </summary>
        [JsonIgnore]
        public Microsoft.Web.WebView2.Wpf.WebView2? AssociatedWebView { get; set; }

        /// <summary>
        /// タイマーを初期化し、現在の設定に基づいて開始または停止します。
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
        /// 設定（有効/無効、間隔）に基づいてタイマーの状態を更新します。
        /// </summary>
        public void UpdateTimer()
        {
            // 既存のタイマーがあれば一旦停止
            Timer?.Stop();

            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
            {
                // カウントダウン初期値をセット（タイマー停止中でも表示は更新されるように）
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
        /// カウントダウンの秒数を初期値（設定間隔）にリセットします。
        /// </summary>
        public void ResetCountdown()
        {
            if (IsAutoRefreshEnabled && RefreshIntervalSeconds > 0)
                RemainingSeconds = RefreshIntervalSeconds;
            else
                RemainingSeconds = 0;
        }

        /// <summary>
        /// 残り秒数から表示用のテキストを更新します。
        /// </summary>
        private void UpdateCountdownText()
        {
            if (!IsAutoRefreshEnabled || RemainingSeconds <= 0)
                CountdownText = "";
            else
                CountdownText = $"({TimeSpan.FromSeconds(RemainingSeconds):m\\:ss})";
        }

        /// <summary>
        /// カラム削除時などにタイマーを停止し、リソースを解放します。
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

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion
    }
}