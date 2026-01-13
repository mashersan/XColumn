using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace XColumn
{
    public partial class MainWindow
    {
        private DispatcherTimer? _memoryTimer;

        /// <summary>
        /// メモリ最適化タイマーを初期化し、開始します。
        /// </summary>
        private void InitializeMemoryOptimizer()
        {
            // 5分ごとに実行
            _memoryTimer = new DispatcherTimer();
            _memoryTimer.Interval = TimeSpan.FromMinutes(5);
            _memoryTimer.Tick += (s, e) => OptimizeMemory();
            _memoryTimer.Start();

            // アプリが非アクティブになった時（バックグラウンドに回った時）にも実行
            this.Deactivated += (s, e) => OptimizeMemory();
        }

        /// <summary>
        /// メモリ解放処理を実行します。
        /// </summary>
        private void OptimizeMemory()
        {
            try
            {
                // 1. C#側のガベージコレクションを強制実行
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // 2. WebView2のメモリ整理 (Chromiumのキャッシュ解放など)
                foreach (var col in Columns)
                {
                    OptimizeWebViewMemory(col.AssociatedWebView?.CoreWebView2);
                }
                OptimizeWebViewMemory(FocusWebView?.CoreWebView2);

                // 3. ワーキングセットの縮小 (OSに使用していない物理メモリを返却)
                // これによりタスクマネージャー上の「メモリ使用量」が見かけ上大きく減ります
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);

                // Logger.Log("[Memory] Optimization executed.");
            }
            catch (Exception ex)
            {
                Logger.Log($"[Memory] Optimization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定されたWebView2インスタンスのメモリ使用量を最適化します。
        /// </summary>
        private void OptimizeWebViewMemory(CoreWebView2? coreWebView)
        {
            if (coreWebView == null) return;

            try
            {
                // アプリがアクティブならNormal、非アクティブならLow（積極的な解放）を設定
                // Lowに設定するとブラウザ内部のキャッシュや未使用リソースが解放されます
                var targetLevel = _isAppActive
                    ? CoreWebView2MemoryUsageTargetLevel.Normal
                    : CoreWebView2MemoryUsageTargetLevel.Low;

                coreWebView.MemoryUsageTargetLevel = targetLevel;
            }
            catch { /* 無視 */ }
        }

        // Win32 API Import
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);
    }
}