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

        #region UI表示設定
        /// <summary>
        /// ホーム以外のカラムで左側メニューを非表示にするか。
        /// </summary>
        public bool HideMenuInNonHome { get; set; } = false;

        /// <summary>
        /// ホームカラムで左側メニューを非表示にするか（新規追加）。
        /// </summary>
        public bool HideMenuInHome { get; set; } = false;
        public bool HideListHeader { get; set; } = false;
        public bool HideRightSidebar { get; set; } = false;

        // レイアウト設定
        public double ColumnWidth { get; set; } = 380;
        public bool UseUniformGrid { get; set; } = false;
        #endregion

        #region 動作設定
        /// <summary>
        /// 自動更新時にページ全体のリロードを行わず、JSで新着取得ボタンを押す（ソフト更新）かどうか。
        /// true: ソフト更新 (v1.6方式), false: 完全リロード (v1.5以前の方式)
        /// </summary>
        public bool UseSoftRefresh { get; set; } = true;
        public double AppVolume { get; set; } = 0.5;

        // ウィンドウのスナップ（吸着）機能を有効にするかどうか
        public bool EnableWindowSnap { get; set; } = true;
        #endregion

        // カスタムCSS
        public string CustomCss { get; set; } = "";

        // --- フォント設定 ---
        /// <summary>
        /// アプリ全体で使用するフォントファミリ名。空の場合はサイトのデフォルト。
        /// </summary>
        public string AppFontFamily { get; set; } = "Meiryo";

        /// <summary>
        /// アプリ全体のフォントサイズ(px)。0または負の場合はサイトのデフォルト(通常15px)。
        /// </summary>
        public int AppFontSize { get; set; } = 15;
    }
}