using System.IO;
using System.Runtime.InteropServices;

namespace IndigoMovieManager.Services
{
    /// <summary>ファイル／フォルダを Windows のゴミ箱へ送る。</summary>
    internal static class RecycleBinHelper
    {
        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;
        private const ushort FOF_WANTNUKEWARNING = 0x4000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

        /// <summary>
        /// パスをゴミ箱へ送る。失敗しても完全削除にはフォールバックしない。
        /// </summary>
        public static bool TrySendToRecycleBin(string path, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "パスが空です。";
                return false;
            }

            if (!Directory.Exists(path) && !File.Exists(path))
            {
                errorMessage = "対象が存在しません。";
                return false;
            }

            try
            {
                // SHFileOperation は二重 null 終端のリストを要求する。
                string from = Path.GetFullPath(path) + '\0' + '\0';
                var op = new SHFILEOPSTRUCT
                {
                    hwnd = IntPtr.Zero,
                    wFunc = FO_DELETE,
                    pFrom = from,
                    pTo = null,
                    fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_WANTNUKEWARNING),
                    fAnyOperationsAborted = false,
                    hNameMappings = IntPtr.Zero,
                    lpszProgressTitle = null,
                };

                int result = SHFileOperation(ref op);
                if (result != 0 || op.fAnyOperationsAborted)
                {
                    errorMessage = $"ゴミ箱への移動に失敗しました（コード {result}）。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
