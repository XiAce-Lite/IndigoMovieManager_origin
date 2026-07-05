using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IndigoMovieManager.Services.WpfSkin
{
    internal static class WpfSkinListChrome
    {
        private static readonly DependencyProperty SurfaceBrushProperty =
            DependencyProperty.RegisterAttached(
                "SurfaceBrush",
                typeof(Brush),
                typeof(WpfSkinListChrome),
                new PropertyMetadata(null, OnSurfaceBrushChanged));

        public static void ApplySurface(ListView listView, Brush surfaceBg, params Panel[] hostPanels)
        {
            if (listView == null)
            {
                return;
            }

            Brush brush = surfaceBg ?? Brushes.Transparent;
            listView.SetValue(SurfaceBrushProperty, brush);
            listView.Background = brush;

            if (hostPanels != null)
            {
                foreach (Panel host in hostPanels)
                {
                    if (host != null)
                    {
                        host.Background = brush;
                    }
                }
            }

            ApplyScrollViewerBackground(listView, brush);
            EnsureLayoutUpdatedHook(listView);
        }

        private static void EnsureLayoutUpdatedHook(ListView listView)
        {
            listView.LayoutUpdated -= ListView_LayoutUpdated;
            listView.LayoutUpdated += ListView_LayoutUpdated;
        }

        private static void ListView_LayoutUpdated(object sender, EventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            Brush brush = listView.GetValue(SurfaceBrushProperty) as Brush;
            if (brush == null)
            {
                return;
            }

            ApplyScrollViewerBackground(listView, brush);
        }

        private static void OnSurfaceBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListView listView && e.NewValue is Brush brush)
            {
                ApplyScrollViewerBackground(listView, brush);
            }
        }

        private static void ApplyScrollViewerBackground(ListView listView, Brush brush)
        {
            listView.ApplyTemplate();
            ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(listView);
            if (scrollViewer != null)
            {
                scrollViewer.Background = brush;
            }

            Border border = FindVisualChild<Border>(listView);
            if (border != null && border.Child == scrollViewer)
            {
                border.Background = brush;
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                T found = FindVisualChild<T>(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
