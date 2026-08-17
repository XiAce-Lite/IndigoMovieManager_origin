using System.Windows;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    public partial class TagBarEditWindow : Window
    {
        private MessageBoxResult _closeStatus = MessageBoxResult.Cancel;

        public TagBarEditWindow()
        {
            InitializeComponent();
            OwnedModalWindowHelper.ExcludeFromAltTab(this);
            ContentRendered += TagBarEditWindow_ContentRendered;
        }

        public bool FocusSearchContentsOnOpen { get; set; }

        private void TagBarEditWindow_ContentRendered(object sender, EventArgs e)
        {
            if (FocusSearchContentsOnOpen)
            {
                ContentsBox.Focus();
                ContentsBox.SelectAll();
            }
            else
            {
                TitleBox.Focus();
                TitleBox.SelectAll();
            }
        }

        public string DisplayTitle
        {
            get => TitleBox.Text ?? "";
            set => TitleBox.Text = value ?? "";
        }

        public string SearchContents
        {
            get => ContentsBox.Text ?? "";
            set => ContentsBox.Text = value ?? "";
        }

        public MessageBoxResult CloseStatus() => _closeStatus;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string title = DisplayTitle.Trim();
            string contents = SearchContents.Trim();
            if (!TagBarService.TryNormalizeSaveFields(ref title, ref contents))
            {
                MessageBox.Show(
                    this,
                    "表示名または検索条件のいずれかを入力してください。",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TitleBox.Focus();
                return;
            }

            DisplayTitle = title;
            SearchContents = contents;
            _closeStatus = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _closeStatus = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
