using System.IO;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// レイアウトキー（例: 360x203x1x1）の近傍検索。
    /// 1px 差で別フォルダになると無駄な再生成が走るため、既存近いサイズを提案する。
    /// </summary>
    public static class ThumbnailLayoutNearMatch
    {
        private static readonly Regex KeyRegex = new(
            @"^(?<w>\d+)x(?<h>\d+)x(?<c>\d+)x(?<r>\d+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsNear(ThumbnailLayoutSpec a, ThumbnailLayoutSpec b, int tolerancePx = 1)
        {
            if (a == null || b == null || a.Equals(b))
            {
                return false;
            }

            return a.Columns == b.Columns
                && a.Rows == b.Rows
                && Math.Abs(a.Width - b.Width) <= tolerancePx
                && Math.Abs(a.Height - b.Height) <= tolerancePx;
        }

        public static bool TryParseKey(string key, out ThumbnailLayoutSpec spec)
        {
            spec = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            Match m = KeyRegex.Match(key.Trim());
            if (!m.Success)
            {
                return false;
            }

            spec = new ThumbnailLayoutSpec(
                int.Parse(m.Groups["w"].Value),
                int.Parse(m.Groups["h"].Value),
                int.Parse(m.Groups["c"].Value),
                int.Parse(m.Groups["r"].Value));
            return true;
        }

        /// <summary>
        /// thumbRoot 直下のレイアウトフォルダから、target に近く中身のあるものを返す。
        /// </summary>
        public static ThumbnailLayoutSpec FindNearExistingWithFiles(
            string thumbRoot,
            ThumbnailLayoutSpec target,
            int tolerancePx = 1)
        {
            if (string.IsNullOrWhiteSpace(thumbRoot) || target == null || !Directory.Exists(thumbRoot))
            {
                return null;
            }

            ThumbnailLayoutSpec best = null;
            int bestDist = int.MaxValue;

            foreach (string dir in Directory.EnumerateDirectories(thumbRoot))
            {
                string name = Path.GetFileName(dir);
                if (!TryParseKey(name, out ThumbnailLayoutSpec candidate))
                {
                    continue;
                }

                if (!IsNear(target, candidate, tolerancePx))
                {
                    continue;
                }

                if (!DirectoryHasThumbFiles(dir))
                {
                    continue;
                }

                int dist = Math.Abs(candidate.Width - target.Width)
                    + Math.Abs(candidate.Height - target.Height);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool DirectoryHasThumbFiles(string dir)
        {
            try
            {
                return Directory.EnumerateFiles(dir, "*.jpg").Any()
                    || Directory.EnumerateFiles(dir, "*.jpeg").Any()
                    || Directory.EnumerateFiles(dir, "*.png").Any();
            }
            catch
            {
                return false;
            }
        }
    }
}
