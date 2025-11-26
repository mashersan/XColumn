using System.Windows.Controls;
using System.Windows.Input;

namespace XColumn
{
    /// <summary>
    /// WebView2内のテキストボックス等でのカーソル移動を妨害しないように、
    /// 矢印キーによるスクロール処理を無効化したScrollViewer。
    /// </summary>
    public class ExtendedScrollViewer : ScrollViewer
    {
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // 矢印キーが押された場合、イベントを「処理済み(Handled=true)」としてマークする。
            // これによりScrollViewerのスクロール動作をキャンセルし、
            // イベントをWebView2 (HwndHost) 側に通過させます。
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                return;
            }

            base.OnKeyDown(e);
        }
    }
}