using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// TagControl.xaml の相互作用ロジック
    /// </summary>
    public partial class TagControl : UserControl
    {
        private bool ctrlFlg = false;
        public TagControl()
        {
            InitializeComponent();
        }

        private async void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null)
            {
                return;
            }

            var item = (Hyperlink)sender;
            if (item != null)
            {
                string keyword;
                if (ctrlFlg)
                {
                    keyword = actions.SearchBox.Text + " " + item.DataContext.ToString();
                }
                else
                {
                    keyword = item.DataContext.ToString();
                }

                await actions.SearchByKeywordAsync(keyword).ConfigureAwait(true);
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            var container = FindParent<DataGridRow>(this)
                         ?? (DependencyObject)FindParent<ListViewItem>(this)
                         ?? FindParent<ListBoxItem>(this);

            ItemsControl parent = null;
            if (container is DataGridRow dgr)
                parent = ItemsControl.ItemsControlFromItemContainer(dgr);
            else if (container is ListViewItem lvi)
                parent = ItemsControl.ItemsControlFromItemContainer(lvi);
            else if (container is ListBoxItem lbi)
                parent = ItemsControl.ItemsControlFromItemContainer(lbi);

            object itemData = null;
            if (container is FrameworkElement fe && fe.DataContext is MovieRecords rec)
                itemData = rec;

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
            var item = (Hyperlink)sender;
            if (actions != null && item != null)
            {
                if (!actions.IsMovieListActive) return;
                if (itemData is not MovieRecords mv) return;

                if (mv.Tag.Contains(item.DataContext))
                {
                    mv.Tag.Remove(item.DataContext.ToString());
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
            }
        }

        private void TagGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl)
            {
                ctrlFlg = true;
            }
        }

        private void TagGrid_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl)
            {
                ctrlFlg = false;
            }
        }
    }
}
