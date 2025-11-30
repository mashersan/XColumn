using System.Windows;
using Application = System.Windows.Application;

namespace XColumn
{
    public partial class MessageWindow : Window
    {
        public MessageWindow(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            InitializeComponent();
            this.Title = title;
            this.MessageTextBlock.Text = message;

            // ボタンの表示制御
            if (buttons == MessageBoxButton.YesNo)
            {
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
            }
            else if (buttons == MessageBoxButton.OKCancel)
            {
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
            }
            // 必要に応じて他のパターンも追加
        }

        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            var dlg = new MessageWindow(message, title, buttons, icon);
            dlg.Owner = owner ?? Application.Current.MainWindow; // ownerがnullならメインウィンドウを親にする
            dlg.ShowDialog();
            return dlg.Result;
        }

        // パターン2: 親ウィンドウ指定あり、アイコン省略
        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            return Show(owner, message, title, buttons, MessageBoxImage.None);
        }

        // パターン3: 親ウィンドウ省略 (現在のコードに合わせる場合便利)
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