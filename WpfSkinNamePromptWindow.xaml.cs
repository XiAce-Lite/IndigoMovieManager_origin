using System.Windows;
using System.Windows.Input;

namespace IndigoMovieManager
{
    /// <summary>スキンフォルダ名の入力ダイアログ。</summary>
    public partial class WpfSkinNamePromptWindow : Window
    {
        public string FolderName { get; private set; } = "";

        public WpfSkinNamePromptWindow(Window owner, string title, string initialName)
        {
            InitializeComponent();
            Owner = owner;
            Title = title;
            NameBox.Text = initialName ?? "";
            NameBox.SelectAll();
            Loaded += (_, _) => NameBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            FolderName = NameBox.Text?.Trim() ?? "";
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
