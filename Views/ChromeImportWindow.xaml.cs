using System.Windows;
using XColumn.Helpers;
using XColumn.Models;

namespace XColumn.Views
{
    /// <summary>
    /// Chromeにインストール済みの拡張機能を走査し、選択したものをアプリへインポートするダイアログ。
    /// </summary>
    public partial class ChromeImportWindow : Window
    {
        // ===== Properties =====

        /// <summary>
        /// インポートが確定した拡張機能のリスト（呼び出し側が結果として受け取ります）。
        /// </summary>
        public List<ExtensionItem> ImportedExtensions { get; private set; } = new List<ExtensionItem>();

        // ===== Constructor =====

        /// <summary>
        /// ダイアログを初期化し、拡張機能の走査を開始します。
        /// </summary>
        public ChromeImportWindow()
        {
            InitializeComponent();
            LoadExtensions();
        }

        // ===== Private Methods =====

        /// <summary>
        /// Chromeの拡張機能を非同期で走査し、一覧に表示します。
        /// 既定では全項目のチェックを外した状態にします。
        /// </summary>
        private async void LoadExtensions()
        {
            LoadingBar.Visibility = Visibility.Visible;
            ExtensionsGrid.Visibility = Visibility.Hidden;

            // UIをブロックしないように非同期でスキャン
            var extensions = await Task.Run(() => ChromeExtensionImporter.ScanChromeExtensions());

            // 既定ではチェックを外しておく（IsEnabledプロパティをチェック状態として流用）
            foreach (var ext in extensions)
            {
                ext.IsEnabled = false;
            }

            ExtensionsGrid.ItemsSource = extensions;

            LoadingBar.Visibility = Visibility.Collapsed;
            ExtensionsGrid.Visibility = Visibility.Visible;

            if (extensions.Count == 0)
            {
                MessageWindow.Show(this, Properties.Resources.ChromeImport_NoExtensionsFound, Properties.Resources.Information);
            }
        }

        /// <summary>
        /// 「インポート」ボタンクリック時の処理。
        /// 選択された拡張機能をアプリ用フォルダへコピーし、結果を ImportedExtensions に格納して閉じます。
        /// </summary>
        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var list = ExtensionsGrid.ItemsSource as List<ExtensionItem>;
            if (list == null) return;

            var selected = list.Where(x => x.IsEnabled).ToList();
            if (selected.Count == 0)
            {
                MessageWindow.Show(this, Properties.Resources.ChromeImport_NoExtensionsSelected, Properties.Resources.Confirmation);
                return;
            }

            ImportButton.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(() =>
                {
                    foreach (var item in selected)
                    {
                        // アプリ用フォルダにコピーし、パスを更新
                        string newPath = ChromeExtensionImporter.CopyExtensionToAppFolder(item.Path, item.Name);
                        item.Path = newPath;
                        item.IsEnabled = true; // アプリ上では有効にする
                        ImportedExtensions.Add(item);
                    }
                });

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageWindow.Show(this,
                    string.Format(Properties.Resources.Msg_Err_ChromeImportFailed, ex.Message),
                    Properties.Resources.Error);
                ImportButton.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 「キャンセル」ボタンクリック時の処理。何もインポートせずに閉じます。
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}