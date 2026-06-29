using System.Windows.Controls;
using IndigoMovieManager.UserControls;

namespace IndigoMovieManager.Services
{
    internal static class TabListRefreshHelper
    {
        public static void RefreshListByTabIndex(int tabIndex, IMainWindowListViews views)
        {
            switch (tabIndex)
            {
                case 0: views.SmallList.Items.Refresh(); break;
                case 1: views.BigList.Items.Refresh(); break;
                case 2: views.GridList.Items.Refresh(); break;
                case 3: views.ListDataGrid.Items.Refresh(); break;
                case 4: views.BigList10.Items.Refresh(); break;
                case SkinTabIndexHelper.WpfSkinTabIndex:
                    views.WpfSkinList.Items.Refresh();
                    break;
                case SkinTabIndexHelper.WbSkinTabIndex:
                    views.SkinViewGridWb.RenderItems(views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
                    break;
            }
        }
    }

    internal interface IMainWindowListViews
    {
        ListView SmallList { get; }
        ListView BigList { get; }
        ListView GridList { get; }
        DataGrid ListDataGrid { get; }
        ListView BigList10 { get; }
        ListView WpfSkinList { get; }
        SkinView SkinViewGridWb { get; }
    }
}
