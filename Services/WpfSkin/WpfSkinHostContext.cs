using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// WPF スキンカード内のイベント・コンバータを MainWindow から共有する。
    /// </summary>
    internal static class WpfSkinHostContext
    {
        public static ContextMenu ItemContextMenu { get; set; }
        public static MouseButtonEventHandler ThumbnailDoubleClick { get; set; }
        public static MouseButtonEventHandler ThumbnailMouseDown { get; set; }
        public static MouseButtonEventHandler ThumbnailRightDown { get; set; }
        public static IValueConverter ImageConverter { get; set; }
        public static IValueConverter AspectConverter { get; set; }
        public static IValueConverter FileSizeConverter { get; set; }

        /// <summary>path/dir/drive リンククリック。引数は MovieRecords と field エイリアス。</summary>
        public static Action<MovieRecords, string> PathLinkClick { get; set; }

        public static IDisposable PushScope(
            ContextMenu itemContextMenu,
            MouseButtonEventHandler thumbnailDoubleClick,
            MouseButtonEventHandler thumbnailMouseDown,
            MouseButtonEventHandler thumbnailRightDown,
            IValueConverter imageConverter,
            IValueConverter aspectConverter,
            IValueConverter fileSizeConverter,
            Action<MovieRecords, string> pathLinkClick = null)
        {
            Snapshot snapshot = Capture();
            ItemContextMenu = itemContextMenu;
            ThumbnailDoubleClick = thumbnailDoubleClick;
            ThumbnailMouseDown = thumbnailMouseDown;
            ThumbnailRightDown = thumbnailRightDown;
            ImageConverter = imageConverter;
            AspectConverter = aspectConverter;
            FileSizeConverter = fileSizeConverter;
            PathLinkClick = pathLinkClick;
            return new RestoreDisposable(snapshot);
        }

        private static Snapshot Capture() =>
            new()
            {
                ItemContextMenu = ItemContextMenu,
                ThumbnailDoubleClick = ThumbnailDoubleClick,
                ThumbnailMouseDown = ThumbnailMouseDown,
                ThumbnailRightDown = ThumbnailRightDown,
                ImageConverter = ImageConverter,
                AspectConverter = AspectConverter,
                FileSizeConverter = FileSizeConverter,
                PathLinkClick = PathLinkClick,
            };

        private sealed class Snapshot
        {
            public ContextMenu ItemContextMenu { get; init; }
            public MouseButtonEventHandler ThumbnailDoubleClick { get; init; }
            public MouseButtonEventHandler ThumbnailMouseDown { get; init; }
            public MouseButtonEventHandler ThumbnailRightDown { get; init; }
            public IValueConverter ImageConverter { get; init; }
            public IValueConverter AspectConverter { get; init; }
            public IValueConverter FileSizeConverter { get; init; }
            public Action<MovieRecords, string> PathLinkClick { get; init; }
        }

        private sealed class RestoreDisposable : IDisposable
        {
            private readonly Snapshot _snapshot;

            public RestoreDisposable(Snapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public void Dispose()
            {
                ItemContextMenu = _snapshot.ItemContextMenu;
                ThumbnailDoubleClick = _snapshot.ThumbnailDoubleClick;
                ThumbnailMouseDown = _snapshot.ThumbnailMouseDown;
                ThumbnailRightDown = _snapshot.ThumbnailRightDown;
                ImageConverter = _snapshot.ImageConverter;
                AspectConverter = _snapshot.AspectConverter;
                FileSizeConverter = _snapshot.FileSizeConverter;
                PathLinkClick = _snapshot.PathLinkClick;
            }
        }
    }
}
