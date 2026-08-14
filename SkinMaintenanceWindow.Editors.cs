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
                string src = node.Source?.Trim().ToLowerInvariant() ?? "";
                SelectedNodeLayoutHint.Text = src switch
                {
                    "comment1" => "選択: ジャケ写（Comment1）— preferJacket / sources は左ペイン",
                    "local" => "選択: サムネイル（ローカル）— 生成ピクセルは左ペイン「サムネ生成」",
                    _ => "選択: サムネ（兼用枠）— preferJacket 時はジャケ差し替え／生成は左ペイン",
                };
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

            if (ReferenceEquals(sender, TypeCombo))
            {
                // Type を先に反映して list/card の有効状態を更新
                _working.Type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "card";
                UpdateThumbnailModeChecksEnabled();
            }

            CapturePropertyUndoIfNeeded();
            MarkDirty();
            RefreshPreview();
        }

        private void ThumbPreferJacketCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            if (ThumbPreferJacketCheck.IsChecked == true)
            {
                _suppressUi = true;
                ThumbCoexistSourcesCheck.IsChecked = false;
                _suppressUi = false;
            }

            UpdateThumbnailModeChecksEnabled();
            Field_Changed(sender, e);
        }

        private void ThumbCoexistSourcesCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressUi || _working == null)
            {
                return;
            }

            if (ThumbCoexistSourcesCheck.IsChecked == true)
            {
                _suppressUi = true;
                ThumbPreferJacketCheck.IsChecked = false;
                _suppressUi = false;
            }

            UpdateThumbnailModeChecksEnabled();
            Field_Changed(sender, e);
        }

        private void UpdateThumbnailModeChecksEnabled()
        {
            if (_working == null)
            {
                return;
            }

            bool isList = string.Equals(
                (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _working.Type,
                "list",
                StringComparison.OrdinalIgnoreCase);

            // list では同居チェックを無効（値は保持）。preferJacket は list でも可。
            ThumbCoexistSourcesCheck.IsEnabled = !isList;
            ThumbPreferJacketCheck.IsEnabled = ThumbCoexistSourcesCheck.IsChecked != true;
            if (isList)
            {
                // list 中は preferJacket を常に操作可（sources は描画無視）
                ThumbPreferJacketCheck.IsEnabled = true;
            }
            else if (ThumbPreferJacketCheck.IsChecked == true)
            {
                ThumbCoexistSourcesCheck.IsEnabled = false;
            }
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
    }
}
