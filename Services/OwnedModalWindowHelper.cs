using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 確認・編集などの小さい所有モーダルを Alt+Tab / タスクバーから外す。
    /// （設定・DMM 検索など大きな子 Window には使わない）
    /// </summary>
    internal static class OwnedModalWindowHelper
    {
        private const int GwlExstyle = -20;
        private const int WsExAppwindow = 0x00040000;
        private const int WsExToolwindow = 0x00000080;

        /// <summary>
        /// ShowInTaskbar=false と WS_EX_TOOLWINDOW を適用する。
        /// InitializeComponent の直後に呼ぶ。
        /// </summary>
        public static void ExcludeFromAltTab(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.ShowInTaskbar = false;

            void OnSourceInitialized(object sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;
                ApplyToolWindowStyle(window);
            }

            window.SourceInitialized += OnSourceInitialized;

            // 既に HWND がある場合（再表示など）
            if (PresentationSource.FromVisual(window) != null)
            {
                ApplyToolWindowStyle(window);
            }
        }

        private static void ApplyToolWindowStyle(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            IntPtr exStyle = GetWindowLongPtr(hwnd, GwlExstyle);
            long value = exStyle.ToInt64();
            value |= WsExToolwindow;
            value &= ~WsExAppwindow;
            _ = SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(value));
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
