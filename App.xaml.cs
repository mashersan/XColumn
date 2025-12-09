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
            // 言語設定の適用（UI表示前に行う）
            ApplyLanguageSettings();

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

            // メインウィンドウを起動
            var mainWindow = new MainWindow(targetProfile);
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
    }
}