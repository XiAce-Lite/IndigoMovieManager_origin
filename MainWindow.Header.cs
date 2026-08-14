using System.Windows;
using System.Windows.Controls;
namespace IndigoMovieManager
{
    public partial class MainWindow
    {
        private const double HeaderContentLeftInset = 8;
        private const double HeaderStackedBreakpoint = 1320;
        private const double HeaderCompactBreakpoint = 1000;

        private enum HeaderLayoutMode
        {
            Wide,
            Stacked,
            Compact,
        }

        private HeaderLayoutMode _headerLayoutMode;

        private static HeaderLayoutMode ResolveHeaderLayoutMode(double width)
        {
            if (width < HeaderCompactBreakpoint)
            {
                return HeaderLayoutMode.Compact;
            }

            if (width < HeaderStackedBreakpoint)
            {
                return HeaderLayoutMode.Stacked;
            }

            return HeaderLayoutMode.Wide;
        }

        private void HeaderZone_Loaded(object sender, RoutedEventArgs e) =>
            ApplyHeaderLayout(HeaderZone.ActualWidth);

        private void HeaderZone_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyHeaderLayout(HeaderZone.ActualWidth);

            double headerHeight = HeaderZone.ActualHeight;
            if (headerHeight > 0 && headerHeight < 300)
            {
                uxDockingManager.Margin = new Thickness(0, headerHeight, 0, 0);
            }
        }

        private void ApplyHeaderLayout(double width)
        {
            if (HeaderLayoutGrid == null
                || HeaderSearchPanel == null
                || HeaderToolbarHost == null
                || HeaderModePanel == null
                || HeaderSortPanel == null
                || HeaderSortLabel == null
                || lbDbFullPath == null
                || SearchBox == null)
            {
                return;
            }

            HeaderLayoutMode mode = ResolveHeaderLayoutMode(width);
            if (_headerLayoutMode == mode && HeaderLayoutGrid.IsLoaded)
            {
                return;
            }

            _headerLayoutMode = mode;
            EnsureHeaderToolbarChildrenReparented(mode == HeaderLayoutMode.Compact);

            Thickness contentMargin = new(HeaderContentLeftInset, 0, 12, 0);
            Thickness rowMargin = new(HeaderContentLeftInset, 2, 12, 0);
            Thickness dbMarginWide = new(HeaderContentLeftInset, -6, 0, 0);
            Thickness dbMarginTight = new(HeaderContentLeftInset, -2, 0, 0);

            switch (mode)
            {
                case HeaderLayoutMode.Wide:
                    ApplyHeaderWideLayout(contentMargin, dbMarginWide);
                    break;
                case HeaderLayoutMode.Stacked:
                    ApplyHeaderStackedLayout(contentMargin, rowMargin, dbMarginTight);
                    break;
                case HeaderLayoutMode.Compact:
                    ApplyHeaderCompactLayout(contentMargin, rowMargin, dbMarginTight);
                    break;
            }
        }

        private void ApplyHeaderWideLayout(Thickness contentMargin, Thickness dbMargin)
        {
            Grid.SetRow(MenuToggleButton, 0);
            Grid.SetColumn(MenuToggleButton, 0);
            Grid.SetRowSpan(MenuToggleButton, 1);

            Grid.SetRow(HeaderSearchPanel, 0);
            Grid.SetColumn(HeaderSearchPanel, 1);
            Grid.SetColumnSpan(HeaderSearchPanel, 1);
            HeaderSearchPanel.Margin = contentMargin;
            HeaderSearchPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            SearchBox.Width = 326;
            SearchBox.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(HeaderToolbarHost, 0);
            Grid.SetColumn(HeaderToolbarHost, 2);
            Grid.SetColumnSpan(HeaderToolbarHost, 1);
            HeaderToolbarHost.Visibility = Visibility.Visible;
            HeaderToolbarHost.Margin = new Thickness(0);

            Grid.SetRow(lbDbFullPath, 1);
            Grid.SetColumn(lbDbFullPath, 1);
            Grid.SetColumnSpan(lbDbFullPath, 2);
            lbDbFullPath.Margin = dbMargin;

            HeaderModePanel.Margin = new Thickness(0);
            HeaderSortLabel.Margin = new Thickness(8, 0, 4, 0);
        }

        private void ApplyHeaderStackedLayout(Thickness contentMargin, Thickness rowMargin, Thickness dbMargin)
        {
            Grid.SetRow(MenuToggleButton, 0);
            Grid.SetColumn(MenuToggleButton, 0);
            Grid.SetRowSpan(MenuToggleButton, 1);

            Grid.SetRow(HeaderSearchPanel, 0);
            Grid.SetColumn(HeaderSearchPanel, 1);
            Grid.SetColumnSpan(HeaderSearchPanel, 2);
            HeaderSearchPanel.Margin = contentMargin;
            HeaderSearchPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            SearchBox.Width = double.NaN;
            SearchBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            Grid.SetRow(HeaderToolbarHost, 1);
            Grid.SetColumn(HeaderToolbarHost, 1);
            Grid.SetColumnSpan(HeaderToolbarHost, 2);
            HeaderToolbarHost.Visibility = Visibility.Visible;
            HeaderToolbarHost.Margin = rowMargin;
            HeaderToolbarHost.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(lbDbFullPath, 2);
            Grid.SetColumn(lbDbFullPath, 1);
            Grid.SetColumnSpan(lbDbFullPath, 2);
            lbDbFullPath.Margin = dbMargin;

            HeaderModePanel.Margin = new Thickness(0);
            HeaderSortLabel.Margin = new Thickness(8, 0, 4, 0);
        }

        private void ApplyHeaderCompactLayout(Thickness contentMargin, Thickness rowMargin, Thickness dbMargin)
        {
            Grid.SetRow(MenuToggleButton, 0);
            Grid.SetColumn(MenuToggleButton, 0);
            Grid.SetRowSpan(MenuToggleButton, 1);

            Grid.SetRow(HeaderSearchPanel, 0);
            Grid.SetColumn(HeaderSearchPanel, 1);
            Grid.SetColumnSpan(HeaderSearchPanel, 2);
            HeaderSearchPanel.Margin = contentMargin;
            HeaderSearchPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            SearchBox.Width = double.NaN;
            SearchBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            Grid.SetRow(HeaderModePanel, 1);
            Grid.SetColumn(HeaderModePanel, 1);
            Grid.SetColumnSpan(HeaderModePanel, 2);
            HeaderModePanel.Margin = rowMargin;
            HeaderModePanel.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(HeaderSortPanel, 2);
            Grid.SetColumn(HeaderSortPanel, 1);
            Grid.SetColumnSpan(HeaderSortPanel, 2);
            HeaderSortPanel.Margin = rowMargin;
            HeaderSortPanel.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(lbDbFullPath, 3);
            Grid.SetColumn(lbDbFullPath, 1);
            Grid.SetColumnSpan(lbDbFullPath, 2);
            lbDbFullPath.Margin = dbMargin;

            HeaderToolbarHost.Visibility = Visibility.Collapsed;
            HeaderSortLabel.Margin = new Thickness(0, 0, 4, 0);
        }

        private void EnsureHeaderToolbarChildrenReparented(bool compact)
        {
            if (compact)
            {
                if (HeaderModePanel.Parent == HeaderToolbarHost)
                {
                    HeaderToolbarHost.Children.Remove(HeaderModePanel);
                    HeaderToolbarHost.Children.Remove(HeaderSortPanel);
                    HeaderLayoutGrid.Children.Add(HeaderModePanel);
                    HeaderLayoutGrid.Children.Add(HeaderSortPanel);
                }
            }
            else if (HeaderModePanel.Parent == HeaderLayoutGrid)
            {
                HeaderLayoutGrid.Children.Remove(HeaderModePanel);
                HeaderLayoutGrid.Children.Remove(HeaderSortPanel);
                HeaderToolbarHost.Children.Add(HeaderModePanel);
                HeaderToolbarHost.Children.Add(HeaderSortPanel);
            }
        }
    }
}
