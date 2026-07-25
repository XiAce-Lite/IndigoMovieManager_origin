using System.Collections.Generic;
using System.Windows;

namespace IndigoMovieManager
{
    /// <summary>削除対象など、スキン名を1つ選ぶダイアログ。</summary>
    public partial class WpfSkinPickWindow : Window
    {
        public string SelectedSkinName { get; private set; }

        public WpfSkinPickWindow(Window owner, string title, string message, IReadOnlyList<string> names)
        {
            InitializeComponent();
            Owner = owner;
            Title = title;
            MessageText.Text = message;
            SkinList.ItemsSource = names;
            if (names != null && names.Count > 0)
            {
                SkinList.SelectedIndex = 0;
            }

            OkButton.IsEnabled = names != null && names.Count > 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedSkinName = SkinList.SelectedItem as string;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SkinList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SkinList.SelectedItem != null)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}
