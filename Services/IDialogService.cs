using System.Windows;
using XColumn.Models;

namespace XColumn.Services
{
    /// <summary>
    /// ダイアログ表示を抽象化するサービス。
    /// ViewModel から Window 型に依存せずにダイアログを利用できるようにします。
    /// 親ウィンドウ(Owner)の解決は実装側で行います。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>テキスト入力ダイアログを表示します。</summary>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <param name="prompt">説明文。</param>
        /// <param name="defaultText">初期値。</param>
        /// <returns>入力された文字列(Trim済)。キャンセル時は null。</returns>
        string? ShowInput(string title, string prompt, string defaultText = "");

        /// <summary>メッセージダイアログを表示します。</summary>
        /// <returns>押されたボタンに対応する結果。</returns>
        MessageBoxResult ShowMessage(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None);

        /// <summary>フォルダ選択ダイアログを表示します。</summary>
        /// <param name="description">ダイアログに表示する説明文。</param>
        /// <returns>選択されたフォルダのパス。キャンセル時は null。</returns>
        string? ShowFolderPicker(string description);

        /// <summary>Chrome拡張機能のインポートダイアログを表示します。</summary>
        /// <returns>インポートが確定した拡張機能のリスト。キャンセル時は null。</returns>
        List<ExtensionItem>? ShowChromeImport();
    }
}