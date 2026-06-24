using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailValidityHelper
    {
        /// <summary>
        /// 利用可能な複合サムネか。存在しない場合は即 false（ファイルを開かない）。
        /// </summary>
        public static bool IsUsableCompositeThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return LooksLikeCompositeThumbnail(path);
        }

        /// <summary>
        /// 複合サムネ（メタデータフッター付き）かどうか。error プレースホルダーは false。
        /// </summary>
        public static bool LooksLikeCompositeThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 64)
                {
                    return false;
                }

                stream.Seek(-60, SeekOrigin.End);
                byte[] settingBuf = new byte[60];
                if (stream.Read(settingBuf, 0, 60) < 60)
                {
                    return false;
                }

                int thumbCount = BitConverter.ToUInt16(settingBuf, 0);
                return thumbCount is > 0 and <= 100;
            }
            catch
            {
                return false;
            }
        }
    }
}
