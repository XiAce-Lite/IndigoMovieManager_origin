using System;
using System.IO;
using IndigoMovieManager.Services.WpfSkin;

using IndigoMovieManager.Services;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// サムネイル生成レイアウト（W×H×C×R）。出力フォルダ名のキーにもなる。
    /// </summary>
    public sealed class ThumbnailLayoutSpec : IEquatable<ThumbnailLayoutSpec>
    {
        public int Width { get; }
        public int Height { get; }
        public int Columns { get; }
        public int Rows { get; }

        public int DivCount => Columns * Rows;

        public string Key => $"{Width}x{Height}x{Columns}x{Rows}";

        public ThumbnailLayoutSpec(int width, int height, int columns, int rows)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Columns = Math.Max(1, columns);
            Rows = Math.Max(1, rows);
        }

        public static ThumbnailLayoutSpec FromTabIndex(int tabIndex) =>
            tabIndex switch
            {
                0 => new ThumbnailLayoutSpec(120, 90, 3, 1),
                1 => new ThumbnailLayoutSpec(200, 150, 3, 1),
                2 => new ThumbnailLayoutSpec(160, 120, 1, 1),
                3 => new ThumbnailLayoutSpec(56, 42, 5, 1),
                4 => new ThumbnailLayoutSpec(120, 90, 5, 2),
                99 => new ThumbnailLayoutSpec(120, 90, 1, 1),
                _ => new ThumbnailLayoutSpec(160, 120, 1, 1),
            };

        public static ThumbnailLayoutSpec FromWpfSkinThumbnail(WpfSkinThumbnail thumbnail)
        {
            if (thumbnail == null)
            {
                return new ThumbnailLayoutSpec(400, 225, 1, 1);
            }

            return new ThumbnailLayoutSpec(
                thumbnail.Width,
                thumbnail.Height,
                thumbnail.Columns > 0 ? thumbnail.Columns : 1,
                thumbnail.Rows > 0 ? thumbnail.Rows : 1);
        }

        internal static ThumbnailLayoutSpec FromSkinConfig(SkinConfig config)
        {
            SkinConfig resolved = (config ?? SkinConfig.DefaultGridWeb()).WithFallback(SkinConfig.DefaultGridWeb());
            return new ThumbnailLayoutSpec(
                resolved.ThumbWidth,
                resolved.ThumbHeight,
                resolved.ThumbColumn,
                resolved.ThumbRow);
        }

        public string GetOutPath(string dbName, string thumbFolder) =>
            Path.Combine(ApplicationPaths.ResolveThumbRoot(dbName, thumbFolder), Key);

        public bool Equals(ThumbnailLayoutSpec other)
        {
            if (other is null)
            {
                return false;
            }

            return Width == other.Width
                && Height == other.Height
                && Columns == other.Columns
                && Rows == other.Rows;
        }

        public override bool Equals(object obj) => Equals(obj as ThumbnailLayoutSpec);

        public override int GetHashCode() => HashCode.Combine(Width, Height, Columns, Rows);
    }
}
