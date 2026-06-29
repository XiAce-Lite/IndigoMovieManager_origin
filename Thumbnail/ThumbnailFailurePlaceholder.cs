using System.Diagnostics;
using System.IO;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// サムネイル生成失敗時に出力先へ error プレースホルダーを書き、再試行ループを止める。
    /// </summary>
    internal static class ThumbnailFailurePlaceholder
    {
        public static bool TryWrite(ThumbnailLayoutCache cache, int tabIndex, string saveThumbFileName)
        {
            if (cache == null || string.IsNullOrWhiteSpace(saveThumbFileName))
            {
                return false;
            }

            try
            {
                int errorIndex = tabIndex == SkinTabIndexHelper.WpfSkinThumbnailSlotIndex ? 2 : tabIndex;
                string errorSource = cache.GetErrorPath(errorIndex);
                if (!File.Exists(errorSource))
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(saveThumbFileName);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(errorSource, saveThumbFileName, true);
                return File.Exists(saveThumbFileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] failure placeholder: {ex.Message}");
                return false;
            }
        }
    }
}
