using System.IO;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailMetadataWriter
    {
        public static void AppendMetadata(string savePath, ThumbInfo thumbInfo)
        {
            using FileStream dest = new(savePath, FileMode.Append, FileAccess.Write);
            dest.Write(thumbInfo.SecBuffer);
            dest.Write(thumbInfo.InfoBuffer);
        }

        public static void CleanupPartialOutput(string saveThumbFileName, IReadOnlyList<string> panelPaths)
        {
            if (!string.IsNullOrWhiteSpace(saveThumbFileName) && File.Exists(saveThumbFileName))
            {
                try { File.Delete(saveThumbFileName); } catch { /* ignore */ }
            }

            if (panelPaths == null) { return; }
            foreach (string path in panelPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { continue; }
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
    }
}
