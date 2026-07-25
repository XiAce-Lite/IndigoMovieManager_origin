using IndigoMovieManager.Data;
using IndigoMovieManager.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IndigoMovieManager.UserControls
{
    public partial class TagControl : UserControl
    {
        private bool ctrlFlg;

        public TagControl()
        {
            InitializeComponent();
        }

        private async void TagText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null || DataContext == null)
            {
                return;
            }

            string tag = DataContext.ToString();
            string keyword = ctrlFlg
                ? actions.SearchBox.Text + " " + tag
                : tag;

            await actions.SearchByKeywordAsync(keyword).ConfigureAwait(true);
            e.Handled = true;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            return parent as T;
        }

        private void RemoveTag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null || DataContext == null)
            {
                return;
            }

            if (!actions.IsMovieListActive)
            {
                return;
            }

            string tag = DataContext.ToString();
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            MovieRecords owner = ResolveOwnerRecord();
            IReadOnlyList<MovieRecords> selected = actions.GetSelectedMovies() ?? [];
            List<MovieRecords> targets;
            if (selected.Count > 0)
            {
                targets = [.. selected.Where(m => m?.Tag != null && m.Tag.Contains(tag))];
                // 詳細パネル等で owner が選択外のときは owner も対象にする
                if (owner != null
                    && owner.Tag != null
                    && owner.Tag.Contains(tag)
                    && !targets.Exists(m => m.Movie_Id == owner.Movie_Id))
                {
                    targets.Add(owner);
                }
            }
            else if (owner != null && owner.Tag != null && owner.Tag.Contains(tag))
            {
                targets = [owner];
            }
            else
            {
                return;
            }

            if (targets.Count == 0)
            {
                return;
            }

            foreach (MovieRecords mv in targets)
            {
                TagMutationService.ApplyDelete(mv, tag);
                actions.UpdateMovieColumn(mv.Movie_Id, MovieColumn.Tag, mv.Tags);
            }

            try
            {
                actions.RefreshActiveList(actions.CurrentSkinEngine);
                actions.RefreshExtDetail();
            }
            catch (Exception)
            {
            }

            e.Handled = true;
        }

        private MovieRecords ResolveOwnerRecord()
        {
            var container = FindParent<DataGridRow>(this)
                         ?? (DependencyObject)FindParent<ListViewItem>(this)
                         ?? FindParent<ListBoxItem>(this);

            if (container is FrameworkElement fe && fe.DataContext is MovieRecords fromRow)
            {
                return fromRow;
            }

            ExtDetail detail = FindParent<ExtDetail>(this);
            if (detail?.DataContext is MovieRecords fromDetail)
            {
                return fromDetail;
            }

            return null;
        }

        private void TagGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                ctrlFlg = true;
            }
        }

        private void TagGrid_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                ctrlFlg = false;
            }
        }
    }
}
