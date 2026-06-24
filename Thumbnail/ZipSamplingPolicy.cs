namespace IndigoMovieManager.Thumbnail
{
    internal static class ZipSamplingPolicy
    {
        /// <summary>
        /// 動画サムネと同様 k/(N+1) の比率で画像インデックス（0 始まり）を選ぶ。
        /// </summary>
        public static int[] PickIndices(int imageCount, int panelCount)
        {
            if (imageCount <= 0 || panelCount <= 0)
            {
                return [];
            }

            int[] indices = new int[panelCount];
            for (int k = 1; k <= panelCount; k++)
            {
                int index = (int)Math.Round((double)k * imageCount / (panelCount + 1)) - 1;
                if (index < 0)
                {
                    index = 0;
                }

                if (index >= imageCount)
                {
                    index = imageCount - 1;
                }

                indices[k - 1] = index;
            }

            return indices;
        }
    }
}
