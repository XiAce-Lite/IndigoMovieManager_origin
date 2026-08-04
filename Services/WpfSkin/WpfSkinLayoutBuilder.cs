using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using IndigoMovieManager.Converter;
using IndigoMovieManager.Services.WpfSkin.Design;
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

            element = WrapWithChrome(element, node, def);
            return WpfSkinDesignSession.Wrap(element, node);
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
                    FontFamily = new FontFamily(WpfSkinFontResolver.DefaultFontFamily),
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

            bool horizontal = string.Equals(node.Stack, "horizontal", StringComparison.OrdinalIgnoreCase);
            bool designChrome = Design.WpfSkinDesignSession.Active && Design.WpfSkinDesignSession.ShowDesignChrome;
            bool fillCardHeight = def?.Card?.Height > 0;

            // デザイン用スプリッター付き、またはカード高さ固定時の縦積み追従
            if (Design.WpfSkinDesignSession.Active
                && node.Children != null
                && node.Children.Count > 0
                && (designChrome && node.Children.Count > 1 || (!horizontal && fillCardHeight)))
            {
                return BuildStackAsGrid(node, def, horizontal, withSplitters: designChrome && node.Children.Count > 1);
            }

            var panel = new StackPanel
            {
                Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            };

            ApplyBox(panel, node, skipSize: false, def);
            if (fillCardHeight)
            {
                panel.VerticalAlignment = VerticalAlignment.Stretch;
            }

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

        /// <summary>
        /// デザインモード専用。vertical/horizontal Stack を Grid に変換し、
        /// 子要素間にスプリッターを挿入する（withSplitters）。
        /// Stack は親が固定サイズではないため、スプリッターは「直前の子の幅/高さ」だけを
        /// 変更する（隣接から奪い合う Grid 方式だと内容の最小サイズでほぼ動かない）。
        /// </summary>
        private static UIElement BuildStackAsGrid(WpfSkinNode node, WpfSkinDefinition def, bool horizontal, bool withSplitters)
        {
            const double splitterSize = 8;
            var grid = new Grid();
            ApplyBox(grid, node, skipSize: false, def);
            bool fillCardHeight = def?.Card?.Height > 0;

            int childCount = node.Children.Count;

            if (horizontal)
            {
                // カード全幅に追従させる（Auto だと内容幅で縮み、余りが埋らない）
                grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                if (fillCardHeight)
                {
                    grid.VerticalAlignment = VerticalAlignment.Stretch;
                }

                for (int i = 0; i < childCount; i++)
                {
                    bool hasFixedWidth = node.Children[i].Width.HasValue && node.Children[i].Width.Value > 0;
                    // 最後の子は常に残り幅を吸収（*）。途中は Width 指定があれば Pixel、なければ *
                    bool isLast = i == childCount - 1;
                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = !isLast && hasFixedWidth
                            ? new GridLength(node.Children[i].Width.Value, GridUnitType.Pixel)
                            : new GridLength(1, GridUnitType.Star),
                        MinWidth = 8,
                    });
                    if (withSplitters && i < childCount - 1)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition
                        {
                            Width = new GridLength(splitterSize, GridUnitType.Pixel),
                            MinWidth = splitterSize,
                        });
                    }
                }

                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = fillCardHeight
                        ? new GridLength(1, GridUnitType.Star)
                        : GridLength.Auto,
                });

                for (int i = 0; i < childCount; i++)
                {
                    UIElement childElement = Build(node.Children[i], def);
                    if (childElement != null)
                    {
                        var host = new Border
                        {
                            Child = childElement,
                            ClipToBounds = true,
                            Background = Brushes.Transparent,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = fillCardHeight
                                ? VerticalAlignment.Stretch
                                : VerticalAlignment.Top,
                        };
                        Grid.SetColumn(host, withSplitters ? i * 2 : i);
                        grid.Children.Add(host);
                    }

                    if (withSplitters && i < childCount - 1)
                    {
                        int splitterCol = i * 2 + 1;
                        bool leftFixed = node.Children[i].Width.HasValue && node.Children[i].Width.Value > 0;
                        bool rightIsLast = i + 1 == childCount - 1;
                        bool rightFixed = !rightIsLast
                            && node.Children[i + 1].Width.HasValue
                            && node.Children[i + 1].Width.Value > 0;
                        string leftLabel = leftFixed ? "固定" : "残り";
                        string rightLabel = rightIsLast || !rightFixed ? "残り" : "固定";
                        var splitter = MakeSplitter(
                            isHorizontalResize: true,
                            tooltip: $"要素 {i + 1}（{leftLabel}）／{i + 2}（{rightLabel}）の幅境界");
                        Panel.SetZIndex(splitter, 200);
                        splitter.DragStarted += (_, _) =>
                        {
                            Design.WpfSkinDesignSession.OnColumnResizeStarted?.Invoke(node);
                            PrepareAdjacentColumnsForResize(grid, splitterCol);
                        };
                        splitter.DragDelta += (_, e) =>
                            ApplyColumnResizeDelta(grid, splitterCol, e.HorizontalChange);
                        splitter.DragCompleted += (_, _) => SyncStackChildWidths(grid, node);
                        Grid.SetColumn(splitter, splitterCol);
                        grid.Children.Add(splitter);
                    }
                }
            }
            else
            {
                if (fillCardHeight)
                {
                    grid.VerticalAlignment = VerticalAlignment.Stretch;
                    grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                }

                for (int i = 0; i < childCount; i++)
                {
                    bool isLast = i == childCount - 1;
                    bool hasFixedHeight = node.Children[i].Height.HasValue && node.Children[i].Height.Value > 0;
                    // カード高さ固定時は最終行が余りを吸収して中身が縦に伸びる
                    GridLength rowHeight = isLast && fillCardHeight
                        ? new GridLength(1, GridUnitType.Star)
                        : (hasFixedHeight
                            ? new GridLength(node.Children[i].Height.Value, GridUnitType.Pixel)
                            : GridLength.Auto);
                    grid.RowDefinitions.Add(new RowDefinition
                    {
                        Height = rowHeight,
                        MinHeight = 8,
                    });
                    if (withSplitters && i < childCount - 1)
                    {
                        grid.RowDefinitions.Add(new RowDefinition
                        {
                            Height = new GridLength(splitterSize, GridUnitType.Pixel),
                            MinHeight = splitterSize,
                        });
                    }
                }

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int i = 0; i < childCount; i++)
                {
                    UIElement childElement = Build(node.Children[i], def);
                    if (childElement != null)
                    {
                        var host = new Border
                        {
                            Child = childElement,
                            ClipToBounds = true,
                            Background = Brushes.Transparent,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = fillCardHeight && i == childCount - 1
                                ? VerticalAlignment.Stretch
                                : VerticalAlignment.Top,
                        };
                        Grid.SetRow(host, withSplitters ? i * 2 : i);
                        grid.Children.Add(host);
                    }

                    if (withSplitters && i < childCount - 1)
                    {
                        int splitterRow = i * 2 + 1;
                        var splitter = MakeSplitter(
                            isHorizontalResize: false,
                            tooltip: $"要素 {i + 1} の高さをドラッグして調整");
                        Panel.SetZIndex(splitter, 200);
                        splitter.DragStarted += (_, _) =>
                        {
                            Design.WpfSkinDesignSession.OnColumnResizeStarted?.Invoke(node);
                            PrepareStackRowForResize(grid, splitterRow);
                        };
                        splitter.DragDelta += (_, e) =>
                            ApplyStackRowResizeDelta(grid, splitterRow, e.VerticalChange);
                        splitter.DragCompleted += (_, _) => SyncStackChildHeights(grid, node);
                        Grid.SetRow(splitter, splitterRow);
                        grid.Children.Add(splitter);
                    }
                }
            }

            return grid;
        }

        private static Grid BuildGrid(WpfSkinNode node, WpfSkinDefinition def)
        {
            var grid = new Grid();
            ApplyBox(grid, node, skipSize: false, def);

            bool designMode = Design.WpfSkinDesignSession.Active && Design.WpfSkinDesignSession.ShowDesignChrome;
            bool fillCardHeight = def?.Card?.Height > 0;
            const double splitterSize = 6;

            if (fillCardHeight)
            {
                grid.VerticalAlignment = VerticalAlignment.Stretch;
            }

            // カード／親セルの全幅を使う（* 列が効くようにする）
            if (node.Columns != null && node.Columns.Count > 0)
            {
                grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            // ── 行定義 ──
            if (node.Rows != null)
            {
                for (int ri = 0; ri < node.Rows.Count; ri++)
                {
                    grid.RowDefinitions.Add(new RowDefinition
                    {
                        Height = WpfSkinGridLengthParser.Parse(node.Rows[ri]),
                    });
                    if (designMode && ri < node.Rows.Count - 1)
                    {
                        grid.RowDefinitions.Add(new RowDefinition
                        {
                            Height = new GridLength(splitterSize, GridUnitType.Pixel),
                            MinHeight = splitterSize,
                        });
                    }
                }
            }

            // ── 列定義 ──
            if (node.Columns != null)
            {
                for (int ci = 0; ci < node.Columns.Count; ci++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = WpfSkinGridLengthParser.Parse(node.Columns[ci]),
                    });
                    if (designMode && ci < node.Columns.Count - 1)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition
                        {
                            Width = new GridLength(splitterSize, GridUnitType.Pixel),
                            MinWidth = splitterSize,
                        });
                    }
                }
            }

            if (grid.RowDefinitions.Count == 0 && node.Children != null)
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = fillCardHeight
                        ? new GridLength(1, GridUnitType.Star)
                        : GridLength.Auto,
                });
            }

            if (grid.ColumnDefinitions.Count == 0 && node.Children != null)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            // カード高さ固定で * 行が無いとき、最終データ行を * にして余りを吸収（JSON の rows は触らない）
            if (fillCardHeight && grid.RowDefinitions.Count > 0)
            {
                bool hasStar = false;
                for (int i = 0; i < grid.RowDefinitions.Count; i++)
                {
                    if (grid.RowDefinitions[i].Height.IsStar)
                    {
                        hasStar = true;
                        break;
                    }
                }

                if (!hasStar)
                {
                    int lastDataIdx = designMode && node.Rows != null && node.Rows.Count > 1
                        ? (node.Rows.Count - 1) * 2
                        : grid.RowDefinitions.Count - 1;
                    if (lastDataIdx >= 0 && lastDataIdx < grid.RowDefinitions.Count)
                    {
                        grid.RowDefinitions[lastDataIdx].Height = new GridLength(1, GridUnitType.Star);
                    }
                }
            }

            // ── 子要素（デザインモードは行/列インデックスを 2 倍オフセット）──
            int rowMult = designMode && node.Rows != null && node.Rows.Count > 1 ? 2 : 1;
            int colMult = designMode && node.Columns != null && node.Columns.Count > 1 ? 2 : 1;

            foreach (WpfSkinNode child in node.Children)
            {
                UIElement childElement = Build(child, def);
                if (childElement == null)
                {
                    continue;
                }

                int gridRow = child.Row * rowMult;
                int gridCol = child.Col * colMult;
                Grid.SetRow(childElement, gridRow);
                Grid.SetColumn(childElement, gridCol);

                if (child.RowSpan > 1)
                {
                    // span の範囲内にスプリッター行も含まれる
                    Grid.SetRowSpan(childElement, child.RowSpan * rowMult - (rowMult - 1));
                }

                if (child.ColSpan > 1)
                {
                    Grid.SetColumnSpan(childElement, child.ColSpan * colMult - (colMult - 1));
                }

                grid.Children.Add(childElement);
            }

            // ── デザインモード: スプリッターを挿入 ──
            if (designMode)
            {
                int totalRows = grid.RowDefinitions.Count;
                int totalCols = grid.ColumnDefinitions.Count;

                // 列スプリッター（列境界ごと）＋ Fixed/Fill バッジ
                if (node.Columns != null && node.Columns.Count > 1)
                {
                    for (int ci = 0; ci < node.Columns.Count; ci++)
                    {
                        AddColumnConstraintBadge(grid, node, ci, totalRows);
                    }

                    for (int ci = 0; ci < node.Columns.Count - 1; ci++)
                    {
                        int splitterCol = ci * 2 + 1;
                        string left = FormatConstraintShort(node.Columns[ci]);
                        string right = FormatConstraintShort(node.Columns[ci + 1]);
                        var splitter = MakeSplitter(isHorizontalResize: true,
                            tooltip: $"列 {ci + 1}（{left}）／列 {ci + 2}（{right}）の境界をドラッグ");
                        splitter.Tag = ConstraintSplitterTag(ci);
                        splitter.DragStarted += (_, _) =>
                        {
                            Design.WpfSkinDesignSession.OnColumnResizeStarted?.Invoke(node);
                            PrepareAdjacentColumnsForResize(grid, splitterCol);
                        };
                        splitter.DragDelta += (_, e) => ApplyColumnResizeDelta(grid, splitterCol, e.HorizontalChange);
                        Grid.SetColumn(splitter, splitterCol);
                        Grid.SetRowSpan(splitter, totalRows);
                        splitter.DragCompleted += (_, _) => SyncGridDimensions(grid, node);
                        grid.Children.Add(splitter);
                    }
                }

                // 行スプリッター（行境界ごと）
                if (node.Rows != null && node.Rows.Count > 1)
                {
                    for (int ri = 0; ri < node.Rows.Count - 1; ri++)
                    {
                        int splitterRow = ri * 2 + 1;
                        string top = FormatConstraintShort(node.Rows[ri]);
                        string bottom = FormatConstraintShort(node.Rows[ri + 1]);
                        var splitter = MakeSplitter(isHorizontalResize: false,
                            tooltip: $"行 {ri + 1}（{top}）／行 {ri + 2}（{bottom}）の境界をドラッグ");
                        splitter.DragStarted += (_, _) =>
                        {
                            Design.WpfSkinDesignSession.OnColumnResizeStarted?.Invoke(node);
                            PrepareAdjacentRowsForResize(grid, splitterRow);
                        };
                        splitter.DragDelta += (_, e) => ApplyRowResizeDelta(grid, splitterRow, e.VerticalChange);
                        Grid.SetRow(splitter, splitterRow);
                        Grid.SetColumnSpan(splitter, totalCols);
                        splitter.DragCompleted += (_, _) => SyncGridDimensions(grid, node);
                        grid.Children.Add(splitter);
                    }
                }
            }

            return grid;
        }

        private const string ConstraintBadgeTagPrefix = "ConstraintBadge:";
        private const string ConstraintSplitterTagPrefix = "ConstraintSplitter:";

        private static string ConstraintBadgeTag(int dataColIndex) => ConstraintBadgeTagPrefix + dataColIndex;

        private static string ConstraintSplitterTag(int boundaryIndex) => ConstraintSplitterTagPrefix + boundaryIndex;

        private static string FormatConstraintShort(string constraint)
        {
            if (string.IsNullOrWhiteSpace(constraint))
            {
                return "自動";
            }

            string t = constraint.Trim();
            if (t.EndsWith('*') || string.Equals(t, "*", StringComparison.Ordinal))
            {
                return "残り";
            }

            if (string.Equals(t, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "自動";
            }

            return "固定";
        }

        private static void AddColumnConstraintBadge(Grid grid, WpfSkinNode node, int dataColIndex, int totalRows)
        {
            string label = FormatConstraintShort(node.Columns[dataColIndex]);
            // 残りは *、固定は数値。見た目で区別しやすい短いバッジ
            var badge = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xE0, 0x15, 0x65, 0xC0)),
                Background = new SolidColorBrush(Color.FromArgb(0xA0, 0xE3, 0xF2, 0xFD)),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(2, 2, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Tag = ConstraintBadgeTag(dataColIndex),
                ToolTip = dataColIndex == node.Columns.Count - 1
                    ? "残り（*）: カード幅の余りを吸収"
                    : $"{label}: スプリッターで幅を変えられます",
            };
            Panel.SetZIndex(badge, 150);
            Grid.SetColumn(badge, dataColIndex * 2);
            if (totalRows > 0)
            {
                Grid.SetRow(badge, 0);
            }

            grid.Children.Add(badge);
        }

        private static void RefreshColumnConstraintChrome(Grid grid, WpfSkinNode node)
        {
            if (node.Columns == null || node.Columns.Count == 0)
            {
                return;
            }

            foreach (UIElement child in grid.Children)
            {
                if (child is TextBlock badge
                    && badge.Tag is string tag
                    && tag.StartsWith(ConstraintBadgeTagPrefix, StringComparison.Ordinal)
                    && int.TryParse(tag.AsSpan(ConstraintBadgeTagPrefix.Length), out int colIdx)
                    && colIdx >= 0
                    && colIdx < node.Columns.Count)
                {
                    string label = FormatConstraintShort(node.Columns[colIdx]);
                    badge.Text = label;
                    badge.ToolTip = colIdx == node.Columns.Count - 1
                        ? "残り（*）: カード幅の余りを吸収"
                        : $"{label}: スプリッターで幅を変えられます";
                }

                if (child is Thumb splitter
                    && splitter.Tag is string sTag
                    && sTag.StartsWith(ConstraintSplitterTagPrefix, StringComparison.Ordinal)
                    && int.TryParse(sTag.AsSpan(ConstraintSplitterTagPrefix.Length), out int boundary)
                    && boundary >= 0
                    && boundary < node.Columns.Count - 1)
                {
                    string left = FormatConstraintShort(node.Columns[boundary]);
                    string right = FormatConstraintShort(node.Columns[boundary + 1]);
                    splitter.ToolTip = $"列 {boundary + 1}（{left}）／列 {boundary + 2}（{right}）の境界をドラッグ";
                }
            }
        }

        private static Thumb MakeSplitter(bool isHorizontalResize, string tooltip)
        {
            if (isHorizontalResize)
            {
                return new Thumb
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x88, 0xE5)),
                    Cursor = Cursors.SizeWE,
                    ToolTip = tooltip,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
            }
            else
            {
                return new Thumb
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x88, 0xE5)),
                    Cursor = Cursors.SizeNS,
                    ToolTip = tooltip,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
            }
        }

        private static void PrepareAdjacentColumnsForResize(Grid grid, int splitterCol)
        {
            if (splitterCol <= 0 || splitterCol >= grid.ColumnDefinitions.Count - 1)
            {
                return;
            }

            ColumnDefinition previous = grid.ColumnDefinitions[splitterCol - 1];
            ColumnDefinition next = grid.ColumnDefinitions[splitterCol + 1];
            if (previous.ActualWidth > 0)
            {
                previous.Width = new GridLength(previous.ActualWidth, GridUnitType.Pixel);
            }

            if (next.ActualWidth > 0)
            {
                next.Width = new GridLength(next.ActualWidth, GridUnitType.Pixel);
            }
        }

        private static void ApplyColumnResizeDelta(Grid grid, int splitterCol, double delta)
        {
            if (splitterCol <= 0 || splitterCol >= grid.ColumnDefinitions.Count - 1)
            {
                return;
            }

            ColumnDefinition previous = grid.ColumnDefinitions[splitterCol - 1];
            ColumnDefinition next = grid.ColumnDefinitions[splitterCol + 1];
            double previousWidth = previous.ActualWidth;
            double nextWidth = next.ActualWidth;
            if (previousWidth <= 0 || nextWidth <= 0)
            {
                return;
            }

            const double minSize = 24;
            const double snap = 8;
            double total = previousWidth + nextWidth;
            double newPrevious = Math.Max(minSize, previousWidth + delta);
            double newNext = Math.Max(minSize, total - newPrevious);
            newPrevious = total - newNext;
            newPrevious = SnapSize(newPrevious, snap, minSize, total - minSize);
            newNext = total - newPrevious;

            previous.Width = new GridLength(newPrevious, GridUnitType.Pixel);
            next.Width = new GridLength(newNext, GridUnitType.Pixel);
        }

        private static double SnapSize(double value, double snap, double min, double max)
        {
            double snapped = Math.Round(value / snap) * snap;
            return Math.Clamp(snapped, min, max);
        }

        private static void PrepareAdjacentRowsForResize(Grid grid, int splitterRow)
        {
            if (splitterRow <= 0 || splitterRow >= grid.RowDefinitions.Count - 1)
            {
                return;
            }

            RowDefinition previous = grid.RowDefinitions[splitterRow - 1];
            RowDefinition next = grid.RowDefinitions[splitterRow + 1];
            if (previous.ActualHeight > 0)
            {
                previous.Height = new GridLength(previous.ActualHeight, GridUnitType.Pixel);
            }

            if (next.ActualHeight > 0)
            {
                next.Height = new GridLength(next.ActualHeight, GridUnitType.Pixel);
            }
        }

        /// <summary>Stack 縦並び: スプリッター直前の行だけ Pixel 化する。</summary>
        private static void PrepareStackRowForResize(Grid grid, int splitterRow)
        {
            int prevIdx = splitterRow - 1;
            if (prevIdx < 0 || prevIdx >= grid.RowDefinitions.Count)
            {
                return;
            }

            RowDefinition previous = grid.RowDefinitions[prevIdx];
            double h = previous.Height.IsAbsolute && previous.Height.Value > 0
                ? previous.Height.Value
                : previous.ActualHeight;
            if (h > 0)
            {
                previous.Height = new GridLength(h, GridUnitType.Pixel);
            }
        }

        /// <summary>Stack 縦並び: 直前の子の高さだけ増減（全体高さが伸び縮みする）。</summary>
        private static void ApplyStackRowResizeDelta(Grid grid, int splitterRow, double delta)
        {
            int prevIdx = splitterRow - 1;
            if (prevIdx < 0 || prevIdx >= grid.RowDefinitions.Count)
            {
                return;
            }

            RowDefinition previous = grid.RowDefinitions[prevIdx];
            double height = previous.Height.IsAbsolute && previous.Height.Value > 0
                ? previous.Height.Value
                : previous.ActualHeight;
            if (height <= 0)
            {
                return;
            }

            previous.Height = new GridLength(SnapSize(Math.Max(8, height + delta), 8, 8, 4000), GridUnitType.Pixel);
        }

        private static void ApplyRowResizeDelta(Grid grid, int splitterRow, double delta)
        {
            if (splitterRow <= 0 || splitterRow >= grid.RowDefinitions.Count - 1)
            {
                return;
            }

            RowDefinition previous = grid.RowDefinitions[splitterRow - 1];
            RowDefinition next = grid.RowDefinitions[splitterRow + 1];
            double previousHeight = previous.ActualHeight;
            double nextHeight = next.ActualHeight;
            if (previousHeight <= 0 || nextHeight <= 0)
            {
                return;
            }

            const double minSize = 24;
            const double snap = 8;
            double total = previousHeight + nextHeight;
            double newPrevious = Math.Max(minSize, previousHeight + delta);
            double newNext = Math.Max(minSize, total - newPrevious);
            newPrevious = total - newNext;
            newPrevious = SnapSize(newPrevious, snap, minSize, total - minSize);
            newNext = total - newPrevious;

            previous.Height = new GridLength(newPrevious, GridUnitType.Pixel);
            next.Height = new GridLength(newNext, GridUnitType.Pixel);
        }

        private static void SyncStackChildWidths(Grid grid, WpfSkinNode node)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            bool changed = false;
            int last = node.Children.Count - 1;
            for (int i = 0; i < node.Children.Count; i++)
            {
                int defIdx = i * 2;
                if (defIdx >= grid.ColumnDefinitions.Count)
                {
                    break;
                }

                // 最後の子は残り幅（*）を持たせる。固定 Width は外す。
                if (i == last)
                {
                    if (node.Children[i].Width != null)
                    {
                        node.Children[i].Width = null;
                        changed = true;
                    }

                    grid.ColumnDefinitions[defIdx].Width = new GridLength(1, GridUnitType.Star);
                    continue;
                }

                double width = ReadPixelWidth(grid.ColumnDefinitions[defIdx]);
                if (double.IsNaN(width) || width < 1)
                {
                    continue;
                }

                double rounded = Math.Round(width);
                grid.ColumnDefinitions[defIdx].Width = new GridLength(rounded, GridUnitType.Pixel);
                if (node.Children[i].Width != rounded)
                {
                    node.Children[i].Width = rounded;
                    changed = true;
                }
            }

            // 最後を * にしただけでも、手前の Width 確定は保存対象
            if (!changed && last >= 0)
            {
                changed = true;
            }

            grid.InvalidateMeasure();
            grid.UpdateLayout();

            if (changed)
            {
                Design.WpfSkinDesignSession.OnColumnResized?.Invoke(node);
            }
        }

        private static void SyncStackChildHeights(Grid grid, WpfSkinNode node)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < node.Children.Count; i++)
            {
                int defIdx = i * 2;
                if (defIdx >= grid.RowDefinitions.Count)
                {
                    break;
                }

                RowDefinition def = grid.RowDefinitions[defIdx];
                if (!def.Height.IsAbsolute || def.Height.Value < 1)
                {
                    continue;
                }

                double rounded = Math.Round(def.Height.Value);
                if (node.Children[i].Height != rounded)
                {
                    node.Children[i].Height = rounded;
                    changed = true;
                }
            }

            if (changed)
            {
                Design.WpfSkinDesignSession.OnColumnResized?.Invoke(node);
            }
        }

        /// <summary>
        /// GridSplitter ドラッグ完了後、Grid の実幅/高をスキャンして node.Rows / node.Columns に書き戻す。
        /// スプリッター専用行/列（奇数インデックス）はスキップする。
        /// </summary>
        private static void SyncGridDimensions(Grid grid, WpfSkinNode node)
        {
            bool changed = false;

            // 列幅の同期（スプリッター列は偶数インデックスだけ）
            // カード全幅を使い切るため、最後の列は常に *（残り吸収）、手前は Pixel。
            if (node.Columns != null && node.Columns.Count > 0)
            {
                int dataColCount = node.Columns.Count;
                for (int i = 0; i < dataColCount; i++)
                {
                    int defIdx = i * 2;
                    if (defIdx >= grid.ColumnDefinitions.Count)
                    {
                        break;
                    }

                    if (i == dataColCount - 1)
                    {
                        if (node.Columns[i] != "*")
                        {
                            node.Columns[i] = "*";
                            changed = true;
                        }

                        grid.ColumnDefinitions[defIdx].Width = new GridLength(1, GridUnitType.Star);
                        continue;
                    }

                    double w = ReadPixelWidth(grid.ColumnDefinitions[defIdx]);
                    if (double.IsNaN(w) || w < 1)
                    {
                        continue;
                    }

                    double snapped = SnapSize(Math.Round(w), 8, 24, 4000);
                    string newVal = ((int)snapped).ToString();
                    grid.ColumnDefinitions[defIdx].Width = new GridLength(snapped, GridUnitType.Pixel);
                    if (node.Columns[i] != newVal)
                    {
                        node.Columns[i] = newVal;
                        changed = true;
                    }
                }

                // 最後を * に揃えただけでも Dirty にしたい
                if (!changed && dataColCount > 1)
                {
                    changed = true;
                }

                // 列幅確定後にサムネ等の SizeChanged を走らせる
                grid.InvalidateMeasure();
                grid.UpdateLayout();

                // サムネノードに残っている固定 width を列幅へ揃え、JSON と表示を一致させる
                // （生成用 thumbnail.width は触らない）
                if (SyncThumbnailNodeWidthsFromColumns(node))
                {
                    changed = true;
                }

                RefreshColumnConstraintChrome(grid, node);
            }

            // 行高の同期
            if (node.Rows != null)
            {
                int dataRowCount = node.Rows.Count;
                for (int i = 0; i < dataRowCount; i++)
                {
                    int defIdx = i * 2;
                    if (defIdx >= grid.RowDefinitions.Count) break;
                    double h = ReadPixelHeight(grid.RowDefinitions[defIdx]);
                    if (double.IsNaN(h) || h < 1) continue;
                    string newVal = ((int)Math.Round(h)).ToString();
                    if (node.Rows[i] != newVal)
                    {
                        node.Rows[i] = newVal;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Design.WpfSkinDesignSession.OnColumnResized?.Invoke(node);
            }
        }

        /// <summary>
        /// grid 列の Pixel 幅に、その列にある thumbnail ノードの width を合わせる。
        /// </summary>
        private static bool SyncThumbnailNodeWidthsFromColumns(WpfSkinNode gridNode)
        {
            if (gridNode?.Children == null || gridNode.Columns == null)
            {
                return false;
            }

            bool changed = false;
            foreach (WpfSkinNode child in gridNode.Children)
            {
                if (!string.Equals(child.Type, "thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int col = child.Col;
                if (col < 0 || col >= gridNode.Columns.Count)
                {
                    continue;
                }

                string colDef = gridNode.Columns[col]?.Trim() ?? "";
                if (colDef.EndsWith('*') || string.Equals(colDef, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (child.Width != null)
                    {
                        child.Width = null;
                        changed = true;
                    }

                    continue;
                }

                if (!double.TryParse(colDef, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double px)
                    || px < 1)
                {
                    continue;
                }

                double rounded = Math.Round(px);
                if (child.Width != rounded)
                {
                    child.Width = rounded;
                    changed = true;
                }
            }

            return changed;
        }

        private static double ReadPixelWidth(ColumnDefinition definition)
        {
            if (definition.Width.IsAbsolute && definition.Width.Value > 0)
            {
                return definition.Width.Value;
            }

            return definition.ActualWidth;
        }

        private static double ReadPixelHeight(RowDefinition definition)
        {
            if (definition.Height.IsAbsolute && definition.Height.Value > 0)
            {
                return definition.Height.Value;
            }

            return definition.ActualHeight;
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
                text.FontFamily = new FontFamily(
                    WpfSkinFontResolver.ResolveFamilyName(style.FontFamily));
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
                // 縦 Stack は子を無限幅で測るため、親の実幅を MaxWidth に載せて折り返す
                if (!node.Width.HasValue)
                {
                    text.HorizontalAlignment = HorizontalAlignment.Stretch;
                    AttachParentConstrainedMaxWidth(text);
                }
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

            if (ShouldRenderAsLink(node))
            {
                text.TextDecorations = TextDecorations.Underline;
                text.Cursor = Cursors.Hand;
                if (text.Foreground == Brushes.Black || Equals(text.Foreground, Brushes.Black))
                {
                    text.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
                }

                string fieldAlias = node.Field;
                text.MouseLeftButtonUp += (_, e) =>
                {
                    if (text.DataContext is MovieRecords mv)
                    {
                        WpfSkinHostContext.PathLinkClick?.Invoke(mv, fieldAlias);
                        e.Handled = true;
                    }
                };
            }

            return text;
        }

        private static bool ShouldRenderAsLink(WpfSkinNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.Link == false)
            {
                return false;
            }

            if (node.Link == true)
            {
                return true;
            }

            return WpfSkinFieldCatalog.IsPathField(node.Field);
        }

        private static UIElement BuildThumbnail(WpfSkinNode node, WpfSkinDefinition def)
        {
            string nodeSource = node?.Source?.Trim().ToLowerInvariant() ?? "";
            bool hasNodeSource = nodeSource is "local" or "comment1";

            if (WpfSkinThumbnailSources.TryGetRenderKinds(def, out IReadOnlyList<string> sourceKinds))
            {
                if (hasNodeSource)
                {
                    IReadOnlyList<string> filtered = [.. sourceKinds.Where(k =>
                        string.Equals(k, nodeSource, StringComparison.OrdinalIgnoreCase))];
                    // sources に無くてもノード source を優先（編集で枠だけ置いた場合）
                    sourceKinds = filtered.Count > 0 ? filtered : [nodeSource];
                }

                return BuildThumbnailFromSources(node, def, sourceKinds);
            }

            // thumbnail.sources 未設定でも、ノードに source があればその kind で描画
            if (hasNodeSource)
            {
                return BuildThumbnailFromSources(node, def, [nodeSource]);
            }

            bool preferJacket = def.Thumbnail?.PreferJacket == true;
            return BuildThumbnailSingle(node, def, preferJacket);
        }

        private static UIElement BuildThumbnailFromSources(
            WpfSkinNode node,
            WpfSkinDefinition def,
            IReadOnlyList<string> kinds)
        {
            bool trackParentWidth = WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(node)
                && !node.Width.HasValue;
            bool autoHeight = WpfSkinThumbnailDisplaySize.ShouldAutoHeight(node);
            int count = Math.Max(1, kinds.Count);

            // 各 kind の枠サイズ（ジャケは JacketInfo フォールバック、local はノードまたは生成格子の表示サイズ）
            var slotSizes = new (double W, double H)[count];
            double totalW = 0;
            double maxH = 0;
            for (int i = 0; i < count; i++)
            {
                bool isJacket = string.Equals(kinds[i], WpfSkinThumbnailSources.KindComment1, StringComparison.Ordinal);
                double w;
                double h;
                if (isJacket)
                {
                    w = count == 1 && node.Width is > 0
                        ? node.Width.Value
                        : WpfSkinThumbnailSources.JacketInfoFallbackWidth;
                    h = count == 1 && node.Height is > 0
                        ? node.Height.Value
                        : WpfSkinThumbnailSources.JacketInfoFallbackHeight;
                }
                else
                {
                    // local: ノード明示サイズ優先。未指定時はセル×格子の表示サイズ
                    // （thumbnail.Width/Height はセル寸法。CalcDisplayHeight は総枠参照のためここでは使わない）
                    w = count == 1 && node.Width is > 0
                        ? node.Width.Value
                        : (def.Thumbnail != null
                            ? def.Thumbnail.Width * Math.Max(1, def.Thumbnail.Columns)
                            : WpfSkinThumbnailSources.DefaultBig10DisplayWidth);
                    if (count == 1 && node.Height is > 0)
                    {
                        h = node.Height.Value;
                    }
                    else if (def.Thumbnail != null)
                    {
                        h = def.Thumbnail.Height * Math.Max(1, def.Thumbnail.Rows)
                            * (w / (def.Thumbnail.Width * Math.Max(1, def.Thumbnail.Columns)));
                    }
                    else
                    {
                        h = WpfSkinThumbnailSources.DefaultBig10DisplayHeight;
                    }
                }

                slotSizes[i] = (w, h);
                totalW += w;
                if (h > maxH)
                {
                    maxH = h;
                }
            }

            if (count == 1 && node.Width is > 0)
            {
                totalW = node.Width.Value;
                slotSizes[0].W = totalW;
            }

            var row = new Grid { Background = Brushes.Black };
            for (int i = 0; i < count; i++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(slotSizes[i].W, GridUnitType.Pixel),
                });
            }

            Label host = null;
            for (int i = 0; i < count; i++)
            {
                string kind = kinds[i];
                bool isJacket = string.Equals(kind, WpfSkinThumbnailSources.KindComment1, StringComparison.Ordinal);
                // ジャケ枠が1つのとき、URL 無し／失敗なら local を JacketInfo 枠サイズで表示。
                // （同居で右に local があっても左は 360×203 枠のフォールバック）
                bool fallbackLocal = isJacket && count == 1;
                Label slot = CreateThumbnailSlot(
                    def,
                    slotSizes[i].W,
                    slotSizes[i].H,
                    isJacket,
                    fallbackLocal);
                if (isJacket)
                {
                    slot.Tag = WpfSkinThumbnailSources.JacketPlaySlotTag;
                }

                Grid.SetColumn(slot, i);
                row.Children.Add(slot);
                slot.SizeChanged += (_, _) =>
                {
                    if (host != null)
                    {
                        SyncSourcesRowHeight(host, row);
                    }
                };
            }

            host = new Label
            {
                Background = Brushes.Black,
                Padding = new Thickness(0),
                ClipToBounds = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = row,
                Height = maxH,
            };

            if (trackParentWidth && count == 1)
            {
                host.HorizontalAlignment = HorizontalAlignment.Stretch;
                host.Width = double.NaN;
                host.VerticalAlignment = VerticalAlignment.Top;
                if (autoHeight)
                {
                    AttachSourcesParentWidthSync(host, row, def.Thumbnail, count);
                }
                else if (maxH > 0)
                {
                    host.Height = maxH;
                    // 幅だけ親追従、高さはノード固定。スロット幅も親に合わせる
                    AttachSourcesParentWidthFixedHeight(host, row, maxH, count);
                }
            }
            else if (totalW > 0)
            {
                host.Width = totalW;
                host.Height = maxH;
                host.HorizontalAlignment = HorizontalAlignment.Left;
                host.VerticalAlignment = VerticalAlignment.Top;
            }

            ApplyBox(host, node, skipSize: true, def);
            return host;
        }

        private static Label CreateThumbnailSlot(
            WpfSkinDefinition def,
            double slotW,
            double localH,
            bool jacketSlot,
            bool fallbackToLocal)
        {
            var label = new Label
            {
                Background = Brushes.Black,
                Padding = new Thickness(0),
                ClipToBounds = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Width = slotW > 0 ? slotW : double.NaN,
                Height = localH > 0 ? localH : double.NaN,
            };

            if (WpfSkinHostContext.ThumbnailDoubleClick != null)
            {
                label.MouseDoubleClick += WpfSkinHostContext.ThumbnailDoubleClick;
            }

            if (WpfSkinHostContext.ThumbnailMouseDown != null)
            {
                label.MouseDown += WpfSkinHostContext.ThumbnailMouseDown;
            }

            var image = new Image { Stretch = Stretch.Uniform };
            UIElement content = image;

            if (jacketSlot)
            {
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

                JacketSlotImageBehavior.SetHost(image, label);
                JacketSlotImageBehavior.SetFrameWidth(image, slotW);
                JacketSlotImageBehavior.SetLocalFrameHeight(image, localH);
                JacketSlotImageBehavior.SetFallbackToLocal(image, fallbackToLocal);
                JacketSlotImageBehavior.SetLocalConverter(image, WpfSkinHostContext.ImageConverter);
                JacketSlotImageBehavior.SetLoadingIndicator(image, loadingBar);
                BindingOperations.SetBinding(
                    image,
                    JacketSlotImageBehavior.JacketUrlProperty,
                    new Binding(nameof(MovieRecords.Comment1)));
                BindingOperations.SetBinding(
                    image,
                    JacketSlotImageBehavior.LocalPathProperty,
                    new Binding(nameof(MovieRecords.ThumbPathWpfSkin)));
                BindingOperations.SetBinding(
                    image,
                    JacketSlotImageBehavior.LocalExistsProperty,
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

                double targetAspect = localH > 0 && slotW > 0
                    ? slotW / localH
                    : (def.Thumbnail?.TargetAspect > 0 ? def.Thumbnail.TargetAspect : 16.0 / 9.0);
                image.SetBinding(Image.StretchProperty, new Binding(nameof(Image.Source))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    Converter = WpfSkinHostContext.AspectConverter,
                    ConverterParameter = targetAspect,
                });
                image.Stretch = Stretch.UniformToFill;
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
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
            return label;
        }

        private static void SyncSourcesRowHeight(FrameworkElement host, Grid row)
        {
            if (host == null || row == null)
            {
                return;
            }

            double maxH = 0;
            foreach (UIElement child in row.Children)
            {
                if (child is FrameworkElement fe)
                {
                    double h = fe.ActualHeight > 1 ? fe.ActualHeight : fe.Height;
                    if (!double.IsNaN(h) && h > maxH)
                    {
                        maxH = h;
                    }
                }
            }

            if (maxH < 1)
            {
                return;
            }

            if (double.IsNaN(host.Height) || Math.Abs(host.Height - maxH) > 0.5)
            {
                host.Height = maxH;
            }

            foreach (UIElement child in row.Children)
            {
                if (child is FrameworkElement fe
                    && (double.IsNaN(fe.Height) || Math.Abs(fe.Height - maxH) > 0.5))
                {
                    fe.Height = maxH;
                }
            }
        }

        private static void AttachSourcesParentWidthFixedHeight(
            FrameworkElement host,
            Grid row,
            double fixedHeight,
            int slotCount)
        {
            void ApplyFromWidth()
            {
                double aw = host.ActualWidth;
                if (aw < 1 || slotCount < 1)
                {
                    return;
                }

                host.Height = fixedHeight;
                host.ClearValue(FrameworkElement.WidthProperty);
                host.HorizontalAlignment = HorizontalAlignment.Stretch;

                double slotW = aw / slotCount;
                foreach (UIElement child in row.Children)
                {
                    if (child is not Label slot)
                    {
                        continue;
                    }

                    slot.Width = slotW;
                    slot.Height = fixedHeight;
                    if (FindDescendantImage(slot) is Image img
                        && slot.Tag as string == WpfSkinThumbnailSources.JacketPlaySlotTag)
                    {
                        JacketSlotImageBehavior.SetFrameWidth(img, slotW);
                        JacketSlotImageBehavior.SetLocalFrameHeight(img, fixedHeight);
                    }
                }
            }

            host.Loaded += (_, _) => ApplyFromWidth();
            host.SizeChanged += (_, e) =>
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5)
                {
                    return;
                }

                ApplyFromWidth();
            };
        }

        private static void AttachSourcesParentWidthSync(
            FrameworkElement host,
            Grid row,
            WpfSkinThumbnail thumb,
            int slotCount)
        {
            void ApplyFromWidth()
            {
                double aw = host.ActualWidth;
                if (aw < 1 || slotCount < 1)
                {
                    return;
                }

                double newH = WpfSkinThumbnailDisplaySize.CalcDisplayHeight(aw, thumb);
                if (newH < 1)
                {
                    return;
                }

                if (double.IsNaN(host.Height) || Math.Abs(host.Height - newH) > 0.5)
                {
                    host.Height = newH;
                }

                host.ClearValue(FrameworkElement.WidthProperty);
                host.HorizontalAlignment = HorizontalAlignment.Stretch;

                double slotW = aw / slotCount;
                foreach (UIElement child in row.Children)
                {
                    if (child is not Label slot)
                    {
                        continue;
                    }

                    slot.Width = slotW;
                    // 高さは高い方に合わせる: ローカル格子高を基準にしつつ、ジャケ枠は Behavior 側で伸ばし得る
                    if (slot.Tag as string != WpfSkinThumbnailSources.JacketPlaySlotTag)
                    {
                        slot.Height = newH;
                    }

                    if (FindDescendantImage(slot) is Image img
                        && slot.Tag as string == WpfSkinThumbnailSources.JacketPlaySlotTag)
                    {
                        JacketSlotImageBehavior.SetFrameWidth(img, slotW);
                        JacketSlotImageBehavior.SetLocalFrameHeight(img, newH);
                    }
                }

                // 行全体の高さ = 各スロット高さの max
                double maxSlotH = newH;
                foreach (UIElement child in row.Children)
                {
                    if (child is FrameworkElement fe && fe.Height > maxSlotH)
                    {
                        maxSlotH = fe.Height;
                    }
                }

                if (Math.Abs(host.Height - maxSlotH) > 0.5)
                {
                    host.Height = maxSlotH;
                }

                foreach (UIElement child in row.Children)
                {
                    if (child is FrameworkElement fe)
                    {
                        fe.Height = maxSlotH;
                    }
                }
            }

            host.Loaded += (_, _) => ApplyFromWidth();
            host.SizeChanged += (_, e) =>
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5)
                {
                    return;
                }

                ApplyFromWidth();
            };
        }

        private static Image FindDescendantImage(DependencyObject root)
        {
            if (root is Image image)
            {
                return image;
            }

            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                Image found = FindDescendantImage(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static UIElement BuildThumbnailSingle(WpfSkinNode node, WpfSkinDefinition def, bool preferJacket)
        {
            bool trackParentWidth = WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(node);
            bool autoHeight = WpfSkinThumbnailDisplaySize.ShouldAutoHeight(node);

            // 生成用ピクセル（def.Thumbnail.Width/Height）は表示枠の初期値・アスペクト参照にのみ使う
            double refW = def.Thumbnail?.Width > 0 ? def.Thumbnail.Width : 400;
            double refH = def.Thumbnail?.Height > 0 ? def.Thumbnail.Height : 225;
            double w = node.Width ?? refW;
            double? h = node.Height;
            if (!h.HasValue && !trackParentWidth)
            {
                // 固定幅表示時は参照高さ（または格子計算）を初期値に
                h = WpfSkinThumbnailDisplaySize.CalcDisplayHeight(w, def.Thumbnail);
            }

            var label = new Label
            {
                Background = Brushes.Black,
                Padding = new Thickness(0),
                ClipToBounds = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };

            if (trackParentWidth)
            {
                label.HorizontalAlignment = HorizontalAlignment.Stretch;
                label.Width = double.NaN;
            }
            else if (w > 0)
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

                double localH = h ?? refH;
                double targetAspect = localH > 0 && w > 0 ? w / localH : def.Thumbnail.TargetAspect;

                PreferJacketImageBehavior.SetHost(image, label);
                PreferJacketImageBehavior.SetTrackParentWidth(image, trackParentWidth);
                PreferJacketImageBehavior.SetFrameWidth(image, trackParentWidth ? 0 : w);
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

                double targetAspect = def.Thumbnail?.TargetAspect > 0
                    ? def.Thumbnail.TargetAspect
                    : (h is > 0 ? w / h.Value : 16.0 / 9.0);
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

            if (trackParentWidth)
            {
                label.HorizontalAlignment = HorizontalAlignment.Stretch;
                // 高さは格子アスペクトで決めるため、縦 Stretch だと親セルに引き伸ばされて崩れる
                label.VerticalAlignment = autoHeight
                    ? VerticalAlignment.Top
                    : (!string.IsNullOrEmpty(node.VAlign)
                        ? ResolveVerticalAlignment(node.VAlign)
                        : VerticalAlignment.Top);

                // 親列幅が変わったら（スプリッター確定後など）格子基準で高さを合わせる
                if (autoHeight)
                {
                    AttachParentWidthHeightSync(label, image, def.Thumbnail, preferJacket);
                }
            }
            else if (preferJacket)
            {
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.VerticalAlignment = VerticalAlignment.Top;
            }

            return label;
        }

        /// <summary>
        /// 親から割り当てられた ActualWidth に合わせて、格子基準の表示高さを更新する。
        /// 生成用 Width/Height は変更しない。
        /// </summary>
        private static void AttachParentWidthHeightSync(
            FrameworkElement host,
            Image image,
            WpfSkinThumbnail thumb,
            bool preferJacket)
        {
            void ApplyFromWidth()
            {
                double aw = host.ActualWidth;
                if (aw < 1)
                {
                    return;
                }

                double newH = WpfSkinThumbnailDisplaySize.CalcDisplayHeight(aw, thumb);
                if (newH < 1)
                {
                    return;
                }

                if (double.IsNaN(host.Height) || Math.Abs(host.Height - newH) > 0.5)
                {
                    host.Height = newH;
                }

                // PreferJacket 側が Width 固定／Left 寄せに戻しても、親追従を維持する
                host.ClearValue(FrameworkElement.WidthProperty);
                host.HorizontalAlignment = HorizontalAlignment.Stretch;

                if (preferJacket)
                {
                    PreferJacketImageBehavior.SetTrackParentWidth(image, true);
                    PreferJacketImageBehavior.SetFrameWidth(image, aw);
                    PreferJacketImageBehavior.SetLocalFrameHeight(image, newH);
                    double aspect = newH > 0 ? aw / newH : (thumb?.TargetAspect ?? 16.0 / 9.0);
                    PreferJacketImageBehavior.SetTargetAspect(image, aspect);
                }
            }

            host.Loaded += (_, _) => ApplyFromWidth();
            host.SizeChanged += (_, e) =>
            {
                // 幅変化時のみ（高さ設定のフィードバックでループしない）
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5)
                {
                    return;
                }

                ApplyFromWidth();
            };
        }

        /// <summary>
        /// 縦 Stack 配下でも TextWrapping が親幅で効くよう、親の ActualWidth を MaxWidth に載せる。
        /// </summary>
        private static void AttachParentConstrainedMaxWidth(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            void Apply()
            {
                DependencyObject parent = VisualTreeHelper.GetParent(element);
                if (parent is not FrameworkElement fe || fe.ActualWidth < 1)
                {
                    return;
                }

                double w = fe.ActualWidth;
                if (double.IsNaN(element.MaxWidth) || Math.Abs(element.MaxWidth - w) > 0.5)
                {
                    element.MaxWidth = w;
                }
            }

            element.Loaded += (_, _) =>
            {
                Apply();
                if (VisualTreeHelper.GetParent(element) is FrameworkElement parent)
                {
                    parent.SizeChanged -= ParentOnSizeChanged;
                    parent.SizeChanged += ParentOnSizeChanged;
                }
            };

            void ParentOnSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5)
                {
                    return;
                }

                Apply();
            }
        }

        private static UIElement BuildTags(WpfSkinNode node, WpfSkinDefinition def)
        {
            var items = new ItemsControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
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

            if (node.Width.HasValue && node.Width.Value > 0)
            {
                wrap.SetValue(FrameworkElement.WidthProperty, node.Width.Value);
            }
            else
            {
                // Card.Width 固定だと「右列の実幅」より広くなりタグが見切れる。
                // 縦 Stack は子を無限幅で測るため、ItemsControl の ActualWidth にバインドして折り返す。
                wrap.SetBinding(
                    FrameworkElement.WidthProperty,
                    new Binding(nameof(FrameworkElement.ActualWidth))
                    {
                        RelativeSource = new RelativeSource(
                            RelativeSourceMode.FindAncestor,
                            typeof(ItemsControl),
                            1),
                    });
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
