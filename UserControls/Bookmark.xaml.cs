using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// Bookmark.xaml の相互作用ロジック
    /// </summary>
    public partial class Bookmark : UserControl
    {
        public Bookmark()
        {
            InitializeComponent();
        }

        private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
                actions?.PlayMovie_Click(sender, e);
            }
        }

        private void FileNameLink_Click(object sender, RoutedEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            var item = (Hyperlink)sender;
            if (actions != null && item != null)
            {
                MovieRecords mv = item.DataContext as MovieRecords;
                actions.SearchBox.Text = mv.Movie_Body;
            }
        }

        private void DeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions is MainWindow ownerWindow)
            {
                ownerWindow.DeleteBookmark(sender, e);
            }
        }
    }
}
