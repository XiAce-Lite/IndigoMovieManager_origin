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
            bool hasSources = WpfSkinThumbnailSources.Normalize(_working.Thumbnail?.Sources).Count > 0;
            ThumbCoexistSourcesCheck.IsChecked = hasSources;
            ThumbPreferJacketCheck.IsChecked = !hasSources && _working.Thumbnail?.PreferJacket == true;
            UpdateThumbnailModeChecksEnabled();

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
            // sources 優先: 同居 ON なら PreferJacket は false。list でも Sources は消さない。
            if (ThumbCoexistSourcesCheck.IsChecked == true)
            {
                if (WpfSkinThumbnailSources.Normalize(_working.Thumbnail.Sources).Count == 0)
                {
                    _working.Thumbnail.Sources = WpfSkinThumbnailSources.CreateDefaultCoexist();
                }

                _working.Thumbnail.PreferJacket = false;
            }
            else if (ThumbPreferJacketCheck.IsChecked == true)
            {
                _working.Thumbnail.PreferJacket = true;
                // 同居 OFF にしたときだけ sources を消す（list 切替では触らない）
                if (!_working.IsList)
                {
                    _working.Thumbnail.Sources = null;
                }
            }
            else
            {
                _working.Thumbnail.PreferJacket = false;
                if (!_working.IsList)
                {
                    _working.Thumbnail.Sources = null;
                }
            }

            ApplyStyleEditorsToWorking();
            ApplyNodeEditorsToWorking();
        }


    }
}
