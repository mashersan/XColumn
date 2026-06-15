using System.Diagnostics;
using System.Windows;

namespace XColumn.Views
{
    /// <summary>
    /// 開発者への支援・寄付リンクを表示し、外部ブラウザで開くためのウィンドウ。
    /// </summary>
    public partial class SupportWindow : Window
    {
        // ===== Constructor =====

        /// <summary>
        /// ウィンドウを初期化します。
        /// </summary>
        public SupportWindow()
        {
            InitializeComponent();
        }

        // ===== Event Handlers =====

        /// <summary>
        /// 「Buy Me a Coffee」ボタンクリック時の処理。支援ページを外部ブラウザで開きます。
        /// </summary>
        private void BuyMeACoffee_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://buymeacoffee.com/mashersan");
        }

        /// <summary>
        /// 「GitHub Sponsors」ボタンクリック時の処理。支援ページを外部ブラウザで開きます。
        /// </summary>
        private void GithubSponsors_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/sponsors/mashersan");
        }

        /// <summary>
        /// 「閉じる」ボタンクリック時の処理。
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ===== Private Methods =====

        /// <summary>
        /// 指定したURLを既定のブラウザで開きます。失敗時はエラーダイアログを表示します。
        /// </summary>
        /// <param name="url">開くURL。</param>
        private void OpenUrl(string url)
        {
            try
            {
                // .NET Core以降でURLを標準ブラウザで開くための設定
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                System.Windows.MessageBox.Show(Properties.Resources.Err_LinkOpenFailed, Properties.Resources.Common_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}