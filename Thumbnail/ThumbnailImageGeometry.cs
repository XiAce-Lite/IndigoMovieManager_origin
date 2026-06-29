using OpenCvSharp;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailImageGeometry
    {
        /// <summary>
        /// レガシー 4:3 センタークロップ（ブックマーク等で継続利用）。
        /// </summary>
        public static Rect GetAspect(int imgWidth, int imgHeight)
        {
            int w = imgWidth;
            int h = imgHeight;
            int wdiff = 0;
            int hdiff = 0;

            float aspect = (float)imgWidth / imgHeight;
            if (aspect > 1.34f)
            {
                h = (int)Math.Floor((decimal)imgHeight / 3);
                w = (int)Math.Floor((decimal)h * 4);
                h = imgHeight;
                wdiff = (imgWidth - w) / 2;
                hdiff = 0;
            }

            if (aspect < 1.33f)
            {
                w = (int)Math.Floor((decimal)imgWidth / 4);
                h = (int)Math.Floor((decimal)w * 3);
                w = imgWidth;
                hdiff = (imgHeight - h) / 2;
                wdiff = 0;
            }

            return new Rect(wdiff, hdiff, w, h);
        }

        /// <summary>
        /// フレームをパネルサイズ（TabInfo / JSON 定義）に合わせる。
        /// 常にパネル比へセンタークロップして全面を埋める（黒帯は焼き込まない）。
        /// 4:3 パネル（Small〜List 互換）なら 4:3 を、16:9 パネルなら 16:9 を維持する。
        /// </summary>
        public static Mat FitFrameToPanel(
            Mat source,
            int panelWidth,
            int panelHeight)
        {
            if (source == null || source.Empty() || panelWidth < 1 || panelHeight < 1)
            {
                return new Mat();
            }

            double targetAspect = (double)panelWidth / panelHeight;

            Rect cropRect = GetCenterCropRect(source.Width, source.Height, targetAspect);
            using Mat cropped = new(source, cropRect);
            var result = new Mat(panelHeight, panelWidth, MatType.CV_8UC3, Scalar.Black);
            Cv2.Resize(cropped, result, new Size(panelWidth, panelHeight));
            return result;
        }

        private static Rect GetCenterCropRect(int width, int height, double targetAspect)
        {
            double sourceAspect = (double)width / height;
            int cropW;
            int cropH;

            if (sourceAspect > targetAspect)
            {
                cropH = height;
                cropW = Math.Max(1, (int)Math.Round(height * targetAspect));
            }
            else
            {
                cropW = width;
                cropH = Math.Max(1, (int)Math.Round(width / targetAspect));
            }

            int x = Math.Max(0, (width - cropW) / 2);
            int y = Math.Max(0, (height - cropH) / 2);
            return new Rect(x, y, cropW, cropH);
        }
    }
}
