using System.Windows.Controls;

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
    }
}
