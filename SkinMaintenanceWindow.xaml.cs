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
    /// <summary>WPF スキンの見た目調整と layout ツリー編集を行うウィンドウ。</summary>
    public partial class SkinMaintenanceWindow : Window
    {
        public enum OpenMode
        {
            EditExisting,
            CreateNew,
        }

        private sealed class AspectPreset
        {
            public string Label { get; init; }
            public int Rw { get; init; }
            public int Rh { get; init; }
            public bool IsCustom => Rw <= 0 || Rh <= 0;
        }

        private static readonly AspectPreset[] AspectPresets =
        [
            new() { Label = "16:9（横長）", Rw = 16, Rh = 9 },
            new() { Label = "16:10（横長）", Rw = 16, Rh = 10 },
            new() { Label = "4:3（横長）", Rw = 4, Rh = 3 },
            new() { Label = "3:2（横長）", Rw = 3, Rh = 2 },
            new() { Label = "1:1（正方形）", Rw = 1, Rh = 1 },
            new() { Label = "9:16（縦長）", Rw = 9, Rh = 16 },
            new() { Label = "10:16（縦長）", Rw = 10, Rh = 16 },
            new() { Label = "3:4（縦長）", Rw = 3, Rh = 4 },
            new() { Label = "2:3（縦長）", Rw = 2, Rh = 3 },
            new() { Label = "9:12（マンガ頁寄り）", Rw = 9, Rh = 12 },
            new() { Label = "カスタム", Rw = 0, Rh = 0 },
        ];

        /// <summary>JSON 未指定・空文字用。実在フォント名と衝突しない表示ラベル。</summary>
        private const string FontFamilyUnspecifiedDisplay = "(未指定)";

        private sealed class FieldPaletteItem
        {
            public WpfSkinFieldDescriptor Field { get; init; }
            public bool IsPlaced { get; init; }
            public string DisplayName => Field?.DisplayName ?? "";
            public string Id => Field?.Id ?? "";
        }

        private readonly PreviewThumbConverter _previewThumbConverter = new(new Converter.NoLockImageConverter());
        private readonly Converter.AspectStretchConverter _previewAspectConverter = new();
        private readonly Converter.FileSizeConverter _previewFileSizeConverter = new();
        private MovieRecords _previewRecord;
        private bool _previewFromSelection;
        private readonly MovieRecords _selectionPreviewRecord;
        private MovieRecords _samplePreviewRecord;

        private WpfSkinDefinition _working;
        private string _folderName;
        private bool _isUnsavedNew;
        private bool _suppressUi;
        private string _selectedStyleKey;
        private bool _dirty;
        private bool _allowClose;
        private bool _suppressClosePrompt;
        private bool _gridClampedFromSource;
        private int _sourceColumns;
        private int _sourceRows;
        private ObservableCollection<WpfSkinLayoutTreeNode> _layoutRoots = [];
        private WpfSkinLayoutTreeNode _selectedLayoutNode;
        private Point _paletteDragStart;
        private Point _treeDragStart;
        private Point _fieldPaletteDragStart;
        private WpfSkinLayoutTreeNode _treeDragSource;
        private FieldPaletteItem _fieldPaletteDragSource;
        private bool _dropApplying;
        private IDisposable _designSessionScope;
        private readonly Stack<WpfSkinDefinition> _undoStack = new();
        private readonly Stack<WpfSkinDefinition> _redoStack = new();
        private bool _suppressUndo;
        private bool _propertyUndoArmed = true;
        private bool _cardWidthDragging;
        private bool _cardHeightDragging;
        private int _columnConstraintIndex = -1;
        private WpfSkinNode _columnConstraintGrid;

        public string ResultSkinFolderName { get; private set; }
        public Action<string> LiveApplyRequested { get; set; }

        public bool SkinWasDeleted { get; private set; }

        public bool SkinWasSaved { get; private set; }

        public SkinMaintenanceWindow(
            Window owner,
            OpenMode mode,
            string folderName = null,
            MovieRecords previewRecord = null,
            string templateFolderName = null,
            WpfSkinDefinition prebuiltDefinition = null)
        {
            _suppressUi = true;
            InitializeComponent();
            Owner = owner;
            InitStaticSelectors();
            _selectionPreviewRecord = previewRecord;
            _previewFromSelection = previewRecord != null;
            _previewRecord = previewRecord ?? EnsureSamplePreviewRecord();
            PreviewPresenter.DataContext = _previewRecord;
            InitPreviewSourceRadios();
            UpdatePreviewSourceCaption();
            if (PreviewScroll != null)
            {
                PreviewScroll.SizeChanged += (_, _) => ApplyPreviewStretchSlot();
            }

            if (mode == OpenMode.CreateNew)
            {
                if (prebuiltDefinition != null)
                {
                    _working = prebuiltDefinition;
                }
                else
                {
                    _working = string.IsNullOrWhiteSpace(templateFolderName)
                        ? WpfSkinStorage.CreateFromDefaultTemplate()
                        : WpfSkinStorage.CreateFromTemplate(templateFolderName);
                }
                _folderName = null;
                _isUnsavedNew = true;
            }
            else
            {
                string target = folderName;
                if (string.IsNullOrWhiteSpace(target)
                    || !WpfSkinLoader.TryLoad(target, out WpfSkinDefinition loaded))
                {
                    loaded = WpfSkinLoader.LoadDefault();
                    target = loaded.Name;
                    if (!WpfSkinStorage.FolderExists(target))
                    {
                        target = WpfSkinLoader.DefaultSkinName;
                        if (!WpfSkinLoader.TryLoad(target, out loaded))
                        {
                            loaded = WpfSkinLoader.CreateBuiltInDefault();
                        }
                    }
                }

                _working = WpfSkinStorage.Clone(loaded);
                _folderName = target;
                _isUnsavedNew = false;
            }

            LoadFormFromWorking();
            RefreshChrome();
            RefreshPreview();
            _suppressUi = false;
            _dirty = false;
        }

        private bool IsProtected =>
            !_isUnsavedNew && SkinNameSortHelper.IsProtectedDefaultSkin(_folderName);

        private void InitStaticSelectors()
        {
            ThumbAspectCombo.ItemsSource = AspectPresets;
            ThumbAspectCombo.DisplayMemberPath = nameof(AspectPreset.Label);
            ThumbAspectCombo.SelectedIndex = 0;

            ThumbColumnsCombo.ItemsSource = Enumerable.Range(1, 5).ToList();
            ThumbRowsCombo.ItemsSource = Enumerable.Range(1, 5).ToList();
            ThumbColumnsCombo.SelectedItem = 1;
            ThumbRowsCombo.SelectedItem = 1;

            List<string> families = [FontFamilyUnspecifiedDisplay];
            families.AddRange(
                Fonts.SystemFontFamilies
                    .Select(f => f.Source)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            StyleFontFamilyCombo.ItemsSource = families;
            NodeFontFamilyCombo.ItemsSource = families;

            NodeFieldCombo.ItemsSource = WpfSkinLayoutEditor.FieldOptions;
        }

        private void LoadFormFromWorking()
        {
            _suppressUi = true;
            DisplayNameBox.Text = _working.Name ?? "";
            SelectComboByContent(TypeCombo, string.IsNullOrWhiteSpace(_working.Type) ? "card" : _working.Type);
            SelectColorProfile(_working.ColorProfile);
            SurfaceBackgroundBox.Text = _working.Surface?.Background ?? "";
            CardPaddingSpin.Value = (int)Math.Round(_working.Card?.Padding ?? 0);
            CardWidthSpin.Value = Math.Max(0, (int)Math.Round(_working.Card?.Width ?? 0));
            CardHeightSpin.Value = Math.Max(0, (int)Math.Round(_working.Card?.Height ?? 0));
            CardBackgroundBox.Text = _working.Card?.Background ?? "";
            CardStretchCheck.IsChecked = _working.Card?.Stretch == true;

            int width = Math.Max(1, _working.Thumbnail?.Width ?? 400);
            int height = Math.Max(1, _working.Thumbnail?.Height ?? 225);
            ThumbWidthSpin.Value = width;
            ThumbHeightSpin.Value = height;
            SelectAspectForSize(width, height);
            UpdateHeightEditability();

            _sourceColumns = Math.Max(1, _working.Thumbnail?.Columns ?? 1);
            _sourceRows = Math.Max(1, _working.Thumbnail?.Rows ?? 1);
            _gridClampedFromSource = _sourceColumns > 5 || _sourceRows > 5;
            ThumbColumnsCombo.SelectedItem = Math.Clamp(_sourceColumns, 1, 5);
            ThumbRowsCombo.SelectedItem = Math.Clamp(_sourceRows, 1, 5);
            ThumbPreferJacketCheck.IsChecked = _working.Thumbnail?.PreferJacket == true;

            RefreshStyleList();
            RebuildLayoutTree(_working.Card?.Layout);
            RefreshFieldPalette();
            _suppressUi = false;
        }

        private void RefreshFieldPalette()
        {
            WpfSkinNode layout = _working?.Card?.Layout;
            HashSet<string> used = WpfSkinFieldCatalog.CollectUsedFieldIds(layout);
            FieldPaletteList.ItemsSource = WpfSkinFieldCatalog.All
                .Select(f => new FieldPaletteItem
                {
                    Field = f,
                    IsPlaced = used.Contains(f.Id),
                })
                .ToList();
        }

        private void EnsureAutomaticStyleForNode(WpfSkinNode node)
        {
            if (_working == null || node == null || node.IsContainer || !string.Equals(node.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string autoStyle = WpfSkinFieldCatalog.GetDefaultStyleKey(node.Field);
            if (string.IsNullOrWhiteSpace(autoStyle))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(node.Style))
            {
                node.Style = autoStyle;
            }

            if (string.Equals(node.Style, autoStyle, StringComparison.OrdinalIgnoreCase))
            {
                WpfSkinLayoutEditor.EnsureStyleExists(_working, autoStyle);
            }
        }

        private void RefreshStyleList()
        {
            StyleList.ItemsSource = _working.Styles?.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
                ?? [];
            RefreshNodeStyleOptions();

            if (StyleList.Items.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(_selectedStyleKey)
                    || !StyleList.Items.Cast<string>().Any(k => string.Equals(k, _selectedStyleKey, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedStyleKey = StyleList.Items[0] as string;
                }

                StyleList.SelectedItem = StyleList.Items.Cast<string>()
                    .FirstOrDefault(k => string.Equals(k, _selectedStyleKey, StringComparison.OrdinalIgnoreCase));
                StyleKeyBox.Text = _selectedStyleKey ?? "";
                LoadStyleEditors();
            }
            else
            {
                _selectedStyleKey = null;
                StyleKeyBox.Text = "";
                ClearStyleEditors();
            }
        }

        private void RefreshNodeStyleOptions()
        {
            List<string> styleKeys = [""];
            if (_working?.Styles != null)
            {
                styleKeys.AddRange(_working.Styles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            }

            NodeStyleCombo.ItemsSource = styleKeys;
        }

        private void RefreshChrome()
        {
            string folderLabel = _isUnsavedNew ? "(未保存)" : _folderName;
            StatusTitle.Text = _isUnsavedNew
                ? "WPFスキン — 新規（テンプレート複製）"
                : $"WPFスキン — {folderLabel}";

            var lines = new List<string>();
            if (_isUnsavedNew)
            {
                lines.Add("CardLarge を雛形にした未保存スキンです。初回は「名前を付けて保存」でフォルダを作成してください。");
            }
            else if (IsProtected)
            {
                lines.Add("同梱 Default 系は上書き・削除できません。変更を残す場合は「名前を付けて保存」でフォークしてください。");
            }
            else
            {
                lines.Add($"保存先: Skins/Wpf/{_folderName}/skin.json");
            }

            if (_gridClampedFromSource)
            {
                lines.Add($"元 JSON の格子は {_sourceColumns}×{_sourceRows} です。UI 上限は 5×5。保存すると UI の値に更新されます。");
            }

            lines.Add("layout は tree で編集し、プレビューは単一カードで確認します。");
            StatusBanner.Text = string.Join(" ", lines);
            UpdateSaveButtonEnabled();
            DeleteButton.IsEnabled = !_isUnsavedNew && !IsProtected;
            SaveAsButton.IsEnabled = true;
        }

        private void UpdateSaveButtonEnabled()
        {
            SaveButton.IsEnabled = _dirty && !_isUnsavedNew && !IsProtected;
        }

        private void MarkDirty()
        {
            if (_suppressUi)
            {
                return;
            }

            _dirty = true;
            UpdateSaveButtonEnabled();
        }

        private void CaptureUndoSnapshot()
        {
            if (_suppressUi || _suppressUndo || _working == null)
            {
                return;
            }

            ApplyFormToWorking();
            _undoStack.Push(WpfSkinStorage.Clone(_working));
            _redoStack.Clear();
            UpdateUndoRedoButtons();
        }

        private void CapturePropertyUndoIfNeeded()
        {
            if (!_propertyUndoArmed)
            {
                return;
            }

            CaptureUndoSnapshot();
            _propertyUndoArmed = false;
        }

        private void UpdateUndoRedoButtons()
        {
            UndoButton.IsEnabled = _undoStack.Count > 0;
            RedoButton.IsEnabled = _redoStack.Count > 0;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e) => Undo();
        private void RedoButton_Click(object sender, RoutedEventArgs e) => Redo();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsEditingTextInput())
            {
                return;
            }

            ModifierKeys mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.Control && e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (mods == ModifierKeys.Control && e.Key == Key.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if (mods == ModifierKeys.Control && e.Key == Key.S)
            {
                SaveCurrentSkin(saveAs: false);
                e.Handled = true;
            }
            else if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
            {
                SaveCurrentSkin(saveAs: true);
                e.Handled = true;
            }
            else if (mods == ModifierKeys.Control && e.Key == Key.D)
            {
                DuplicateSelectedNode();
                e.Handled = true;
            }
            else if (mods == ModifierKeys.None && e.Key == Key.Delete)
            {
                DeleteNodeButton_Click(null, null);
                e.Handled = true;
            }
        }

        private static bool IsEditingTextInput()
        {
            DependencyObject focused = Keyboard.FocusedElement as DependencyObject;
            for (DependencyObject current = focused; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is TextBoxBase or PasswordBox or RichTextBox)
                {
                    return true;
                }

                if (current is ComboBox combo && combo.IsEditable)
                {
                    return true;
                }
            }

            return false;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            ApplyFormToWorking();
            _redoStack.Push(WpfSkinStorage.Clone(_working));
            RestoreWorking(_undoStack.Pop());
            UpdateUndoRedoButtons();
        }

        private void Redo()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            ApplyFormToWorking();
            _undoStack.Push(WpfSkinStorage.Clone(_working));
            RestoreWorking(_redoStack.Pop());
            UpdateUndoRedoButtons();
        }

        private void RestoreWorking(WpfSkinDefinition snapshot)
        {
            _suppressUndo = true;
            _suppressUi = true;
            _working = WpfSkinStorage.Clone(snapshot);
            LoadFormFromWorking();
            RefreshChrome();
            RefreshPreview();
            _dirty = true;
            _propertyUndoArmed = true;
            _suppressUndo = false;
            UpdateSaveButtonEnabled();
            UpdateUndoRedoButtons();
        }

        private void ApplyFormToWorking()
        {
            _working.Name = DisplayNameBox.Text?.Trim() ?? "";
            _working.Type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "card";
            _working.ColorProfile = (ColorProfileCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            _working.Surface ??= new WpfSkinSurface();
            _working.Surface.Background = SurfaceBackgroundBox.Text?.Trim() ?? "";
            _working.Card ??= new WpfSkinCard();
            _working.Card.Padding = CardPaddingSpin.Value;
            _working.Card.Width = CardWidthSpin.Value;
            _working.Card.Height = CardHeightSpin.Value;
            _working.Card.Background = CardBackgroundBox.Text?.Trim() ?? "";
            _working.Card.Stretch = CardStretchCheck.IsChecked == true;
            _working.Card.Layout ??= new WpfSkinNode();
            _working.Thumbnail ??= new WpfSkinThumbnail();
            _working.Thumbnail.Width = Math.Max(1, ThumbWidthSpin.Value);
            _working.Thumbnail.Height = Math.Max(1, ThumbHeightSpin.Value);
            _working.Thumbnail.Columns = Math.Clamp(ThumbColumnsCombo.SelectedItem as int? ?? 1, 1, 5);
            _working.Thumbnail.Rows = Math.Clamp(ThumbRowsCombo.SelectedItem as int? ?? 1, 1, 5);
            _working.Thumbnail.PreferJacket = ThumbPreferJacketCheck.IsChecked == true;
            ApplyStyleEditorsToWorking();
            ApplyNodeEditorsToWorking();
        }

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

        private void LoadNodeEditors()
        {
            _suppressUi = true;
            bool hasSelection = _selectedLayoutNode?.Model != null;
            SetNodeEditorsEnabled(hasSelection);
            if (!hasSelection)
            {
                ClearNodeEditors();
                UpdateSelectedNodeLayoutHint(null);
                _suppressUi = false;
                return;
            }

            WpfSkinNode node = _selectedLayoutNode.Model;
            SelectedNodePathText.Text = BuildNodePath(_selectedLayoutNode);

            SelectComboByContent(NodePanelCombo, node.ResolvePanel());
            SelectComboByContent(NodeStackCombo, string.IsNullOrWhiteSpace(node.Stack) ? "vertical" : node.Stack);
            SelectComboByContent(NodeTypeCombo, string.IsNullOrWhiteSpace(node.Type) ? "text" : node.Type);
            SetEditableComboValue(NodeFieldCombo, node.Field);
            NodeLabelBox.Text = node.Label ?? "";
            NodeHeaderBox.Text = node.Header ?? "";
            NodeFormatBox.Text = node.Format ?? "";
            SetEditableComboValue(NodeStyleCombo, node.Style);
            SelectComboByContent(NodeAlignCombo, node.Align ?? "");
            NodeRowsBox.Text = string.Join(",", node.Rows ?? []);
            NodeColumnsBox.Text = string.Join(",", node.Columns ?? []);
            NodeRowSpin.Value = Math.Max(0, node.Row);
            NodeColSpin.Value = Math.Max(0, node.Col);
            NodeRowSpanSpin.Value = Math.Max(1, node.RowSpan);
            NodeColSpanSpin.Value = Math.Max(1, node.ColSpan);
            NodeWidthSpin.Value = node.Width.HasValue && node.Width.Value > 0
                ? (int)Math.Round(node.Width.Value)
                : 0;
            NodeFontSizeSpin.Value = node.FontSize > 0 ? (int)Math.Round(node.FontSize) : 0;
            SelectFontFamily(NodeFontFamilyCombo, node.FontFamily);
            NodeMarginBox.Text = WpfSkinLayoutEditor.FormatSpacing(node.Margin);
            NodePaddingBox.Text = WpfSkinLayoutEditor.FormatSpacing(node.Padding);
            NodeForegroundBox.Text = node.Foreground ?? "";
            NodeBackgroundBox.Text = node.Background ?? "";
            SelectComboByContent(NodeHAlignCombo, node.HAlign ?? "");
            SelectComboByContent(NodeVAlignCombo, node.VAlign ?? "");
            NodeBoldCheck.IsChecked = node.Bold;
            NodeItalicCheck.IsChecked = node.Italic;
            NodeWrapCheck.IsChecked = node.Wrap;

            bool isContainer = node.IsContainer;
            bool isThumbnail = string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase);
            NodePanelCombo.IsEnabled = isContainer;
            NodeStackCombo.IsEnabled = isContainer && !node.IsGrid;
            NodeRowsBox.IsEnabled = isContainer && node.IsGrid;
            NodeColumnsBox.IsEnabled = isContainer && node.IsGrid;
            NodeTypeCombo.IsEnabled = !isContainer;
            NodeFieldCombo.IsEnabled = !isContainer;
            NodeLabelBox.IsEnabled = !isContainer;
            NodeHeaderBox.IsEnabled = !isContainer;
            NodeFormatBox.IsEnabled = !isContainer;
            bool styleApplicable = !isContainer
                && !isThumbnail
                && !string.Equals(node.Type, "tags", StringComparison.OrdinalIgnoreCase);
            NodeStyleCombo.IsEnabled = styleApplicable;
            NodeAlignCombo.IsEnabled = !isContainer;
            NodeFontSizeSpin.IsEnabled = !isContainer;
            NodeFontFamilyCombo.IsEnabled = !isContainer;
            NodeForegroundBox.IsEnabled = !isContainer;
            NodeBackgroundBox.IsEnabled = true;
            NodeBoldCheck.IsEnabled = !isContainer;
            NodeItalicCheck.IsEnabled = !isContainer;
            NodeWrapCheck.IsEnabled = !isContainer;
            // サムネは表示幅が親列追従のため、width は参照表示（編集可だが Tip で説明）
            NodeWidthSpin.IsEnabled = hasSelection;
            NodeWidthHintText.Text = isThumbnail
                ? "サムネ: 表示は親列に追従。0=自動。生成サイズは左の「サムネ生成」"
                : "0 で親に追従（固定したいときだけ px）";

            UpdateSelectedNodeLayoutHint(node);

            _suppressUi = false;
        }

        private void UpdateSelectedNodeLayoutHint(WpfSkinNode node)
        {
            if (SelectedNodeLayoutHint == null)
            {
                return;
            }

            if (node == null)
            {
                SelectedNodeLayoutHint.Visibility = Visibility.Collapsed;
                SelectedNodeLayoutHint.Text = "";
                return;
            }

            if (string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                SelectedNodeLayoutHint.Text =
                    "選択: サムネ — 表示枠は親列幅に追従／生成ピクセルは左ペイン「サムネ生成」";
                SelectedNodeLayoutHint.Visibility = Visibility.Visible;
                return;
            }

            if (node.IsGrid && node.Columns != null && node.Columns.Count > 0)
            {
                string cols = string.Join(" | ", node.Columns.Select(FormatColumnConstraintLabel));
                SelectedNodeLayoutHint.Text = $"選択: grid 列 = {cols}";
                SelectedNodeLayoutHint.Visibility = Visibility.Visible;
                return;
            }

            SelectedNodeLayoutHint.Visibility = Visibility.Collapsed;
            SelectedNodeLayoutHint.Text = "";
        }

        private static string FormatColumnConstraintLabel(string col)
        {
            if (string.IsNullOrWhiteSpace(col))
            {
                return "自動";
            }

            string t = col.Trim();
            if (t.EndsWith('*'))
            {
                return "残り(*)";
            }

            if (string.Equals(t, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "自動";
            }

            return $"固定({t})";
        }

        private void SetNodeEditorsEnabled(bool enabled)
        {
            foreach (Control control in new Control[]
            {
                NodePanelCombo, NodeStackCombo, NodeTypeCombo, NodeFieldCombo, NodeLabelBox, NodeHeaderBox, NodeFormatBox,
                NodeStyleCombo, NodeAlignCombo, NodeRowsBox, NodeColumnsBox, NodeFontFamilyCombo, NodeMarginBox, NodePaddingBox,
                NodeForegroundBox, NodeBackgroundBox, NodeHAlignCombo, NodeVAlignCombo
            })
            {
                control.IsEnabled = enabled;
            }

            foreach (IntegerSpinBox spin in new[]
            {
                NodeRowSpin, NodeColSpin, NodeRowSpanSpin, NodeColSpanSpin, NodeWidthSpin, NodeFontSizeSpin
            })
            {
                spin.IsEnabled = enabled;
            }

            foreach (CheckBox check in new[] { NodeBoldCheck, NodeItalicCheck, NodeWrapCheck })
            {
                check.IsEnabled = enabled;
            }
        }

        private void ClearNodeEditors()
        {
            SelectedNodePathText.Text = "ルートを選択してください。";
            SelectComboByContent(NodePanelCombo, "stack");
            SelectComboByContent(NodeStackCombo, "vertical");
            SelectComboByContent(NodeTypeCombo, "text");
            SetEditableComboValue(NodeFieldCombo, "");
            NodeLabelBox.Text = "";
            NodeHeaderBox.Text = "";
            NodeFormatBox.Text = "";
            SetEditableComboValue(NodeStyleCombo, "");
            SelectComboByContent(NodeAlignCombo, "");
            NodeRowsBox.Text = "";
            NodeColumnsBox.Text = "";
            NodeRowSpin.Value = 0;
            NodeColSpin.Value = 0;
            NodeRowSpanSpin.Value = 1;
            NodeColSpanSpin.Value = 1;
            NodeWidthSpin.Value = 0;
            if (NodeWidthHintText != null)
            {
                NodeWidthHintText.Text = "0 で親に追従";
            }

            NodeFontSizeSpin.Value = 0;
            SelectFontFamily(NodeFontFamilyCombo, null);
            NodeMarginBox.Text = "";
            NodePaddingBox.Text = "";
            NodeForegroundBox.Text = "";
            NodeBackgroundBox.Text = "";
            SelectComboByContent(NodeHAlignCombo, "");
            SelectComboByContent(NodeVAlignCombo, "");
            NodeBoldCheck.IsChecked = false;
            NodeItalicCheck.IsChecked = false;
            NodeWrapCheck.IsChecked = false;
        }

        private void ApplyNodeEditorsToWorking()
        {
            if (_selectedLayoutNode?.Model == null)
            {
                return;
            }

            WpfSkinNode node = _selectedLayoutNode.Model;
            if (node.IsContainer)
            {
                node.Panel = (NodePanelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? node.ResolvePanel();
                node.Stack = (NodeStackCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "vertical";
                if (string.Equals(node.ResolvePanel(), "grid", StringComparison.OrdinalIgnoreCase))
                {
                    node.Rows = ParseCsvList(NodeRowsBox.Text, "auto");
                    node.Columns = ParseCsvList(NodeColumnsBox.Text, "*");
                }
            }
            else
            {
                node.Type = (NodeTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "text";
                node.Field = GetEditableComboValue(NodeFieldCombo);
                node.Label = NodeLabelBox.Text?.Trim() ?? "";
                node.Header = NodeHeaderBox.Text?.Trim() ?? "";
                node.Format = NodeFormatBox.Text?.Trim() ?? "";
                node.Style = GetEditableComboValue(NodeStyleCombo);
                node.Align = (NodeAlignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                node.FontSize = NodeFontSizeSpin.Value;
                node.FontFamily = ResolveSelectedFontFamilyOrNull(NodeFontFamilyCombo);
                node.Foreground = NodeForegroundBox.Text?.Trim() ?? "";
                node.Bold = NodeBoldCheck.IsChecked == true;
                node.Italic = NodeItalicCheck.IsChecked == true;
                node.Wrap = NodeWrapCheck.IsChecked == true;
            }

            node.Row = NodeRowSpin.Value;
            node.Col = NodeColSpin.Value;
            node.RowSpan = Math.Max(1, NodeRowSpanSpin.Value);
            node.ColSpan = Math.Max(1, NodeColSpanSpin.Value);
            int widthPx = NodeWidthSpin.Value;
            node.Width = widthPx > 0 ? widthPx : null;
            node.Margin = WpfSkinSpacing.Parse(NodeMarginBox.Text);
            node.Padding = WpfSkinSpacing.Parse(NodePaddingBox.Text);
            node.Background = NodeBackgroundBox.Text?.Trim() ?? "";
            node.HAlign = (NodeHAlignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            node.VAlign = (NodeVAlignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _selectedLayoutNode.NotifyDisplayNameChanged();
        }

        private static List<string> ParseCsvList(string text, string fallback)
        {
            List<string> values = text?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList() ?? [];
            if (values.Count == 0)
            {
                values.Add(fallback);
            }

            return values;
        }

        private static string BuildNodePath(WpfSkinLayoutTreeNode node)
        {
            var parts = new Stack<string>();
            for (WpfSkinLayoutTreeNode current = node; current != null; current = current.Parent)
            {
                parts.Push(current.DisplayName);
            }

            return string.Join(" > ", parts);
        }

        private void Field_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            MarkDirty();
            RefreshPreview();
        }

        private void Spin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            MarkDirty();
            RefreshPreview();
        }

        private void ThumbSpin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            if (!IsCustomAspectSelected() && ReferenceEquals(sender, ThumbWidthSpin))
            {
                RecalcHeightFromAspect();
            }

            MarkDirty();
            RefreshPreview();
        }

        private void StyleSpin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            MarkDirty();
            ApplyStyleEditorsToWorking();
            RefreshPreview();
        }

        private void ThumbAspect_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            UpdateHeightEditability();
            if (!IsCustomAspectSelected())
            {
                RecalcHeightFromAspect();
            }

            MarkDirty();
            RefreshPreview();
        }

        private bool IsCustomAspectSelected() =>
            ThumbAspectCombo.SelectedItem is AspectPreset preset && preset.IsCustom;

        private void UpdateHeightEditability()
        {
            bool custom = IsCustomAspectSelected();
            ThumbHeightSpin.IsEnabled = custom;
            ThumbHeightSpin.Opacity = custom ? 1.0 : 0.7;
        }

        private void RecalcHeightFromAspect()
        {
            if (ThumbAspectCombo.SelectedItem is not AspectPreset preset || preset.IsCustom)
            {
                return;
            }

            int width = Math.Max(1, ThumbWidthSpin.Value);
            int height = WpfSkinAspectMath.HeightFromWidth(width, preset.Rw, preset.Rh);
            _suppressUi = true;
            ThumbHeightSpin.Value = height;
            _suppressUi = false;
        }

        private void SelectAspectForSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                ThumbAspectCombo.SelectedItem = AspectPresets[^1];
                return;
            }

            double actual = (double)width / height;
            AspectPreset best = AspectPresets[^1];
            foreach (AspectPreset preset in AspectPresets)
            {
                if (preset.IsCustom)
                {
                    continue;
                }

                double target = (double)preset.Rw / preset.Rh;
                double rel = Math.Abs(actual - target) / target;
                if (rel <= 0.005)
                {
                    best = preset;
                    break;
                }
            }

            ThumbAspectCombo.SelectedItem = best;
        }

        private void StyleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            ApplyStyleEditorsToWorking();
            _selectedStyleKey = StyleList.SelectedItem as string;
            StyleKeyBox.Text = _selectedStyleKey ?? "";
            LoadStyleEditors();
        }

        private void StyleField_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            MarkDirty();
            ApplyStyleEditorsToWorking();
            RefreshPreview();
        }

        private void LoadStyleEditors()
        {
            _suppressUi = true;
            if (string.IsNullOrEmpty(_selectedStyleKey)
                || _working.Styles == null
                || !_working.Styles.TryGetValue(_selectedStyleKey, out WpfSkinStyle style)
                || style == null)
            {
                ClearStyleEditors();
                _suppressUi = false;
                return;
            }

            int fontSize = (int)Math.Round(style.FontSize <= 0 ? 13 : style.FontSize);
            StyleFontSizeSpin.Value = Math.Clamp(fontSize, 6, 72);
            SelectFontFamily(StyleFontFamilyCombo, style.FontFamily);
            StyleForegroundBox.Text = style.Foreground ?? "";
            StyleBackgroundBox.Text = style.Background ?? "";
            SelectComboByContent(StyleAlignCombo, NormalizeAlign(style.Align));
            StyleBoldCheck.IsChecked = style.Bold;
            StyleItalicCheck.IsChecked = style.Italic;
            StyleWrapCheck.IsChecked = style.Wrap;
            _suppressUi = false;
        }

        private void ClearStyleEditors()
        {
            StyleFontSizeSpin.Value = 13;
            SelectFontFamily(StyleFontFamilyCombo, null);
            StyleForegroundBox.Text = "";
            StyleBackgroundBox.Text = "";
            SelectComboByContent(StyleAlignCombo, "left");
            StyleBoldCheck.IsChecked = false;
            StyleItalicCheck.IsChecked = false;
            StyleWrapCheck.IsChecked = false;
        }

        private void ApplyStyleEditorsToWorking()
        {
            if (string.IsNullOrEmpty(_selectedStyleKey) || _working.Styles == null)
            {
                return;
            }

            if (!_working.Styles.TryGetValue(_selectedStyleKey, out WpfSkinStyle style) || style == null)
            {
                style = new WpfSkinStyle();
                _working.Styles[_selectedStyleKey] = style;
            }

            style.FontSize = StyleFontSizeSpin.Value;
            style.FontFamily = ResolveSelectedFontFamilyOrNull(StyleFontFamilyCombo);
            style.Foreground = StyleForegroundBox.Text?.Trim() ?? "";
            style.Background = StyleBackgroundBox.Text?.Trim() ?? "";
            style.Align = (StyleAlignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "left";
            style.Bold = StyleBoldCheck.IsChecked == true;
            style.Italic = StyleItalicCheck.IsChecked == true;
            style.Wrap = StyleWrapCheck.IsChecked == true;
        }

        private void StyleAddButton_Click(object sender, RoutedEventArgs e)
        {
            bool canApply = _selectedLayoutNode?.Model != null && !_selectedLayoutNode.Model.IsContainer;
            var dialog = new StyleQuickCreateWindow(this, StyleKeyBox.Text, canApply);
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            CaptureUndoSnapshot();
            WpfSkinStyle initial = WpfSkinLayoutEditor.CreateStylePreset(dialog.PresetId);
            if (!WpfSkinLayoutEditor.TryAddStyle(_working, dialog.StyleKey, initial, out string error))
            {
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    UpdateUndoRedoButtons();
                }

                ShowError(error);
                return;
            }

            _selectedStyleKey = dialog.StyleKey.Trim();
            StyleKeyBox.Text = _selectedStyleKey;
            if (dialog.ApplyToSelectedNode && canApply)
            {
                _selectedLayoutNode.Model.Style = _selectedStyleKey;
                LoadNodeEditors();
            }

            RefreshStyleList();
            MarkDirty();
            RefreshPreview();
        }

        private void StyleRenameButton_Click(object sender, RoutedEventArgs e)
        {
            CaptureUndoSnapshot();
            if (!WpfSkinLayoutEditor.TryRenameStyle(_working, _selectedStyleKey, StyleKeyBox.Text, out string error))
            {
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                    UpdateUndoRedoButtons();
                }

                ShowError(error);
                return;
            }

            _selectedStyleKey = StyleKeyBox.Text.Trim();
            RefreshStyleList();
            LoadNodeEditors();
            MarkDirty();
            RefreshPreview();
        }

        private void StyleDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedStyleKey))
            {
                return;
            }

            CaptureUndoSnapshot();
            if (!WpfSkinLayoutEditor.DeleteStyle(_working, _selectedStyleKey))
            {
                return;
            }

            _selectedStyleKey = null;
            RefreshStyleList();
            LoadNodeEditors();
            MarkDirty();
            RefreshPreview();
        }

        private void StyleApplyToSelectedNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayoutNode?.Model == null || _selectedLayoutNode.Model.IsContainer)
            {
                ShowError("style を適用したい text / tags / thumbnail を、中央プレビューまたは右ツリーで選択してください。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedStyleKey))
            {
                ShowError("先に左ペインで style キーを選択してください。");
                return;
            }

            CaptureUndoSnapshot();
            _selectedLayoutNode.Model.Style = _selectedStyleKey;
            LoadNodeEditors();
            MarkDirty();
            RefreshPreview();
        }

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
            MarkDirty();
            SelectLayoutNode(parent.Model);
            RefreshPreview();
            RefreshFieldPalette();
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

        private void NodeField_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _selectedLayoutNode?.Model == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            ApplyNodeEditorsToWorking();
            SelectedNodePathText.Text = BuildNodePath(_selectedLayoutNode);
            MarkDirty();
            RefreshPreview();
        }

        private void NodeSpin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _selectedLayoutNode?.Model == null)
            {
                return;
            }

            CapturePropertyUndoIfNeeded();
            ApplyNodeEditorsToWorking();
            MarkDirty();
            RefreshPreview();
        }

        private void SurfaceBackgroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(SurfaceBackgroundBox);
        private void CardBackgroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(CardBackgroundBox);
        private void StyleForegroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(StyleForegroundBox);
        private void StyleBackgroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(StyleBackgroundBox);
        private void NodeForegroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(NodeForegroundBox);
        private void NodeBackgroundPick_Click(object sender, RoutedEventArgs e) => PickColorInto(NodeBackgroundBox);

        private void PickColorInto(TextBox target)
        {
            var dialog = new WpfColorPickerWindow(this, target.Text);
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            target.Text = dialog.SelectedHex;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSkin(saveAs: false);
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSkin(saveAs: true);
        }

        private void SaveCurrentSkin(bool saveAs)
        {
            if (!saveAs && (_isUnsavedNew || IsProtected))
            {
                saveAs = true;
            }

            if (!saveAs && !_dirty)
            {
                return;
            }

            if (saveAs)
            {
                SaveAsCurrentSkin();
                return;
            }

            ApplyFormToWorking();
            if (!WpfSkinStorage.TrySave(_working, _folderName, overwriteExisting: true, out string error))
            {
                ShowError(error);
                return;
            }

            CompleteSave(_folderName);
        }

        private void SaveAsCurrentSkin()
        {
            ApplyFormToWorking();
            string suggested = _isUnsavedNew ? "MySkin" : (_folderName + "Copy");
            var prompt = new WpfSkinNamePromptWindow(this, "名前を付けて保存", suggested);
            if (prompt.ShowDialog() != true)
            {
                return;
            }

            string name = prompt.FolderName;
            if (!WpfSkinStorage.TryValidateFolderName(name, out string validateError))
            {
                ShowError(validateError);
                return;
            }

            bool exists = WpfSkinStorage.FolderExists(name);
            if (exists)
            {
                if (SkinNameSortHelper.IsProtectedDefaultSkin(name))
                {
                    ShowError("Default 系スキンは上書きできません。");
                    return;
                }

                var confirm = new MessageBoxEx(this)
                {
                    DlogTitle = "上書き確認",
                    DlogMessage = $"スキン「{name}」は既に存在します。上書きしますか？",
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.EventQuestion,
                };
                confirm.ShowDialog();
                if (confirm.CloseStatus() != MessageBoxResult.OK)
                {
                    return;
                }
            }

            if (!WpfSkinStorage.TrySave(_working, name, overwriteExisting: exists, out string error))
            {
                ShowError(error);
                return;
            }

            _folderName = name;
            _isUnsavedNew = false;
            CompleteSave(name);
        }

        private void CompleteSave(string name)
        {
            _folderName = name;
            SkinWasSaved = true;
            ResultSkinFolderName = name;
            _dirty = false;
            _gridClampedFromSource = false;
            RefreshChrome();
            LiveApplyRequested?.Invoke(name);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUnsavedNew || IsProtected || string.IsNullOrWhiteSpace(_folderName))
            {
                return;
            }

            var confirm = new MessageBoxEx(this)
            {
                DlogTitle = "スキンをゴミ箱へ",
                DlogMessage = $"スキン「{_folderName}」をゴミ箱へ移動します。よろしいですか？",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.Delete,
            };
            confirm.ShowDialog();
            if (confirm.CloseStatus() != MessageBoxResult.OK)
            {
                return;
            }

            if (!WpfSkinStorage.TryDeleteToRecycleBin(_folderName, out string error))
            {
                ShowError(error);
                return;
            }

            SkinWasDeleted = true;
            ResultSkinFolderName = _folderName;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                _designSessionScope?.Dispose();
                _designSessionScope = null;
                DialogResult = SkinWasSaved || SkinWasDeleted;
                return;
            }

            if (_suppressClosePrompt)
            {
                e.Cancel = true;
                return;
            }

            if (!_dirty)
            {
                _designSessionScope?.Dispose();
                _designSessionScope = null;
                DialogResult = SkinWasSaved || SkinWasDeleted;
                return;
            }

            e.Cancel = true;
            _suppressClosePrompt = true;

            var confirm = new MessageBoxEx(this)
            {
                DlogTitle = "未保存の変更",
                DlogMessage = "変更が保存されていません。閉じますか？",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.EventQuestion,
            };
            confirm.ShowDialog();

            if (confirm.CloseStatus() == MessageBoxResult.OK)
            {
                _allowClose = true;
                _suppressClosePrompt = false;
                Dispatcher.BeginInvoke(new Action(Close));
                return;
            }

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(() => _suppressClosePrompt = false));
        }

        private void ShowError(string message) =>
            MessageBox.Show(this, message ?? "エラーが発生しました。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void SelectComboByContent(ComboBox combo, string content)
        {
            foreach (ComboBoxItem item in combo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private static void SetEditableComboValue(ComboBox combo, string value)
        {
            string text = value?.Trim() ?? "";
            if (combo.Items.OfType<string>().Any(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase)))
            {
                combo.SelectedItem = combo.Items.OfType<string>()
                    .First(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                combo.Text = text;
            }
        }

        private static string GetEditableComboValue(ComboBox combo) =>
            (combo.SelectedItem as string ?? combo.Text ?? "").Trim();

        private void SelectColorProfile(string profile)
        {
            string value = profile?.Trim() ?? "";
            foreach (ComboBoxItem item in ColorProfileCombo.Items.OfType<ComboBoxItem>())
            {
                string tag = item.Tag as string ?? "";
                if (string.Equals(tag, value, StringComparison.OrdinalIgnoreCase))
                {
                    ColorProfileCombo.SelectedItem = item;
                    return;
                }
            }

            ColorProfileCombo.SelectedIndex = 0;
        }

        private static string NormalizeAlign(string align)
        {
            string value = align?.Trim().ToLowerInvariant() ?? "";
            return value switch
            {
                "center" => "center",
                "right" => "right",
                _ => "left",
            };
        }

        private static void SelectFontFamily(ComboBox combo, string family)
        {
            if (!string.IsNullOrWhiteSpace(family)
                && combo.Items.OfType<string>()
                    .Any(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase)))
            {
                combo.SelectedItem = combo.Items.OfType<string>()
                    .First(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase));
                return;
            }

            if (combo.Items.OfType<string>()
                .Any(n => string.Equals(n, FontFamilyUnspecifiedDisplay, StringComparison.Ordinal)))
            {
                combo.SelectedItem = FontFamilyUnspecifiedDisplay;
            }
            else
            {
                combo.SelectedIndex = -1;
                combo.SelectedItem = null;
            }
        }

        private static string ResolveSelectedFontFamilyOrNull(ComboBox combo)
        {
            string selected = combo.SelectedItem as string ?? combo.Text;
            if (string.IsNullOrWhiteSpace(selected)
                || string.Equals(selected, FontFamilyUnspecifiedDisplay, StringComparison.Ordinal))
            {
                return null;
            }

            return selected.Trim();
        }
    }
}
