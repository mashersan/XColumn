using System.Windows;

namespace XColumn
{
    /// <summary>
    /// アプリケーションのエントリポイント。
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// スタートアップ処理。
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string? targetProfile = null;

            // コマンドライン引数の解析
            // 例: XColumn.exe --profile "WorkAccount"
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i] == "--profile" && i + 1 < e.Args.Length)
                {
                    targetProfile = e.Args[i + 1];
                    break;
                }
            }

            // メインウィンドウを起動（プロファイル名を渡す）
            var mainWindow = new MainWindow(targetProfile);
            mainWindow.Show();
        }
    }
}