using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndigoMovieManager.Controls;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;

namespace IndigoMovieManager
{
    /// <summary>WPF スキンの見た目調整・別名保存・削除ウィンドウ（layout 編集は対象外）。</summary>
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

        private readonly OpenMode _mode;
        private readonly PreviewThumbConverter _previewThumbConverter = new(new Converter.NoLockImageConverter());
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

        public string ResultSkinFolderName { get; private set; }

        public bool SkinWasDeleted { get; private set; }

        public bool SkinWasSaved { get; private set; }

        public SkinMaintenanceWindow(Window owner, OpenMode mode, string folderName = null)
        {
            _suppressUi = true;
            InitializeComponent();
            Owner = owner;
            _mode = mode;
            InitStaticSelectors();

            if (mode == OpenMode.CreateNew)
            {
                _working = WpfSkinStorage.CreateFromDefaultTemplate();
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

            StyleFontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void LoadFormFromWorking()
        {
            DisplayNameBox.Text = _working.Name ?? "";
            SelectComboByContent(TypeCombo, string.IsNullOrWhiteSpace(_working.Type) ? "card" : _working.Type);
            SelectColorProfile(_working.ColorProfile);
            SurfaceBackgroundBox.Text = _working.Surface?.Background ?? "";
            CardWidthSpin.Value = (int)Math.Round(_working.Card?.Width ?? 0);
            CardHeightSpin.Value = (int)Math.Round(_working.Card?.Height ?? 0);
            CardPaddingSpin.Value = (int)Math.Round(_working.Card?.Padding ?? 0);
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

            StyleList.ItemsSource = _working.Styles?.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();
            if (StyleList.Items.Count > 0)
            {
                StyleList.SelectedIndex = 0;
                _selectedStyleKey = StyleList.SelectedItem as string;
                LoadStyleEditors();
            }
            else
            {
                _selectedStyleKey = null;
                ClearStyleEditors();
            }
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

            StatusBanner.Text = string.Join(" ", lines);
            SaveButton.IsEnabled = !_isUnsavedNew && !IsProtected;
            DeleteButton.IsEnabled = !_isUnsavedNew && !IsProtected;
            SaveAsButton.IsEnabled = true;
        }

        private void ApplyFormToWorking()
        {
            _working.Name = DisplayNameBox.Text?.Trim() ?? "";
            _working.Type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "card";
            _working.ColorProfile = (ColorProfileCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            _working.Surface ??= new WpfSkinSurface();
            _working.Surface.Background = SurfaceBackgroundBox.Text?.Trim() ?? "";
            _working.Card ??= new WpfSkinCard();
            _working.Card.Width = CardWidthSpin.Value;
            _working.Card.Height = CardHeightSpin.Value;
            _working.Card.Padding = CardPaddingSpin.Value;
            _working.Card.Background = CardBackgroundBox.Text?.Trim() ?? "";
            _working.Card.Stretch = CardStretchCheck.IsChecked == true;
            _working.Thumbnail ??= new WpfSkinThumbnail();
            _working.Thumbnail.Width = Math.Max(1, ThumbWidthSpin.Value);
            _working.Thumbnail.Height = Math.Max(1, ThumbHeightSpin.Value);
            _working.Thumbnail.Columns = Math.Clamp(ThumbColumnsCombo.SelectedItem as int? ?? 1, 1, 5);
            _working.Thumbnail.Rows = Math.Clamp(ThumbRowsCombo.SelectedItem as int? ?? 1, 1, 5);
            ApplyStyleEditorsToWorking();
        }

        private void RefreshPreview()
        {
            if (_working == null)
            {
                return;
            }

            ApplyFormToWorking();
            WpfSkinDefinition previewDef = WpfSkinStorage.Clone(_working);
            _previewThumbConverter.UpdateLayout(
                previewDef.Thumbnail.Width,
                previewDef.Thumbnail.Height,
                previewDef.Thumbnail.Columns,
                previewDef.Thumbnail.Rows);

            var context = new WpfSkinTemplateBuilder.BuildContext
            {
                ItemContextMenu = null,
                ImageConverter = _previewThumbConverter,
                AspectConverter = new Converter.AspectStretchConverter(),
                FileSizeConverter = new Converter.FileSizeConverter(),
            };
            WpfSkinTemplateBuilder.ApplyHostContext(context);

            PreviewList.ItemsPanel = WpfSkinTemplateBuilder.BuildItemsPanel(previewDef);
            PreviewList.ItemTemplate = WpfSkinTemplateBuilder.BuildItemTemplate(previewDef);
            PreviewList.Background = WpfSkinTemplateBuilder.ParseSurfaceBackground(previewDef)
                ?? Brushes.Transparent;

            PreviewList.ItemsSource = new ObservableCollection<MovieRecords>
            {
                CreateSampleRecord("サンプル動画 A.mp4", "プレビュー用タイトル A"),
                CreateSampleRecord("サンプル動画 B.mp4", "プレビュー用タイトル B"),
            };
        }

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

        private void Field_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            _dirty = true;
            RefreshPreview();
        }

        private void Spin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            _dirty = true;
            RefreshPreview();
        }

        private void ThumbSpin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            if (!IsCustomAspectSelected() && ReferenceEquals(sender, ThumbWidthSpin))
            {
                RecalcHeightFromAspect();
            }

            _dirty = true;
            RefreshPreview();
        }

        private void StyleSpin_Changed(object sender, RoutedPropertyChangedEventArgs<int> e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            _dirty = true;
            ApplyStyleEditorsToWorking();
            RefreshPreview();
        }

        private void ThumbAspect_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            UpdateHeightEditability();
            if (!IsCustomAspectSelected())
            {
                RecalcHeightFromAspect();
            }

            _dirty = true;
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
            int height = Math.Max(1, (int)Math.Round(width * (double)preset.Rh / preset.Rw));
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
            LoadStyleEditors();
        }

        private void StyleField_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            _dirty = true;
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

            string family = style.FontFamily ?? "";
            if (!string.IsNullOrWhiteSpace(family)
                && StyleFontFamilyCombo.Items.OfType<string>()
                    .Any(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase)))
            {
                StyleFontFamilyCombo.SelectedItem = StyleFontFamilyCombo.Items.OfType<string>()
                    .First(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase));
            }
            else if (StyleFontFamilyCombo.Items.Count > 0)
            {
                StyleFontFamilyCombo.SelectedIndex = 0;
            }

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
            if (StyleFontFamilyCombo.Items.Count > 0)
            {
                StyleFontFamilyCombo.SelectedIndex = 0;
            }

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
            style.FontFamily = StyleFontFamilyCombo.SelectedItem as string ?? "";
            style.Foreground = StyleForegroundBox.Text?.Trim() ?? "";
            style.Background = StyleBackgroundBox.Text?.Trim() ?? "";
            style.Align = (StyleAlignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "left";
            style.Bold = StyleBoldCheck.IsChecked == true;
            style.Italic = StyleItalicCheck.IsChecked == true;
            style.Wrap = StyleWrapCheck.IsChecked == true;
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

        private void SurfaceBackgroundPick_Click(object sender, RoutedEventArgs e) =>
            PickColorInto(SurfaceBackgroundBox);

        private void CardBackgroundPick_Click(object sender, RoutedEventArgs e) =>
            PickColorInto(CardBackgroundBox);

        private void StyleForegroundPick_Click(object sender, RoutedEventArgs e) =>
            PickColorInto(StyleForegroundBox);

        private void StyleBackgroundPick_Click(object sender, RoutedEventArgs e) =>
            PickColorInto(StyleBackgroundBox);

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
            if (_isUnsavedNew || IsProtected)
            {
                return;
            }

            ApplyFormToWorking();
            if (!WpfSkinStorage.TrySave(_working, _folderName, overwriteExisting: true, out string error))
            {
                ShowError(error);
                return;
            }

            SkinWasSaved = true;
            ResultSkinFolderName = _folderName;
            _dirty = false;
            _gridClampedFromSource = false;
            RefreshChrome();
            MessageBox.Show(this, $"「{_folderName}」を保存しました。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
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
            SkinWasSaved = true;
            ResultSkinFolderName = name;
            _dirty = false;
            _gridClampedFromSource = false;
            RefreshChrome();
            MessageBox.Show(this, $"「{name}」として保存しました。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
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

        // IsCancel=True が DialogResult 経由で閉じる。ここで Close() すると Closing が二重発火する。
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                DialogResult = SkinWasSaved || SkinWasDeleted;
                return;
            }

            // 確認ダイアログ直後の Esc / IsCancel 再配送で Closing が再入するのを防ぐ
            if (_suppressClosePrompt)
            {
                e.Cancel = true;
                return;
            }

            if (!_dirty)
            {
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
    }
}
