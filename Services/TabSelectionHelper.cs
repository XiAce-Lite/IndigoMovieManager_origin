using System.Windows.Controls;
using IndigoMovieManager.UserControls;

namespace IndigoMovieManager.Services
{
    internal interface IMainWindowListViews
    {
        ListView WpfSkinList { get; }
        SkinView SkinViewGridWb { get; }
        SkinEngine CurrentSkinEngine { get; }
        bool IsMovieListActive { get; }
    }

    internal interface IMainWindowListHost : IMainWindowListViews
    {
        ExtDetail ViewExtDetail { get; }
    }

    internal static class TabSelectionHelper
    {
        public static SkinView GetSkinView(IMainWindowListViews views) =>
            views.CurrentSkinEngine == SkinEngine.Wb ? views.SkinViewGridWb : null;

        public static MovieRecords GetSelectedItem(IMainWindowListViews views)
        {
            if (!views.IsMovieListActive)
            {
                return null;
            }

            return views.CurrentSkinEngine switch
            {
                SkinEngine.Wpf => views.WpfSkinList.SelectedItem as MovieRecords,
                SkinEngine.Wb => views.SkinViewGridWb.GetPrimarySelection(
                    views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>),
                _ => null,
            };
        }

        public static List<MovieRecords> GetSelectedItems(IMainWindowListViews views)
        {
            if (!views.IsMovieListActive)
            {
                return null;
            }

            if (views.CurrentSkinEngine == SkinEngine.Wb)
            {
                return views.SkinViewGridWb.GetSelectedItems(
                    views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
            }

            List<MovieRecords> selected = [];
            foreach (MovieRecords item in views.WpfSkinList.SelectedItems)
            {
                selected.Add(item);
            }

            return selected;
        }

        public static void SelectFirstItem(IMainWindowListViews views)
        {
            if (!views.IsMovieListActive)
            {
                return;
            }

            switch (views.CurrentSkinEngine)
            {
                case SkinEngine.Wpf:
                    if (views.WpfSkinList.Items.Count > 0)
                    {
                        views.WpfSkinList.SelectedIndex = 0;
                    }

                    break;
                case SkinEngine.Wb:
                    views.SkinViewGridWb.SelectFirstItem(
                        views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
                    break;
            }
        }

        public static void RefreshLists(IMainWindowListHost views)
        {
            if (views.CurrentSkinEngine == SkinEngine.Wpf)
            {
                views.WpfSkinList.Items.Refresh();
            }
            else
            {
                views.SkinViewGridWb.RenderItems(
                    views.SkinViewGridWb.Tag as IEnumerable<MovieRecords>);
            }

            MovieRecords mv = GetSelectedItem(views);
            if (mv == null)
            {
                return;
            }

            views.ViewExtDetail.DataContext = mv;
        }
    }
}
