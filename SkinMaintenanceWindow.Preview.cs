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
        private void RefreshPreview()
        {
            if (_working == null)
            {
                return;
            }

            ApplyFormToWorking();
            _previewThumbConverter.UpdateLayout(
                _working.Thumbnail.Width,
                _working.Thumbnail.Height,
                _working.Thumbnail.Columns,
                _working.Thumbnail.Rows);

            // クローンしない: プレビュー選択ハイライトはノード参照一致で判定する。
            // DesignSession はプレビュー操作の間ずっと有効にする（Refresh 直後に Dispose すると
            // クリック選択・右クリック・ノード Drop コールバックが消える）。
            _designSessionScope?.Dispose();
            _designSessionScope = WpfSkinDesignSession.Push(
                _selectedLayoutNode?.Model,
                SelectNodeFromPreview,
                OnPreviewNodeDragOver,
                OnPreviewNodeDrop,
                EditSelectedNodeProperties,
                DeleteNodeFromPreview,
                OnGridColumnResized,
                OnGridColumnResizeStarted);
            using (WpfSkinHostContext.PushScope(
                itemContextMenu: null,
                thumbnailDoubleClick: null,
                thumbnailMouseDown: null,
                thumbnailRightDown: null,
                imageConverter: _previewThumbConverter,
                aspectConverter: _previewAspectConverter,
                fileSizeConverter: _previewFileSizeConverter))
            {
                if (!ReferenceEquals(PreviewPresenter.SkinDefinition, _working))
                {
                    PreviewPresenter.SkinDefinition = _working;
                }
                else
                {
                    PreviewPresenter.RebuildLayoutNow();
                }
            }

            PreviewListHeaderHost.Content = _working.IsList
                ? WpfSkinLayoutBuilder.BuildListHeader(_working)
                : null;
            RefreshFieldPalette();
            ApplyPreviewStretchSlot();
            UpdateCardWidthGripState();
            UpdateSelectionQuickBar();
            UpdateColumnConstraintPanel();
        }

        private void ApplyPreviewStretchSlot()
        {
            if (PreviewCardHost == null || PreviewPresenter == null || PreviewScroll == null)
            {
                return;
            }

            bool stretch = CardStretchCheck?.IsChecked == true;
            if (stretch)
            {
                double viewport = PreviewScroll.ViewportWidth;
                if (viewport < 40)
                {
                    viewport = Math.Max(80, PreviewScroll.ActualWidth - 24);
                }

                // 一覧のスロット幅相当として、スクロール領域幅までカードを伸ばす
                PreviewCardHost.HorizontalAlignment = HorizontalAlignment.Stretch;
                PreviewCardHost.Width = Math.Max(80, viewport);
                PreviewPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                PreviewPresenter.Width = double.NaN;
            }
            else
            {
                PreviewCardHost.ClearValue(FrameworkElement.WidthProperty);
                PreviewCardHost.HorizontalAlignment = HorizontalAlignment.Left;
                PreviewPresenter.ClearValue(FrameworkElement.WidthProperty);
                PreviewPresenter.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        private void UpdateCardWidthGripState()
        {
            if (CardWidthGrip == null)
            {
                return;
            }

            bool chrome = ShowDesignGuidesCheck?.IsChecked != false;
            bool stretch = CardStretchCheck?.IsChecked == true;

            CardWidthGrip.Visibility = chrome ? Visibility.Visible : Visibility.Collapsed;
            CardHeightGrip.Visibility = chrome ? Visibility.Visible : Visibility.Collapsed;

            CardWidthGrip.IsEnabled = chrome && !stretch;
            CardWidthGrip.Opacity = stretch ? 0.35 : 0.95;
            CardWidthGrip.Cursor = stretch ? Cursors.Arrow : Cursors.SizeWE;
            CardWidthGrip.ToolTip = stretch
                ? "一覧の列幅に合わせているため、カード幅グリップは無効です"
                : "右下をドラッグしてカード幅を変更";

            if (CardHeightGrip != null)
            {
                CardHeightGrip.IsEnabled = chrome;
                CardHeightGrip.Opacity = 0.85;
            }
        }

        private void CardWidthGrip_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (CardStretchCheck?.IsChecked == true || _working == null)
            {
                _cardWidthDragging = false;
                return;
            }

            CaptureUndoSnapshot();
            _cardWidthDragging = true;
        }

        private void CardWidthGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_cardWidthDragging || CardStretchCheck?.IsChecked == true)
            {
                return;
            }

            double current = PreviewPresenter.GetPreviewCardWidth();
            if (current < 1)
            {
                current = Math.Max(80, CardWidthSpin.Value);
            }

            double newWidth = Math.Clamp(current + e.HorizontalChange, 80, 4000);
            PreviewPresenter.SetPreviewCardWidth(newWidth);
        }

        private void CardWidthGrip_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!_cardWidthDragging)
            {
                return;
            }

            _cardWidthDragging = false;
            if (CardStretchCheck?.IsChecked == true)
            {
                return;
            }

            double width = PreviewPresenter.GetPreviewCardWidth();
            if (width < 1)
            {
                return;
            }

            int rounded = (int)Math.Round(width);
            _suppressUi = true;
            CardWidthSpin.Value = rounded;
            _suppressUi = false;
            _working.Card ??= new WpfSkinCard();
            _working.Card.Width = rounded;
            MarkDirty();
        }

        private void CardHeightGrip_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (_working == null)
            {
                _cardHeightDragging = false;
                return;
            }

            CaptureUndoSnapshot();
            _cardHeightDragging = true;
        }

        private void CardHeightGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_cardHeightDragging)
            {
                return;
            }

            double current = PreviewPresenter.GetPreviewCardHeight();
            if (current < 1)
            {
                current = Math.Max(40, CardHeightSpin.Value > 0 ? CardHeightSpin.Value : 120);
            }

            double newHeight = Math.Clamp(current + e.VerticalChange, 40, 4000);
            PreviewPresenter.SetPreviewCardHeight(newHeight);
        }

        private void CardHeightGrip_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!_cardHeightDragging)
            {
                return;
            }

            _cardHeightDragging = false;
            double height = PreviewPresenter.GetPreviewCardHeight();
            if (height < 1)
            {
                return;
            }

            int rounded = (int)Math.Round(height);
            _suppressUi = true;
            CardHeightSpin.Value = rounded;
            _suppressUi = false;
            _working.Card ??= new WpfSkinCard();
            _working.Card.Height = rounded;
            MarkDirty();
            RefreshPreview();
        }

        private void UpdateSelectionQuickBar()
        {
            if (SelectionQuickBar == null)
            {
                return;
            }

            WpfSkinLayoutTreeNode node = _selectedLayoutNode;
            if (node?.Model == null)
            {
                SelectionQuickBar.Visibility = Visibility.Collapsed;
                return;
            }

            SelectionQuickBar.Visibility = Visibility.Visible;
            QuickBarNodeName.Text = node.DisplayName ?? "（無題）";
            QuickBarDeleteButton.IsEnabled = !node.IsRoot;
            QuickBarPropsButton.IsEnabled = true;
        }

        private void QuickBarPropsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayoutNode?.Model != null)
            {
                EditSelectedNodeProperties(_selectedLayoutNode.Model);
            }
        }

        private void QuickBarDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayoutNode != null && !_selectedLayoutNode.IsRoot)
            {
                RemoveLayoutTreeNode(_selectedLayoutNode);
            }
        }

        private void EditSelectedNodeProperties(WpfSkinNode node)
        {
            if (node == null)
            {
                return;
            }

            SelectLayoutNode(node);
            CaptureUndoSnapshot();
            var dialog = new SkinNodePropertyWindow(this, node);
            if (dialog.ShowDialog() != true)
            {
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    UpdateUndoRedoButtons();
                }

                return;
            }

            MarkDirty();
            _selectedLayoutNode?.NotifyDisplayNameChanged();
            LoadNodeEditors();
            RefreshPreview();
        }

        private void DeleteNodeFromPreview(WpfSkinNode node)
        {
            if (node == null)
            {
                return;
            }

            WpfSkinLayoutTreeNode treeNode = FindTreeNode(node);
            if (treeNode == null || treeNode.IsRoot)
            {
                return;
            }

            RemoveLayoutTreeNode(treeNode);
        }

        /// <summary>
        /// スプリッター開始前。右ペイン値を flush した「変更前」を Undo に積む。
        /// </summary>
        private void OnGridColumnResizeStarted(WpfSkinNode node)
        {
            CaptureUndoSnapshot();
        }

        /// <summary>
        /// スプリッター完了。モデルは既に Sync 済みなので、右ペインの古い値で
        /// ApplyFormToWorking 上書きしないよう、先にエディタを同期してから Dirty にする。
        /// </summary>
        private void OnGridColumnResized(WpfSkinNode node)
        {
            WpfSkinNode selected = _selectedLayoutNode?.Model;
            // 格子自体、または列幅同期で Width が変わる子孫（サムネ等）を選択中なら右ペインを追従
            if (selected != null
                && (ReferenceEquals(selected, node) || IsDescendantOf(node, selected)))
            {
                LoadNodeEditors();
            }

            // CaptureUndoSnapshot は呼ばない（ApplyFormToWorking が古い Rows/Columns で巻き戻すため）。
            // Undo 用スナップショットは DragStarted 側で積んである。
            MarkDirty();
        }

        private static bool IsDescendantOf(WpfSkinNode ancestor, WpfSkinNode candidate)
        {
            if (ancestor?.Children == null || candidate == null)
            {
                return false;
            }

            foreach (WpfSkinNode child in ancestor.Children)
            {
                if (ReferenceEquals(child, candidate) || IsDescendantOf(child, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private void SelectNodeFromPreview(WpfSkinNode node)
        {
            if (_suppressUi || node == null)
            {
                return;
            }

            // ツリー SelectedItemChanged 経由の二重 Refresh を避け、ハイライト更新は一度だけ。
            _suppressUi = true;
            try
            {
                SelectLayoutNode(node);
            }
            finally
            {
                _suppressUi = false;
            }

            RefreshPreview();
        }

        private void UpdatePreviewSourceCaption()
        {
            if (PreviewSourceCaption == null)
            {
                return;
            }

            if (_previewFromSelection && _previewRecord != null)
            {
                string label = !string.IsNullOrWhiteSpace(_previewRecord.Title)
                    ? _previewRecord.Title
                    : (!string.IsNullOrWhiteSpace(_previewRecord.Movie_Body)
                        ? _previewRecord.Movie_Body
                        : _previewRecord.Movie_Name);
                PreviewSourceCaption.Text = string.IsNullOrWhiteSpace(label)
                    ? "プレビュー元: 一覧の選択作品"
                    : $"プレビュー元: {label}";
                return;
            }

            PreviewSourceCaption.Text = "プレビュー元: サンプルデータ";
        }

        private void InitPreviewSourceRadios()
        {
            if (PreviewFromSelectionRadio == null || PreviewSampleRadio == null)
            {
                return;
            }

            bool hasSelection = _selectionPreviewRecord != null;
            PreviewFromSelectionRadio.IsEnabled = hasSelection;
            bool prev = _suppressUi;
            _suppressUi = true;
            if (_previewFromSelection && hasSelection)
            {
                PreviewFromSelectionRadio.IsChecked = true;
            }
            else
            {
                PreviewSampleRadio.IsChecked = true;
            }

            _suppressUi = prev;
        }

        private void PreviewSource_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || PreviewPresenter == null)
            {
                return;
            }

            bool useSelection = PreviewFromSelectionRadio?.IsChecked == true;
            if (useSelection)
            {
                if (_selectionPreviewRecord == null)
                {
                    _suppressUi = true;
                    PreviewSampleRadio.IsChecked = true;
                    _suppressUi = false;
                    return;
                }

                _previewFromSelection = true;
                _previewRecord = _selectionPreviewRecord;
            }
            else
            {
                _previewFromSelection = false;
                _previewRecord = EnsureSamplePreviewRecord();
            }

            PreviewPresenter.DataContext = _previewRecord;
            UpdatePreviewSourceCaption();
        }

        private MovieRecords EnsureSamplePreviewRecord() =>
            _samplePreviewRecord ??= CreateSampleRecord("サンプル動画 A.mp4", "プレビュー用タイトル A");

        private static MovieRecords CreateSampleRecord(string fileName, string title) =>
            new()
            {
                Movie_Name = fileName,
                Movie_Body = System.IO.Path.GetFileNameWithoutExtension(fileName),
                Title = title,
                Movie_Size = 512L * 1024 * 1024,
                Movie_Length = "01:23:45",
                Score = 3,
                View_Count = 1,
                Tags = "preview\nsample",
                Tag = ["preview", "sample"],
                Artist = "Sample Maker",
                Genre = "サンプル",
                IsExists = true,
            };
    }
}
