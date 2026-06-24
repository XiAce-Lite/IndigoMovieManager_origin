using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// exe 配置基準のパス。CurrentDirectory は起動場所で変わるためサムネ出力に使わない。
    /// </summary>
    internal static class ApplicationPaths
    {
        public static string ApplicationBase => AppContext.BaseDirectory;

        public static string TempDirectory
        {
            get
            {
                string path = Path.Combine(ApplicationBase, "temp");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        }

        public static string ImagesDirectory => Path.Combine(ApplicationBase, "Images");

        public static string LayoutFilePath => Path.Combine(ApplicationBase, "layout.xml");

        public static string ResolveThumbRoot(string dbName, string thumbFolder)
        {
            if (!string.IsNullOrWhiteSpace(thumbFolder))
            {
                return thumbFolder;
            }

            return Path.Combine(ApplicationBase, "Thumb", dbName ?? "");
        }
    }
}
