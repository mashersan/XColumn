using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using XColumn.Models;
using XColumn.Services;
using XColumn.ViewModels;

namespace XColumn.Views
{
    /// <summary>
    /// 拡張機能を管理（追加・削除・インポート）するためのウィンドウ。
    /// ロジックは ExtensionViewModel に委譲し、本クラスはウィンドウ固有の操作
    /// （オプションページ表示・ダイアログ結果の確定）のみを担当します。
    /// </summary>
    public partial class ExtensionWindow : Window
    {
        /// <summary>このウィンドウのビューモデル。</summary>
        public ExtensionViewModel ViewModel { get; }

        /// <summary>
        /// 編集対象の拡張機能コレクション（呼び出し側が結果として参照します）。
        /// </summary>
        public ObservableCollection<ExtensionItem> Extensions => ViewModel.Extensions;

        /// <summary>
        /// 現在の拡張機能リストを受け取ってウィンドウを初期化します。
        /// </summary>
        /// <param name="currentExtensions">初期表示する拡張機能のリスト。</param>
        public ExtensionWindow(List<ExtensionItem> currentExtensions)
        {
            InitializeComponent();

            ViewModel = new ExtensionViewModel(currentExtensions, App.Current.Services.GetRequiredService<IDialogService>());
            DataContext = ViewModel;

            // VM からのウィンドウ固有要求を購読
            ViewModel.OpenOptionsRequested += OnOpenOptionsRequested;
            ViewModel.CloseRequested += OnCloseRequested;
        }

        /// <summary>
        /// VM からの「オプションページを開く」要求。親(MainWindow)経由でページを開き、管理ウィンドウを閉じます。
        /// </summary>
        private void OnOpenOptionsRequested(ExtensionItem item)
        {
            if (Owner is MainWindow mw)
            {
                mw.OpenExtensionOptions(item);
                // 設定画面がユーザーに見えるよう、管理ウィンドウはいったん閉じる
                Close();
            }
        }

        /// <summary>
        /// VM からの「ダイアログを閉じる」要求。DialogResult を設定して閉じます。
        /// </summary>
        private void OnCloseRequested(bool result)
        {
            DialogResult = result;
            Close();
        }

        /// <summary>
        /// ウィンドウが閉じられる際に VM のイベント購読を解除します。
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            ViewModel.OpenOptionsRequested -= OnOpenOptionsRequested;
            ViewModel.CloseRequested -= OnCloseRequested;
            base.OnClosed(e);
        }
    }
}