using Microsoft.Extensions.DependencyInjection;
using ModernWpf.Controls.Primitives;
using System.Windows;
using XColumn.Models;
using XColumn.Services;
using XColumn.ViewModels;

namespace XColumn.Views
{
    /// <summary>
    /// 設定ウィンドウ。実体のロジックは SettingsViewModel が持ち、
    /// 本クラスは VM の生成・イベント購読（閉じる／再起動／リンク遷移）のみを担う薄いグルーです。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        #region Properties

        /// <summary>
        /// 設定ウィンドウのビューモデル。
        /// </summary>
        public SettingsViewModel ViewModel { get; }

        /// <summary>
        /// 編集結果の設定。OKで閉じられた場合に確定値が入ります（キャンセル時は呼び出し側で破棄）。
        /// 呼び出し側互換のため ViewModel.Result を委譲します。
        /// </summary>
        public AppSettings Settings => ViewModel.Result;

        #endregion

        #region Constructor

        /// <summary>
        /// ウィンドウを初期化し、現在の設定値を反映したビューモデルを生成します。
        /// </summary>
        /// <param name="currentSettings">現在のプロファイル設定。</param>
        /// <param name="appConfig">アプリ全体構成。</param>
        /// <param name="configPath">app_config.json のパス。</param>
        public SettingsWindow(AppSettings currentSettings, AppConfig appConfig, string configPath)
        {
            InitializeComponent();

            // ModernWpfのモダンウィンドウスタイルを適用
            WindowHelper.SetUseModernWindowStyle(this, true);

            // IDialogService は DI から取得し、VM を生成
            var dialogService = App.Current.Services.GetRequiredService<IDialogService>();
            ViewModel = new SettingsViewModel(currentSettings, appConfig, configPath, dialogService);

            // VM からの要求を購読
            ViewModel.CloseRequested += OnCloseRequested;
            ViewModel.RestartRequested += OnRestartRequested;

            DataContext = ViewModel;

            this.Closed += OnClosed;
        }

        #endregion

        #region ViewModel Event Handlers

        /// <summary>
        /// VM からの「閉じる」要求に応じて DialogResult を設定して閉じます。
        /// </summary>
        /// <param name="result">true=OK / false=キャンセル。</param>
        private void OnCloseRequested(bool result)
        {
            DialogResult = result;
            Close();
        }

        /// <summary>
        /// VM からの「再起動」要求に応じてアプリを再起動します。
        /// </summary>
        private void OnRestartRequested()
        {
            // MainWindow の共通再起動処理（--wait-pid付き・プロファイル維持）を使う
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.RestartApplication();
                return;
            }

            // フォールバック（通常は到達しない）
            try
            {
                var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (module != null)
                {
                    System.Diagnostics.Process.Start(module.FileName);
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch { }
        }

        /// <summary>
        /// ウィンドウが閉じられた際に購読を解除します。
        /// </summary>
        private void OnClosed(object? sender, EventArgs e)
        {
            ViewModel.CloseRequested -= OnCloseRequested;
            ViewModel.RestartRequested -= OnRestartRequested;
            this.Closed -= OnClosed;
        }

        #endregion

        #region View-only Handlers

        /// <summary>
        /// 設定内のリンクをクリックした時の処理。
        /// 親ウィンドウ(MainWindow)のフォーカスモードで該当URLを開きます。
        /// （Owner と e.Uri を扱う純View操作のためコードビハインドに残置）
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.OpenFocusMode(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 数値のみ入力を許可するための入力バリデーション。
        /// </summary>
        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        #endregion
    }
}