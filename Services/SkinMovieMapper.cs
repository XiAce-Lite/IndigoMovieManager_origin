using System.IO;

namespace IndigoMovieManager.Services
{
    internal static class SkinMovieMapper
    {
        public static WhiteBrowserMovieDto ToWhiteBrowserDto(
            MovieRecords rec,
            int tabIndex,
            Func<string, string> toThumbUrl,
            IReadOnlyCollection<long> selectedIds,
            long? focusedId)
        {
            string thumbPath = PlayPositionResolver.GetThumbPathForEngine(rec, SkinEngineHelper.FromLegacyThumbTabIndex(tabIndex));
            bool selected = selectedIds.Contains(rec.Movie_Id);

            return new WhiteBrowserMovieDto
            {
                Id = rec.Movie_Id,
                Thum = string.IsNullOrEmpty(thumbPath) ? "" : toThumbUrl(thumbPath),
                Title = rec.Movie_Body ?? rec.Movie_Name ?? "",
                Ext = rec.Ext ?? "",
                Exist = rec.IsExists,
                Select = selected ? 1 : 0,
                Score = rec.Score,
                FileDate = rec.File_Date ?? "",
                Size = FormatFileSize(rec.Movie_Size),
                Len = rec.Movie_Length ?? "",
                Tags = rec.Tag?.ToArray() ?? [],
                Drive = rec.Drive ?? "",
                Dir = rec.Dir ?? "",
                Container = rec.Container ?? "",
                Video = rec.Video ?? "",
                Audio = rec.Audio ?? "",
                Comments =
                [
                    rec.Comment1 ?? "",
                    rec.Comment2 ?? "",
                    rec.Comment3 ?? "",
                ],
            };
        }

        public static SkinMovieDto ToDto(
            MovieRecords rec,
            int tabIndex,
            Func<string, string> toThumbUrl,
            IReadOnlyCollection<long> selectedIds,
            long? focusedId)
        {
            string thumbPath = PlayPositionResolver.GetThumbPathForEngine(rec, SkinEngineHelper.FromLegacyThumbTabIndex(tabIndex));

            return new SkinMovieDto
            {
                Id = rec.Movie_Id,
                MovieName = rec.Movie_Name ?? "",
                MovieBody = rec.Movie_Body ?? "",
                Ext = rec.Ext ?? "",
                MoviePath = rec.Movie_Path ?? "",
                Thumb = string.IsNullOrEmpty(thumbPath) ? "" : toThumbUrl(thumbPath),
                Score = rec.Score,
                FileDate = rec.File_Date ?? "",
                SizeText = FormatFileSize(rec.Movie_Size),
                Length = rec.Movie_Length ?? "",
                Tags = rec.Tag?.ToArray() ?? [],
                Exists = rec.IsExists,
                Selected = selectedIds.Contains(rec.Movie_Id),
                Focused = focusedId == rec.Movie_Id,
            };
        }

        public static string FormatFileSize(long bytes)
        {
            string[] suffix = ["", "K", "M", "G", "T"];
            double size = bytes;
            int i;
            for (i = 0; i < suffix.Length - 1; i++)
            {
                if (size < 1024) break;
                size /= 1024;
            }

            return $"{size.ToString(i == 0 ? "0" : "0.0")} {suffix[i]}B";
        }

        public static string ToVirtualThumbUrl(string fullPath, string thumbRoot, string virtualHost)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(thumbRoot))
            {
                return "";
            }

            string normalizedRoot = thumbRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string relative = normalizedPath[normalizedRoot.Length..].Replace('\\', '/');
            return $"https://{virtualHost}/{EncodePath(relative)}";
        }

        private static string EncodePath(string relative)
        {
            string[] segments = relative.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = Uri.EscapeDataString(segments[i]);
            }

            return string.Join('/', segments);
        }

        public static string ToVirtualImageUrl(string fullPath, string imagesRoot, string virtualHost)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(imagesRoot))
            {
                return "";
            }

            string normalizedRoot = imagesRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string relative = normalizedPath[normalizedRoot.Length..].Replace('\\', '/');
            return $"https://{virtualHost}/{EncodePath(relative)}";
        }
    }
}
