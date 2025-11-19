using System.Collections.Generic;
using System.Windows;

namespace XColumn.Models
{
    /// <summary>
    /// プロファイルごとに保存される詳細な設定データ。
    /// ウィンドウ位置、カラム構成、拡張機能リストなどを保持します。
    /// このクラスのインスタンスはJSONシリアライズされ、DPAPIで暗号化して保存されます。
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 表示するカラム（WebView）のリスト。
        /// </summary>
        public List<ColumnData> Columns { get; set; } = new List<ColumnData>();

        /// <summary>
        /// インストール（登録）されたChrome拡張機能のリスト。
        /// </summary>
        public List<ExtensionItem> Extensions { get; set; } = new List<ExtensionItem>();

        #region ウィンドウ位置・サイズ設定
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowHeight { get; set; } = 800;
        public double WindowWidth { get; set; } = 1200;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        #endregion

        #region フォーカスモード設定
        /// <summary>
        /// 前回終了時にフォーカスモード（単一ビュー）だったかどうか。
        /// </summary>
        public bool IsFocusMode { get; set; } = false;

        /// <summary>
        /// フォーカスモードで表示していたURL。
        /// </summary>
        public string? FocusUrl { get; set; } = null;
        #endregion

        /// <summary>
        /// アプリがアクティブ（操作中）の時に、カラムの自動更新タイマーを一時停止するかどうか。
        /// </summary>
        public bool StopTimerWhenActive { get; set; } = true;

        /// <summary>
        /// ユーザーがスキップした最新バージョンのタグ名（例: "1.2.0"）。
        /// アップデート通知の制御に使用します。
        /// </summary>
        public string SkippedVersion { get; set; } = "0.0.0";
    }
}