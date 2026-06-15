using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using XColumn.Models;
using XColumn.Properties;
using XColumn.Services;

namespace XColumn.ViewModels
{
    /// <summary>
    /// 拡張機能管理ダイアログのビューモデル。
    /// 拡張機能コレクションの操作（追加・削除・Chrome取込）を担い、
    /// 確認・エラー表示は IDialogService 経由で行います。
    /// オプションページ表示やダイアログのクローズなどウィンドウ固有の操作は、
    /// イベント(OpenOptionsRequested / CloseRequested)で View へ要求します。
    /// </summary>
    public partial class ExtensionViewModel : ViewModelBase
    {
        /// <summary>ダイアログ表示を担うサービス。</summary>
        private readonly IDialogService _dialogService;

        /// <summary>画面に表示・編集する拡張機能のコレクション（UIにバインドされます）。</summary>
        public ObservableCollection<ExtensionItem> Extensions { get; }

        /// <summary>「オプションページを開く」要求（View が親ウィンドウ経由で処理します）。</summary>
        public event Action<ExtensionItem>? OpenOptionsRequested;

        /// <summary>「ダイアログを閉じる」要求（引数は DialogResult）。</summary>
        public event Action<bool>? CloseRequested;

        /// <summary>
        /// 初期表示する拡張機能リストと IDialogService を受け取って初期化します。
        /// </summary>
        /// <param name="initial">初期表示する拡張機能のリスト。</param>
        /// <param name="dialogService">ダイアログ表示サービス。</param>
        public ExtensionViewModel(IEnumerable<ExtensionItem> initial, IDialogService dialogService)
        {
            _dialogService = dialogService;
            Extensions = new ObservableCollection<ExtensionItem>(initial);
        }

        /// <summary>
        /// 「フォルダから追加」。フォルダを選択させ、パス重複がなければリストへ追加します。
        /// </summary>
        [RelayCommand]
        private void AddExtension()
        {
            string? path = _dialogService.ShowFolderPicker(Resources.Extension_SelectFolderPrompt);
            if (string.IsNullOrEmpty(path)) return;

            // パス重複チェック
            if (Extensions.Any(ext => ext.Path == path))
            {
                _dialogService.ShowMessage(Resources.Extension_AlreadyAdded, Resources.Confirmation);
                return;
            }

            Extensions.Add(new ExtensionItem
            {
                Name = Path.GetFileName(path),
                Path = path,
                IsEnabled = true
            });
        }

        /// <summary>
        /// 「削除」。確認のうえリストから除去し、実体フォルダの削除を試みます（失敗時は警告表示）。
        /// </summary>
        [RelayCommand]
        private void RemoveExtension(ExtensionItem item)
        {
            if (item == null) return;

            if (_dialogService.ShowMessage(
                    string.Format(Resources.Extension_ConfirmRemove, item.Name),
                    Resources.Confirmation,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            // 1. リストからの削除（UIへの即時反映）
            Extensions.Remove(item);

            // 2. 物理フォルダの削除を試みる
            if (!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path))
            {
                try
                {
                    // サブフォルダ・ファイルも含めて再帰的に削除
                    Directory.Delete(item.Path, true);
                }
                catch (Exception ex)
                {
                    // WebView2 がファイルをロックしている場合などへの安全対策
                    _dialogService.ShowMessage(
                        string.Format(Resources.Msg_Err_ExtensionFolderDeleteFailed, item.Path, ex.Message),
                        Resources.Extension_FolderDeletePartialFailTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// 「設定」。オプションページ表示要求を View へ送ります（親 MainWindow 経由で開きます）。
        /// </summary>
        [RelayCommand]
        private void OpenOptions(ExtensionItem item)
        {
            if (item != null) OpenOptionsRequested?.Invoke(item);
        }

        /// <summary>
        /// 「フォルダを開く」。拡張機能の格納フォルダをエクスプローラーで開きます。
        /// </summary>
        [RelayCommand]
        private void OpenFolder(ExtensionItem item)
        {
            if (item == null) return;

            if (Directory.Exists(item.Path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = item.Path, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage(
                        string.Format(Resources.Msg_Err_OpenFolderFailed, ex.Message),
                        Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                _dialogService.ShowMessage(Resources.Msg_Err_FolderNotFound,
                    Resources.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 「Chromeからインポート」。取込ダイアログを表示し、重複（パスまたは名前）を除外してマージします。
        /// </summary>
        [RelayCommand]
        private void ImportFromChrome()
        {
            var imported = _dialogService.ShowChromeImport();
            if (imported == null) return;

            int count = 0;
            foreach (var newItem in imported)
            {
                // 重複チェック（パスまたは名前で判断）
                bool exists = Extensions.Any(e => e.Path == newItem.Path || e.Name == newItem.Name);
                if (!exists)
                {
                    Extensions.Add(newItem);
                    count++;
                }
            }

            if (count > 0)
            {
                _dialogService.ShowMessage(
                    string.Format(Resources.Msg_ExtensionsImported, count),
                    Resources.Title_Completed);
            }
            else
            {
                _dialogService.ShowMessage(Resources.Extension_NoNewExtensions, Resources.Information);
            }
        }

        /// <summary>「OK」。変更を確定してダイアログを閉じます。</summary>
        [RelayCommand]
        private void Ok() => CloseRequested?.Invoke(true);

        /// <summary>「キャンセル」。変更を破棄してダイアログを閉じます。</summary>
        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(false);
    }
}