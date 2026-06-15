using System.Windows;
using XColumn.Models;
using XColumn.Views;

// 曖昧さ回避
using Application = System.Windows.Application;

namespace XColumn.Services
{
    /// <summary>
    /// IDialogService の既定実装。
    /// 既存の InputWindow / MessageWindow / ChromeImportWindow をラップし、
    /// 親ウィンドウは Application.Current.MainWindow を使用します。
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>現在の親ウィンドウ（メインウィンドウ）を取得します。</summary>
        private static Window? OwnerWindow => Application.Current?.MainWindow;

        /// <inheritdoc/>
        public string? ShowInput(string title, string prompt, string defaultText = "")
        {
            var dlg = new InputWindow(title, prompt, defaultText) { Owner = OwnerWindow };
            return dlg.ShowDialog() == true ? dlg.InputText?.Trim() : null;
        }

        /// <inheritdoc/>
        public MessageBoxResult ShowMessage(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            return MessageWindow.Show(OwnerWindow, message, title, buttons, icon);
        }

        /// <inheritdoc/>
        public string? ShowFolderPicker(string description)
        {
            // WinForms のフォルダ選択ダイアログをラップ（WPF/WinForms の型衝突を避けるため完全修飾名で指定）
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true
            };
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        /// <inheritdoc/>
        public List<ExtensionItem>? ShowChromeImport()
        {
            var win = new ChromeImportWindow { Owner = OwnerWindow };
            return win.ShowDialog() == true ? win.ImportedExtensions : null;
        }
    }
}