using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
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
                            System.Windows.MessageBox.Show("この拡張機能は既に追加されています。", "確認");
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
        /// 「削除」ボタンの処理。選択された拡張機能をリストから除外します。
        /// </summary>
        private void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            // Button は System.Windows.Controls.Button として認識されます
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ExtensionItem item)
            {
                // MessageBox は System.Windows.MessageBox として認識されます
                if (System.Windows.MessageBox.Show($"拡張機能 '{item.Name}' を削除しますか？", "確認",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Extensions.Remove(item);
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
    }
}