using System.Runtime.InteropServices;
using System.Windows;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// クリップボード占有による COMException（CLIPBRD_E_CANT_OPEN）をリトライで吸収する。
    /// </summary>
    internal static class ClipboardAccess
    {
        // OpenClipboard failed (CLIPBRD_E_CANT_OPEN)
        private const int ClipbrdECantOpen = unchecked((int)0x800401D0);

        private const int MaxAttempts = 10;
        private const int RetryDelayMs = 20;

        public static bool IsClipboardBusyException(Exception exception)
        {
            for (Exception ex = exception; ex != null; ex = ex.InnerException)
            {
                if (ex is COMException com && com.ErrorCode == ClipbrdECantOpen)
                {
                    return true;
                }

                // 一部環境では ExternalException として上がる
                if (ex is ExternalException external && external.ErrorCode == ClipbrdECantOpen)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TrySetText(string text)
        {
            text ??= string.Empty;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    // copy:true で自プロセス終了後も残す（タグ／メタコピー用途）
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch (Exception ex) when (IsClipboardBusyException(ex))
                {
                    if (attempt == MaxAttempts)
                    {
                        AppFileLogger.LogError(ex, "ClipboardAccess.TrySetText", $"failed after {MaxAttempts} attempts");
                        return false;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }

            return false;
        }

        public static bool TryGetText(out string text)
        {
            text = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsText(TextDataFormat.Text))
                    {
                        return false;
                    }

                    text = Clipboard.GetText(TextDataFormat.Text);
                    return true;
                }
                catch (Exception ex) when (IsClipboardBusyException(ex))
                {
                    if (attempt == MaxAttempts)
                    {
                        AppFileLogger.LogError(ex, "ClipboardAccess.TryGetText", $"failed after {MaxAttempts} attempts");
                        return false;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }

            return false;
        }
    }
}
