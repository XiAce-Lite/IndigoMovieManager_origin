using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IndigoMovieManager.Controls;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
namespace IndigoMovieManager
{
    public partial class SkinMaintenanceWindow
    {
        private void RebuildLayoutTree(WpfSkinNode preferred = null)
        {
            _working.Card ??= new WpfSkinCard();
            _working.Card.Layout ??= new WpfSkinNode();
            _layoutRoots = WpfSkinLayoutTreeNode.BuildRoot(_working.Card.Layout);
            LayoutTree.ItemsSource = _layoutRoots;
            WpfSkinNode target = preferred ?? _selectedLayoutNode?.Model ?? _working.Card.Layout;
            SelectLayoutNode(target);
        }

        private void SelectLayoutNode(WpfSkinNode target)
        {
            if (_layoutRoots.Count == 0 || target == null)
            {
                _selectedLayoutNode = null;
                LoadNodeEditors();
                UpdateSelectionQuickBar();
                UpdateColumnConstraintPanel();
                return;
            }

            WpfSkinLayoutTreeNode found = _layoutRoots[0].FindByModel(target) ?? _layoutRoots[0];
            _selectedLayoutNode = found;
            if (!ReferenceEquals(LayoutTree.SelectedItem, found))
            {
                ExpandTo(found);
                if (FindTreeViewItem(LayoutTree, found) is TreeViewItem item)
                {
                    item.IsSelected = true;
                    item.BringIntoView();
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (FindTreeViewItem(LayoutTree, found) is TreeViewItem deferred)
                        {
                            deferred.IsSelected = true;
                            deferred.BringIntoView();
                        }
                    }));
                }
            }

            LoadNodeEditors();
            UpdateSelectionQuickBar();
            UpdateColumnConstraintPanel();
        }

        private static void ExpandTo(WpfSkinLayoutTreeNode node)
        {
            for (WpfSkinLayoutTreeNode current = node?.Parent; current != null; current = current.Parent)
            {
                // Expand is applied when TreeViewItem is materialized in FindTreeViewItem.
            }
        }

        private void PreviewZoomCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PreviewCardHost == null || PreviewZoomCombo?.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            double zoom = 1;
            if (item.Tag is string tag && double.TryParse(tag, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed))
            {
                zoom = parsed;
            }

            PreviewCardHost.LayoutTransform = Math.Abs(zoom - 1) < 0.001
                ? Transform.Identity
                : new ScaleTransform(zoom, zoom);
        }

        private void DesignChromeOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            WpfSkinDesignSession.ShowDesignChrome = ShowDesignGuidesCheck?.IsChecked != false;
            WpfSkinDesignSession.ForceLocalThumbnail = ForceLocalThumbCheck?.IsChecked == true;
            RefreshPreview();
        }

        private void UpdateColumnConstraintPanel()
        {
            if (ColumnConstraintPanel == null)
            {
                return;
            }

            _columnConstraintGrid = null;
            _columnConstraintIndex = -1;
            WpfSkinNode selected = _selectedLayoutNode?.Model;
            if (selected == null)
            {
                ColumnConstraintPanel.Visibility = Visibility.Collapsed;
                return;
            }

            WpfSkinNode gridNode = null;
            int colIndex = 0;
            if (selected.IsGrid && selected.Columns != null && selected.Columns.Count > 0)
            {
                gridNode = selected;
                colIndex = 0;
            }
            else if (_selectedLayoutNode?.Parent?.Model is { IsGrid: true } parentGrid
                     && parentGrid.Columns != null
                     && parentGrid.Columns.Count > 0)
            {
                gridNode = parentGrid;
                colIndex = Math.Clamp(selected.Col, 0, parentGrid.Columns.Count - 1);
            }

            if (gridNode == null)
            {
                ColumnConstraintPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _columnConstraintGrid = gridNode;
            _columnConstraintIndex = colIndex;
            ColumnConstraintPanel.Visibility = Visibility.Visible;
            ColumnConstraintTargetText.Text = $"列 {colIndex + 1}/{gridNode.Columns.Count}";
            string current = gridNode.Columns[colIndex]?.Trim() ?? "*";
            bool isFill = current.EndsWith('*') || string.Equals(current, "*", StringComparison.Ordinal);
            _suppressUi = true;
            ColumnConstraintFillRadio.IsChecked = isFill;
            ColumnConstraintFixedRadio.IsChecked = !isFill;
            _suppressUi = false;
        }

        private void ColumnConstraint_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi
                || _columnConstraintGrid?.Columns == null
                || _columnConstraintIndex < 0
                || _columnConstraintIndex >= _columnConstraintGrid.Columns.Count)
            {
                return;
            }

            CaptureUndoSnapshot();
            if (ColumnConstraintFillRadio?.IsChecked == true)
            {
                _columnConstraintGrid.Columns[_columnConstraintIndex] = "*";
            }
            else
            {
                string current = _columnConstraintGrid.Columns[_columnConstraintIndex]?.Trim() ?? "";
                if (!int.TryParse(current, out int px) || px <= 0)
                {
                    px = Math.Max(80, CardWidthSpin.Value > 0 ? CardWidthSpin.Value / 2 : 200);
                }

                _columnConstraintGrid.Columns[_columnConstraintIndex] = px.ToString();
            }

            // 右ペインの CSV も追従
            if (ReferenceEquals(_selectedLayoutNode?.Model, _columnConstraintGrid)
                || (_selectedLayoutNode?.Parent?.Model != null
                    && ReferenceEquals(_selectedLayoutNode.Parent.Model, _columnConstraintGrid)))
            {
                _suppressUi = true;
                if (ReferenceEquals(_selectedLayoutNode?.Model, _columnConstraintGrid))
                {
                    NodeColumnsBox.Text = string.Join(",", _columnConstraintGrid.Columns);
                }

                _suppressUi = false;
            }

            MarkDirty();
            RefreshPreview();
        }

        private void DuplicateSelectedNode()
        {
            WpfSkinLayoutTreeNode treeNode = _selectedLayoutNode;
            if (treeNode?.Model == null || treeNode.IsRoot || treeNode.Parent?.Model == null)
            {
                return;
            }

            // フィールド一意制約: 同じ field を複製できない場合は弾く
            string uniqueKey = WpfSkinFieldCatalog.ResolveUniqueKey(treeNode.Model);
            if (!string.IsNullOrEmpty(uniqueKey))
            {
                HashSet<string> used = WpfSkinFieldCatalog.CollectUsedFieldIds(_working?.Card?.Layout);
                if (used.Contains(uniqueKey))
                {
                    MessageBox.Show(
                        this,
                        "この項目は既に配置済みのため複製できません（同じ DB 項目は1つまで）。",
                        "複製",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
            }

            WpfSkinLayoutTreeNode parent = treeNode.Parent;
            int index = parent.Children.IndexOf(treeNode);
            CaptureUndoSnapshot();
            WpfSkinNode clone = WpfSkinLayoutEditor.InsertClonedChild(
                parent.Model,
                treeNode.Model,
                index < 0 ? parent.Children.Count : index + 1);
            MarkDirty();
            RebuildLayoutTree(clone);
            RefreshPreview();
            RefreshFieldPalette();
        }

        private static TreeViewItem FindTreeViewItem(ItemsControl container, object item)
        {
            if (container == null)
            {
                return null;
            }

            TreeViewItem direct = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (direct != null)
            {
                direct.IsExpanded = true;
                return direct;
            }

            foreach (object childItem in container.Items)
            {
                if (container.ItemContainerGenerator.ContainerFromItem(childItem) is TreeViewItem child)
                {
                    child.IsExpanded = true;
                    TreeViewItem match = FindTreeViewItem(child, item);
                    if (match != null)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private static T FindAncestor<T>(DependencyObject origin) where T : DependencyObject
        {
            for (DependencyObject current = origin; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private static bool IsDragPastThreshold(Point start, Point current) =>
            Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

        private void LayoutTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var newNode = e.NewValue as WpfSkinLayoutTreeNode;
            bool alreadySelected = ReferenceEquals(_selectedLayoutNode, newNode);
            _selectedLayoutNode = newNode;
            _propertyUndoArmed = true;
            LoadNodeEditors();
            if (!_suppressUi && !alreadySelected)
            {
                RefreshPreview();
            }
        }

        private void FieldPalette_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _fieldPaletteDragStart = e.GetPosition(this);
            if (ItemsControl.ContainerFromElement(FieldPaletteList, e.OriginalSource as DependencyObject) is ListBoxItem item)
            {
                _fieldPaletteDragSource = item.DataContext as FieldPaletteItem;
                item.IsSelected = true;
            }
            else
            {
                _fieldPaletteDragSource = FieldPaletteList.SelectedItem as FieldPaletteItem;
            }
        }

        private void FieldPalette_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed
                || _fieldPaletteDragSource == null
                || _fieldPaletteDragSource.IsPlaced)
            {
                return;
            }

            Point current = e.GetPosition(this);
            if (!IsDragPastThreshold(_fieldPaletteDragStart, current))
            {
                return;
            }

            string fieldId = _fieldPaletteDragSource.Id;
            _fieldPaletteDragSource = null;
            var data = new DataObject();
            data.SetData(WpfSkinDesignSession.FieldPaletteDataFormat, fieldId, false);
            DragDrop.DoDragDrop(FieldPaletteList, data, DragDropEffects.Copy);
        }

        private void FieldPalette_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FieldPaletteList.SelectedItem is not FieldPaletteItem item || item.IsPlaced)
            {
                return;
            }

            WpfSkinLayoutTreeNode parent = ResolveFieldInsertParent();
            if (parent == null)
            {
                return;
            }

            AddFieldToParent(parent, item.Id, parent.Children.Count);
        }

        /// <summary>
        /// ダブルクリック追加先: 選択がコンテナならその中、否則親、最終的にルート。
        /// </summary>
        private WpfSkinLayoutTreeNode ResolveFieldInsertParent()
        {
            WpfSkinLayoutTreeNode selected = _selectedLayoutNode;
            if (selected == null)
            {
                return _layoutRoots.FirstOrDefault();
            }

            if (IsContainerTarget(selected))
            {
                return selected;
            }

            return selected.Parent ?? _layoutRoots.FirstOrDefault();
        }

        private void PaletteButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _paletteDragStart = e.GetPosition(this);
        }

        private void PaletteButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || sender is not Button button)
            {
                return;
            }

            Point current = e.GetPosition(this);
            if (!IsDragPastThreshold(_paletteDragStart, current))
            {
                return;
            }

            if (!Enum.TryParse(button.Tag?.ToString(), ignoreCase: true, out WpfSkinNodeKind kind))
            {
                return;
            }

            DragDrop.DoDragDrop(
                button,
                new DataObject(WpfSkinDesignSession.PaletteDataFormat, kind),
                DragDropEffects.Copy);
        }

        private void LayoutTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStart = e.GetPosition(LayoutTree);
            _treeDragSource = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext as WpfSkinLayoutTreeNode;
        }

        private void LayoutTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _treeDragSource == null || _treeDragSource.IsRoot)
            {
                return;
            }

            Point current = e.GetPosition(LayoutTree);
            if (!IsDragPastThreshold(_treeDragStart, current))
            {
                return;
            }

            DragDrop.DoDragDrop(
                LayoutTree,
                new DataObject(WpfSkinDesignSession.TreeNodeDataFormat, _treeDragSource),
                DragDropEffects.Move);
            _treeDragSource = null;
        }

        private void LayoutTree_DragOver(object sender, DragEventArgs e)
        {
            var targetItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            WpfSkinLayoutTreeNode targetNode = targetItem?.DataContext as WpfSkinLayoutTreeNode ?? _layoutRoots.FirstOrDefault();
            bool can = ResolveDropOnTarget(targetNode, e, apply: false, dropHost: null);
            e.Effects = can
                ? (IsCopyDropData(e) ? DragDropEffects.Copy : DragDropEffects.Move)
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void LayoutTree_Drop(object sender, DragEventArgs e)
        {
            var targetItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            WpfSkinLayoutTreeNode targetNode = targetItem?.DataContext as WpfSkinLayoutTreeNode ?? _layoutRoots.FirstOrDefault();
            if (ResolveDropOnTarget(targetNode, e, apply: true, dropHost: null))
            {
                MarkDirty();
                RefreshPreview();
            }

            e.Handled = true;
        }

        private void OnPreviewNodeDragOver(WpfSkinNode targetModel, DragEventArgs e)
        {
            // フィールド／種別パレット追加は PreviewSurface の PreviewDrop で一回だけ処理する。
            // ここでは Effects=None にしない（ノード上へのドロップが拒否されてしまうため）。
            if (TryGetPaletteFieldId(e, out string fieldId) || e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat))
            {
                WpfSkinDesignSession.InsertAfterHint = null;
                bool can = true;
                if (!string.IsNullOrEmpty(fieldId)
                    && _working?.Card?.Layout != null
                    && WpfSkinLayoutEditor.IsFieldUsed(_working.Card.Layout, fieldId))
                {
                    can = false;
                }

                e.Effects = can ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
                return;
            }

            WpfSkinLayoutTreeNode targetNode = FindTreeNode(targetModel);
            FrameworkElement dropHost = e.Source as FrameworkElement;
            bool canMove = ResolveDropOnTarget(targetNode, e, apply: false, dropHost: dropHost);
            e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
            if (canMove && targetNode != null && !IsContainerTarget(targetNode) && dropHost != null)
            {
                bool horizontal = WpfSkinDesignInsertGeometry.IsHorizontalStack(targetNode.Parent?.Model);
                Point pos = e.GetPosition(dropHost);
                WpfSkinDesignSession.InsertHorizontalHint = horizontal;
                WpfSkinDesignSession.InsertAfterHint = WpfSkinDesignInsertGeometry.IsInsertAfter(
                    pos,
                    dropHost.ActualWidth,
                    dropHost.ActualHeight,
                    horizontal);
            }
            else
            {
                WpfSkinDesignSession.InsertAfterHint = null;
            }

            e.Handled = true;
        }

        private void OnPreviewNodeDrop(WpfSkinNode targetModel, DragEventArgs e)
        {
            // パレット追加は PreviewSurface_PreviewDrop 側。ここではノード移動のみ。
            if (TryGetPaletteFieldId(e, out _) || e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat))
            {
                return;
            }

            if (_dropApplying)
            {
                return;
            }

            _dropApplying = true;
            try
            {
                WpfSkinLayoutTreeNode targetNode = FindTreeNode(targetModel);
                if (ResolveDropOnTarget(targetNode, e, apply: true, dropHost: e.Source as FrameworkElement))
                {
                    MarkDirty();
                    RefreshPreview();
                }
            }
            finally
            {
                _dropApplying = false;
            }
        }

        private void PreviewSurface_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (TryGetPaletteFieldId(e, out string fieldId) || e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat))
            {
                bool can = true;
                if (!string.IsNullOrEmpty(fieldId)
                    && _working?.Card?.Layout != null
                    && WpfSkinLayoutEditor.IsFieldUsed(_working.Card.Layout, fieldId))
                {
                    can = false;
                }

                e.Effects = can ? DragDropEffects.Copy : DragDropEffects.None;
                if (can)
                {
                    PreviewSurface.BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
                    PreviewSurface.BorderThickness = new Thickness(2);
                }

                e.Handled = true;
                return;
            }

            // プレビュー内でのノード移動（空き領域＝ルートへ）
            if (TryGetDraggedLayoutNode(e, out WpfSkinLayoutTreeNode dragged) && dragged is { IsRoot: false })
            {
                e.Effects = DragDropEffects.Move;
                PreviewSurface.BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
                PreviewSurface.BorderThickness = new Thickness(2);
                e.Handled = true;
            }
        }

        private void PreviewSurface_PreviewDragLeave(object sender, DragEventArgs e)
        {
            WpfSkinDesignSession.InsertAfterHint = null;
            ResetPreviewSurfaceChrome();
        }

        private void PreviewSurface_PreviewDrop(object sender, DragEventArgs e)
        {
            if (_dropApplying)
            {
                e.Handled = true;
                return;
            }

            bool isPalette = TryGetPaletteFieldId(e, out _) || e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat);
            bool isMove = TryGetDraggedLayoutNode(e, out _);
            if (!isPalette && !isMove)
            {
                return;
            }

            _dropApplying = true;
            try
            {
                WpfSkinDesignSession.InsertAfterHint = null;
                ResetPreviewSurfaceChrome();
                TryGetDraggedLayoutNode(e, out WpfSkinLayoutTreeNode dragged);
                WpfSkinLayoutTreeNode target = HitTestPreviewLayoutNode(e, dragged?.Model, out FrameworkElement hitElement)
                    ?? _layoutRoots.FirstOrDefault();
                FrameworkElement dropHost = hitElement ?? e.Source as FrameworkElement;
                if (target != null && ResolveDropOnTarget(target, e, apply: true, dropHost: dropHost))
                {
                    MarkDirty();
                    RefreshPreview();
                }
            }
            finally
            {
                _dropApplying = false;
            }

            e.Handled = true;
        }

        private void FieldPalette_DragOver(object sender, DragEventArgs e)
        {
            bool can = TryGetDraggedLayoutNode(e, out WpfSkinLayoutTreeNode node)
                && node != null
                && !node.IsRoot;
            e.Effects = can ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void FieldPalette_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedLayoutNode(e, out WpfSkinLayoutTreeNode node) || node == null || node.IsRoot)
            {
                e.Handled = true;
                return;
            }

            RemoveLayoutTreeNode(node);
            e.Handled = true;
        }

        private bool TryGetDraggedLayoutNode(DragEventArgs e, out WpfSkinLayoutTreeNode node)
        {
            node = null;
            if (e.Data.GetDataPresent(WpfSkinDesignSession.TreeNodeDataFormat)
                && e.Data.GetData(WpfSkinDesignSession.TreeNodeDataFormat) is WpfSkinLayoutTreeNode treeNode)
            {
                node = treeNode;
                return true;
            }

            WpfSkinNode previewModel = WpfSkinDesignSession.DraggingPreviewNode;
            if (previewModel == null
                && e.Data.GetDataPresent(WpfSkinDesignSession.PreviewNodeDataFormat, false)
                && e.Data.GetData(WpfSkinDesignSession.PreviewNodeDataFormat, false) is WpfSkinNode fromData)
            {
                previewModel = fromData;
            }
            else if (previewModel == null
                && e.Data.GetDataPresent(WpfSkinDesignSession.PreviewNodeDataFormat)
                && e.Data.GetData(WpfSkinDesignSession.PreviewNodeDataFormat) is WpfSkinNode fromDataAuto)
            {
                previewModel = fromDataAuto;
            }

            if (previewModel != null)
            {
                node = FindTreeNode(previewModel);
                return node != null;
            }

            return false;
        }

        private static bool IsCopyDropData(DragEventArgs e) =>
            e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat)
            || e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat)
            || e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat, false);

        private static bool TryGetPaletteFieldId(DragEventArgs e, out string fieldId)
        {
            fieldId = null;
            if (e?.Data == null || !e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat, false))
            {
                // 自動変換付きでも試す
                if (e?.Data == null || !e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat))
                {
                    return false;
                }
            }

            object raw = e.Data.GetData(WpfSkinDesignSession.FieldPaletteDataFormat, false)
                ?? e.Data.GetData(WpfSkinDesignSession.FieldPaletteDataFormat);
            fieldId = raw as string ?? raw?.ToString();
            return !string.IsNullOrWhiteSpace(fieldId);
        }

        private WpfSkinLayoutTreeNode HitTestPreviewLayoutNode(
            DragEventArgs e,
            WpfSkinNode exclude,
            out FrameworkElement hitElement)
        {
            hitElement = null;
            if (PreviewSurface == null)
            {
                return null;
            }

            Point pos = e.GetPosition(PreviewSurface);
            WpfSkinNode foundModel = null;
            FrameworkElement foundElement = null;
            VisualTreeHelper.HitTest(
                PreviewSurface,
                potential =>
                {
                    for (DependencyObject current = potential as DependencyObject;
                         current != null;
                         current = VisualTreeHelper.GetParent(current))
                    {
                        if (current is FrameworkElement fe
                            && fe.Tag is WpfSkinNode model
                            && exclude != null
                            && ReferenceEquals(model, exclude))
                        {
                            return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                        }
                    }

                    return HitTestFilterBehavior.Continue;
                },
                result =>
                {
                    for (DependencyObject current = result.VisualHit;
                         current != null;
                         current = VisualTreeHelper.GetParent(current))
                    {
                        if (current is FrameworkElement fe && fe.Tag is WpfSkinNode model)
                        {
                            if (exclude != null && ReferenceEquals(model, exclude))
                            {
                                continue;
                            }

                            foundModel = model;
                            foundElement = fe;
                            return HitTestResultBehavior.Stop;
                        }
                    }

                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pos));

            if (foundModel == null)
            {
                return _layoutRoots.FirstOrDefault();
            }

            hitElement = foundElement;
            return FindTreeNode(foundModel) ?? _layoutRoots.FirstOrDefault();
        }

        private void ResetPreviewSurfaceChrome()
        {
            if (PreviewSurface == null)
            {
                return;
            }

            PreviewSurface.BorderBrush = TryFindResource("MaterialDesign.Brush.ForegroundLight") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
            PreviewSurface.BorderThickness = new Thickness(1);
        }

        private WpfSkinLayoutTreeNode FindTreeNode(WpfSkinNode model)
        {
            if (model == null || _layoutRoots.Count == 0)
            {
                return null;
            }

            return _layoutRoots[0].FindByModel(model);
        }

        private static bool IsContainerTarget(WpfSkinLayoutTreeNode node) =>
            node != null
            && (node.IsRoot || WpfSkinLayoutEditor.CanContainChildren(node.Model));

        private void AddTextNodeButton_Click(object sender, RoutedEventArgs e) => AddNode(WpfSkinNodeKind.Text);
        private void AddThumbnailNodeButton_Click(object sender, RoutedEventArgs e) => AddNode(WpfSkinNodeKind.Thumbnail);
        private void AddTagsNodeButton_Click(object sender, RoutedEventArgs e) => AddNode(WpfSkinNodeKind.Tags);
        private void AddStackNodeButton_Click(object sender, RoutedEventArgs e) => AddNode(WpfSkinNodeKind.Stack);
        private void AddGridNodeButton_Click(object sender, RoutedEventArgs e) => AddNode(WpfSkinNodeKind.Grid);

        private void AddNode(WpfSkinNodeKind kind)
        {
            if (_selectedLayoutNode?.Model == null)
            {
                return;
            }

            WpfSkinLayoutTreeNode parentView = IsContainerTarget(_selectedLayoutNode)
                ? _selectedLayoutNode
                : _selectedLayoutNode.Parent;
            if (parentView?.Model == null)
            {
                return;
            }

            AddNodeToParent(parentView, kind, parentView.Children.Count);
        }

        private void AddNodeToParent(WpfSkinLayoutTreeNode parentView, WpfSkinNodeKind kind, int index, int? gridRow = null, int? gridCol = null)
        {
            if (parentView?.Model == null)
            {
                return;
            }

            CaptureUndoSnapshot();
            WpfSkinNode added = WpfSkinLayoutEditor.InsertChild(parentView.Model, kind, index);
            if (gridRow.HasValue && gridCol.HasValue
                && string.Equals(parentView.Model.ResolvePanel(), "grid", StringComparison.OrdinalIgnoreCase))
            {
                WpfSkinLayoutEditor.AssignGridSlot(added, gridRow.Value, gridCol.Value);
            }

            EnsureAutomaticStyleForNode(added);

            var treeChild = new WpfSkinLayoutTreeNode(added, parentView);
            int safeIndex = Math.Clamp(index, 0, parentView.Children.Count);
            parentView.Children.Insert(safeIndex, treeChild);
            parentView.NotifyDisplayNameChanged();
            MarkDirty();
            SelectLayoutNode(added);
            RefreshPreview();
            RefreshFieldPalette();
        }

        private void AddFieldToParent(WpfSkinLayoutTreeNode parentView, string fieldId, int index, int? gridRow = null, int? gridCol = null)
        {
            if (parentView?.Model == null || _working?.Card?.Layout == null)
            {
                return;
            }

            CaptureUndoSnapshot();
            if (!WpfSkinLayoutEditor.TryInsertField(
                    _working.Card.Layout,
                    parentView.Model,
                    fieldId,
                    index,
                    out WpfSkinNode added,
                    out string error,
                    isListSkin: _working.IsList))
            {
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    UpdateUndoRedoButtons();
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowError(error);
                }

                return;
            }

            if (gridRow.HasValue && gridCol.HasValue
                && string.Equals(parentView.Model.ResolvePanel(), "grid", StringComparison.OrdinalIgnoreCase))
            {
                WpfSkinLayoutEditor.AssignGridSlot(added, gridRow.Value, gridCol.Value);
            }

            WpfSkinThumbnailSources.SyncSourcesFromLayout(_working, _working.Card.Layout);
            SyncThumbnailModeChecksFromWorking();

            var treeChild = new WpfSkinLayoutTreeNode(added, parentView);
            int safeIndex = Math.Clamp(index, 0, parentView.Children.Count);
            parentView.Children.Insert(safeIndex, treeChild);
            parentView.NotifyDisplayNameChanged();
            MarkDirty();
            SelectLayoutNode(added);
            RefreshPreview();
            RefreshFieldPalette();
        }

        private bool ResolveDropOnTarget(WpfSkinLayoutTreeNode targetNode, DragEventArgs e, bool apply, FrameworkElement dropHost)
        {
            if (targetNode?.Model == null)
            {
                return false;
            }

            if (e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat)
                || e.Data.GetDataPresent(WpfSkinDesignSession.FieldPaletteDataFormat, false))
            {
                if (!TryGetPaletteFieldId(e, out string fieldId))
                {
                    return false;
                }

                WpfSkinLayoutTreeNode parentNode = IsContainerTarget(targetNode)
                    ? targetNode
                    : targetNode.Parent;
                if (parentNode?.Model == null)
                {
                    return false;
                }

                if (_working?.Card?.Layout != null
                    && WpfSkinLayoutEditor.IsFieldUsed(_working.Card.Layout, fieldId))
                {
                    return false;
                }

                if (apply)
                {
                    int fieldInsertIndex = IsContainerTarget(targetNode)
                        ? parentNode.Children.Count
                        : parentNode.Children.IndexOf(targetNode) + 1;
                    TryResolveGridSlot(parentNode, targetNode, e, dropHost, out int? row, out int? col);
                    AddFieldToParent(parentNode, fieldId, fieldInsertIndex, row, col);
                }

                return true;
            }

            if (e.Data.GetDataPresent(WpfSkinDesignSession.PaletteDataFormat))
            {
                if (e.Data.GetData(WpfSkinDesignSession.PaletteDataFormat) is not WpfSkinNodeKind kind)
                {
                    return false;
                }

                WpfSkinLayoutTreeNode parentNode = IsContainerTarget(targetNode)
                    ? targetNode
                    : targetNode.Parent;
                if (parentNode?.Model == null)
                {
                    return false;
                }

                if (apply)
                {
                    int paletteInsertIndex = IsContainerTarget(targetNode)
                        ? parentNode.Children.Count
                        : parentNode.Children.IndexOf(targetNode) + 1;
                    TryResolveGridSlot(parentNode, targetNode, e, dropHost, out int? row, out int? col);
                    AddNodeToParent(parentNode, kind, paletteInsertIndex, row, col);
                }

                return true;
            }

            WpfSkinLayoutTreeNode dragged = null;
            if (e.Data.GetDataPresent(WpfSkinDesignSession.TreeNodeDataFormat)
                && e.Data.GetData(WpfSkinDesignSession.TreeNodeDataFormat) is WpfSkinLayoutTreeNode treeDragged)
            {
                dragged = treeDragged;
            }
            else if (e.Data.GetDataPresent(WpfSkinDesignSession.PreviewNodeDataFormat)
                && e.Data.GetData(WpfSkinDesignSession.PreviewNodeDataFormat) is WpfSkinNode previewModel)
            {
                dragged = FindTreeNode(previewModel);
            }

            if (dragged?.Parent?.Model?.Children == null
                || ReferenceEquals(dragged, targetNode)
                || IsDescendantOf(targetNode, dragged)
                || dragged.IsRoot)
            {
                return false;
            }

            WpfSkinLayoutTreeNode destinationParent = IsContainerTarget(targetNode)
                ? targetNode
                : targetNode.Parent;
            if (destinationParent?.Model == null)
            {
                return false;
            }

            int insertIndex = ResolveMoveInsertIndex(destinationParent, targetNode, e, dropHost);
            if (insertIndex < 0)
            {
                return false;
            }

            if (!apply)
            {
                return true;
            }

            CaptureUndoSnapshot();
            WpfSkinLayoutTreeNode sourceParent = dragged.Parent;
            int sourceIndex = sourceParent.Children.IndexOf(dragged);
            if (sourceIndex < 0)
            {
                return false;
            }

            if (ReferenceEquals(sourceParent, destinationParent))
            {
                int adjustedIndex = insertIndex;
                if (sourceIndex < adjustedIndex)
                {
                    adjustedIndex--;
                }

                if (adjustedIndex == sourceIndex)
                {
                    TryResolveGridSlot(destinationParent, targetNode, e, dropHost, out int? sameRow, out int? sameCol);
                    if (sameRow.HasValue && sameCol.HasValue)
                    {
                        WpfSkinLayoutEditor.AssignGridSlot(dragged.Model, sameRow.Value, sameCol.Value);
                        SelectLayoutNode(dragged.Model);
                        return true;
                    }

                    SelectLayoutNode(dragged.Model);
                    return false;
                }

                if (!WpfSkinLayoutEditor.MoveNodeToParent(sourceParent.Model, dragged.Model, destinationParent.Model, adjustedIndex))
                {
                    return false;
                }

                sourceParent.Children.RemoveAt(sourceIndex);
                sourceParent.Children.Insert(adjustedIndex, dragged);
                sourceParent.NotifyDisplayNameChanged();
            }
            else
            {
                if (!WpfSkinLayoutEditor.MoveNodeToParent(sourceParent.Model, dragged.Model, destinationParent.Model, insertIndex))
                {
                    return false;
                }

                sourceParent.Children.Remove(dragged);
                dragged.Reparent(destinationParent);
                destinationParent.Children.Insert(insertIndex, dragged);
                sourceParent.NotifyDisplayNameChanged();
                destinationParent.NotifyDisplayNameChanged();
            }

            TryResolveGridSlot(destinationParent, targetNode, e, dropHost, out int? gridRow, out int? gridCol);
            if (gridRow.HasValue && gridCol.HasValue)
            {
                WpfSkinLayoutEditor.AssignGridSlot(dragged.Model, gridRow.Value, gridCol.Value);
            }

            SelectLayoutNode(dragged.Model);
            return true;
        }

        private void TryResolveGridSlot(
            WpfSkinLayoutTreeNode destinationParent,
            WpfSkinLayoutTreeNode targetNode,
            DragEventArgs e,
            FrameworkElement dropHost,
            out int? row,
            out int? col)
        {
            row = null;
            col = null;
            if (destinationParent?.Model == null
                || !string.Equals(destinationParent.Model.ResolvePanel(), "grid", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (dropHost != null
                && WpfSkinDesignSession.TryHitGridCell(dropHost, e, destinationParent.Model, out int hitRow, out int hitCol))
            {
                row = hitRow;
                col = hitCol;
                return;
            }

            if (!IsContainerTarget(targetNode) && targetNode?.Model != null)
            {
                row = Math.Max(0, targetNode.Model.Row);
                col = Math.Max(0, targetNode.Model.Col);
            }
        }

        private static bool IsDescendantOf(WpfSkinLayoutTreeNode node, WpfSkinLayoutTreeNode ancestor)
        {
            for (WpfSkinLayoutTreeNode current = node; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private void DeleteNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayoutNode == null || _selectedLayoutNode.IsRoot)
            {
                return;
            }

            RemoveLayoutTreeNode(_selectedLayoutNode);
        }

        private void RemoveLayoutTreeNode(WpfSkinLayoutTreeNode treeNode)
        {
            if (treeNode == null || treeNode.IsRoot || treeNode.Parent?.Model == null)
            {
                return;
            }

            WpfSkinLayoutTreeNode parent = treeNode.Parent;
            CaptureUndoSnapshot();
            if (!WpfSkinLayoutEditor.RemoveNode(parent.Model, treeNode.Model))
            {
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    UpdateUndoRedoButtons();
                }

                return;
            }

            parent.Children.Remove(treeNode);
            parent.NotifyDisplayNameChanged();
            WpfSkinThumbnailSources.SyncSourcesFromLayout(_working, _working.Card?.Layout);
            SyncThumbnailModeChecksFromWorking();
            MarkDirty();
            SelectLayoutNode(parent.Model);
            RefreshPreview();
            RefreshFieldPalette();
        }

        private void SyncThumbnailModeChecksFromWorking()
        {
            if (_working == null)
            {
                return;
            }

            _suppressUi = true;
            bool hasSources = WpfSkinThumbnailSources.Normalize(_working.Thumbnail?.Sources).Count > 0;
            ThumbCoexistSourcesCheck.IsChecked = hasSources;
            ThumbPreferJacketCheck.IsChecked = !hasSources && _working.Thumbnail?.PreferJacket == true;
            UpdateThumbnailModeChecksEnabled();
            _suppressUi = false;
        }

        /// <summary>
        /// リーフ上では半分より先＝後へ挿入（縦=Y / 横=X）。コンテナ上は末尾。
        /// </summary>
        private static int ResolveMoveInsertIndex(
            WpfSkinLayoutTreeNode destinationParent,
            WpfSkinLayoutTreeNode targetNode,
            DragEventArgs e,
            FrameworkElement dropHost)
        {
            if (destinationParent == null)
            {
                return -1;
            }

            if (IsContainerTarget(targetNode))
            {
                return destinationParent.Children.Count;
            }

            int index = destinationParent.Children.IndexOf(targetNode);
            if (index < 0)
            {
                return -1;
            }

            bool insertAfter = false;
            if (dropHost != null && e != null)
            {
                bool horizontal = WpfSkinDesignInsertGeometry.IsHorizontalStack(destinationParent.Model);
                Point pos = e.GetPosition(dropHost);
                insertAfter = WpfSkinDesignInsertGeometry.IsInsertAfter(
                    pos,
                    dropHost.ActualWidth,
                    dropHost.ActualHeight,
                    horizontal);
            }

            return index + (insertAfter ? 1 : 0);
        }

        private void MoveNodeUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedNode(-1);
        private void MoveNodeDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedNode(1);

        private void MoveSelectedNode(int delta)
        {
            if (_selectedLayoutNode?.Parent?.Model?.Children == null)
            {
                return;
            }

            ObservableCollection<WpfSkinLayoutTreeNode> siblings = _selectedLayoutNode.Parent.Children;
            int index = siblings.IndexOf(_selectedLayoutNode);
            if (index < 0)
            {
                return;
            }

            CaptureUndoSnapshot();
            if (!WpfSkinLayoutEditor.MoveNode(_selectedLayoutNode.Parent.Model.Children, _selectedLayoutNode.Model, delta))
            {
                return;
            }

            siblings.Move(index, index + delta);
            MarkDirty();
            RefreshPreview();
        }
    }
}
