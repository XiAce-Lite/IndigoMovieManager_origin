using System.Windows.Controls;
using IndigoMovieManager.UserControls;

namespace IndigoMovieManager.Services
{
    internal interface IMainWindowTabViews : IMainWindowListViews
    {
        TabControl Tabs { get; }
        TabItem TabSmall { get; }
        TabItem TabBig { get; }
        TabItem TabGrid { get; }
        TabItem TabList { get; }
        TabItem TabBig10 { get; }
        ExtDetail ViewExtDetail { get; }
    }

    internal static class TabSelectionHelper
    {
        public static SkinView GetSkinView(IMainWindowListViews views, int tabIndex) =>
            tabIndex switch
            {
                SkinTabIndexHelper.WbSkinTabIndex => views.SkinViewGridWb,
                _ => null,
            };

        public static MovieRecords GetSelectedItem(IMainWindowTabViews views)
        {
            return views.Tabs.SelectedIndex switch
            {
                0 => views.SmallList.SelectedItem as MovieRecords,
                1 => views.BigList.SelectedItem as MovieRecords,
                2 => views.GridList.SelectedItem as MovieRecords,
                3 => views.ListDataGrid.SelectedItem as MovieRecords,
                4 => views.BigList10.SelectedItem as MovieRecords,
                SkinTabIndexHelper.WpfSkinTabIndex => views.WpfSkinList.SelectedItem as MovieRecords,
                SkinTabIndexHelper.WbSkinTabIndex => views.SkinViewGridWb.GetPrimarySelection(views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>),
                _ => null,
            };
        }

        public static List<MovieRecords> GetSelectedItems(IMainWindowTabViews views)
        {
            List<MovieRecords> mv = [];
            switch (views.Tabs.SelectedIndex)
            {
                case 0:
                    foreach (MovieRecords item in views.SmallList.SelectedItems) { mv.Add(item); }
                    break;
                case 1:
                    foreach (MovieRecords item in views.BigList.SelectedItems) { mv.Add(item); }
                    break;
                case 2:
                    foreach (MovieRecords item in views.GridList.SelectedItems) { mv.Add(item); }
                    break;
                case 3:
                    foreach (MovieRecords item in views.ListDataGrid.SelectedItems) { mv.Add(item); }
                    break;
                case 4:
                    foreach (MovieRecords item in views.BigList10.SelectedItems) { mv.Add(item); }
                    break;
                case SkinTabIndexHelper.WpfSkinTabIndex:
                    foreach (MovieRecords item in views.WpfSkinList.SelectedItems) { mv.Add(item); }
                    break;
                case SkinTabIndexHelper.WbSkinTabIndex:
                    return views.SkinViewGridWb.GetSelectedItems(views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
                default: return null;
            }

            return mv;
        }

        public static void SwitchTab(IMainWindowTabViews views, string skin)
        {
            switch (skin)
            {
                case "DefaultSmall":
                    views.TabSmall.IsSelected = true;
                    if (views.SmallList.Items.Count > 0) { views.SmallList.SelectedIndex = 0; }
                    break;
                case "DefaultBig":
                    views.TabBig.IsSelected = true;
                    if (views.BigList.Items.Count > 0) { views.BigList.SelectedIndex = 0; }
                    break;
                case "DefaultGrid":
                    views.TabGrid.IsSelected = true;
                    if (views.GridList.Items.Count > 0) { views.GridList.SelectedIndex = 0; }
                    break;
                case "DefaultList":
                    views.TabList.IsSelected = true;
                    if (views.ListDataGrid.Items.Count > 0) { views.ListDataGrid.SelectedIndex = 0; }
                    break;
                default:
                    views.TabSmall.IsSelected = true;
                    if (views.SmallList.Items.Count > 0) { views.SmallList.SelectedIndex = 0; }
                    break;
            }
        }

        public static void SelectFirstItem(IMainWindowTabViews views)
        {
            switch (views.Tabs.SelectedIndex)
            {
                case 0:
                    views.TabSmall.IsSelected = true;
                    if (views.SmallList.Items.Count > 0) { views.SmallList.SelectedIndex = 0; }
                    break;
                case 1:
                    views.TabBig.IsSelected = true;
                    if (views.BigList.Items.Count > 0) { views.BigList.SelectedIndex = 0; }
                    break;
                case 2:
                    views.TabGrid.IsSelected = true;
                    if (views.GridList.Items.Count > 0) { views.GridList.SelectedIndex = 0; }
                    break;
                case 3:
                    views.TabList.IsSelected = true;
                    if (views.ListDataGrid.Items.Count > 0) { views.ListDataGrid.SelectedIndex = 0; }
                    break;
                case 4:
                    views.TabBig10.IsSelected = true;
                    if (views.BigList10.Items.Count > 0) { views.BigList10.SelectedIndex = 0; }
                    break;
                case SkinTabIndexHelper.WpfSkinTabIndex:
                    if (views.WpfSkinList.Items.Count > 0) { views.WpfSkinList.SelectedIndex = 0; }
                    break;
                case SkinTabIndexHelper.WbSkinTabIndex:
                    views.SkinViewGridWb.SelectFirstItem(views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
                    break;
                default:
                    views.TabSmall.IsSelected = true;
                    if (views.SmallList.Items.Count > 0) { views.SmallList.SelectedIndex = 0; }
                    break;
            }
        }

        public static void RefreshLists(IMainWindowTabViews views)
        {
            views.SmallList.Items.Refresh();
            views.BigList.Items.Refresh();
            views.GridList.Items.Refresh();
            views.ListDataGrid.Items.Refresh();
            views.BigList10.Items.Refresh();

            if (views.WpfSkinList != null)
            {
                views.WpfSkinList.Items.Refresh();
            }

            if (views.SkinViewGridWb != null)
            {
                views.SkinViewGridWb.RenderItems(views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
            }

            MovieRecords mv = GetSelectedItem(views);
            if (mv == null) { return; }
            views.ViewExtDetail.DataContext = mv;
        }
    }
}
