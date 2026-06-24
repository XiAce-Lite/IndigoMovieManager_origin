using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// タブ別サムネ出力パスのキャッシュとパス解決。
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

        public string ImagesBasePath { get; private set; } = "";
        public string[] TabOutPaths { get; private set; } = [];
        public string DetailOutPath { get; private set; } = "";

        public void Refresh(string dbName, string thumbFolder, int tabCount)
        {
            ImagesBasePath = ApplicationPaths.ImagesDirectory;
            TabOutPaths = new string[tabCount];
            for (int i = 0; i < tabCount; i++)
            {
                TabOutPaths[i] = new TabInfo(i, dbName, thumbFolder).OutPath;
            }

            DetailOutPath = new TabInfo(99, dbName, thumbFolder).OutPath;
        }

        public string BuildThumbPath(int tabIndex, string thumbFileName, bool checkExists)
        {
            string fullPath = tabIndex == 99
                ? Path.Combine(DetailOutPath, thumbFileName)
                : Path.Combine(TabOutPaths[tabIndex], thumbFileName);

            if (!checkExists)
            {
                return GetErrorPath(tabIndex);
            }

            return File.Exists(fullPath)
                ? fullPath
                : GetErrorPath(tabIndex);
        }

        public string GetErrorPath(int tabIndex)
        {
            int errorIndex = tabIndex == 99 ? 2 : tabIndex;
            if (errorIndex < 0 || errorIndex >= ErrorFileNames.Length)
            {
                errorIndex = 0;
            }

            return Path.Combine(ImagesBasePath, ErrorFileNames[errorIndex]);
        }

        public string GetExpectedThumbPath(int tabIndex, string movieNameWithoutExt, string hash)
        {
            string thumbFileName = GetThumbFileName(movieNameWithoutExt, hash);
            if (tabIndex == 99)
            {
                return Path.Combine(DetailOutPath, thumbFileName);
            }

            if (tabIndex < 0 || tabIndex >= TabOutPaths.Length)
            {
                return Path.Combine(TabOutPaths[0], thumbFileName);
            }

            return Path.Combine(TabOutPaths[tabIndex], thumbFileName);
        }

        public static int GetTabIndexFromSkin(string skin)
        {
            if (string.IsNullOrWhiteSpace(skin))
            {
                return 0;
            }

            string normalized = skin.Replace(" ", "");
            return normalized switch
            {
                "DefaultSmall" => 0,
                "DefaultBig" => 1,
                "DefaultGrid" => 2,
                "DefaultList" => 3,
                "DefaultBig10" => 4,
                _ => 0,
            };
        }

        public static string GetThumbFileName(string movieNameWithoutExt, string hash)
        {
            string body = (movieNameWithoutExt ?? "").ToLowerInvariant();
            return $"{body}.#{hash}.jpg";
        }
    }
}
