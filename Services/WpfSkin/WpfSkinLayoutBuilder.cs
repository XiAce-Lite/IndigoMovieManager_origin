using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using IndigoMovieManager.Converter;
using IndigoMovieManager.UserControls;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// skin.json のペインツリーを実際の WPF 要素に組み立てる。
    /// Grid / Stack の入れ子と比率指定に対応する。
    /// </summary>
    internal static class WpfSkinLayoutBuilder
    {
        public static UIElement Build(WpfSkinNode node, WpfSkinDefinition def)
        {
            if (node == null)
            {
                return null;
            }

            UIElement element = node.IsContainer
                ? BuildContainer(node, def)
                : BuildLeaf(node, def);

            return WrapWithChrome(element, node, def);
        }

        /// <summary>
        /// list 型スキンのカラム見出し行を組み立てる。ルートが grid で、子に header が
        /// 1 つ以上あるときのみ生成する（なければ null）。
        /// </summary>
        public static UIElement BuildListHeader(WpfSkinDefinition def)
        {
            WpfSkinNode root = def?.Card?.Layout;
            if (def == null || !def.IsList || root == null || !root.IsGrid || root.Children == null)
            {
                return null;
            }

            if (!root.Children.Any(c => !string.IsNullOrEmpty(c.Header)))
            {
                return null;
            }

            var grid = new Grid
            {
                Background = WpfSkinColorResolver.ResolveBrush("#F0F0F0", null, def)
                    ?? System.Windows.Application.Current?.TryFindResource("ImmListHeaderBackground") as Brush,
            };

            if (def.Card.Width > 0)
            {
                grid.Width = def.Card.Width;
                grid.HorizontalAlignment = HorizontalAlignment.Left;
            }

            if (root.Columns != null)
            {
                foreach (string col in root.Columns)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = WpfSkinGridLengthParser.Parse(col) });
                }
            }

            foreach (WpfSkinNode child in root.Children)
            {
                if (string.IsNullOrEmpty(child.Header))
                {
                    continue;
                }

                var header = new TextBlock
                {
                    Text = child.Header,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Yu Gothic UI"),
                    Foreground = WpfSkinColorResolver.ResolveBrush("#000000", Brushes.Black, def),
                    Padding = child.Padding != null && !child.Padding.IsEmpty
                        ? child.Padding.ToThickness()
                        : new Thickness(4, 3, 4, 3),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(header, child.Col);
                grid.Children.Add(header);
            }

            var border = new Border
            {
                Child = grid,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            return border;
        }

        private static UIElement BuildContainer(WpfSkinNode node, WpfSkinDefinition def)
        {
            if (node.IsGrid)
            {
                return BuildGrid(node, def);
            }

            var panel = new StackPanel
            {
                Orientation = string.Equals(node.Stack, "horizontal", StringComparison.OrdinalIgnoreCase)
                    ? Orientation.Horizontal
                    : Orientation.Vertical,
            };

            ApplyBox(panel, node, skipSize: false, def);

            foreach (WpfSkinNode child in node.Children)
            {
                UIElement childElement = Build(child, def);
                if (childElement != null)
                {
                    panel.Children.Add(childElement);
                }
            }

            return panel;
        }

        private static Grid BuildGrid(WpfSkinNode node, WpfSkinDefinition def)
        {
            var grid = new Grid();
            ApplyBox(grid, node, skipSize: false, def);

            if (node.Rows != null)
            {
                foreach (string row in node.Rows)
                {
                    grid.RowDefinitions.Add(new RowDefinition
                    {
                        Height = WpfSkinGridLengthParser.Parse(row),
                    });
                }
            }

            if (node.Columns != null)
            {
                foreach (string col in node.Columns)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = WpfSkinGridLengthParser.Parse(col),
                    });
                }
            }

            if (grid.RowDefinitions.Count == 0 && node.Children != null)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            if (grid.ColumnDefinitions.Count == 0 && node.Children != null)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            foreach (WpfSkinNode child in node.Children)
            {
                UIElement childElement = Build(child, def);
                if (childElement == null)
                {
                    continue;
                }

                Grid.SetRow(childElement, child.Row);
                Grid.SetColumn(childElement, child.Col);
                if (child.RowSpan > 1)
                {
                    Grid.SetRowSpan(childElement, child.RowSpan);
                }

                if (child.ColSpan > 1)
                {
                    Grid.SetColumnSpan(childElement, child.ColSpan);
                }

                grid.Children.Add(childElement);
            }

            return grid;
        }

        private static UIElement BuildLeaf(WpfSkinNode node, WpfSkinDefinition def)
        {
            return node.Type?.ToLowerInvariant() switch
            {
                "thumbnail" => BuildThumbnail(node, def),
                "tags" => BuildTags(node, def),
                "text" => BuildText(node, def),
                _ => null,
            };
        }

        private static UIElement BuildText(WpfSkinNode node, WpfSkinDefinition def)
        {
            ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, def.Styles);
            var text = new TextBlock
            {
                FontSize = style.FontSize,
                FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = ParseBrush(style.Foreground, Brushes.Black, def),
            };

            if (!string.IsNullOrEmpty(style.FontFamily))
            {
                text.FontFamily = new FontFamily(style.FontFamily);
            }

            // field 未指定で label のみのノードは静的ラベル（バインドしない）。
            // ここでバインドすると既定の Movie_Name が引かれ、全項目にファイル名が混入する。
            if (string.IsNullOrWhiteSpace(node.Field))
            {
                text.Text = node.Label ?? "";
            }
            else
            {
                Binding binding;
                if (string.Equals(node.Format, "filesize", StringComparison.OrdinalIgnoreCase)
                    && WpfSkinHostContext.FileSizeConverter != null)
                {
                    binding = new Binding(GetFieldPath(node.Field))
                    {
                        Converter = WpfSkinHostContext.FileSizeConverter,
                    };
                }
                else
                {
                    binding = new Binding(GetFieldPath(node.Field));
                }

                if (!string.IsNullOrEmpty(node.Label))
                {
                    binding.StringFormat = node.Label + "{0}";
                }

                text.SetBinding(TextBlock.TextProperty, binding);
            }

            if (style.Wrap)
            {
                text.TextWrapping = TextWrapping.Wrap;
            }
            else
            {
                text.TextWrapping = TextWrapping.NoWrap;
                text.TextTrimming = TextTrimming.CharacterEllipsis;
            }

            (TextAlignment alignment, HorizontalAlignment horizontal) = ResolveTextAlignment(style.Align);
            text.TextAlignment = alignment;
            if (string.IsNullOrEmpty(node.HAlign))
            {
                text.HorizontalAlignment = horizontal;
            }

            ApplyBox(text, node, skipSize: false, def);
            return text;
        }

        private static UIElement BuildThumbnail(WpfSkinNode node, WpfSkinDefinition def)
        {
            bool preferJacket = def.Thumbnail?.PreferJacket == true;
            // preferJacket は枠サイズを自前制御するためセル伸縮しない
            bool stretchInCell = !preferJacket
                && !node.Height.HasValue
                && !string.IsNullOrEmpty(node.VAlign)
                && string.Equals(node.VAlign, "stretch", StringComparison.OrdinalIgnoreCase);

            double w = node.Width ?? def.Thumbnail.Width;
            double? h = preferJacket
                ? (node.Height ?? (def.Thumbnail.Height > 0 ? def.Thumbnail.Height : null))
                : (node.Height ?? (stretchInCell ? null : def.Thumbnail.Height));

            var label = new Label
            {
                Background = Brushes.Black,
                Padding = new Thickness(0),
                ClipToBounds = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };

            if (w > 0)
            {
                label.Width = w;
            }

            if (h.HasValue)
            {
                label.Height = h.Value;
            }

            if (WpfSkinHostContext.ThumbnailDoubleClick != null)
            {
                label.MouseDoubleClick += WpfSkinHostContext.ThumbnailDoubleClick;
            }

            if (WpfSkinHostContext.ThumbnailMouseDown != null)
            {
                label.MouseDown += WpfSkinHostContext.ThumbnailMouseDown;
            }

            var image = new Image();
            UIElement content = image;
            if (preferJacket)
            {
                image.Stretch = Stretch.Uniform;

                var loadingBar = new ProgressBar
                {
                    Height = 3,
                    Minimum = 0,
                    Maximum = 1,
                    IsIndeterminate = true,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Visibility = Visibility.Collapsed,
                    Opacity = 0.85,
                };

                double localH = h ?? (def.Thumbnail.Height > 0 ? def.Thumbnail.Height : 0);
                double targetAspect = localH > 0 && w > 0 ? w / localH : def.Thumbnail.TargetAspect;

                PreferJacketImageBehavior.SetHost(image, label);
                PreferJacketImageBehavior.SetFrameWidth(image, w);
                PreferJacketImageBehavior.SetLocalFrameHeight(image, localH);
                PreferJacketImageBehavior.SetTargetAspect(image, targetAspect);
                PreferJacketImageBehavior.SetAspectConverter(image, WpfSkinHostContext.AspectConverter);
                PreferJacketImageBehavior.SetLoadingIndicator(image, loadingBar);
                PreferJacketImageBehavior.SetLocalConverter(image, WpfSkinHostContext.ImageConverter);
                BindingOperations.SetBinding(
                    image,
                    PreferJacketImageBehavior.JacketUrlProperty,
                    new Binding(nameof(MovieRecords.Comment1)));
                BindingOperations.SetBinding(
                    image,
                    PreferJacketImageBehavior.LocalPathProperty,
                    new Binding(nameof(MovieRecords.ThumbPathWpfSkin)));
                BindingOperations.SetBinding(
                    image,
                    PreferJacketImageBehavior.LocalExistsProperty,
                    new Binding(nameof(MovieRecords.IsExists)));

                var overlay = new Grid();
                overlay.Children.Add(image);
                overlay.Children.Add(loadingBar);
                content = overlay;
            }
            else
            {
                var sourceBinding = new MultiBinding
                {
                    Converter = new ThumbSourceAdapter(WpfSkinHostContext.ImageConverter),
                };
                sourceBinding.Bindings.Add(new Binding(nameof(MovieRecords.ThumbPathWpfSkin)));
                sourceBinding.Bindings.Add(new Binding(nameof(MovieRecords.IsExists)));
                image.SetBinding(Image.SourceProperty, sourceBinding);

                double targetAspect = h is > 0 ? w / h.Value : def.Thumbnail.TargetAspect;
                image.SetBinding(Image.StretchProperty, new Binding(nameof(Image.Source))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    Converter = WpfSkinHostContext.AspectConverter,
                    ConverterParameter = targetAspect,
                });
            }

            if (WpfSkinHostContext.ItemContextMenu != null)
            {
                image.ContextMenu = WpfSkinHostContext.ItemContextMenu;
            }

            if (WpfSkinHostContext.ThumbnailRightDown != null)
            {
                image.PreviewMouseRightButtonDown += WpfSkinHostContext.ThumbnailRightDown;
            }

            label.Content = content;
            ApplyBox(label, node, skipSize: true, def);

            if (stretchInCell)
            {
                label.HorizontalAlignment = HorizontalAlignment.Stretch;
                label.VerticalAlignment = VerticalAlignment.Stretch;
                label.Width = double.NaN;
                label.Height = double.NaN;
            }
            else if (preferJacket)
            {
                // ジャケ／ローカルとも枠サイズは Behavior が更新。初期は JSON 幅・高さ。
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.VerticalAlignment = VerticalAlignment.Top;
            }

            return label;
        }

        private static UIElement BuildTags(WpfSkinNode node, WpfSkinDefinition def)
        {
            var items = new ItemsControl();
            items.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MovieRecords.Tag)) { FallbackValue = null });

            var itemTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TagControl));
            factory.SetBinding(FrameworkElement.DataContextProperty, new Binding());
            itemTemplate.VisualTree = factory;
            items.ItemTemplate = itemTemplate;

            var panelTemplate = new ItemsPanelTemplate();
            var wrap = new FrameworkElementFactory(typeof(WrapPanel));
            // 幅指定（width）があると StackPanel 内で「幅固定＋Stretch」となり中央寄せされ、
            // テキスト情報の左位置とタグの左位置がずれる。左寄せに固定して始点を揃える。
            wrap.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            double tagsWidth = node.Width ?? (def.Card.Width > 0 ? def.Card.Width : def.Thumbnail.Width);
            if (tagsWidth > 0)
            {
                wrap.SetValue(FrameworkElement.WidthProperty, tagsWidth);
            }

            if (node.MinHeight.HasValue)
            {
                wrap.SetValue(FrameworkElement.MinHeightProperty, node.MinHeight.Value);
            }

            if (node.MaxHeight.HasValue)
            {
                wrap.SetValue(FrameworkElement.MaxHeightProperty, node.MaxHeight.Value);
            }

            panelTemplate.VisualTree = wrap;
            items.ItemsPanel = panelTemplate;

            ApplyBox(items, node, skipSize: true, def);
            return items;
        }

        private static UIElement WrapWithChrome(UIElement element, WpfSkinNode node, WpfSkinDefinition def)
        {
            if (element == null)
            {
                return null;
            }

            bool hasPadding = node.Padding != null && !node.Padding.IsEmpty;
            bool hasBackground = !string.IsNullOrEmpty(node.Background);
            if (!hasPadding && !hasBackground)
            {
                return element;
            }

            var border = new Border
            {
                Child = element,
            };

            if (hasPadding)
            {
                border.Padding = node.Padding.ToThickness();
            }

            if (hasBackground)
            {
                border.Background = ParseBrush(node.Background, null, def);
            }

            return border;
        }

        private static void ApplyBox(FrameworkElement element, WpfSkinNode node, bool skipSize, WpfSkinDefinition def)
        {
            if (!skipSize)
            {
                if (node.Width.HasValue)
                {
                    element.Width = node.Width.Value;
                }

                if (node.Height.HasValue)
                {
                    element.Height = node.Height.Value;
                }
            }

            if (node.MinWidth.HasValue)
            {
                element.MinWidth = node.MinWidth.Value;
            }

            if (node.MaxWidth.HasValue)
            {
                element.MaxWidth = node.MaxWidth.Value;
            }

            if (node.MinHeight.HasValue)
            {
                element.MinHeight = node.MinHeight.Value;
            }

            if (node.MaxHeight.HasValue)
            {
                element.MaxHeight = node.MaxHeight.Value;
            }

            if (node.Margin != null && !node.Margin.IsEmpty)
            {
                element.Margin = node.Margin.ToThickness();
            }

            if (!string.IsNullOrEmpty(node.VAlign))
            {
                element.VerticalAlignment = ResolveVerticalAlignment(node.VAlign);
            }

            if (!string.IsNullOrEmpty(node.HAlign))
            {
                element.HorizontalAlignment = ResolveHorizontalAlignment(node.HAlign);
            }

            if (element is Panel panel && !string.IsNullOrEmpty(node.Background))
            {
                panel.Background = ParseBrush(node.Background, null, def);
            }
        }

        private static (TextAlignment, HorizontalAlignment) ResolveTextAlignment(string align)
        {
            return align?.ToLowerInvariant() switch
            {
                "center" => (TextAlignment.Center, HorizontalAlignment.Center),
                "right" => (TextAlignment.Right, HorizontalAlignment.Right),
                _ => (TextAlignment.Left, HorizontalAlignment.Left),
            };
        }

        private static VerticalAlignment ResolveVerticalAlignment(string align)
        {
            return align?.ToLowerInvariant() switch
            {
                "center" => VerticalAlignment.Center,
                "bottom" => VerticalAlignment.Bottom,
                "stretch" => VerticalAlignment.Stretch,
                _ => VerticalAlignment.Top,
            };
        }

        private static HorizontalAlignment ResolveHorizontalAlignment(string align)
        {
            return align?.ToLowerInvariant() switch
            {
                "center" => HorizontalAlignment.Center,
                "right" => HorizontalAlignment.Right,
                "stretch" => HorizontalAlignment.Stretch,
                _ => HorizontalAlignment.Left,
            };
        }

        private static string GetFieldPath(string field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return nameof(MovieRecords.Movie_Name);
            }

            return field.ToLowerInvariant() switch
            {
                "title" or "name" => nameof(MovieRecords.Movie_Name),
                "id" or "movieid" => nameof(MovieRecords.Movie_Id),
                "body" => nameof(MovieRecords.Movie_Body),
                "metatitle" => nameof(MovieRecords.Title),
                "path" => nameof(MovieRecords.Movie_Path),
                "length" => nameof(MovieRecords.Movie_Length),
                "size" => nameof(MovieRecords.Movie_Size),
                "filedate" => nameof(MovieRecords.File_Date),
                "registdate" => nameof(MovieRecords.Regist_Date),
                "lastdate" => nameof(MovieRecords.Last_Date),
                "score" => nameof(MovieRecords.Score),
                "viewcount" => nameof(MovieRecords.View_Count),
                "container" => nameof(MovieRecords.Container),
                "video" => nameof(MovieRecords.Video),
                "audio" => nameof(MovieRecords.Audio),
                "ext" => nameof(MovieRecords.Ext),
                "drive" => nameof(MovieRecords.Drive),
                "dir" => nameof(MovieRecords.Dir),
                "artist" => nameof(MovieRecords.Artist),
                "album" => nameof(MovieRecords.Album),
                "genre" => nameof(MovieRecords.Genre),
                "comment1" => nameof(MovieRecords.Comment1),
                "comment2" => nameof(MovieRecords.Comment2),
                "comment3" => nameof(MovieRecords.Comment3),
                _ => field,
            };
        }

        private static Brush ParseBrush(string value, Brush fallback, WpfSkinDefinition def) =>
            WpfSkinColorResolver.ResolveBrush(value, fallback, def);

        private sealed class ThumbSourceAdapter : IMultiValueConverter
        {
            private readonly IValueConverter _inner;

            public ThumbSourceAdapter(IValueConverter inner)
            {
                _inner = inner;
            }

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                if (_inner == null || values == null || values.Length == 0)
                {
                    return Binding.DoNothing;
                }

                return _inner.Convert(values[0], targetType, values.Length > 1 ? values[1] : null, culture);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}
