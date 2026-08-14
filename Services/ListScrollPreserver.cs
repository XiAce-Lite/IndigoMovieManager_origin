using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        /// <summary>
        /// Shift+マウスホイールを横スクロールとして扱う。処理したら true（e.Handled 済み）。
        /// 横にスクロール余地が無い／Shift 未押下なら false。
        /// </summary>
        public static bool TryHandleShiftMouseWheel(ListView listView, MouseWheelEventArgs e)
        {
            if (listView == null || e == null || e.Delta == 0)
            {
                return false;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                return false;
            }

            ScrollViewer scrollViewer = FindScrollViewer(listView);
            if (scrollViewer == null || scrollViewer.ScrollableWidth <= 0)
            {
                return false;
            }

            double next = scrollViewer.HorizontalOffset - e.Delta;
            if (next < 0)
            {
                next = 0;
            }
            else if (next > scrollViewer.ScrollableWidth)
            {
                next = scrollViewer.ScrollableWidth;
            }

            scrollViewer.ScrollToHorizontalOffset(next);
            e.Handled = true;
            return true;
        }

        internal static ScrollViewer FindScrollViewer(DependencyObject parent)
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
