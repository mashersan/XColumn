using System.Windows.Controls;
using System.Windows.Input;

namespace XColumn
{
    /// <summary>
    /// WebView2内のテキストボックス等でのカーソル移動を妨害しないように、
    /// 矢印キーおよびナビゲーションキーによるスクロール処理を無効化したScrollViewer。
    /// </summary>
    public class ExtendedScrollViewer : ScrollViewer
    {
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // 矢印キーやHome/Endキーが押された場合、
            // ScrollViewerの標準動作（スクロール）を実行せずに処理を抜けます。
            // e.Handled = true; を設定すると、IME入力や他の必要なイベント処理まで
            // 止まってしまう場合があるため、単に base.OnKeyDown を呼ばないことで対応します。
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