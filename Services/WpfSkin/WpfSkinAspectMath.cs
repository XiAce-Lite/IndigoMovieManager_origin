namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// サムネ枠のアスペクト計算。高さは切り上げで統一する
    /// （360@16:9 → 203。Math.Round の銀行家丸めだと 202 になり再生成が発生する）。
    /// </summary>
    public static class WpfSkinAspectMath
    {
        /// <summary>width × (rh/rw) を切り上げた高さ。</summary>
        public static int HeightFromWidth(int width, int ratioW, int ratioH)
        {
            if (width < 1 || ratioW < 1 || ratioH < 1)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Ceiling(width * (double)ratioH / ratioW));
        }

        /// <summary>height × (rw/rh) を切り上げた幅。</summary>
        public static int WidthFromHeight(int height, int ratioW, int ratioH)
        {
            if (height < 1 || ratioW < 1 || ratioH < 1)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Ceiling(height * (double)ratioW / ratioH));
        }
    }
}
