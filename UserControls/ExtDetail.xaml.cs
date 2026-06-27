using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// ExtDetail.xaml の相互作用ロジック
    /// </summary>
    public partial class ExtDetail : UserControl
    {
        public ExtDetail()
        {
            InitializeComponent();
            DataContext = new MovieRecords();
        }

        private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
                actions?.PlayMovie_Click(sender, e);
            }
        }

        private void ThumbnailImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1)
            {
                return;
            }

            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            actions?.RequestDetailThumbnailRecreate();
        }

        public void Refresh()
        {
            ExtDetailTags.Items.Refresh();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var item = (Hyperlink)sender;
            if (item != null)
            {
                MovieRecords mv = item.DataContext as MovieRecords;
                if (Path.Exists(mv.Movie_Path))
                {
                    Process.Start("explorer.exe", $"/select,{mv.Movie_Path}");
                }
            }
        }

        private async void FileNameLink_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MovieRecords record)
            {
                IMainWindowActions actions = MainWindowActionsHelper.GetActions(this)
                    ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (actions != null)
                {
                    var quoted = $"\"{record.Movie_Body}\"";
                    await actions.SearchByKeywordAsync(quoted).ConfigureAwait(true);
                }
            }
        }

        private async void Ext_Click(object sender, RoutedEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            var item = (Hyperlink)sender;
            if (actions != null && item != null)
            {
                MovieRecords mv = item.DataContext as MovieRecords;
                await actions.SearchByKeywordAsync(mv.Ext).ConfigureAwait(true);
            }
        }
    }
}
