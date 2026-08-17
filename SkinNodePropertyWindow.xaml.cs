using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndigoMovieManager.Controls;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using MaterialDesignThemes.Wpf;

namespace IndigoMovieManager
{
    /// <summary>
    /// 配置済みノードの Kind 別プロパティ編集。
    /// </summary>
    public class SkinNodePropertyWindow : Window
    {
        private readonly WpfSkinNode _node;
        private readonly WpfSkinFieldKind _kind;
        private CheckBox _linkCheck;
        private CheckBox _wrapCheck;
        private CheckBox _boldCheck;
        private CheckBox _italicCheck;
        private IntegerSpinBox _fontSizeSpin;
        private ComboBox _fontFamilyCombo;
        private TextBox _foregroundBox;
        private TextBox _marginBox;
        private TextBox _paddingBox;
        private ComboBox _alignCombo;
        private TextBox _styleBox;
        private TextBox _labelBox;
        private TextBox _headerBox;

        public SkinNodePropertyWindow(Window owner, WpfSkinNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            Owner = owner;
            Title = "ノードのプロパティ";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            OwnedModalWindowHelper.ExcludeFromAltTab(this);
            Background = TryFindResource("MaterialDesign.Brush.Background") as Brush
                ?? SystemColors.WindowBrush;

            string key = WpfSkinFieldCatalog.ResolveUniqueKey(node) ?? node.Type ?? "node";
            WpfSkinFieldCatalog.TryGet(key, out WpfSkinFieldDescriptor desc);
            _kind = desc?.Kind ?? InferKind(node);

            var root = new DockPanel { Margin = new Thickness(12) };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
            };
            DockPanel.SetDock(buttons, Dock.Bottom);
            var ok = new Button { Content = "OK", Width = 88, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "キャンセル", Width = 88, IsCancel = true };
            ok.Click += (_, _) =>
            {
                ApplyToNode();
                DialogResult = true;
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            var form = new StackPanel();
            form.Children.Add(new TextBlock
            {
                Text = desc?.DisplayName ?? key,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
            });
            form.Children.Add(new TextBlock
            {
                Text = $"種別: {_kind}",
                Opacity = 0.75,
                Margin = new Thickness(0, 0, 0, 10),
            });

            BuildForm(form);
            root.Children.Add(form);
            Content = root;
        }

        private static WpfSkinFieldKind InferKind(WpfSkinNode node)
        {
            string type = node.Type?.Trim().ToLowerInvariant() ?? "";
            if (type == "thumbnail")
            {
                return WpfSkinFieldKind.Thumbnail;
            }

            if (type == "tags")
            {
                return WpfSkinFieldKind.Tags;
            }

            if (WpfSkinFieldCatalog.IsPathField(node.Field))
            {
                return WpfSkinFieldKind.Path;
            }

            return WpfSkinFieldKind.Text;
        }

        private void BuildForm(StackPanel form)
        {
            if (_kind == WpfSkinFieldKind.Thumbnail)
            {
                form.Children.Add(new TextBlock
                {
                    Text = "サムネ枠の個別 source はノードに保存されます。preferJacket / sources 同居チェックは左ペインのスキン全体設定です。",
                    TextWrapping = TextWrapping.Wrap,
                });
                return;
            }

            if (_kind == WpfSkinFieldKind.Tags)
            {
                _wrapCheck = AddCheck(form, "折り返し (wrap)", _node.Wrap);
                return;
            }

            if (_kind == WpfSkinFieldKind.Path)
            {
                bool link = _node.Link ?? true;
                _linkCheck = AddCheck(form, "リンクにする（URL/パスを開く）", link);
                _wrapCheck = AddCheck(form, "折り返し (wrap)", _node.Wrap);
                AddCommonTextProps(form, includeTypography: true);
                return;
            }

            _wrapCheck = AddCheck(form, "折り返し (wrap)", _node.Wrap);
            _boldCheck = AddCheck(form, "太字", _node.Bold);
            _italicCheck = AddCheck(form, "斜体", _node.Italic);
            AddCommonTextProps(form, includeTypography: true);
        }

        private void AddCommonTextProps(StackPanel form, bool includeTypography)
        {
            _labelBox = AddText(form, "label（接頭辞）", _node.Label ?? "");
            _headerBox = AddText(form, "header（list 列見出し）", _node.Header ?? "");
            _styleBox = AddText(form, "style キー", _node.Style ?? "");
            if (includeTypography)
            {
                _fontSizeSpin = new IntegerSpinBox
                {
                    Hint = "fontSize（0=style依存）",
                    Minimum = 0,
                    Maximum = 72,
                    Value = _node.FontSize > 0 ? (int)Math.Round(_node.FontSize) : 0,
                    Margin = new Thickness(0, 0, 0, 6),
                };
                form.Children.Add(_fontSizeSpin);

                _fontFamilyCombo = new ComboBox
                {
                    IsEditable = true,
                    Margin = new Thickness(0, 0, 0, 6),
                };
                HintAssist.SetHint(_fontFamilyCombo, "fontFamily");
                HintAssist.SetIsFloating(_fontFamilyCombo, true);
                TextFieldAssist.SetHasOutlinedTextField(_fontFamilyCombo, true);
                _fontFamilyCombo.Text = _node.FontFamily ?? "";
                form.Children.Add(_fontFamilyCombo);

                _foregroundBox = AddText(form, "foreground", _node.Foreground ?? "");
                _alignCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
                HintAssist.SetHint(_alignCombo, "align");
                HintAssist.SetIsFloating(_alignCombo, true);
                TextFieldAssist.SetHasOutlinedTextField(_alignCombo, true);
                foreach (string a in new[] { "", "left", "center", "right" })
                {
                    _alignCombo.Items.Add(new ComboBoxItem { Content = a });
                }

                SelectCombo(_alignCombo, _node.Align ?? "");
                form.Children.Add(_alignCombo);
            }

            _marginBox = AddText(form, "margin", WpfSkinLayoutEditor.FormatSpacing(_node.Margin));
            _paddingBox = AddText(form, "padding", WpfSkinLayoutEditor.FormatSpacing(_node.Padding));
        }

        private void ApplyToNode()
        {
            if (_kind == WpfSkinFieldKind.Thumbnail)
            {
                return;
            }

            if (_wrapCheck != null)
            {
                _node.Wrap = _wrapCheck.IsChecked == true;
            }

            if (_kind == WpfSkinFieldKind.Tags)
            {
                return;
            }

            if (_linkCheck != null)
            {
                _node.Link = _linkCheck.IsChecked == true;
            }

            if (_boldCheck != null)
            {
                _node.Bold = _boldCheck.IsChecked == true;
            }

            if (_italicCheck != null)
            {
                _node.Italic = _italicCheck.IsChecked == true;
            }

            if (_labelBox != null)
            {
                _node.Label = _labelBox.Text?.Trim() ?? "";
            }

            if (_headerBox != null)
            {
                _node.Header = _headerBox.Text?.Trim() ?? "";
            }

            if (_styleBox != null)
            {
                _node.Style = _styleBox.Text?.Trim() ?? "";
            }

            if (_fontSizeSpin != null)
            {
                _node.FontSize = _fontSizeSpin.Value;
            }

            if (_fontFamilyCombo != null)
            {
                _node.FontFamily = string.IsNullOrWhiteSpace(_fontFamilyCombo.Text)
                    ? ""
                    : _fontFamilyCombo.Text.Trim();
            }

            if (_foregroundBox != null)
            {
                _node.Foreground = _foregroundBox.Text?.Trim() ?? "";
            }

            if (_alignCombo != null)
            {
                _node.Align = (_alignCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            }

            if (_marginBox != null)
            {
                _node.Margin = WpfSkinSpacing.Parse(_marginBox.Text);
            }

            if (_paddingBox != null)
            {
                _node.Padding = WpfSkinSpacing.Parse(_paddingBox.Text);
            }
        }

        private static CheckBox AddCheck(StackPanel form, string label, bool value)
        {
            var check = new CheckBox
            {
                Content = label,
                IsChecked = value,
                Margin = new Thickness(0, 0, 0, 8),
            };
            form.Children.Add(check);
            return check;
        }

        private static TextBox AddText(StackPanel form, string hint, string value)
        {
            var box = new TextBox
            {
                Text = value,
                Margin = new Thickness(0, 0, 0, 6),
            };
            HintAssist.SetHint(box, hint);
            HintAssist.SetIsFloating(box, true);
            TextFieldAssist.SetHasOutlinedTextField(box, true);
            form.Children.Add(box);
            return box;
        }

        private static void SelectCombo(ComboBox combo, string value)
        {
            foreach (object item in combo.Items)
            {
                if (item is ComboBoxItem cbi
                    && string.Equals(cbi.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }
    }
}
