using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using XColumn.Code;
using XColumn.Models;

namespace XColumn
{
    public partial class ChromeImportWindow : Window
    {
        // 選択された拡張機能を返すためのプロパティ
        public List<ExtensionItem> ImportedExtensions { get; private set; } = new List<ExtensionItem>();

        public ChromeImportWindow()
        {
            InitializeComponent();
            LoadExtensions();
        }

        private async void LoadExtensions()
        {
            LoadingBar.Visibility = Visibility.Visible;
            ExtensionsGrid.Visibility = Visibility.Hidden;

            // UIをブロックしないように非同期でスキャン
            var extensions = await Task.Run(() => ChromeExtensionImporter.ScanChromeExtensions());

            // デフォルトではチェックを外しておく（IsEnabledプロパティをチェック状態として流用）
            foreach (var ext in extensions)
            {
                ext.IsEnabled = false;
            }

            ExtensionsGrid.ItemsSource = extensions;

            LoadingBar.Visibility = Visibility.Collapsed;
            ExtensionsGrid.Visibility = Visibility.Visible;

            if (extensions.Count == 0)
            {
                MessageWindow.Show(this, "Chromeのデフォルトプロファイルから拡張機能が見つかりませんでした。", "情報");
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var list = ExtensionsGrid.ItemsSource as List<ExtensionItem>;
            if (list == null) return;

            var selected = list.Where(x => x.IsEnabled).ToList();
            if (selected.Count == 0)
            {
                MessageWindow.Show(this, "インポートする拡張機能を選択してください。", "確認");
                return;
            }

            ImportButton.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;

            // コピー処理
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
            catch (System.Exception ex)
            {
                MessageWindow.Show(this, $"インポート中にエラーが発生しました。\n{ex.Message}", "エラー");
                ImportButton.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}