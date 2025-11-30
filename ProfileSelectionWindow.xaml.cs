using System.Collections.Generic;
using System.Windows;
using XColumn.Models;

namespace XColumn
{
    public partial class ProfileSelectionWindow : Window
    {
        public string SelectedProfileName { get; private set; } = "";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="profiles">プロファイル一覧</param>
        /// <param name="defaultProfileName">初期選択するプロファイル名</param>
        /// <param name="title">ウィンドウのタイトル</param>
        /// <param name="message">ユーザーへのメッセージ</param>
        /// <param name="buttonText">実行ボタンのテキスト</param>
        public ProfileSelectionWindow(
            IEnumerable<object> profiles,
            string defaultProfileName,
            string title = "プロファイル選択",
            string message = "プロファイルを選択してください:",
            string buttonText = "OK")
        {
            InitializeComponent();

            // UIテキストの反映
            this.Title = title;
            this.MessageText.Text = message;
            this.ActionButton.Content = buttonText;

            // プロファイル一覧を設定
            ProfileCombo.ItemsSource = profiles;

            // 初期選択
            foreach (var item in ProfileCombo.Items)
            {
                if (item is ProfileItem p && p.Name == defaultProfileName)
                {
                    ProfileCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileCombo.SelectedItem is ProfileItem item)
            {
                SelectedProfileName = item.Name;
                DialogResult = true;
            }
            else
            {
                MessageWindow.Show("プロファイルを選択してください。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}