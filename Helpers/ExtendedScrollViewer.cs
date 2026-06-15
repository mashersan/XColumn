using System.Windows.Controls;
using System.Windows.Input;






// 曖昧さ回避
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace XColumn.Helpers
{
    /// <summary>
    /// WebView2内のテキストボックス等でのカーソル移動を妨害しないように、
    /// 矢印キーおよびナビゲーションキー（Home/End/PageUp/PageDown）による
    /// 標準スクロール処理を無効化した ScrollViewer。
    /// </summary>
    public class ExtendedScrollViewer : ScrollViewer
    {
        /// <summary>
        /// キー押下時の処理。ナビゲーション系キーの場合は ScrollViewer 標準のスクロール動作を抑止します。
        /// </summary>
        /// <param name="e">キーイベント引数。</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // 矢印キー・Home/End・PageUp/PageDown の場合は、ScrollViewer 標準のスクロール動作を行わない。
            //
            // 【注意】ここで e.Handled = true は設定しない。
            // Handled にするとイベントが完全に消費され、WebView2 側のIME入力や
            // 他の必要なキー処理まで止まってしまうことがある。
            // そのため「base.OnKeyDown を呼ばない」ことだけでスクロールを抑止し、
            // イベント自体は後続へ伝播させる。
            if (e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Home || e.Key == Key.End ||
                e.Key == Key.PageUp || e.Key == Key.PageDown)
            {
                return;
            }

            base.OnKeyDown(e);
        }
    }
}