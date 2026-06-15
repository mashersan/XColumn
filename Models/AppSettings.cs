using System.Windows;

namespace XColumn.Models
{
    /// <summary>
    /// プロファイルごとに保存される詳細な設定データ。
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

        /// <summary>ウィンドウの上端位置（px）。</summary>
        public double WindowTop { get; set; } = 100;

        /// <summary>ウィンドウの左端位置（px）。</summary>
        public double WindowLeft { get; set; } = 100;

        /// <summary>ウィンドウの高さ（px）。</summary>
        public double WindowHeight { get; set; } = 800;

        /// <summary>ウィンドウの幅（px）。</summary>
        public double WindowWidth { get; set; } = 1200;

        /// <summary>ウィンドウの状態（通常・最大化・最小化）。</summary>
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
        /// ホームカラムで左側メニューを非表示にするか。
        /// </summary>
        public bool HideMenuInHome { get; set; } = false;

        /// <summary>リストページでリストヘッダーを非表示にするか。</summary>
        public bool HideListHeader { get; set; } = false;

        /// <summary>右サイドバーを非表示にするか。</summary>
        public bool HideRightSidebar { get; set; } = false;

        /// <summary>カラム1つあたりの幅（px）。</summary>
        public double ColumnWidth { get; set; } = 380;

        /// <summary>カラム配置に UniformGrid（均等配置）を使用するか。</summary>
        public bool UseUniformGrid { get; set; } = false;

        #endregion

        #region 動作設定

        /// <summary>
        /// ソフト更新（JSによる更新）を使用するかどうか。
        /// </summary>
        public bool UseSoftRefresh { get; set; } = true;

        /// <summary>アプリ全体の音量（0.0～1.0）。</summary>
        public double AppVolume { get; set; } = 0.5;

        /// <summary>ウィンドウのスナップ（吸着）機能を有効にするかどうか。</summary>
        public bool EnableWindowSnap { get; set; } = true;

        /// <summary>タイムラインの一番上と判定するスクロールの許容誤差（ピクセル）。</summary>
        public int ScrollTopTolerance { get; set; } = 50;

        /// <summary>新規カラムを左側に追加するかどうか（false の場合は右側）。</summary>
        public bool AddColumnToLeft { get; set; } = false;

        /// <summary>
        /// メディア（画像・動画）クリック時にフォーカスモードへの遷移を無効にするかどうか。
        /// </summary>
        public bool DisableFocusModeOnMediaClick { get; set; } = false;

        /// <summary>
        /// ポストクリック時にフォーカスモードへ遷移しないかどうか。
        /// </summary>
        public bool DisableFocusModeOnTweetClick { get; set; } = false;

        /// <summary>
        /// サーバー接続状態チェックの間隔（分）。
        /// </summary>
        public int ServerCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 未読位置を保持するかどうか（スクロール中はマウスが外れていても自動更新しない）。
        /// </summary>
        public bool KeepUnreadPosition { get; set; } = false;

        /// <summary>
        /// 非アクティブ時の自動シャットダウンを有効にするかどうか。
        /// </summary>
        public bool AutoShutdownEnabled { get; set; } = false;

        /// <summary>
        /// 自動シャットダウンまでの待機時間（分）。
        /// </summary>
        public int AutoShutdownMinutes { get; set; } = 30;

        #endregion

        /// <summary>ユーザー定義のカスタムCSS。</summary>
        public string CustomCss { get; set; } = "";

        #region フォント設定

        /// <summary>
        /// アプリ全体で使用するフォントファミリ名。空の場合はサイトのデフォルト。
        /// </summary>
        public string AppFontFamily { get; set; } = "Meiryo";

        /// <summary>
        /// アプリ全体のフォントサイズ(px)。0または負の場合はサイトのデフォルト(通常15px)。
        /// </summary>
        public int AppFontSize { get; set; } = 15;

        #endregion

        /// <summary>
        /// アプリのテーマ設定（"Light" / "Dark" / "System" のいずれか）。
        /// </summary>
        public string AppTheme { get; set; } = "System";

        /// <summary>NGワード（含むポストを非表示にする）のリスト。</summary>
        public List<string> NgWords { get; set; } = new List<string>();

        /// <summary>カラム上部にURLを表示するかどうか。</summary>
        public bool ShowColumnUrl { get; set; } = true;

        /// <summary>リスト自動遷移時の待機時間（ミリ秒）。</summary>
        public int ListAutoNavDelay { get; set; } = 2000;

        /// <summary>起動時のアップデートチェックを行うかどうか。</summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>
        /// 動画の自動再生を強制的に無効化するかどうか。
        /// trueの場合、--autoplay-policy=user-gesture-required を適用します。
        /// </summary>
        public bool ForceDisableAutoPlay { get; set; } = false;

        /// <summary>
        /// ポストの投稿時間を相対時間ではなく絶対時間で表示するかどうか。
        /// </summary>
        public bool ShowAbsoluteTime { get; set; } = false;

        /// <summary>
        /// 試験的な機能（複数プロファイルの混在など）を有効にするかどうか。
        /// </summary>
        public bool UseExperimentalFeatures { get; set; } = false;

        /// <summary>
        /// カラムを上下2段に分割して表示するかどうか。
        /// </summary>
        public bool UseTwoTierLayout { get; set; } = false;

        /// <summary>
        /// 動画クリック時に自動的にPiP(最前面ウィンドウ)で開くかどうか。
        /// </summary>
        public bool AutoPipForVideo { get; set; } = false;

        #region PiPウィンドウ位置・サイズ設定

        /// <summary>PiPウィンドウの上端位置（px）。</summary>
        public double PipWindowTop { get; set; } = 0.0;

        /// <summary>PiPウィンドウの左端位置（px）。</summary>
        public double PipWindowLeft { get; set; } = 0.0;

        /// <summary>PiPウィンドウの高さ（px）。</summary>
        public double PipWindowHeight { get; set; } = 600;

        /// <summary>PiPウィンドウの幅（px）。</summary>
        public double PipWindowWidth { get; set; } = 800;

        /// <summary>
        /// PiPウィンドウを常に最前面に表示するかどうか（デフォルト: true）。
        /// </summary>
        public bool PipAlwaysOnTop { get; set; } = true;

        #endregion
    }
}