using System.Windows;
using System.Windows.Controls;

namespace IndigoMovieManager
{
    /// <summary>named style の簡易作成ダイアログ。</summary>
    public partial class StyleQuickCreateWindow : Window
    {
        public string StyleKey { get; private set; }
        public string PresetId { get; private set; }
        public bool ApplyToSelectedNode { get; private set; }

        public StyleQuickCreateWindow(Window owner, string suggestedKey, bool canApplyToSelected)
        {
            InitializeComponent();
            Owner = owner;
            KeyBox.Text = suggestedKey ?? "";
            ApplyToSelectedCheck.IsEnabled = canApplyToSelected;
            ApplyToSelectedCheck.IsChecked = canApplyToSelected;
            KeyBox.SelectAll();
            KeyBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            StyleKey = KeyBox.Text?.Trim() ?? "";
            PresetId = (PresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "empty";
            ApplyToSelectedNode = ApplyToSelectedCheck.IsChecked == true && ApplyToSelectedCheck.IsEnabled;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
