using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms; // FolderBrowserDialog用
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// Chrome拡張機能を管理（追加・削除・有効無効化）するためのウィンドウ。
    /// </summary>
    public partial class ExtensionWindow : Window
    {
        /// <summary>
        /// UIにバインディングされる拡張機能のコレクション。
        /// </summary>
        public ObservableCollection<ExtensionItem> Extensions { get; private set; }

        /// <summary>
        /// コンストラクタ。現在の拡張機能リストを受け取り、UI用コレクションを初期化します。
        /// </summary>
        /// <param name="currentExtensions">現在の拡張機能リスト</param>
        public ExtensionWindow(System.Collections.Generic.List<ExtensionItem> currentExtensions)
        {
            InitializeComponent();
            // 親ウィンドウのリストに直接影響を与えないよう、新しいコレクションとしてコピーを作成して操作する
            Extensions = new ObservableCollection<ExtensionItem>(currentExtensions);
            ExtensionsListBox.ItemsSource = Extensions;
        }

        /// <summary>
        /// 「フォルダから追加」ボタンの処理。
        /// フォルダ選択ダイアログを開き、manifest.json の存在確認を行った上でリストに追加します。
        /// </summary>
        private void AddExtension_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "拡張機能のフォルダ（manifest.jsonが含まれるフォルダ）を選択してください";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;

                    // 必須ファイルのチェック
                    if (!File.Exists(Path.Combine(path, "manifest.json")))
                    {
                        System.Windows.MessageBox.Show("選択されたフォルダに 'manifest.json' が見つかりません。\n正しい拡張機能フォルダか確認してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string name = new DirectoryInfo(path).Name;
                    Extensions.Add(new ExtensionItem { Name = name, Path = path, IsEnabled = true });
                }
            }
        }

        /// <summary>
        /// 「削除」ボタンの処理。選択された拡張機能をリストから除外します。
        /// </summary>
        private void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (ExtensionsListBox.SelectedItem is ExtensionItem selected)
            {
                Extensions.Remove(selected);
            }
        }

        /// <summary>
        /// 「閉じる」ボタンの処理。DialogResultをtrueにしてウィンドウを閉じます。
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}