using System.Collections.ObjectModel;

namespace IndigoMovieManager.ModelViews
{
    public class WatchWindowViewModel
    {
        public ObservableCollection<WatchRecords> WatchRecs { get; set; }

        public WatchWindowViewModel()
        {
            WatchRecs = [];
        }
    }
}
