using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using XColumn.Models;
using XColumn.Services;
using XColumn.ViewModels;
using XColumn.Views;
using XColumn.Helpers;

// 曖昧さ回避
using MessageBox = System.Windows.MessageBox;

namespace XColumn
{
    /// <summary>
    /// アプリケーションのエントリポイント。
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// 型付きで App インスタンスにアクセスするためのショートカット。
        /// </summary>
        public new static App Current => (App)System.Windows.Application.Current;

        /// <summary>
        /// アプリ全体のDIコンテナ。
        /// </summary>
        public IServiceProvider Services { get; private set; } = null!;

        private string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XColumn");

        /// <summary>
        /// スタートアップ処理。
        /// </summary>
        /// <param name="e">起動イベント引数。</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 自己更新(リネーム方式)で残った旧実行ファイル XColumn.exe.old を削除
            CleanupOldExecutable();

            // DIコンテナの構築（既存処理より前に行う）
            Services = ConfigureServices();

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

            // 再起動時に終了を待つ旧プロセスのPID
            int waitPid = 0;

            // コマンドライン引数の解析
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i] == "--profile" && i + 1 < e.Args.Length)
                {
                    targetProfile = e.Args[i + 1];
                    i++; // 値をスキップ（breakすると後続の引数が読めないため）
                }
                // 再起動時: 旧プロセスの終了待ちオプション
                else if (e.Args[i] == "--wait-pid" && i + 1 < e.Args.Length)
                {
                    int.TryParse(e.Args[i + 1], out waitPid);
                    i++;
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

            // 再起動時は旧プロセスの終了を待つ。
            // 旧プロセスのWebView2ブラウザプロセスが生きたまま新環境を
            // 異なるオプションで作成すると 0x8007139F で初期化に失敗するため。
            if (waitPid > 0) WaitForPreviousInstanceExit(waitPid);

            // 引数でプロファイルが指定されておらず、設定でデフォルトプロファイルが指定されている場合
            if (string.IsNullOrEmpty(targetProfile))
            {
                string appConfigPath = Path.Combine(_userDataFolder, "app_config.json");
                if (File.Exists(appConfigPath))
                {
                    try
                    {
                        string json = File.ReadAllText(appConfigPath);
                        var config = JsonSerializer.Deserialize<AppConfig>(json);
                        if (config != null && !string.IsNullOrEmpty(config.StartupProfile))
                        {
                            targetProfile = config.StartupProfile;
                        }
                    }
                    catch { }
                }
            }

            // メインウィンドウを起動
            var mainWindow = new MainWindow(targetProfile, enableDevTools, disableGpu);
            mainWindow.Show();
        }

        /// <summary>
        /// サービスとViewModelをDIコンテナに登録します。
        /// 手順が進むごとにここへ追加していきます。
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // --- Services ---
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IProfileService, ProfileService>();
            services.AddSingleton<IDialogService, DialogService>();

            // --- ViewModels ---
            services.AddTransient<MainWindowViewModel>();

            // 手順が進んだら登録予定:
            // services.AddSingleton<IStatusService, StatusService>();
            // services.AddSingleton<IUpdateService, UpdateService>();

            return services.BuildServiceProvider();
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
        /// 実体の処理は IProfileService に委譲し、失敗時のみエラーダイアログを表示します。
        /// </summary>
        private void ProcessPendingProfileClone()
        {
            try
            {
                Services.GetRequiredService<IProfileService>().ProcessPendingClone();
            }
            catch (Exception ex)
            {
                string msg = string.Format(XColumn.Properties.Resources.Msg_Err_ProfileCloneFailed, ex.Message);
                MessageWindow.Show(msg, XColumn.Properties.Resources.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 自己更新(リネーム方式)で退避した旧実行ファイル "XColumn.exe.old" を削除します。
        /// 新プロセス起動直後は旧プロセスがまだ終了しておらず(WebView2後始末で数秒かかる).oldが
        /// ロックされているため、起動をブロックせずバックグラウンドでロック解放を待って削除します。
        /// </summary>
        private void CleanupOldExecutable()
        {
            string? exe = Environment.ProcessPath
                          ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;

            string oldExe = exe + ".old";

            // 起動をブロックしないようバックグラウンドで実行（最大 ~30秒: 500ms × 60回）
            Task.Run(() =>
            {
                for (int i = 0; i < 60; i++)
                {
                    try
                    {
                        if (!File.Exists(oldExe)) return;
                        File.Delete(oldExe);
                        Logger.Log("Self-update: old executable removed.");
                        return;
                    }
                    catch
                    {
                        Thread.Sleep(500); // 旧プロセス終了→ロック解放まで待つ
                    }
                }
                Logger.Log("Self-update: old executable still locked; will retry on next launch.");
            });
        }

        /// <summary>
        /// 再起動元の旧プロセスの終了を待機します（最大10秒）。
        /// 終了後もWebView2ブラウザプロセスの後片付けが残るため、少しだけ猶予を置きます。
        /// </summary>
        /// <param name="pid">待機対象の旧プロセスID。</param>
        private static void WaitForPreviousInstanceExit(int pid)
        {
            try
            {
                using var prev = System.Diagnostics.Process.GetProcessById(pid);
                prev.WaitForExit(10000);
            }
            catch
            {
                // 既に終了している（GetProcessByIdが例外を投げる）場合は待機不要
            }

            // WebView2ブラウザプロセス(msedgewebview2.exe)終了の猶予
            System.Threading.Thread.Sleep(500);
        }
    }
}