using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using XColumn.Helpers;

namespace XColumn.Views
{
    /// <summary>
    /// MainWindow のウィンドウ・スナップ（吸着）機能を管理する分割クラス。
    /// 自ウィンドウを画面端や他のXColumnウィンドウへドラッグ時に吸着させ、
    /// 接触しているウィンドウ群を連動移動・連動Zオーダー化します。
    /// Win32メッセージフック(WndProc)と user32.dll の相互運用に依存します。
    /// </summary>
    public partial class MainWindow
    {
        // ===== Constants =====

        /// <summary>吸着が発動する距離（ピクセル）。</summary>
        private const int SnapDistance = 15;

        /// <summary>
        /// プロセス間で「このウィンドウはスナップ対象である」ことを共有するためのウィンドウプロパティ名。
        /// 別プロセスのXColumnは GetProp でこのフラグを確認し、スナップ相手にするか判断する。
        /// </summary>
        private const string SNAP_PROP_NAME = "XColumn_SnapEnabled";

        // ===== Fields =====

        /// <summary>スナップ機能の有効/無効フラグ。</summary>
        private bool _enableWindowSnap = true;

        /// <summary>WndProc をフックするための HwndSource。</summary>
        private HwndSource? _hwndSource;

        /// <summary>連動移動の対象となるウィンドウハンドル（ドラッグ開始時に接触していた相手）。</summary>
        private List<IntPtr> _connectedWindows = new List<IntPtr>();

        /// <summary>スナップ対象ウィンドウのハンドルキャッシュ（ドラッグ中のみ有効）。</summary>
        private List<IntPtr> _cachedSnapTargets = new List<IntPtr>();

        /// <summary>全モニターを含んだ仮想スクリーンの領域（ドラッグ中のみ有効）。</summary>
        private RECT _virtualScreenBounds;

        /// <summary>直前のウィンドウ位置（連動移動の差分計算用）。</summary>
        private POINT _lastWindowPos;

        /// <summary>現在ドラッグ（移動/リサイズ）中かどうか。</summary>
        private bool _isDragging = false;

        // ===== Public Methods =====

        /// <summary>
        /// 現在の設定に基づいてウィンドウプロパティ（他プロセスへ公開するスナップ可否フラグ）を更新します。
        /// 設定変更時にも呼び出してください。
        /// </summary>
        public void UpdateSnapProperty()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (_enableWindowSnap)
            {
                SetProp(hwnd, SNAP_PROP_NAME, (IntPtr)1);
            }
            else
            {
                RemoveProp(hwnd, SNAP_PROP_NAME);
            }
        }

        /// <summary>
        /// スナップ機能を無効化し、Win32メッセージフックを解除します（終了時に呼び出します）。
        /// </summary>
        public void DisableWindowSnap()
        {
            if (_hwndSource != null)
            {
                // 終了時は他プロセスから参照されないよう必ずプロパティを削除する
                var hwnd = new WindowInteropHelper(this).Handle;
                RemoveProp(hwnd, SNAP_PROP_NAME);

                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }

        /// <summary>
        /// スナップ（接触）している他のXColumnウィンドウを、このウィンドウの直背面(Zオーダー)へ移動させます。
        /// これにより、アクティブ化された際にグループ全体が前面に表示されるように振る舞います。
        /// </summary>
        public void BringSnappedWindowsToFront()
        {
            // スナップ機能が無効なら処理しない
            if (!_enableWindowSnap) return;

            try
            {
                var myHwnd = new WindowInteropHelper(this).Handle;
                if (myHwnd == IntPtr.Zero) return;

                if (!GetWindowRect(myHwnd, out RECT myRect)) return;

                // スナップ対象となりうる他ウィンドウ（XColumnかつスナップON）を探す
                var targets = FindSnappableWindows(myHwnd);

                // Zオーダー設定用の基準ハンドル（最初は自分自身）
                IntPtr previousHwnd = myHwnd;

                foreach (var targetHwnd in targets)
                {
                    if (!GetWindowRect(targetHwnd, out RECT targetRect)) continue;

                    // 許容誤差 5px で接触判定
                    if (AreRectsTouching(myRect, targetRect, 5))
                    {
                        // 対象を previousHwnd のすぐ後ろ(Zオーダー)へ移動。
                        // SWP_NOACTIVATE 指定でフォーカスは奪わない（アクティブ状態を変えない）。
                        SetWindowPos(targetHwnd, previousHwnd, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                        // 次のウィンドウはこのウィンドウの後ろへ配置するよう基準を更新
                        previousHwnd = targetHwnd;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"BringSnappedWindowsToFront Error: {ex.Message}");
            }
        }

        // ===== Internal Setup (Hook) =====

        /// <summary>
        /// ウィンドウのスナップ機能を有効化し、Win32メッセージフックを登録します。
        /// </summary>
        private void EnableWindowSnap()
        {
            if (_hwndSource != null) return;

            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);

            // 初期状態の公開プロパティをセット
            UpdateSnapProperty();
        }

        /// <summary>
        /// Win32メッセージをフックし、ウィンドウ移動/サイズ変更イベントを監視してスナップ処理を行います。
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // WndProc内での未処理例外はアプリのクラッシュに直結するため、必ず握りつぶす
            try
            {
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
                        // スナップOFF・非ドラッグ中は何もしない
                        if (!_enableWindowSnap) break;
                        if (!_isDragging) break;

                        var windowPos = Marshal.PtrToStructure<WINDOWPOS>(lParam);

                        // 移動を伴わない変更(SWP_NOMOVE)や、Ctrl押下中（一時無効化）はスキップ
                        if ((windowPos.flags & SWP_NOMOVE) != 0) break;
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) break;

                        // スナップ位置補正と連動移動を適用
                        ApplySnap(ref windowPos);
                        MoveConnectedWindows(windowPos.x, windowPos.y);

                        _lastWindowPos.X = windowPos.x;
                        _lastWindowPos.Y = windowPos.y;

                        // 補正後の値を元の構造体へ書き戻す
                        Marshal.StructureToPtr(windowPos, lParam, false);
                        break;
                }
            }
            catch (Exception ex)
            {
                // ログのみ。クラッシュはさせない
                Logger.Log($"Snap WndProc Error: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        // ===== Private Methods (Snap Logic) =====

        /// <summary>
        /// 移動/リサイズ操作の開始時の処理。スナップ対象の探索と接触相手のキャッシュを行います。
        /// </summary>
        private void OnEnterSizeMove(IntPtr myHwnd)
        {
            _isDragging = true;
            _connectedWindows.Clear();
            _cachedSnapTargets.Clear();

            // 移動開始時に全モニターを含む仮想スクリーン領域を取得・キャッシュする。
            // マルチモニター環境では座標が負になり得るため、これを基準に境界チェックする。
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            _virtualScreenBounds = new RECT { Left = vs.Left, Top = vs.Top, Right = vs.Right, Bottom = vs.Bottom };

            // スナップOFFなら連動探索も不要
            if (!_enableWindowSnap) return;

            if (!GetWindowRect(myHwnd, out RECT myRect)) return;

            _lastWindowPos.X = myRect.Left;
            _lastWindowPos.Y = myRect.Top;

            // FindSnappableWindows は重い(プロセス列挙)ため、ドラッグ開始時に一度だけ実行してキャッシュする
            var targets = FindSnappableWindows(myHwnd);
            _cachedSnapTargets.AddRange(targets);

            // 開始時点で既に接触している相手を「連動移動対象」として記録（許容誤差1px）
            foreach (var targetHwnd in targets)
            {
                if (GetWindowRect(targetHwnd, out RECT targetRect) && AreRectsTouching(myRect, targetRect, 1))
                {
                    _connectedWindows.Add(targetHwnd);
                }
            }
        }

        /// <summary>
        /// 移動/リサイズ操作の終了時の処理。ドラッグ中のキャッシュを解放します。
        /// </summary>
        private void OnExitSizeMove()
        {
            _isDragging = false;
            _connectedWindows.Clear();
            _cachedSnapTargets.Clear();
        }

        /// <summary>
        /// 移動中のウィンドウに連動して、接続されている他のウィンドウも同じ差分だけ移動させます。
        /// </summary>
        private void MoveConnectedWindows(int newX, int newY)
        {
            int dx = newX - _lastWindowPos.X;
            int dy = newY - _lastWindowPos.Y;
            if (dx == 0 && dy == 0) return;

            foreach (var hwnd in _connectedWindows)
            {
                if (!GetWindowRect(hwnd, out RECT rect)) continue;

                int destX = rect.Left + dx;
                int destY = rect.Top + dy;

                // 0で頭打ちにすると左/上のモニターへ行けなくなるため、
                // 仮想スクリーンの左上端を下限として補正する（負座標を許容）。
                if (destX < _virtualScreenBounds.Left) destX = _virtualScreenBounds.Left;
                if (destY < _virtualScreenBounds.Top) destY = _virtualScreenBounds.Top;

                SetWindowPos(hwnd, IntPtr.Zero, destX, destY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// スナップ処理を適用し、WINDOWPOS構造体の位置を吸着先へ補正します。
        /// 吸着対象は「現在のモニターの作業領域端」と「キャッシュ済みの他XColumnウィンドウ」です。
        /// </summary>
        private void ApplySnap(ref WINDOWPOS pos)
        {
            int myWidth = pos.cx;
            int myHeight = pos.cy;
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;

            List<RECT> snapTargets = new List<RECT>();

            // 1. 画面端（現在ウィンドウがあるモニターの作業領域。常に吸着対象）
            var screen = System.Windows.Forms.Screen.FromHandle(myHwnd);
            snapTargets.Add(new RECT
            {
                Left = screen.WorkingArea.Left,
                Top = screen.WorkingArea.Top,
                Right = screen.WorkingArea.Right,
                Bottom = screen.WorkingArea.Bottom
            });

            // 2. 他のXColumnウィンドウ（スナップON）。
            //    重い FindSnappableWindows は呼ばず、ドラッグ開始時のキャッシュを使う。
            foreach (var targetHwnd in _cachedSnapTargets)
            {
                if (GetWindowRect(targetHwnd, out RECT rect)) snapTargets.Add(rect);
            }

            // 各ターゲットに対し、四辺それぞれで SnapDistance 以内なら吸着位置へ補正する
            foreach (var target in snapTargets)
            {
                // X方向: 右辺↔左辺 / 左辺↔右辺 / 左辺↔左辺 / 右辺↔右辺
                if (Math.Abs((pos.x + myWidth) - target.Left) <= SnapDistance) pos.x = target.Left - myWidth;
                else if (Math.Abs(pos.x - target.Right) <= SnapDistance) pos.x = target.Right;
                else if (Math.Abs(pos.x - target.Left) <= SnapDistance) pos.x = target.Left;
                else if (Math.Abs((pos.x + myWidth) - target.Right) <= SnapDistance) pos.x = target.Right - myWidth;

                // Y方向: 下辺↔上辺 / 上辺↔下辺 / 上辺↔上辺 / 下辺↔下辺
                if (Math.Abs((pos.y + myHeight) - target.Top) <= SnapDistance) pos.y = target.Top - myHeight;
                else if (Math.Abs(pos.y - target.Bottom) <= SnapDistance) pos.y = target.Bottom;
                else if (Math.Abs(pos.y - target.Top) <= SnapDistance) pos.y = target.Top;
                else if (Math.Abs((pos.y + myHeight) - target.Bottom) <= SnapDistance) pos.y = target.Bottom - myHeight;
            }

            // 仮想スクリーンの左上端での下限補正（マルチモニターの負座標自体は許可）
            if (pos.x < _virtualScreenBounds.Left) pos.x = _virtualScreenBounds.Left;
            if (pos.y < _virtualScreenBounds.Top) pos.y = _virtualScreenBounds.Top;
        }

        /// <summary>
        /// 自分以外のXColumnウィンドウのうち、スナップ機能がONになっているもののハンドルを取得します。
        /// プロセス列挙を伴うため負荷が高く、ドラッグ開始時など限られたタイミングでのみ呼び出します。
        /// </summary>
        private List<IntPtr> FindSnappableWindows(IntPtr myHwnd)
        {
            var list = new List<IntPtr>();

            try
            {
                int myPid = Process.GetCurrentProcess().Id;

                foreach (var process in Process.GetProcessesByName("XColumn"))
                {
                    try
                    {
                        if (process.Id == myPid) continue;
                        if (process.MainWindowHandle == IntPtr.Zero || process.MainWindowHandle == myHwnd) continue;

                        // 相手が「スナップOK」フラグ(SNAP_PROP_NAME)を公開しているか確認
                        if (GetProp(process.MainWindowHandle, SNAP_PROP_NAME) != IntPtr.Zero)
                        {
                            list.Add(process.MainWindowHandle);
                        }
                    }
                    catch
                    {
                        // 個別プロセスへのアクセス失敗は無視して継続
                    }
                }
            }
            catch
            {
                // プロセス一覧取得自体の失敗も無視
            }

            return list;
        }

        /// <summary>
        /// 2つの矩形が指定許容誤差内で接触している（辺が近接かつ他軸が重なっている）かを判定します。
        /// </summary>
        private bool AreRectsTouching(RECT r1, RECT r2, int tolerance)
        {
            // 左右の辺が近接し、かつY方向で重なっている
            bool touchX = (Math.Abs(r1.Right - r2.Left) <= tolerance) || (Math.Abs(r1.Left - r2.Right) <= tolerance);
            bool overlapY = (r1.Top < r2.Bottom) && (r1.Bottom > r2.Top);
            if (touchX && overlapY) return true;

            // 上下の辺が近接し、かつX方向で重なっている
            bool touchY = (Math.Abs(r1.Bottom - r2.Top) <= tolerance) || (Math.Abs(r1.Top - r2.Bottom) <= tolerance);
            bool overlapX = (r1.Left < r2.Right) && (r1.Right > r2.Left);
            if (touchY && overlapX) return true;

            return false;
        }

        // ===== Win32 API =====

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