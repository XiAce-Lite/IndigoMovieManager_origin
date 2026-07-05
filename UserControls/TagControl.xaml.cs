using IndigoMovieManager.Data;
using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static IndigoMovieManager.Tools;

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
            var container = FindParent<DataGridRow>(this)
                         ?? (DependencyObject)FindParent<ListViewItem>(this)
                         ?? FindParent<ListBoxItem>(this);

            ItemsControl parent = null;
            if (container is DataGridRow dgr)
            {
                parent = ItemsControl.ItemsControlFromItemContainer(dgr);
            }
            else if (container is ListViewItem lvi)
            {
                parent = ItemsControl.ItemsControlFromItemContainer(lvi);
            }
            else if (container is ListBoxItem lbi)
            {
                parent = ItemsControl.ItemsControlFromItemContainer(lbi);
            }

            object itemData = null;
            if (container is FrameworkElement fe && fe.DataContext is MovieRecords rec)
            {
                itemData = rec;
            }

            if (parent != null && itemData != null)
            {
                if (parent is DataGrid dg)
                {
                    dg.SelectedItems.Clear();
                    dg.SelectedItem = itemData;
                    dg.ScrollIntoView(itemData);
                }
                else if (parent is ListView lv)
                {
                    lv.SelectedItems.Clear();
                    lv.SelectedItem = itemData;
                    lv.ScrollIntoView(itemData);
                }
                else if (parent is ListBox lb)
                {
                    lb.SelectedItems.Clear();
                    lb.SelectedItem = itemData;
                    lb.ScrollIntoView(itemData);
                }
            }

            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null || DataContext == null)
            {
                return;
            }

            if (!actions.IsMovieListActive)
            {
                return;
            }

            if (itemData is not MovieRecords mv)
            {
                return;
            }

            string tag = DataContext.ToString();
            if (mv.Tag.Contains(tag))
            {
                mv.Tag.Remove(tag);
                mv.Tags = ConvertTagsWithNewLine(mv.Tag);
                actions.UpdateMovieColumn(mv.Movie_Id, MovieColumn.Tag, mv.Tags);

                try
                {
                    actions.RefreshActiveList(actions.CurrentSkinEngine);
                    actions.RefreshExtDetail();
                }
                catch (Exception)
                {
                }
            }

            e.Handled = true;
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
