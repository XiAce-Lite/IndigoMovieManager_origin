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
