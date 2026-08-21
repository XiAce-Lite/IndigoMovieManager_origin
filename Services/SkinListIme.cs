using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 一覧（スキン）上では IME を閉じ、タグ編集や検索の日本語入力は触らない。
    /// </summary>
    internal static class SkinListIme
    {
        public static Key GetEffectiveKey(KeyEventArgs e)
        {
            if (e == null)
            {
                return Key.None;
            }

            return e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
        }

        public static void CloseCurrent()
        {
            InputMethod.Current.ImeState = InputMethodState.Off;
        }

        public static void CloseImeForHwndHost(HwndHost host)
        {
            CloseCurrent();
            if (host == null)
            {
                return;
            }

            IntPtr hwnd = host.Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            CloseImeForWindowTree(hwnd);
        }

        private static void CloseImeForWindowTree(IntPtr hwnd)
        {
            CloseImeForWindow(hwnd);
            EnumChildWindows(hwnd, (child, _) =>
            {
                CloseImeForWindow(child);
                return true;
            }, IntPtr.Zero);
        }

        private static void CloseImeForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            IntPtr himc = ImmGetContext(hwnd);
            if (himc != IntPtr.Zero)
            {
                ImmSetOpenStatus(himc, false);
                ImmReleaseContext(hwnd, himc);
            }

            ImmAssociateContext(hwnd, IntPtr.Zero);
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        private static extern bool ImmSetOpenStatus(IntPtr hIMC, bool fOpen);

        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);
    }
}
