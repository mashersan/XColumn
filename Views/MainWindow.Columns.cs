using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XColumn.Models;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindow のカラム操作（追加・削除・並べ替え・復元）に関するロジックを管理する分割クラス。
    /// カラム追加メニューのコマンドは MainWindowViewModel 側にあり、本クラスは
    /// 実際の生成処理(AddNewColumn / AddListAutoColumn)とリスト復元・削除を担当します。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Constants =====

        /// <summary>
        /// LoadColumnsFromSettings のフォールバック等で使用するホームURL。
        /// </summary>
        private const string DefaultHomeUrl = "https://x.com/home";

        // ===== Public Methods =====

        /// <summary>
        /// 指定されたURLを持つ新しいカラムを作成し、カラムリストに追加します。
        /// 許可ドメイン(x.com / twitter.com)以外の場合はエラーを表示し、追加しません。
        /// </summary>
        /// <param name="url">表示するURL。</param>
        public void AddNewColumn(string url)
        {
            // 通常ドメイン・フォーカス対象ドメインのいずれかとして許可されている場合のみ追加
            if (IsAllowedDomain(url) || IsAllowedDomain(url, true))
            {
                var newColumn = new ColumnData
                {
                    Url = url,
                    UseSoftRefresh = _useSoftRefresh,

                    // 現在のグローバル設定値を初期幅として適用（未設定時は 380）
                    Width = this.ColumnWidth > 0 ? this.ColumnWidth : 380
                };
                AddColumnObject(newColumn);
            }
            else
            {
                MessageWindow.Show(Properties.Resources.Err_DomainNotAllowed, Properties.Resources.Common_Error);
            }
        }

        // ===== Private Methods =====

        /// <summary>
        /// 「リスト自動追加」要求(ViewModel経由)に応じて、リスト自動遷移フラグ付きカラムを生成します。
        /// </summary>
        private void AddListAutoColumn()
        {
            // ホーム画面(x.com)から開始し、リスト自動遷移フラグを立てたカラムを作成
            var newColumn = new ColumnData
            {
                Url = "https://x.com",
                UseSoftRefresh = _useSoftRefresh,
                IsListAutoNav = true,

                // 現在のグローバル設定値を初期幅として適用（未設定時は 380）
                Width = this.ColumnWidth > 0 ? this.ColumnWidth : 380
            };

            AddColumnObject(newColumn);
        }

        /// <summary>
        /// カラムオブジェクトを実際にUI(Columns コレクション)へ追加し、
        /// 追加位置設定に応じてスクロール位置を端へ寄せます。
        /// </summary>
        /// <param name="newColumn">追加するカラム。</param>
        private void AddColumnObject(ColumnData newColumn)
        {
            if (_addColumnToLeft)
            {
                Columns.Insert(0, newColumn);
                // テンプレート内の ScrollViewer を名前で取得して左端へスクロール
                if (ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) is ScrollViewer sv)
                {
                    sv.ScrollToLeftEnd();
                }
            }
            else
            {
                Columns.Add(newColumn);
                if (ColumnItemsControl.Template.FindName("MainScrollViewer", ColumnItemsControl) is ScrollViewer sv)
                {
                    sv.ScrollToRightEnd();
                }
            }
        }

        /// <summary>
        /// アプリ起動時やプロファイル切り替え時に、保存された設定からカラムリストを復元します。
        /// 復元対象が無い場合はデフォルトのホームカラムを1つ作成します。
        /// </summary>
        /// <param name="settings">読み込まれた設定オブジェクト。</param>
        private async void LoadColumnsFromSettings(AppSettings settings)
        {
            bool loaded = false;

            if (settings.Columns != null)
            {
                foreach (var col in settings.Columns)
                {
                    if (!IsAllowedDomain(col.Url)) continue;

                    // 復元時にも全体設定を強制適用する（個別に保存された古い設定を上書き）
                    col.UseSoftRefresh = _useSoftRefresh;
                    col.KeepUnreadPosition = _keepUnreadPosition;

                    // Widthが未設定(0)の場合は、保存されていたグローバル設定値(settings.ColumnWidth)を適用
                    if (col.Width <= 0)
                    {
                        col.Width = settings.ColumnWidth > 0 ? settings.ColumnWidth : 380;
                    }

                    Columns.Add(col);
                    loaded = true;

                    // 【時間差ロード】次のカラムを生成・読み込みする前に少し待機する。
                    // 同時に多数のWebView2を立ち上げるとCPUスパイクやX(Twitter)側のアクセス制限を
                    // 招きやすいため、約200msの間隔を空ける。
                    // 重い場合は 300〜500、軽くしたい場合は 100 程度に調整する。
                    await Task.Delay(200);
                }
            }

            // 有効なカラムが1つも復元されなかった場合はデフォルトのホームを追加
            if (!loaded) AddNewColumn(DefaultHomeUrl);
        }

        // ===== Event Handlers =====

        /// <summary>
        /// カラム削除ボタン（×）または右クリックメニューからの削除処理。
        /// 該当カラムをリストから削除し、関連リソース（タイマー等）を解放します。
        /// Ctrl押下時は削除ではなくロック状態のトグルとして動作します。
        /// </summary>
        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            // Button と MenuItem の両方から呼ばれるため FrameworkElement で受ける
            if (sender is FrameworkElement element && element.Tag is ColumnData col)
            {
                // Ctrlキー押下中は「削除」ではなく「ロックのトグル」として扱う（誤削除防止）
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    col.IsLocked = !col.IsLocked;
                    return;
                }

                // ロック中のカラムは削除しない
                if (col.IsLocked) return;

                // タイマー等のリソースを解放してからコレクションから除去
                col.StopAndDisposeTimer();
                Columns.Remove(col);
            }
        }
    }
}