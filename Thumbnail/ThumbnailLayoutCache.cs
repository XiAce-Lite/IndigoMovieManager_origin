using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// レイアウトキー別サムネ出力パスのキャッシュとパス解決。
    /// </summary>
    internal sealed class ThumbnailLayoutCache
    {
        private static readonly string[] ErrorFileNames =
        [
            "errorSmall.jpg",
            "errorBig.jpg",
            "errorGrid.jpg",
            "errorList.jpg",
            "errorBig.jpg",
        ];

        private static readonly string[] NoFileFileNames =
        [
            "noFileSmall.jpg",
            "noFileBig.jpg",
            "noFileGrid.jpg",
            "noFileList.jpg",
            "noFileBig.jpg",
        ];

        /// <summary>WB DefaultGrid 互換の 1×1 一覧（詳細欠落時の読み取りフォールバック用）。</summary>
        private static readonly ThumbnailLayoutSpec DefaultGridListLayout = new(160, 120, 1, 1);

        public string ImagesBasePath { get; private set; } = "";
        public string ThumbRootPath { get; private set; } = "";
        public string DetailPaneOutPath { get; private set; } = "";
        public string DbName { get; private set; } = "";
        public string ThumbFolder { get; private set; } = "";

        public void Refresh(string dbName, string thumbFolder)
        {
            DbName = dbName ?? "";
            ThumbFolder = thumbFolder ?? "";
            ImagesBasePath = ApplicationPaths.ImagesDirectory;
            ThumbRootPath = ApplicationPaths.ResolveThumbRoot(dbName, thumbFolder);
            DetailPaneOutPath = ThumbnailLayoutSpec.DetailPaneLayout.GetOutPath(dbName, thumbFolder);
        }

        public string BuildThumbPath(ThumbnailLayoutSpec spec, string thumbFileName, bool checkExists)
        {
            if (spec == null)
            {
                return GetErrorPath(2);
            }

            string fullPath = Path.Combine(spec.GetOutPath(DbName, ThumbFolder), thumbFileName);
            if (!checkExists)
            {
                return GetErrorPath(2);
            }

            return File.Exists(fullPath) ? fullPath : GetErrorPath(2);
        }

        public string BuildDetailThumbPath(string thumbFileName, bool checkExists) =>
            BuildThumbPath(ThumbnailLayoutSpec.DetailPaneLayout, thumbFileName, checkExists);

        /// <summary>
        /// 詳細ペイン表示用。正規の <c>120x90x1x1</c> を優先し、無いときのみ 1×1 一覧レイアウトへ読み取りフォールバックする。
        /// </summary>
        public string ResolveDetailThumbPath(
            string thumbFileName,
            bool checkExists,
            ThumbnailLayoutSpec singlePanelListFallback = null)
        {
            if (!checkExists)
            {
                return GetErrorPath(2);
            }

            string primaryPath = Path.Combine(DetailPaneOutPath, thumbFileName);
            if (IsDisplayableThumb(primaryPath))
            {
                return primaryPath;
            }

            ThumbnailLayoutSpec fallback = singlePanelListFallback?.DivCount == 1
                ? singlePanelListFallback
                : DefaultGridListLayout;
            string fallbackPath = Path.Combine(fallback.GetOutPath(DbName, ThumbFolder), thumbFileName);
            if (IsDisplayableThumb(fallbackPath))
            {
                return fallbackPath;
            }

            return File.Exists(primaryPath) ? primaryPath : GetErrorPath(2);
        }

        public string GetExpectedThumbPath(ThumbnailLayoutSpec spec, string movieNameWithoutExt, string hash)
        {
            string thumbFileName = GetThumbFileName(movieNameWithoutExt, hash);
            return Path.Combine(spec.GetOutPath(DbName, ThumbFolder), thumbFileName);
        }

        public string GetExpectedDetailThumbPath(string movieNameWithoutExt, string hash) =>
            GetExpectedThumbPath(ThumbnailLayoutSpec.DetailPaneLayout, movieNameWithoutExt, hash);

        public string GetErrorPath(int errorIndex)
        {
            if (errorIndex < 0 || errorIndex >= ErrorFileNames.Length)
            {
                errorIndex = 2;
            }

            return Path.Combine(ImagesBasePath, ErrorFileNames[errorIndex]);
        }

        public string GetNoFilePath(int noFileIndex)
        {
            if (noFileIndex < 0 || noFileIndex >= NoFileFileNames.Length)
            {
                noFileIndex = 2;
            }

            return Path.Combine(ImagesBasePath, NoFileFileNames[noFileIndex]);
        }

        public static string GetThumbFileName(string movieNameWithoutExt, string hash)
        {
            string body = (movieNameWithoutExt ?? "").ToLowerInvariant();
            return $"{body}.#{hash}.jpg";
        }

        private static bool IsDisplayableThumb(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && ThumbnailValidityHelper.LooksLikeCompositeThumbnail(path);
    }
}
