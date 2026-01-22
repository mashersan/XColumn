using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using XColumn.Models;

// 曖昧さ回避
using MessageBox = System.Windows.MessageBox;

namespace XColumn
{
    /// <summary>
    /// アプリケーションのエントリポイント。
    /// </summary>
    public partial class App : System.Windows.Application
    {

        private string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");

        /// <summary>
        /// スタートアップ処理。
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 言語設定の適用（UI表示前に行う）
            ApplyLanguageSettings();

            // 起動前に、保留されていたプロファイル複製処理を実行
            ProcessPendingProfileClone();

            base.OnStartup(e);

            // プロファイル指定用変数
            string? targetProfile = null;
            // DevToolsおよびGPU設定用変数
            bool enableDevTools = false;
            bool disableGpu = false;

            // コマンドライン引数の解析
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i] == "--profile" && i + 1 < e.Args.Length)
                {
                    targetProfile = e.Args[i + 1];
                    break;
                }
                // DevTools有効化オプション
                else if (e.Args[i] == "--enable-devtools")
                {
                    enableDevTools = true;
                }
                // GPU無効化オプション
                else if (e.Args[i] == "--disable-gpu")
                {
                    disableGpu = true;
                }
            }

            // メインウィンドウを起動
            var mainWindow = new MainWindow(targetProfile, enableDevTools, disableGpu);
            mainWindow.Show();
        }

        /// <summary>
        /// app_config.json を読み込み、設定された言語をスレッドに適用します。
        /// </summary>
        private void ApplyLanguageSettings()
        {
            string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");
            string appConfigPath = Path.Combine(userDataFolder, "app_config.json");
            string language = "ja-JP"; // デフォルト

            if (File.Exists(appConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(appConfigPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null && !string.IsNullOrEmpty(config.Language))
                    {
                        language = config.Language;
                    }
                }
                catch { }
            }

            try
            {
                var culture = new CultureInfo(language);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch { }
        }

        /// <summary>
        /// 再起動前に予約されたプロファイル複製処理（フォルダコピー）を実行します。
        /// </summary>
        private void ProcessPendingProfileClone()
        {
            string pendingFile = Path.Combine(_userDataFolder, "pending_clone.json");
            if (!File.Exists(pendingFile)) return;

            try
            {
                string json = File.ReadAllText(pendingFile);
                var info = JsonSerializer.Deserialize<CloneInfo>(json);

                if (info != null && !string.IsNullOrEmpty(info.SourcePath) && !string.IsNullOrEmpty(info.DestPath))
                {
                    if (Directory.Exists(info.SourcePath))
                    {
                        // WebView2が起動していない今なら確実にコピーできる
                        CopyDirectory(info.SourcePath, info.DestPath);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format(XColumn.Properties.Resources.Msg_Err_ProfileCloneFailed, ex.Message);
                MessageWindow.Show(msg, XColumn.Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 処理が終わったら指示書を削除
                try { File.Delete(pendingFile); } catch { }
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                // Lockfile等は不要
                if (file.Name.Equals("lockfile", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    file.CopyTo(Path.Combine(destinationDir, file.Name), true);
                }
                catch { /* アクセス拒否等は無視 */ }
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                // Cacheフォルダ等は容量削減のため除外しても良いが、完全複製の観点で含める
                // ただし "EBWebView" フォルダ配下のロックされやすい一時ファイルはエラーになりがちなので注意
                CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
            }
        }

        // コピー指示書用データクラス
        private class CloneInfo
        {
            public string SourcePath { get; set; } = "";
            public string DestPath { get; set; } = "";
        }
    }
}