using System.Net;
using System.Windows;
using System.Windows.Controls;
using XColumn.Models;

namespace XColumn
{
    public partial class MainWindow
    {
        private const string DefaultHomeUrl = "https://x.com/home";
        private const string DefaultNotifyUrl = "https://x.com/notifications";
        private const string SearchUrlFormat = "https://x.com/search?q={0}";
        private const string ListUrlFormat = "https://x.com/i/lists/{0}";

        /// <summary>
        /// 新しいカラムを追加します。
        /// </summary>
        private void AddNewColumn(string url)
        {
            if (IsAllowedDomain(url)) Columns.Add(new ColumnData { Url = url });
            else System.Windows.MessageBox.Show("許可されていないドメインです。", "エラー");
        }

        /// <summary>
        /// 設定からカラムリストを復元します。
        /// </summary>
        private void LoadColumnsFromSettings(AppSettings settings)
        {
            bool loaded = false;
            if (settings.Columns != null)
            {
                foreach (var col in settings.Columns)
                {
                    if (IsAllowedDomain(col.Url))
                    {
                        Columns.Add(col);
                        loaded = true;
                    }
                }
            }
            // カラムが0個の場合はデフォルトのホームを追加
            if (!loaded) AddNewColumn(DefaultHomeUrl);
        }

        private void AddHome_Click(object s, RoutedEventArgs e) => AddNewColumn(DefaultHomeUrl);
        private void AddNotify_Click(object s, RoutedEventArgs e) => AddNewColumn(DefaultNotifyUrl);

        private void AddSearch_Click(object s, RoutedEventArgs e)
        {
            var key = ShowInputWindow("検索", "キーワード:");
            if (!string.IsNullOrEmpty(key)) AddNewColumn(string.Format(SearchUrlFormat, WebUtility.UrlEncode(key)));
        }

        private void AddList_Click(object s, RoutedEventArgs e)
        {
            var input = ShowInputWindow("リスト追加", "リストID または URL:");
            if (string.IsNullOrEmpty(input)) return;

            if (input.StartsWith("http"))
            {
                if (IsAllowedDomain(input)) AddNewColumn(input);
                else System.Windows.MessageBox.Show("無効なURLです。", "エラー");
            }
            else if (long.TryParse(input, out _))
            {
                AddNewColumn(string.Format(ListUrlFormat, input));
            }
            else
            {
                System.Windows.MessageBox.Show("IDかURLを入力してください。", "エラー");
            }
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ColumnData col)
            {
                col.StopAndDisposeTimer();
                Columns.Remove(col);
            }
        }

        private void ColumnManualRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ColumnData col) col.ReloadWebView();
        }

        private string? ShowInputWindow(string title, string prompt)
        {
            var dlg = new InputWindow(title, prompt) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.InputText?.Trim() : null;
        }
    }
}