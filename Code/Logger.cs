using System.Diagnostics;

namespace XColumn
{
    /// <summary>
    /// アプリケーション全体のログ出力を管理するクラス。
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// デバッグログを出力するかどうかのフラグ。
        /// リリース時や不要な時は false にします。
        /// </summary>
        public static bool EnableDebugLog { get; set; } = false;

        /// <summary>
        /// フラグがONの場合のみ、デバッグコンソールにメッセージを出力します。
        /// </summary>
        /// <param name="message">出力するメッセージ</param>
        public static void Log(string message)
        {
            if (EnableDebugLog)
            {
                // タイムスタンプとプレフィックスを付けて出力
                // 例: [10:23:45.123] [XColumn] テーマが変更されました
                Debug.WriteLine($"【Debug】[{DateTime.Now:HH:mm:ss.fff}] [XColumn] {message}");
            }
        }
    }
}