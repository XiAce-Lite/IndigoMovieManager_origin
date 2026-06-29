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
    }
}
