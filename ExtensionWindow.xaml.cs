using System.Collections.ObjectModel;
using System.Windows;
using XColumn.Models;


namespace XColumn
{
    /// <summary>
    /// 拡張機能を管理（追加・削除）するためのウィンドウ。
    /// </summary>
    public partial class ExtensionWindow : Window
    {
        public ObservableCollection<ExtensionItem> Extensions { get; set; }

        public ExtensionWindow(List<ExtensionItem> currentExtensions)
        {
            InitializeComponent();

            Extensions = new ObservableCollection<ExtensionItem>(currentExtensions);
            this.DataContext = this;
        }

        /// <summary>
        /// 「フォルダから追加」ボタンの処理。
        /// フォルダ選択ダイアログを開き、manifest.json の存在確認を行った上でリストに追加します。
        /// </summary>
        private void AddExtension_Click(object sender, RoutedEventArgs e)
        {
            // Formsの名前空間をusingせずに、ここで「完全修飾名」を使って指定します
            // これにより、他の場所での Button や MessageBox の競合を防ぎます
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Chrome拡張機能のフォルダ（manifest.jsonが含まれるフォルダ）を選択してください";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;
                    string name = System.IO.Path.GetFileName(path);

                    foreach (var ext in Extensions)
                    {
                        if (ext.Path == path)
                        {
                            MessageWindow.Show(Properties.Resources.Extension_AlreadyAdded, Properties.Resources.Confirmation);
                            return;
                        }
                    }

                    Extensions.Add(new ExtensionItem
                    {
                        Name = name,
                        Path = path,
                        IsEnabled = true
                    });
                }
            }
        }
        /// <summary>
        /// 「削除」ボタンの処理。選択された拡張機能をリストから除外し、物理フォルダの削除を試みます。
        /// </summary>
        private void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            // Button は System.Windows.Controls.Button として認識されます
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ExtensionItem item)
            {
                // MessageBox は System.Windows.MessageBox として認識されます
                if (MessageWindow.Show($"拡張機能 '{item.Name}' を削除しますか？\n(インポートされたフォルダも完全に削除されます)", "確認",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 1. リストからの削除（UIへの即時反映）
                    Extensions.Remove(item);

                    // 2. 物理フォルダの削除を試みる
                    if (!string.IsNullOrEmpty(item.Path) && System.IO.Directory.Exists(item.Path))
                    {
                        try
                        {
                            // trueを指定して、サブフォルダやファイルも含めて再帰的に削除する
                            System.IO.Directory.Delete(item.Path, true);
                        }
                        catch (Exception ex)
                        {
                            // WebView2がファイルをロックしている場合など、アクセス拒否への安全対策
                            MessageWindow.Show(
                                $"リストからは削除されましたが、フォルダの物理削除に一部失敗しました。\n" +
                                $"拡張機能がバックグラウンドで使用中の可能性があります。\n\n" +
                                $"アプリを再起動した後、以下のフォルダを手動で削除してください。\n\nパス: {item.Path}\n詳細: {ex.Message}",
                                "フォルダ削除の一部失敗",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 各アイテムの「設定」ボタンクリック時の処理。
        /// 親ウィンドウ（MainWindow）経由で拡張機能のオプションページを開きます。
        /// </summary>
        private void OpenOptions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ExtensionItem item)
            {
                if (this.Owner is MainWindow mw)
                {
                    mw.OpenExtensionOptions(item);

                    // 設定画面がユーザーに見えるよう、管理ウィンドウはいったん閉じる
                    this.Close();
                }
            }
        }

        /// <summary>
        /// 「フォルダを開く」ボタンクリック時の処理。
        /// 拡張機能の格納フォルダをエクスプローラーで開きます。
        /// </summary>
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ExtensionItem item)
            {
                if (System.IO.Directory.Exists(item.Path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageWindow.Show($"フォルダを開けませんでした。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageWindow.Show("指定されたフォルダが存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 「閉じる」ボタンの処理。DialogResultをtrueにしてウィンドウを閉じ、変更を確定させます。
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Chromeから拡張機能をインポートするボタンの処理。
        /// </summary>
        private void ImportFromChrome_Click(object sender, RoutedEventArgs e)
        {
            var importWin = new ChromeImportWindow();
            importWin.Owner = this;

            if (importWin.ShowDialog() == true)
            {
                int count = 0;
                foreach (var newItem in importWin.ImportedExtensions)
                {
                    // 重複チェック（パスで判断）
                    bool exists = false;
                    foreach (var existing in Extensions)
                    {
                        // 名前またはパスが完全に一致する場合
                        if (existing.Path == newItem.Path || existing.Name == newItem.Name)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        Extensions.Add(newItem);
                        count++;
                    }
                }

                if (count > 0)
                {
                    MessageWindow.Show(this, $"{count} 個の拡張機能をインポートしました。", "完了");
                }
                else
                {
                    MessageWindow.Show(this, Properties.Resources.Extension_NoNewExtensions, Properties.Resources.Information);
                }
            }
        }
    }
}