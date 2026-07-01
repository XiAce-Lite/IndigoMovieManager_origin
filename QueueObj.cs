namespace IndigoMovieManager
{
    public class QueueObj
    {
        private int _jobId;
        private int _tabIndex;
        private long _movieId;
        private string _movieFullPath;
        private string _dbFullPath;
        private int _workGeneration;
        private int? _thumbPanelPos = null;
        private int? _thumbTimePos = null;
        private bool _isManual;
        private Thumbnail.ThumbnailLayoutSpec _thumbnailLayout;
        private string _lastThumbProgressDetail = "";

        public int JobId { get { return _jobId; } set { _jobId = value; } }
        public bool IsManual { get { return _isManual; } set { _isManual = value; } }
        public int TabIndex { get { return _tabIndex; } set { _tabIndex = value; } }
        public int Tabindex { get { return TabIndex; } set { TabIndex = value; } }
        public long MovieId { get { return _movieId; } set { _movieId = value; } }
        public string MovieFullPath { get { return _movieFullPath; } set { _movieFullPath = value; } }
        public string DbFullPath { get { return _dbFullPath; } set { _dbFullPath = value; } }
        public int WorkGeneration { get { return _workGeneration; } set { _workGeneration = value; } }
        public int? ThumbPanelPos { get { return _thumbPanelPos; } set { _thumbPanelPos = value; } }
        public int? ThumbTimePos { get { return _thumbTimePos; } set { _thumbTimePos = value; } }
        public Thumbnail.ThumbnailLayoutSpec ThumbnailLayout
        {
            get => _thumbnailLayout;
            set => _thumbnailLayout = value;
        }

        /// <summary>直近のサムネ作成進捗表示用（フルパス + バックエンド情報）。</summary>
        public string LastThumbProgressDetail
        {
            get => _lastThumbProgressDetail;
            set => _lastThumbProgressDetail = value ?? "";
        }
    }
}
