using System.Net;
using System.Windows;
using System.Windows.Controls;
using XColumn.Models;

namespace XColumn
{
    /// <summary>
    /// MainWindowのカラム操作（追加、削除、並べ替え、復元）に関するロジックを管理する分割クラス。
    /// GongSolutions.WPF.DragDropライブラリのインターフェース(IDropTarget)を実装し、
    /// カラムのドラッグ＆ドロップによる並べ替え機能を提供します。
    /// </summary>
    public partial class MainWindow
    {
        private const string DefaultHomeUrl = "https://x.com/home";
        private const string DefaultNotifyUrl = "https://x.com/notifications";
        private const string DefaultTrendUrl = "https://x.com/explore/tabs/trending";
        private const string SearchUrlFormat = "https://x.com/search?q={0}";
        private const string ListUrlFormat = "https://x.com/i/lists/{0}";

        /// <summary>
        /// 指定されたURLを持つ新しいカラムを作成し、カラムリストの末尾に追加します。
        /// </summary>
        /// <param name="url">カラムに表示する初期URL</param>
        private void AddNewColumn(string url)
        {
            if (IsAllowedDomain(url))
            {
                // 新規カラム作成
                var newColumn = new ColumnData { Url = url, UseSoftRefresh = _useSoftRefresh };

                // 【変更】設定に応じて追加位置を分岐
                if (_addColumnToLeft)
                {
                    // 先頭(左端)に挿入
                    Columns.Insert(0, newColumn);

                    // 左端に追加した場合は、スクロールを左端に戻すと親切です（任意）
                    if (ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) is ScrollViewer sv)
                    {
                        sv.ScrollToLeftEnd();
                    }
                }
                else
                {
                    // 末尾(右端)に追加 (従来通り)
                    Columns.Add(newColumn);

                    // 右端に追加した場合は、スクロールを右端へ（任意）
                    if (ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) is ScrollViewer sv)
                    {
                        sv.ScrollToRightEnd();
                    }
                }
            }
            else
            {
                MessageWindow.Show("許可されていないドメインです。", "エラー");
            }
        }
        
        /// <summary>
        /// アプリ起動時やプロファイル切り替え時に、保存された設定からカラムリストを復元します。
        /// カラム情報がない場合は、デフォルトのカラムセット（ホーム、通知）を作成します。
        /// </summary>
        /// <param name="settings">読み込まれた設定オブジェクト</param>
        private void LoadColumnsFromSettings(AppSettings settings)
        {
            bool loaded = false;
            // 保存されたカラム設定を復元
            if (settings.Columns != null)
            {
                foreach (var col in settings.Columns)
                {
                    if (IsAllowedDomain(col.Url))
                    {
                        // 復元時にも全体設定を強制適用する（個別に保存された古い設定を上書き）
                        col.UseSoftRefresh = _useSoftRefresh;
                        col.KeepUnreadPosition = _keepUnreadPosition;
                        Columns.Add(col);
                        loaded = true;
                    }
                }
            }
            // カラムが0個の場合はデフォルトのホームを追加
            if (!loaded) AddNewColumn(DefaultHomeUrl);
        }

        /// <summary>
        /// 「ホーム追加」ボタンクリック時の処理。
        /// ホームタイムラインを表示するカラムを追加します。
        /// </summary>
        private void AddHome_Click(object s, RoutedEventArgs e) => AddNewColumn(DefaultHomeUrl);

        /// <summary>
        /// 「通知追加」ボタンクリック時の処理。
        /// 通知画面を表示するカラムを追加します。
        /// </summary>
        private void AddNotify_Click(object s, RoutedEventArgs e) => AddNewColumn(DefaultNotifyUrl);

        /// <summary>
        /// 「トレンド追加」ボタンクリック時の処理。
        /// トレンド画面を表示するカラムを追加します。
        /// </summary>
        private void AddTrend_Click(object s, RoutedEventArgs e) => AddNewColumn(DefaultTrendUrl);

        /// <summary>
        /// 「検索追加」ボタンクリック時の処理。
        /// 検索キーワード入力ダイアログを表示し、入力されたキーワードで検索結果カラムを追加します。
        /// </summary>
        private void AddSearch_Click(object s, RoutedEventArgs e)
        {
            var key = ShowInputWindow("検索", "キーワード:");
            if (!string.IsNullOrEmpty(key)) AddNewColumn(string.Format(SearchUrlFormat, WebUtility.UrlEncode(key)));
        }

        /// <summary>
        /// 「リスト追加」ボタンクリック時の処理。
        /// リスト一覧ページを表示するカラムを追加します。
        /// </summary>
        private void AddList_Click(object s, RoutedEventArgs e)
        {
            var input = ShowInputWindow("リスト追加", "リストID または URL:");
            if (string.IsNullOrEmpty(input)) return;

            if (input.StartsWith("http"))
            {
                if (IsAllowedDomain(input)) AddNewColumn(input);
                else MessageWindow.Show("無効なURLです。", "エラー");
            }
            else if (long.TryParse(input, out _))
            {
                AddNewColumn(string.Format(ListUrlFormat, input));
            }
            else
            {
                MessageWindow.Show("IDかURLを入力してください。", "エラー");
            }
        }

        /// <summary>
        /// カラム削除ボタン（×）または右クリックメニューからの削除処理。
        /// 該当するカラムをリストから削除し、関連リソース（タイマーなど）を解放します。
        /// </summary>
        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            // Button と MenuItem の両方に対応するため FrameworkElement にキャスト
            if (sender is FrameworkElement element && element.Tag is ColumnData col)
            {
                col.StopAndDisposeTimer();
                Columns.Remove(col);
            }
        }

        /// <summary>
        /// カラムの手動更新ボタン（↻）クリック時の処理。
        /// Webページをリロードし、自動更新タイマーをリセットします。
        /// </summary>
        private async void ColumnManualRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ColumnData col)
            {
                // 手動更新は設定に関わらず強制リロード
                await col.ReloadWebViewAsync(forceReload: true);
            }
        }

        private string? ShowInputWindow(string title, string prompt)
        {
            var dlg = new InputWindow(title, prompt) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.InputText?.Trim() : null;
        }

        /// <summary>
        /// カラムの「戻る」ボタンクリック時の処理。
        /// ブラウザの履歴があれば前のページに戻ります。
        /// </summary>
        private void ColumnGoBack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ColumnData col)
            {
                // WebViewが有効かつ、戻る履歴が存在する場合のみ実行
                if (col.AssociatedWebView != null && col.AssociatedWebView.CanGoBack)
                {
                    col.AssociatedWebView.GoBack();
                }
            }
        }
    }
}