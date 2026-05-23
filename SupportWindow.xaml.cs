using System.Diagnostics;
using System.Windows;

namespace XColumn
{
    public partial class SupportWindow : Window
    {
        public SupportWindow()
        {
            InitializeComponent();
        }

        private void BuyMeACoffee_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://buymeacoffee.com/mashersan");
        }

        private void GithubSponsors_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/sponsors/mashersan");
        }

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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}