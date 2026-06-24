using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// ZIP の詳細タブ（99）サムネを、既存タブの出力から複製する。
    /// リスト用サムネは別フォルダ（例: 120x90x3x1）のため、詳細用（120x90x1x1）は別途必要。
    /// </summary>
    internal static class ZipDetailThumbnailMaterializer
    {
        public static bool TryCopyFile(string sourcePath, string detailPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)
                || string.IsNullOrWhiteSpace(detailPath)
                || !File.Exists(sourcePath))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(detailPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, detailPath, overwrite: true);
                return File.Exists(detailPath);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryCopyFromExistingTabThumbs(
            ThumbnailLayoutCache cache,
            string movieBody,
            string hash,
            string detailPath)
        {
            if (cache == null
                || string.IsNullOrWhiteSpace(movieBody)
                || string.IsNullOrWhiteSpace(hash)
                || string.IsNullOrWhiteSpace(detailPath))
            {
                return false;
            }

            for (int tabIndex = 0; tabIndex < cache.TabOutPaths.Length; tabIndex++)
            {
                string sourcePath = cache.GetExpectedThumbPath(tabIndex, movieBody, hash);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                if (TryCopyFile(sourcePath, detailPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
