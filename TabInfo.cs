using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
    public class TabInfo
    {
        private int columns = 3;
        private int rows = 1;
        private int width = 120;
        private int height = 90;
        private int divCount;
        private string outPath = "";

        public int Columns => columns;
        public int Rows => rows;
        public int Width => width;
        public int Height => height;
        public int DivCount => divCount;
        public string OutPath => outPath;

        public TabInfo(int tabIndex, string dbName, string thumbFolder = "")
            : this(ThumbnailLayoutSpec.FromTabIndex(tabIndex), dbName, thumbFolder)
        {
        }

        public TabInfo(ThumbnailLayoutSpec spec, string dbName, string thumbFolder = "")
        {
            ThumbnailLayoutSpec resolved = spec ?? ThumbnailLayoutSpec.FromTabIndex(2);
            width = resolved.Width;
            height = resolved.Height;
            columns = resolved.Columns;
            rows = resolved.Rows;
            divCount = resolved.DivCount;
            outPath = resolved.GetOutPath(dbName, thumbFolder);
        }
    }
}
