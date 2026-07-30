using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IndigoMovieManager.Services.WpfSkin.Design
{
    /// <summary>
    /// 編集画面プレビュー専用。本番一覧では Active=false。
    /// </summary>
    internal static class WpfSkinDesignSession
    {
        public const string PaletteDataFormat = "WpfSkinPaletteKind";
        public const string FieldPaletteDataFormat = "WpfSkinFieldId";
        public const string TreeNodeDataFormat = "WpfSkinTreeNode";
        public const string PreviewNodeDataFormat = "WpfSkinPreviewNode";

        private const string GuideKey = "WpfSkinDesignGridGuide";
        private const string InsertGuideKey = "WpfSkinDesignInsertGuide";

        public static bool Active { get; private set; }
        public static WpfSkinNode SelectedNode { get; private set; }
        public static Action<WpfSkinNode> OnSelect { get; private set; }
        public static Action<WpfSkinNode, DragEventArgs> OnDragOver { get; private set; }
        public static Action<WpfSkinNode, DragEventArgs> OnDrop { get; private set; }
        public static Action<WpfSkinNode> OnEditProperties { get; private set; }
        public static Action<WpfSkinNode> OnDeleteNode { get; private set; }

        /// <summary>スプリッタードラッグ開始直前（変更前 Undo 用）。</summary>
        public static Action<WpfSkinNode> OnColumnResizeStarted { get; private set; }

        /// <summary>スプリッターで列/行サイズが確定したとき呼ばれる。</summary>
        public static Action<WpfSkinNode> OnColumnResized { get; private set; }

        /// <summary>プレビューからのドラッグ中ノード（DataObject 参照切れの保険）。</summary>
        public static WpfSkinNode DraggingPreviewNode { get; private set; }

        /// <summary>ノード移動ドラッグ中の挿入位置ヒント（null=非表示）。</summary>
        public static bool? InsertAfterHint { get; set; }

        /// <summary>挿入ラインを横向きにするか。</summary>
        public static bool InsertHorizontalHint { get; set; }

        public static IDisposable Push(
            WpfSkinNode selected,
            Action<WpfSkinNode> onSelect,
            Action<WpfSkinNode, DragEventArgs> onDragOver = null,
            Action<WpfSkinNode, DragEventArgs> onDrop = null,
            Action<WpfSkinNode> onEditProperties = null,
            Action<WpfSkinNode> onDeleteNode = null,
            Action<WpfSkinNode> onColumnResized = null,
            Action<WpfSkinNode> onColumnResizeStarted = null)
        {
            Snapshot previous = Capture();
            Active = true;
            SelectedNode = selected;
            OnSelect = onSelect;
            OnDragOver = onDragOver;
            OnDrop = onDrop;
            OnEditProperties = onEditProperties;
            OnDeleteNode = onDeleteNode;
            OnColumnResized = onColumnResized;
            OnColumnResizeStarted = onColumnResizeStarted;
            return new RestoreDisposable(previous);
        }

        public static UIElement Wrap(UIElement element, WpfSkinNode node)
        {
            if (element == null || node == null || !Active)
            {
                return element;
            }

            WpfSkinDesignGridGuide guide = null;
            var insertGuide = new WpfSkinDesignInsertGuide();
            if (WpfSkinDesignGridGeometry.IsGridPanel(node))
            {
                guide = new WpfSkinDesignGridGuide(node);
                var layer = new Grid();
                layer.Children.Add(element);
                layer.Children.Add(guide.Overlay);
                layer.Children.Add(insertGuide.Overlay);
                element = layer;
            }
            else
            {
                var layer = new Grid();
                layer.Children.Add(element);
                layer.Children.Add(insertGuide.Overlay);
                element = layer;
            }

            bool selected = ReferenceEquals(node, SelectedNode);
            // Push スコープ終了後もハンドラが生きるよう、静的プロパティをクロージャにキャプチャする。
            Action<WpfSkinNode> onSelect = OnSelect;
            Action<WpfSkinNode, DragEventArgs> onDragOver = OnDragOver;
            Action<WpfSkinNode, DragEventArgs> onDrop = OnDrop;
            Action<WpfSkinNode> onEditProperties = OnEditProperties;
            Action<WpfSkinNode> onDeleteNode = OnDeleteNode;

            var border = new Border
            {
                Child = element,
                BorderBrush = selected
                    ? new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5))
                    : new SolidColorBrush(Color.FromArgb(0x55, 0x90, 0x90, 0x90)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Background = selected
                    ? new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x88, 0xE5))
                    : Brushes.Transparent,
                Margin = new Thickness(1),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                Tag = node,
                AllowDrop = true,
                // * 列がカード／親セル幅まで伸びるよう、デザイン枠も横に追従させる
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            if (guide != null)
            {
                border.Resources[GuideKey] = guide;
            }

            border.Resources[InsertGuideKey] = insertGuide;

            Point dragStart = default;
            bool dragPending = false;

            border.PreviewMouseLeftButtonDown += (_, e) =>
            {
                // GridSplitter のドラッグは横取りしない（列幅スプリッターが機能しなくなるため）。
                if (IsGridSplitterSource(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                // 入れ子の内側デザイン枠があるクリックは外側で握らない（根だけ選べる不具合の原因）。
                if (HasNestedDesignWrapBetween(border, e.OriginalSource as DependencyObject))
                {
                    return;
                }

                dragStart = e.GetPosition(border);
                dragPending = true;
                border.CaptureMouse();
                e.Handled = true;
            };

            border.PreviewMouseMove += (_, e) =>
            {
                if (IsGridSplitterSource(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                if (!dragPending || e.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                Point current = e.GetPosition(border);
                if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }

                // 選択の再描画でドラッグ元 Border が破棄されないよう、ドラッグ開始時は選択しない。
                dragPending = false;
                if (border.IsMouseCaptured)
                {
                    border.ReleaseMouseCapture();
                }

                DraggingPreviewNode = node;
                try
                {
                    var data = new DataObject();
                    data.SetData(PreviewNodeDataFormat, node, false);
                    DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
                }
                finally
                {
                    DraggingPreviewNode = null;
                }
            };

            border.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (IsGridSplitterSource(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                if (!dragPending)
                {
                    return;
                }

                dragPending = false;
                if (border.IsMouseCaptured)
                {
                    border.ReleaseMouseCapture();
                }

                onSelect?.Invoke(node);
                e.Handled = true;
            };

            border.LostMouseCapture += (_, _) =>
            {
                // DoDragDrop や他要因でキャプチャが外れたとき、押しっぱなし選択を残さない。
                if (dragPending && Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    dragPending = false;
                }
            };

            var menu = new ContextMenu();
            var editItem = new MenuItem { Header = "プロパティ..." };
            editItem.Click += (_, _) =>
            {
                onSelect?.Invoke(node);
                onEditProperties?.Invoke(node);
            };
            var deleteItem = new MenuItem { Header = "削除" };
            deleteItem.Click += (_, _) =>
            {
                onSelect?.Invoke(node);
                onDeleteNode?.Invoke(node);
            };
            menu.Items.Add(editItem);
            menu.Items.Add(deleteItem);
            border.ContextMenu = menu;
            border.MouseRightButtonUp += (_, e) =>
            {
                onSelect?.Invoke(node);
                e.Handled = true;
                border.ContextMenu.IsOpen = true;
            };

            border.DragOver += (_, e) =>
            {
                onDragOver?.Invoke(node, e);
                bool can = e.Effects != DragDropEffects.None;
                UpdateDropChrome(border, can);
                if (can && guide != null && TryHitGridCell(border, e, node, out int row, out int col))
                {
                    guide.HighlightCell(row, col);
                    insertGuide.Clear();
                }
                else
                {
                    guide?.ClearHighlight();
                    if (can && InsertAfterHint.HasValue && e.Effects == DragDropEffects.Move)
                    {
                        insertGuide.Show(InsertAfterHint.Value, InsertHorizontalHint);
                    }
                    else
                    {
                        insertGuide.Clear();
                    }
                }

                e.Handled = true;
            };

            border.DragLeave += (_, _) =>
            {
                UpdateDropChrome(border, false);
                guide?.ClearHighlight();
                insertGuide.Clear();
            };

            border.Drop += (_, e) =>
            {
                UpdateDropChrome(border, false);
                guide?.ClearHighlight();
                insertGuide.Clear();
                InsertAfterHint = null;
                onDrop?.Invoke(node, e);
                e.Handled = true;
            };

            return border;
        }

        /// <summary>
        /// originalSource と self の間に、別のデザイン枠（Tag=WpfSkinNode）があるか。
        /// Preview イベントは外側から届くため、内側があるなら外側は無視する。
        /// </summary>
        private static bool HasNestedDesignWrapBetween(DependencyObject self, DependencyObject originalSource)
        {
            if (self == null || originalSource == null)
            {
                return false;
            }

            for (DependencyObject current = originalSource;
                 current != null && !ReferenceEquals(current, self);
                 current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement fe
                    && fe.Tag is WpfSkinNode
                    && !ReferenceEquals(current, self))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Grid 上のマウス位置から row/col を推定する。定義が無い場合は false。
        /// </summary>
        public static bool TryHitGridCell(FrameworkElement host, DragEventArgs e, WpfSkinNode gridNode, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (host == null || e == null || !WpfSkinDesignGridGeometry.IsGridPanel(gridNode))
            {
                return false;
            }

            Grid grid = FindDescendantGrid(host);
            if (grid == null)
            {
                return false;
            }

            Point pos = e.GetPosition(grid);
            if (pos.X < 0 || pos.Y < 0 || pos.X > grid.ActualWidth || pos.Y > grid.ActualHeight)
            {
                return false;
            }

            int rows = WpfSkinDesignGridGeometry.ResolveRowCount(gridNode, grid.RowDefinitions.Count);
            int cols = WpfSkinDesignGridGeometry.ResolveColumnCount(gridNode, grid.ColumnDefinitions.Count);
            // デザインモードではスプリッター列/行（奇数インデックス）が混在するため、偶数インデックスのみ取得。
            bool hasSplitterCols = grid.ColumnDefinitions.Count == cols * 2 - 1;
            bool hasSplitterRows = grid.RowDefinitions.Count == rows * 2 - 1;
            IList<double> colSizes = hasSplitterCols
                ? Enumerable.Range(0, cols).Select(i => grid.ColumnDefinitions[i * 2].ActualWidth).ToList()
                : grid.ColumnDefinitions.Select(d => d.ActualWidth).ToList();
            IList<double> rowSizes = hasSplitterRows
                ? Enumerable.Range(0, rows).Select(i => grid.RowDefinitions[i * 2].ActualHeight).ToList()
                : grid.RowDefinitions.Select(d => d.ActualHeight).ToList();
            row = WpfSkinDesignGridGeometry.HitIndex(pos.Y, grid.ActualHeight, rows, rowSizes);
            col = WpfSkinDesignGridGeometry.HitIndex(pos.X, grid.ActualWidth, cols, colSizes);
            return true;
        }

        /// <summary>
        /// クリック元が GridSplitter 自身またはその視覚子かを判定する。
        /// GridSplitter のドラッグを Border が横取りしないための判定。
        /// </summary>
        private static bool IsGridSplitterSource(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is GridSplitter || current is System.Windows.Controls.Primitives.Thumb)
                {
                    return true;
                }

                DependencyObject next = null;
                if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                {
                    next = VisualTreeHelper.GetParent(current);
                }

                if (next == null)
                {
                    next = LogicalTreeHelper.GetParent(current);
                }

                if (next == null)
                {
                    switch (current)
                    {
                        case FrameworkElement fe:
                            next = fe.TemplatedParent as DependencyObject;
                            break;
                        case FrameworkContentElement fce:
                            next = fce.TemplatedParent as DependencyObject;
                            break;
                    }
                }

                current = next;
            }

            return false;
        }

        private static void UpdateDropChrome(Border border, bool active)
        {
            if (active)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x43, 0xA0, 0x47));
                return;
            }

            bool selected = border.Tag is WpfSkinNode node && ReferenceEquals(node, SelectedNode);
            border.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5))
                : new SolidColorBrush(Color.FromArgb(0x55, 0x90, 0x90, 0x90));
            border.BorderThickness = new Thickness(selected ? 2 : 1);
            border.Background = selected
                ? new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x88, 0xE5))
                : Brushes.Transparent;
        }

        private static Grid FindDescendantGrid(DependencyObject root)
        {
            if (root is Grid grid && !IsGuideLayer(grid))
            {
                // ガイド用の外側 Grid（子が2で Overlay を含む）はスキップし、中の実 grid を探す。
                if (grid.Children.Count == 2 && grid.Children[1] is Canvas)
                {
                    return FindDescendantGrid(grid.Children[0] as DependencyObject);
                }

                return grid;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                Grid found = FindDescendantGrid(VisualTreeHelper.GetChild(root, i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsGuideLayer(Grid grid) =>
            grid.Children.Count == 2 && grid.Children[1] is Canvas;

        private static Snapshot Capture() =>
            new()
            {
                Active = Active,
                SelectedNode = SelectedNode,
                OnSelect = OnSelect,
                OnDragOver = OnDragOver,
                OnDrop = OnDrop,
                OnEditProperties = OnEditProperties,
                OnDeleteNode = OnDeleteNode,
                OnColumnResized = OnColumnResized,
                OnColumnResizeStarted = OnColumnResizeStarted,
            };

        private sealed class Snapshot
        {
            public bool Active { get; init; }
            public WpfSkinNode SelectedNode { get; init; }
            public Action<WpfSkinNode> OnSelect { get; init; }
            public Action<WpfSkinNode, DragEventArgs> OnDragOver { get; init; }
            public Action<WpfSkinNode, DragEventArgs> OnDrop { get; init; }
            public Action<WpfSkinNode> OnEditProperties { get; init; }
            public Action<WpfSkinNode> OnDeleteNode { get; init; }
            public Action<WpfSkinNode> OnColumnResized { get; init; }
            public Action<WpfSkinNode> OnColumnResizeStarted { get; init; }
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
                Active = _snapshot.Active;
                SelectedNode = _snapshot.SelectedNode;
                OnSelect = _snapshot.OnSelect;
                OnDragOver = _snapshot.OnDragOver;
                OnDrop = _snapshot.OnDrop;
                OnEditProperties = _snapshot.OnEditProperties;
                OnDeleteNode = _snapshot.OnDeleteNode;
                OnColumnResized = _snapshot.OnColumnResized;
                OnColumnResizeStarted = _snapshot.OnColumnResizeStarted;
            }
        }
    }
}
