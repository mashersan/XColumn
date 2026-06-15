using System.Windows;

// WPFとWinFormsのApplication型の曖昧さ回避
using Application = System.Windows.Application;

namespace XColumn.Views
{
    /// <summary>
    /// ボタン構成やボタン文言をカスタマイズできるカスタムメッセージダイアログ。
    /// 標準の MessageBox の代替として、アプリ内の確認・通知・エラー表示に使用します。
    /// </summary>
    public partial class MessageWindow : Window
    {
        // ===== Properties =====

        /// <summary>
        /// ユーザーが選択した結果。閉じられるまでの既定値は Cancel です。
        /// </summary>
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        // ===== Constructor =====

        /// <summary>
        /// メッセージ・タイトル・ボタン構成・各ボタン文言を指定してダイアログを構築します。
        /// </summary>
        /// <param name="message">本文メッセージ。</param>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <param name="buttons">表示するボタンの組み合わせ。</param>
        /// <param name="icon">アイコン種別（現時点では未使用）。</param>
        /// <param name="yesText">「はい」ボタンの文言（null で既定）。</param>
        /// <param name="noText">「いいえ」ボタンの文言（null で既定）。</param>
        /// <param name="cancelText">「キャンセル」ボタンの文言（null で既定）。</param>
        /// <param name="okText">「OK」ボタンの文言（null で既定）。</param>
        public MessageWindow(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
                             string? yesText = null, string? noText = null, string? cancelText = null, string? okText = null)
        {
            InitializeComponent();
            this.Title = title;
            this.MessageTextBlock.Text = message;

            // ボタン文言のカスタマイズ（指定があるもののみ上書き）
            if (!string.IsNullOrEmpty(yesText)) YesButton.Content = yesText;
            if (!string.IsNullOrEmpty(noText)) NoButton.Content = noText;
            if (!string.IsNullOrEmpty(cancelText)) CancelButton.Content = cancelText;
            if (!string.IsNullOrEmpty(okText)) OkButton.Content = okText;

            // ボタン構成に応じた表示制御
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

        // ===== Public Methods (Show overloads) =====

        /// <summary>
        /// オーナー・ボタン構成・アイコン・各ボタン文言を指定してダイアログを表示します。
        /// </summary>
        /// <param name="owner">親ウィンドウ。null の場合はメインウィンドウを親にします。</param>
        /// <param name="message">本文メッセージ。</param>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <param name="buttons">表示するボタンの組み合わせ。</param>
        /// <param name="icon">アイコン種別。</param>
        /// <param name="yesText">「はい」ボタンの文言（null で既定）。</param>
        /// <param name="noText">「いいえ」ボタンの文言（null で既定）。</param>
        /// <param name="cancelText">「キャンセル」ボタンの文言（null で既定）。</param>
        /// <param name="okText">「OK」ボタンの文言（null で既定）。</param>
        /// <returns>ユーザーが選択した結果。</returns>
        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons, MessageBoxImage icon,
                                            string? yesText = null, string? noText = null, string? cancelText = null, string? okText = null)
        {
            var dlg = new MessageWindow(message, title, buttons, icon, yesText, noText, cancelText, okText);
            dlg.Owner = owner ?? Application.Current.MainWindow; // ownerがnullならメインウィンドウを親にする

            dlg.ShowDialog();
            return dlg.Result;
        }

        /// <summary>
        /// オーナーとボタン構成を指定してダイアログを表示します（アイコンなし）。
        /// </summary>
        /// <param name="owner">親ウィンドウ。null の場合はメインウィンドウを親にします。</param>
        /// <param name="message">本文メッセージ。</param>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <param name="buttons">表示するボタンの組み合わせ。</param>
        /// <returns>ユーザーが選択した結果。</returns>
        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            return Show(owner, message, title, buttons, MessageBoxImage.None);
        }

        /// <summary>
        /// オーナーを指定せずにダイアログを表示します（メインウィンドウを親にします）。
        /// </summary>
        /// <param name="message">本文メッセージ。</param>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <param name="buttons">表示するボタンの組み合わせ。</param>
        /// <param name="icon">アイコン種別。</param>
        /// <returns>ユーザーが選択した結果。</returns>
        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            return Show(null, message, title, buttons, icon);
        }

        // ===== Event Handlers =====

        /// <summary>「OK」ボタンクリック。結果を OK にして閉じます。</summary>
        private void OkButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }

        /// <summary>「はい」ボタンクリック。結果を Yes にして閉じます。</summary>
        private void YesButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }

        /// <summary>「いいえ」ボタンクリック。結果を No にして閉じます。</summary>
        private void NoButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }

        /// <summary>「キャンセル」ボタンクリック。結果を Cancel にして閉じます。</summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }
    }
}