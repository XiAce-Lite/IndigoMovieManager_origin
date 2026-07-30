namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// カード内サムネの「表示サイズ」計算。
    /// 生成用ピクセル（<see cref="WpfSkinThumbnail.Width"/> / Height）は変更しない。
    /// </summary>
    public static class WpfSkinThumbnailDisplaySize
    {
        /// <summary>
        /// 親から与えられた表示幅に対し、格子（columns×rows）と参照アスペクトから
        /// 表示高さを求める。
        /// </summary>
        /// <remarks>
        /// 1セル幅 = availableWidth / columns
        /// 1セル高さ = 1セル幅 ÷ セルアスペクト
        /// セルアスペクト = (refWidth/columns) / (refHeight/rows)
        /// 枠高さ = 1セル高さ × rows
        /// （結果は availableWidth × refHeight / refWidth と一致する）
        /// </remarks>
        public static double CalcDisplayHeight(double availableWidth, WpfSkinThumbnail thumb)
        {
            if (availableWidth <= 0)
            {
                return 0;
            }

            int cols = Math.Max(1, thumb?.Columns ?? 1);
            int rows = Math.Max(1, thumb?.Rows ?? 1);
            double refWidth = thumb?.Width > 0 ? thumb.Width : 400;
            double refHeight = thumb?.Height > 0 ? thumb.Height : 225;

            double cellAspect = (refWidth / cols) / (refHeight / rows);
            if (cellAspect <= 0 || double.IsNaN(cellAspect) || double.IsInfinity(cellAspect))
            {
                cellAspect = 16.0 / 9.0;
            }

            double displayCellWidth = availableWidth / cols;
            double displayCellHeight = displayCellWidth / cellAspect;
            return displayCellHeight * rows;
        }

        /// <summary>
        /// サムネは常に親セル幅に追従する（ノードの width 固定だとスプリッターで縮まないため）。
        /// その他ノードは明示 Width が無いときだけ追従。
        /// </summary>
        public static bool ShouldTrackParentWidth(WpfSkinNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !node.Width.HasValue;
        }

        /// <summary>
        /// ノードに明示 Height が無く、幅から高さを自動計算すべきか。
        /// </summary>
        public static bool ShouldAutoHeight(WpfSkinNode node) =>
            node != null && !node.Height.HasValue;
    }
}
