using System.Diagnostics;

namespace XColumn.Helpers
{
    /// <summary>
    /// アプリケーション全体のデバッグログ出力を管理する静的クラス。
    /// 出力先は Debug 出力（Visual Studio の出力ウィンドウ等）で、リリースビルドでは
    /// Debug.WriteLine 自体がコンパイル時に除外されます。
    /// </summary>
    public static class Logger
    {
        // ===== Properties =====

        /// <summary>
        /// デバッグログを出力するかどうかのフラグ。
        /// リリース時や不要な時は false にします（既定値: false）。
        /// </summary>
        public static bool EnableDebugLog { get; set; } = false;

        // ===== Public Methods =====

        /// <summary>
        /// フラグ(<see cref="EnableDebugLog"/>)が ON の場合のみ、
        /// タイムスタンプとプレフィックスを付けてデバッグ出力にメッセージを書き出します。
        /// </summary>
        /// <param name="message">出力するメッセージ。</param>
        public static void Log(string message)
        {
            if (!EnableDebugLog) return;

            // 例: 【Debug】[10:23:45.123] [XColumn] テーマが変更されました
            Debug.WriteLine($"【Debug】[{DateTime.Now:HH:mm:ss.fff}] [XColumn] {message}");
        }
    }
}