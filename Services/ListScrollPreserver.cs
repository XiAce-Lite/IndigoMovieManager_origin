using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// ListView の Items.Refresh で仮想化コンテナが再生成されても、スクロール位置を維持する。
    /// </summary>
    internal static class ListScrollPreserver
    {
        public static void RefreshListViewPreservingScroll(ListView listView)
        {
            if (listView == null)
            {
                return;
            }

            ScrollViewer scrollViewer = FindScrollViewer(listView);
            double vertical = scrollViewer?.VerticalOffset ?? double.NaN;
            double horizontal = scrollViewer?.HorizontalOffset ?? double.NaN;
            object selected = listView.SelectedItem;

            listView.Items.Refresh();

            void Restore()
            {
                ScrollViewer current = FindScrollViewer(listView) ?? scrollViewer;
                if (current != null && !double.IsNaN(vertical))
                {
                    current.ScrollToHorizontalOffset(double.IsNaN(horizontal) ? 0 : horizontal);
                    current.ScrollToVerticalOffset(vertical);
                    return;
                }

                if (selected != null)
                {
                    listView.ScrollIntoView(selected);
                }
            }

            // 仮想化の再生成後に復元（タイミング差を吸収するため 2 段階）
            listView.Dispatcher.BeginInvoke(Restore, DispatcherPriority.Loaded);
            listView.Dispatcher.BeginInvoke(Restore, DispatcherPriority.ContextIdle);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is ScrollViewer self)
            {
                return self;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                ScrollViewer found = FindScrollViewer(VisualTreeHelper.GetChild(parent, i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
