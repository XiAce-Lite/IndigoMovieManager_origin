using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndigoMovieManager.UserControls;
using WpfToolkit.Controls;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// WpfSkinDefinition から ListView 用の ItemsPanel / ItemTemplate を組み立てる。
    /// カード本体は <see cref="WpfSkinItemPresenter"/> が skin.json のペインツリーを描画する。
    /// </summary>
    internal static class WpfSkinTemplateBuilder
    {
        public sealed class BuildContext
        {
            public ContextMenu ItemContextMenu { get; init; }
            public System.Windows.Input.MouseButtonEventHandler ThumbnailDoubleClick { get; init; }
            public System.Windows.Input.MouseButtonEventHandler ThumbnailMouseDown { get; init; }
            public System.Windows.Input.MouseButtonEventHandler ThumbnailRightDown { get; init; }
            public System.Windows.Data.IValueConverter ImageConverter { get; init; }
            public System.Windows.Data.IValueConverter AspectConverter { get; init; }
            public System.Windows.Data.IValueConverter FileSizeConverter { get; init; }
        }

        public static void ApplyHostContext(BuildContext context)
        {
            WpfSkinHostContext.ItemContextMenu = context.ItemContextMenu;
            WpfSkinHostContext.ThumbnailDoubleClick = context.ThumbnailDoubleClick;
            WpfSkinHostContext.ThumbnailMouseDown = context.ThumbnailMouseDown;
            WpfSkinHostContext.ThumbnailRightDown = context.ThumbnailRightDown;
            WpfSkinHostContext.ImageConverter = context.ImageConverter;
            WpfSkinHostContext.AspectConverter = context.AspectConverter;
            WpfSkinHostContext.FileSizeConverter = context.FileSizeConverter;
        }

        public static ItemsPanelTemplate BuildItemsPanel(WpfSkinDefinition def)
        {
            var panelFactory = new FrameworkElementFactory(typeof(VirtualizingWrapPanel));
            panelFactory.SetValue(VirtualizingWrapPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(VirtualizingWrapPanel.SpacingModeProperty, SpacingMode.Uniform);
            return new ItemsPanelTemplate { VisualTree = panelFactory };
        }

        public static DataTemplate BuildItemTemplate(WpfSkinDefinition def)
        {
            var presenter = new FrameworkElementFactory(typeof(WpfSkinItemPresenter));
            presenter.SetValue(WpfSkinItemPresenter.SkinDefinitionProperty, def);
            return new DataTemplate { VisualTree = presenter };
        }

        public static Brush ParseSurfaceBackground(WpfSkinDefinition def)
        {
            string value = def?.Surface?.Background;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                var brush = (Brush)new BrushConverter().ConvertFromString(value);
                brush?.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
