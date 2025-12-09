using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace XColumn
{
    public partial class MainWindow
    {
        // スナップ機能の有効/無効フラグ
        private bool _enableWindowSnap = true;

        // 吸着する距離（ピクセル）
        private const int SnapDistance = 15;

        // プロセス間で設定を共有するためのプロパティ名
        private const string SNAP_PROP_NAME = "XColumn_SnapEnabled";

        private HwndSource? _hwndSource;
        private List<IntPtr> _connectedWindows = new List<IntPtr>();
        private POINT _lastWindowPos;
        private bool _isDragging = false;

        /// <summary>
        /// ウィンドウのスナップ機能を有効化し、Win32メッセージフックを登録します。
        /// </summary>
        private void EnableWindowSnap()
        {
            if (_hwndSource != null) return;

            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);

            // 初期状態のプロパティをセット
            UpdateSnapProperty();
        }

        /// <summary>
        /// 現在の設定に基づいて、ウィンドウプロパティ（外部公開用フラグ）を更新します。
        /// 設定変更時にも呼び出してください。
        /// </summary>
        public void UpdateSnapProperty()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (_enableWindowSnap)
            {
                // 有効フラグをセット
                SetProp(hwnd, SNAP_PROP_NAME, (IntPtr)1);
            }
            else
            {
                // プロパティを削除
                RemoveProp(hwnd, SNAP_PROP_NAME);
            }
        }

        /// <summary>
        /// スナップ機能を無効化し、Win32メッセージフックを解除します。
        /// </summary>
        public void DisableWindowSnap()
        {
            if (_hwndSource != null)
            {
                // 終了時は必ずプロパティを削除
                var hwnd = new WindowInteropHelper(this).Handle;
                RemoveProp(hwnd, SNAP_PROP_NAME);

                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }

        /// <summary>
        /// スナップ（接触）している他のXColumnウィンドウを、このウィンドウの直背面に移動させます。
        /// これにより、アクティブ化された際にグループ全体が前面に表示されるように振る舞います。
        /// </summary>
        public void BringSnappedWindowsToFront()
        {
            // スナップ機能が無効なら処理しない
            if (!_enableWindowSnap) return;

            var myHwnd = new WindowInteropHelper(this).Handle;
            if (myHwnd == IntPtr.Zero) return;

            // 自分のウィンドウ矩形を取得
            if (GetWindowRect(myHwnd, out RECT myRect))
            {
                // スナップ対象となりうる他のウィンドウ（XColumnかつスナップON）を探す
                var targets = FindSnappableWindows(myHwnd);

                // Zオーダー設定用の基準ハンドル（最初は自分自身）
                IntPtr previousHwnd = myHwnd;

                foreach (var targetHwnd in targets)
                {
                    if (GetWindowRect(targetHwnd, out RECT targetRect))
                    {
                        // 許容誤差 5px で接触判定を行う
                        if (AreRectsTouching(myRect, targetRect, 5))
                        {
                            // 対象ウィンドウを previousHwnd のすぐ後ろ(Zオーダー)に移動する
                            // SWP_NOACTIVATE を指定して、フォーカスは奪わない（アクティブ状態を維持しない）
                            SetWindowPos(targetHwnd, previousHwnd, 0, 0, 0, 0,
                                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                            // 次のウィンドウはこのウィンドウの後ろに配置するよう基準を更新
                            previousHwnd = targetHwnd;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// プロセスのWin32メッセージをフックし、ウィンドウ移動/サイズ変更イベントを監視します。
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // ※ここでは _enableWindowSnap チェックを外します。
            // 理由: OFFの場合でも、ドラッグ終了検知(WM_EXITSIZEMOVE)などは正しく処理させるため。
            // 代わりに個別の処理内でチェックします。

            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE = 0x0232;
            const int WM_WINDOWPOSCHANGING = 0x0046;

            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    OnEnterSizeMove(hwnd);
                    break;

                case WM_EXITSIZEMOVE:
                    OnExitSizeMove();
                    break;

                case WM_WINDOWPOSCHANGING:
                    // 設定OFFなら何もしない
                    if (!_enableWindowSnap) break;

                    var windowPos = Marshal.PtrToStructure<WINDOWPOS>(lParam);

                    // 移動以外の変更や、Ctrlキー押下時はスナップ処理をスキップ
                    if ((windowPos.flags & SWP_NOMOVE) != 0) break;
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) break;

                    if (_isDragging)
                    {
                        ApplySnap(ref windowPos);
                        MoveConnectedWindows(windowPos.x, windowPos.y);
                        _lastWindowPos.X = windowPos.x;
                        _lastWindowPos.Y = windowPos.y;
                    }
                    Marshal.StructureToPtr(windowPos, lParam, true);
                    break;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// サイズ変更/移動操作の開始を検知したときの処理です。
        /// </summary>
        /// <param name="myHwnd"></param>
        private void OnEnterSizeMove(IntPtr myHwnd)
        {
            _isDragging = true;
            _connectedWindows.Clear();

            // 設定OFFなら連動もしないので探索しない
            if (!_enableWindowSnap) return;

            if (GetWindowRect(myHwnd, out RECT myRect))
            {
                _lastWindowPos.X = myRect.Left;
                _lastWindowPos.Y = myRect.Top;

                // 吸着可能な相手を探す
                var targets = FindSnappableWindows(myHwnd);

                foreach (var targetHwnd in targets)
                {
                    if (GetWindowRect(targetHwnd, out RECT targetRect))
                    {
                        if (AreRectsTouching(myRect, targetRect, 1))
                        {
                            _connectedWindows.Add(targetHwnd);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// サイズ変更/移動操作の終了を検知したときの処理です。
        /// </summary>
        private void OnExitSizeMove()
        {
            _isDragging = false;
            _connectedWindows.Clear();
        }

        /// <summary>
        /// 移動中のウィンドウに連動して、接続されている他のウィンドウも移動させます。
        /// </summary>
        /// <param name="newX"></param>
        /// <param name="newY"></param>
        private void MoveConnectedWindows(int newX, int newY)
        {
            int dx = newX - _lastWindowPos.X;
            int dy = newY - _lastWindowPos.Y;
            if (dx == 0 && dy == 0) return;

            foreach (var hwnd in _connectedWindows)
            {
                if (GetWindowRect(hwnd, out RECT rect))
                {
                    SetWindowPos(hwnd, IntPtr.Zero, rect.Left + dx, rect.Top + dy, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
        }

        /// <summary>
        /// スナップ処理を適用し、WINDOWPOS構造体の位置を修正します。
        /// </summary>
        /// <param name="pos"></param>
        private void ApplySnap(ref WINDOWPOS pos)
        {
            int myWidth = pos.cx;
            int myHeight = pos.cy;
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;

            List<RECT> snapTargets = new List<RECT>();

            // 1. 画面端 (常に吸着対象)
            var screen = System.Windows.Forms.Screen.FromHandle(myHwnd);
            snapTargets.Add(new RECT
            {
                Left = screen.WorkingArea.Left,
                Top = screen.WorkingArea.Top,
                Right = screen.WorkingArea.Right,
                Bottom = screen.WorkingArea.Bottom
            });

            // 2. 他のウィンドウ (スナップONの相手のみ)
            var targets = FindSnappableWindows(myHwnd);
            foreach (var targetHwnd in targets)
            {
                if (GetWindowRect(targetHwnd, out RECT rect)) snapTargets.Add(rect);
            }

            // スナップ計算
            foreach (var target in snapTargets)
            {
                if (Math.Abs((pos.x + myWidth) - target.Left) <= SnapDistance) pos.x = target.Left - myWidth;
                else if (Math.Abs(pos.x - target.Right) <= SnapDistance) pos.x = target.Right;
                else if (Math.Abs(pos.x - target.Left) <= SnapDistance) pos.x = target.Left;
                else if (Math.Abs((pos.x + myWidth) - target.Right) <= SnapDistance) pos.x = target.Right - myWidth;

                if (Math.Abs((pos.y + myHeight) - target.Top) <= SnapDistance) pos.y = target.Top - myHeight;
                else if (Math.Abs(pos.y - target.Bottom) <= SnapDistance) pos.y = target.Bottom;
                else if (Math.Abs(pos.y - target.Top) <= SnapDistance) pos.y = target.Top;
                else if (Math.Abs((pos.y + myHeight) - target.Bottom) <= SnapDistance) pos.y = target.Bottom - myHeight;
            }
        }

        /// <summary>
        /// 自分以外のXColumnウィンドウのうち、スナップ機能がONになっているもののハンドルを取得します。
        /// </summary>
        private List<IntPtr> FindSnappableWindows(IntPtr myHwnd)
        {
            var list = new List<IntPtr>();
            int myPid = Process.GetCurrentProcess().Id;

            foreach (var process in Process.GetProcessesByName("XColumn"))
            {
                if (process.Id == myPid && process.MainWindowHandle == myHwnd) continue;
                if (process.MainWindowHandle == IntPtr.Zero) continue;

                // 相手が「スナップOK」のフラグを出しているか確認
                if (GetProp(process.MainWindowHandle, SNAP_PROP_NAME) != IntPtr.Zero)
                {
                    list.Add(process.MainWindowHandle);
                }
            }
            return list;
        }

        /// <summary>
        /// 接触判定を行います。
        /// </summary>
        /// <param name="r1"></param>
        /// <param name="r2"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        private bool AreRectsTouching(RECT r1, RECT r2, int tolerance)
        {
            bool touchX = (Math.Abs(r1.Right - r2.Left) <= tolerance) || (Math.Abs(r1.Left - r2.Right) <= tolerance);
            bool overlapY = (r1.Top < r2.Bottom) && (r1.Bottom > r2.Top);
            if (touchX && overlapY) return true;

            bool touchY = (Math.Abs(r1.Bottom - r2.Top) <= tolerance) || (Math.Abs(r1.Top - r2.Bottom) <= tolerance);
            bool overlapX = (r1.Left < r2.Right) && (r1.Right > r2.Left);
            if (touchY && overlapX) return true;

            return false;
        }

        #region Win32 API
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS { public IntPtr hwnd; public IntPtr hwndInsertAfter; public int x; public int y; public int cx; public int cy; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetProp(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr RemoveProp(IntPtr hWnd, string lpString);

        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        #endregion
    }
}