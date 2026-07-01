using IndigoMovieManager.UserControls;

namespace IndigoMovieManager.Services
{
    internal static class TabListRefreshHelper
    {
        public static void RefreshActiveList(SkinEngine engine, IMainWindowListViews views)
        {
            switch (engine)
            {
                case SkinEngine.Wpf:
                    views.WpfSkinList.Items.Refresh();
                    break;
                case SkinEngine.Wb:
                    views.SkinViewGridWb.RenderItems(
                        views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
                    break;
            }
        }
    }
}
