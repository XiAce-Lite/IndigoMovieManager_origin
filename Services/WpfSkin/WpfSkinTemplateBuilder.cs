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
            if (def != null && def.IsList)
            {
                // リスト型は 1 行 1 アイテムで縦に積む（既定 List タブ相当）。
                var stackFactory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
                stackFactory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Vertical);
                return new ItemsPanelTemplate { VisualTree = stackFactory };
            }

            var panelFactory = new FrameworkElementFactory(typeof(VirtualizingWrapPanel));
            panelFactory.SetValue(VirtualizingWrapPanel.OrientationProperty, Orientation.Horizontal);

            if (def?.Card?.Stretch == true)
            {
                // StretchItems: 1 カラムしか入らない幅ではカードをコンテナ幅いっぱいに広げ、
                // 複数カラム入る幅では等幅で並べる（既定 Big / 5x10 タブと同じ）。
                panelFactory.SetValue(VirtualizingWrapPanel.StretchItemsProperty, true);

                // card に width/height 両方が指定されている場合は ItemSize を固定する。
                // 列数は「カードの自然幅」ではなく card.width 基準で決定されるため、
                // 横長カード（BigInfo / WideGridInfo 等）でも長文に引きずられず、
                // ウィンドウ幅に応じて狙いどおりの列数で並ぶ。
                // ※ StretchItems により各カードはスロット幅まで伸びる（arrange 時）。
                double w = def.Card.Width;
                double h = def.Card.Height;
                if (w > 0 && h > 0)
                {
                    panelFactory.SetValue(VirtualizingWrapPanel.ItemSizeProperty, new Size(w, h));
                }
            }
            else
            {
                // Uniform だと余白が均等配分され、1 カラムしか入らない幅でカードが中央寄せになる。
                // BetweenItemsOnly は両端に余白を入れず左寄せになる（既定 Grid タブと同じ挙動）。
                panelFactory.SetValue(VirtualizingWrapPanel.SpacingModeProperty, SpacingMode.BetweenItemsOnly);
            }

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
            string value = WpfSkinColorResolver.ResolveColor(def?.Surface?.Background, def);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return WpfSkinColorResolver.ResolveBrush(value, null, def);
        }
    }
}
