using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IndigoMovieManager.Services.Dmm;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// sources の comment1 枠専用。リモートジャケを表示し、失敗時は
    /// FallbackToLocal が true のときだけローカルサムネへ落とす。
    /// </summary>
    internal static class JacketSlotImageBehavior
    {
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(40);

        public static readonly DependencyProperty JacketUrlProperty =
            DependencyProperty.RegisterAttached(
                "JacketUrl",
                typeof(string),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty FallbackToLocalProperty =
            DependencyProperty.RegisterAttached(
                "FallbackToLocal",
                typeof(bool),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(false, OnChanged));

        public static readonly DependencyProperty LocalPathProperty =
            DependencyProperty.RegisterAttached(
                "LocalPath",
                typeof(string),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty LocalExistsProperty =
            DependencyProperty.RegisterAttached(
                "LocalExists",
                typeof(bool),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(true, OnChanged));

        public static readonly DependencyProperty LocalConverterProperty =
            DependencyProperty.RegisterAttached(
                "LocalConverter",
                typeof(IValueConverter),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty HostProperty =
            DependencyProperty.RegisterAttached(
                "Host",
                typeof(FrameworkElement),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty FrameWidthProperty =
            DependencyProperty.RegisterAttached(
                "FrameWidth",
                typeof(double),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty LocalFrameHeightProperty =
            DependencyProperty.RegisterAttached(
                "LocalFrameHeight",
                typeof(double),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty LoadingIndicatorProperty =
            DependencyProperty.RegisterAttached(
                "LoadingIndicator",
                typeof(UIElement),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty LoadGenerationProperty =
            DependencyProperty.RegisterAttached(
                "LoadGeneration",
                typeof(int),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(0));

        private static readonly DependencyProperty DebounceTimerProperty =
            DependencyProperty.RegisterAttached(
                "DebounceTimer",
                typeof(DispatcherTimer),
                typeof(JacketSlotImageBehavior),
                new PropertyMetadata(null));

        public static void SetJacketUrl(DependencyObject e, string v) => e.SetValue(JacketUrlProperty, v);
        public static string GetJacketUrl(DependencyObject e) => (string)e.GetValue(JacketUrlProperty);
        public static void SetFallbackToLocal(DependencyObject e, bool v) => e.SetValue(FallbackToLocalProperty, v);
        public static bool GetFallbackToLocal(DependencyObject e) => (bool)e.GetValue(FallbackToLocalProperty);
        public static void SetLocalPath(DependencyObject e, string v) => e.SetValue(LocalPathProperty, v);
        public static string GetLocalPath(DependencyObject e) => (string)e.GetValue(LocalPathProperty);
        public static void SetLocalExists(DependencyObject e, bool v) => e.SetValue(LocalExistsProperty, v);
        public static bool GetLocalExists(DependencyObject e) => (bool)e.GetValue(LocalExistsProperty);
        public static void SetLocalConverter(DependencyObject e, IValueConverter v) => e.SetValue(LocalConverterProperty, v);
        public static IValueConverter GetLocalConverter(DependencyObject e) => (IValueConverter)e.GetValue(LocalConverterProperty);
        public static void SetHost(DependencyObject e, FrameworkElement v) => e.SetValue(HostProperty, v);
        public static FrameworkElement GetHost(DependencyObject e) => (FrameworkElement)e.GetValue(HostProperty);
        public static void SetFrameWidth(DependencyObject e, double v) => e.SetValue(FrameWidthProperty, v);
        public static double GetFrameWidth(DependencyObject e) => (double)e.GetValue(FrameWidthProperty);
        public static void SetLocalFrameHeight(DependencyObject e, double v) => e.SetValue(LocalFrameHeightProperty, v);
        public static double GetLocalFrameHeight(DependencyObject e) => (double)e.GetValue(LocalFrameHeightProperty);
        public static void SetLoadingIndicator(DependencyObject e, UIElement v) => e.SetValue(LoadingIndicatorProperty, v);

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            int generation = (int)image.GetValue(LoadGenerationProperty) + 1;
            image.SetValue(LoadGenerationProperty, generation);

            if (image.GetValue(DebounceTimerProperty) is DispatcherTimer existing)
            {
                existing.Stop();
            }

            var timer = new DispatcherTimer { Interval = DebounceDelay, Tag = generation };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                image.SetValue(DebounceTimerProperty, null);
                if (timer.Tag is int gen && gen == (int)image.GetValue(LoadGenerationProperty))
                {
                    _ = ApplyAsync(image, gen);
                }
            };
            image.SetValue(DebounceTimerProperty, timer);
            timer.Start();
        }

        private static async Task ApplyAsync(Image image, int generation)
        {
            string jacketUrl = GetJacketUrl(image);
            bool fallback = GetFallbackToLocal(image);

            void ClearOrFallback()
            {
                if (fallback)
                {
                    ApplyLocal(image);
                }
                else
                {
                    image.Source = null;
                    FrameworkElement host = GetHost(image);
                    if (host != null)
                    {
                        double w = GetFrameWidth(image);
                        double h = GetLocalFrameHeight(image);
                        if (w > 0)
                        {
                            host.Width = w;
                        }

                        if (h > 0)
                        {
                            host.Height = h;
                        }
                    }
                }
            }

            if (!DmmJacketUrls.IsHttpUrl(jacketUrl))
            {
                SetLoading(image, false);
                ClearOrFallback();
                return;
            }

            string trimmed = jacketUrl.Trim();
            if (DmmRemoteImageLoader.TryGetCached(trimmed, out BitmapSource cached))
            {
                if (!IsCurrent(image, generation))
                {
                    return;
                }

                ApplyRemote(image, cached);
                SetLoading(image, false);
                return;
            }

            SetLoading(image, true);
            BitmapSource remote = await DmmRemoteImageLoader
                .LoadAsync(trimmed, image.Dispatcher)
                .ConfigureAwait(true);

            if (!IsCurrent(image, generation))
            {
                return;
            }

            SetLoading(image, false);
            if (remote != null)
            {
                ApplyRemote(image, remote);
            }
            else
            {
                ClearOrFallback();
            }
        }

        private static void ApplyLocal(Image image)
        {
            IValueConverter converter = GetLocalConverter(image);
            string path = GetLocalPath(image);
            bool exists = GetLocalExists(image);
            BitmapSource local = null;
            if (converter != null)
            {
                local = converter.Convert(path, typeof(BitmapSource), exists, CultureInfo.CurrentCulture) as BitmapSource;
            }

            image.Source = local;
            image.Stretch = Stretch.UniformToFill;
            image.HorizontalAlignment = HorizontalAlignment.Center;
            image.VerticalAlignment = VerticalAlignment.Center;
            FrameworkElement host = GetHost(image);
            if (host != null)
            {
                double w = GetFrameWidth(image);
                double h = GetLocalFrameHeight(image);
                if (w > 0)
                {
                    host.Width = w;
                }

                if (h > 0)
                {
                    host.Height = h;
                }
            }
        }

        private static void ApplyRemote(Image image, BitmapSource remote)
        {
            image.Source = remote;
            image.Stretch = Stretch.Uniform;
            FrameworkElement host = GetHost(image);
            if (host == null || remote.PixelWidth <= 0)
            {
                return;
            }

            double frameW = GetFrameWidth(image);
            if (frameW <= 0)
            {
                frameW = host.ActualWidth > 1 ? host.ActualWidth : host.Width;
            }

            if (frameW <= 0 || double.IsNaN(frameW))
            {
                return;
            }

            double aspect = (double)remote.PixelWidth / remote.PixelHeight;
            double frameH = frameW / aspect;
            host.Width = frameW;
            host.Height = frameH;
        }

        private static void SetLoading(Image image, bool loading)
        {
            if (image.GetValue(LoadingIndicatorProperty) is UIElement bar)
            {
                bar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static bool IsCurrent(Image image, int generation) =>
            (int)image.GetValue(LoadGenerationProperty) == generation;
    }
}
