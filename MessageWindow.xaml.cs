using System.Windows;
using Application = System.Windows.Application;

namespace XColumn
{
    public partial class MessageWindow : Window
    {
        // コンストラクタにボタンテキスト用引数を追加
        public MessageWindow(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
                             string? yesText = null, string? noText = null, string? cancelText = null, string? okText = null)
        {
            InitializeComponent();
            this.Title = title;
            this.MessageTextBlock.Text = message;

            // テキストのカスタマイズ
            if (!string.IsNullOrEmpty(yesText)) YesButton.Content = yesText;
            if (!string.IsNullOrEmpty(noText)) NoButton.Content = noText;
            if (!string.IsNullOrEmpty(cancelText)) CancelButton.Content = cancelText;
            if (!string.IsNullOrEmpty(okText)) OkButton.Content = okText;

            // ボタンの表示制御
            if (buttons == MessageBoxButton.YesNo)
            {
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
            }
            else if (buttons == MessageBoxButton.YesNoCancel)
            {
                // 3ボタン表示
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
            }
            else if (buttons == MessageBoxButton.OKCancel)
            {
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
            }
            else // OKのみ
            {
                OkButton.Visibility = Visibility.Visible;
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
            }

            // アイコン処理が必要であればここに追加
        }

        // Showメソッドを拡張
        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons, MessageBoxImage icon,
                                            string? yesText = null, string? noText = null, string? cancelText = null, string? okText = null)
        {
            var dlg = new MessageWindow(message, title, buttons, icon, yesText, noText, cancelText, okText);
            dlg.Owner = owner ?? Application.Current.MainWindow; // ownerがnullならメインウィンドウを親にする

            // 閉じる前にダイアログを表示
            dlg.ShowDialog();
            return dlg.Result;
        }

        // 既存のオーバーロード（互換性維持のため）
        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            return Show(owner, message, title, buttons, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            return Show(null, message, title, buttons, icon);
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        private void OkButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }
        private void YesButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
        private void NoButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
        private void CancelButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }
    }
}