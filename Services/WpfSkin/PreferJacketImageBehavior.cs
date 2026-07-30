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
    /// preferJacket 用。まずローカルサムネ（JSON の W×H）を出し、Comment1 の HTTP URL が取れたら差し替える。
    /// ジャケ写あり時は枠幅＝JSON 幅、枠高さ＝ジャケ比で自動（黒帯なし）。失敗時はローカルのまま。
    /// </summary>
    internal static class PreferJacketImageBehavior
    {
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(40);

        public static readonly DependencyProperty JacketUrlProperty =
            DependencyProperty.RegisterAttached(
                "JacketUrl",
                typeof(string),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null, OnSourceInputsChanged));

        public static readonly DependencyProperty LocalPathProperty =
            DependencyProperty.RegisterAttached(
                "LocalPath",
                typeof(string),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null, OnSourceInputsChanged));

        public static readonly DependencyProperty LocalExistsProperty =
            DependencyProperty.RegisterAttached(
                "LocalExists",
                typeof(bool),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(true, OnSourceInputsChanged));

        public static readonly DependencyProperty LocalConverterProperty =
            DependencyProperty.RegisterAttached(
                "LocalConverter",
                typeof(IValueConverter),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null, OnSourceInputsChanged));

        public static readonly DependencyProperty AspectConverterProperty =
            DependencyProperty.RegisterAttached(
                "AspectConverter",
                typeof(IValueConverter),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty LoadingIndicatorProperty =
            DependencyProperty.RegisterAttached(
                "LoadingIndicator",
                typeof(UIElement),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty HostProperty =
            DependencyProperty.RegisterAttached(
                "Host",
                typeof(FrameworkElement),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty FrameWidthProperty =
            DependencyProperty.RegisterAttached(
                "FrameWidth",
                typeof(double),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty LocalFrameHeightProperty =
            DependencyProperty.RegisterAttached(
                "LocalFrameHeight",
                typeof(double),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty TargetAspectProperty =
            DependencyProperty.RegisterAttached(
                "TargetAspect",
                typeof(double),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(16.0 / 9.0));

        /// <summary>
        /// true のとき host の Width を固定せず親セル幅に Stretch する（スプリッター連動用）。
        /// </summary>
        public static readonly DependencyProperty TrackParentWidthProperty =
            DependencyProperty.RegisterAttached(
                "TrackParentWidth",
                typeof(bool),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(false));

        private static readonly DependencyProperty LoadGenerationProperty =
            DependencyProperty.RegisterAttached(
                "LoadGeneration",
                typeof(int),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(0));

        private static readonly DependencyProperty AppliedJacketUrlProperty =
            DependencyProperty.RegisterAttached(
                "AppliedJacketUrl",
                typeof(string),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty DebounceTimerProperty =
            DependencyProperty.RegisterAttached(
                "DebounceTimer",
                typeof(DispatcherTimer),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty ShowingRemoteProperty =
            DependencyProperty.RegisterAttached(
                "ShowingRemote",
                typeof(bool),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(false));

        public static void SetJacketUrl(DependencyObject element, string value) =>
            element.SetValue(JacketUrlProperty, value);

        public static string GetJacketUrl(DependencyObject element) =>
            (string)element.GetValue(JacketUrlProperty);

        public static void SetLocalPath(DependencyObject element, string value) =>
            element.SetValue(LocalPathProperty, value);

        public static string GetLocalPath(DependencyObject element) =>
            (string)element.GetValue(LocalPathProperty);

        public static void SetLocalExists(DependencyObject element, bool value) =>
            element.SetValue(LocalExistsProperty, value);

        public static bool GetLocalExists(DependencyObject element) =>
            (bool)element.GetValue(LocalExistsProperty);

        public static void SetLocalConverter(DependencyObject element, IValueConverter value) =>
            element.SetValue(LocalConverterProperty, value);

        public static IValueConverter GetLocalConverter(DependencyObject element) =>
            (IValueConverter)element.GetValue(LocalConverterProperty);

        public static void SetAspectConverter(DependencyObject element, IValueConverter value) =>
            element.SetValue(AspectConverterProperty, value);

        public static IValueConverter GetAspectConverter(DependencyObject element) =>
            (IValueConverter)element.GetValue(AspectConverterProperty);

        public static void SetLoadingIndicator(DependencyObject element, UIElement value) =>
            element.SetValue(LoadingIndicatorProperty, value);

        public static UIElement GetLoadingIndicator(DependencyObject element) =>
            (UIElement)element.GetValue(LoadingIndicatorProperty);

        public static void SetHost(DependencyObject element, FrameworkElement value) =>
            element.SetValue(HostProperty, value);

        public static FrameworkElement GetHost(DependencyObject element) =>
            (FrameworkElement)element.GetValue(HostProperty);

        public static void SetFrameWidth(DependencyObject element, double value) =>
            element.SetValue(FrameWidthProperty, value);

        public static double GetFrameWidth(DependencyObject element) =>
            (double)element.GetValue(FrameWidthProperty);

        public static void SetLocalFrameHeight(DependencyObject element, double value) =>
            element.SetValue(LocalFrameHeightProperty, value);

        public static double GetLocalFrameHeight(DependencyObject element) =>
            (double)element.GetValue(LocalFrameHeightProperty);

        public static void SetTargetAspect(DependencyObject element, double value) =>
            element.SetValue(TargetAspectProperty, value);

        public static double GetTargetAspect(DependencyObject element) =>
            (double)element.GetValue(TargetAspectProperty);

        public static void SetTrackParentWidth(DependencyObject element, bool value) =>
            element.SetValue(TrackParentWidthProperty, value);

        public static bool GetTrackParentWidth(DependencyObject element) =>
            (bool)element.GetValue(TrackParentWidthProperty);

        private static void OnSourceInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            string jacketUrl = GetJacketUrl(image);
            string applied = image.GetValue(AppliedJacketUrlProperty) as string;
            if (DmmJacketUrls.IsHttpUrl(jacketUrl)
                && (bool)image.GetValue(ShowingRemoteProperty)
                && string.Equals(applied, jacketUrl.Trim(), StringComparison.OrdinalIgnoreCase)
                && e.Property != JacketUrlProperty
                && e.Property != null)
            {
                return;
            }

            ScheduleApply(image);
        }

        private static void ScheduleApply(Image image)
        {
            int generation = (int)image.GetValue(LoadGenerationProperty) + 1;
            image.SetValue(LoadGenerationProperty, generation);

            if (image.GetValue(DebounceTimerProperty) is DispatcherTimer existing)
            {
                existing.Stop();
                image.SetValue(DebounceTimerProperty, null);
            }

            var timer = new DispatcherTimer
            {
                Interval = DebounceDelay,
                Tag = generation,
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                image.SetValue(DebounceTimerProperty, null);
                if (timer.Tag is int gen && IsCurrent(image, gen))
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
            string localPath = GetLocalPath(image);
            bool localExists = GetLocalExists(image);
            IValueConverter localConverter = GetLocalConverter(image);

            if (!IsCurrent(image, generation))
            {
                return;
            }

            ApplyLocal(image, localConverter, localPath, localExists);

            if (!DmmJacketUrls.IsHttpUrl(jacketUrl))
            {
                SetLoading(image, false);
                return;
            }

            string trimmed = jacketUrl.Trim();
            if (DmmRemoteImageLoader.TryGetCached(trimmed, out BitmapSource cached))
            {
                if (!IsCurrent(image, generation))
                {
                    return;
                }

                ApplyRemote(image, cached, trimmed);
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
                ApplyRemote(image, remote, trimmed);
            }
        }

        private static void ApplyLocal(
            Image image,
            IValueConverter converter,
            string path,
            bool exists)
        {
            BitmapSource local = LoadLocal(converter, path, exists);
            image.Source = local;
            image.SetValue(AppliedJacketUrlProperty, null);
            image.SetValue(ShowingRemoteProperty, false);

            // ローカルは JSON の W×H 枠（従来サムネと同じ）。AspectConverter があればそれに従う。
            ApplyLocalFrameSize(image);
            image.Stretch = ResolveLocalStretch(image, local);
            image.ClearValue(FrameworkElement.MaxWidthProperty);
            image.ClearValue(FrameworkElement.MaxHeightProperty);
        }

        private static void ApplyRemote(Image image, BitmapSource remote, string url)
        {
            image.Source = remote;
            image.Stretch = Stretch.Uniform;
            image.ClearValue(FrameworkElement.MaxWidthProperty);
            image.ClearValue(FrameworkElement.MaxHeightProperty);
            image.SetValue(AppliedJacketUrlProperty, url);
            image.SetValue(ShowingRemoteProperty, true);

            // 幅＝JSON、高さ＝ジャケ比（枠そのものを合わせる → 黒帯なし）
            ApplyJacketFrameSize(image, remote);
        }

        private static void ApplyLocalFrameSize(Image image)
        {
            FrameworkElement host = GetHost(image);
            if (host == null)
            {
                return;
            }

            bool trackParent = GetTrackParentWidth(image);
            double width = GetFrameWidth(image);
            double height = GetLocalFrameHeight(image);

            if (trackParent)
            {
                host.ClearValue(FrameworkElement.WidthProperty);
                host.HorizontalAlignment = HorizontalAlignment.Stretch;
                host.VerticalAlignment = VerticalAlignment.Top;
                if (height > 0)
                {
                    host.Height = height;
                }
                else
                {
                    host.ClearValue(FrameworkElement.HeightProperty);
                }

                return;
            }

            if (width > 0)
            {
                host.Width = width;
            }

            if (height > 0)
            {
                host.Height = height;
            }
            else
            {
                host.ClearValue(FrameworkElement.HeightProperty);
            }

            host.VerticalAlignment = VerticalAlignment.Stretch;
            host.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private static void ApplyJacketFrameSize(Image image, BitmapSource remote)
        {
            FrameworkElement host = GetHost(image);
            if (host == null || remote == null || remote.PixelWidth <= 0 || remote.PixelHeight <= 0)
            {
                return;
            }

            bool trackParent = GetTrackParentWidth(image);
            double aspect = (double)remote.PixelWidth / remote.PixelHeight;
            if (aspect <= 0)
            {
                return;
            }

            if (trackParent)
            {
                // 幅は親に任せ、高さだけジャケ比で合わせる（スプリッターで縮む）
                host.ClearValue(FrameworkElement.WidthProperty);
                host.HorizontalAlignment = HorizontalAlignment.Stretch;
                host.VerticalAlignment = VerticalAlignment.Top;

                double width = host.ActualWidth > 1 ? host.ActualWidth : GetFrameWidth(image);
                if (width <= 0)
                {
                    // 初回レイアウト前は参照フレーム幅があれば使う
                    width = GetFrameWidth(image);
                }

                if (width > 0)
                {
                    host.Height = width / aspect;
                }

                return;
            }

            double frameWidth = GetFrameWidth(image);
            if (frameWidth <= 0)
            {
                frameWidth = remote.PixelWidth;
            }

            double height = frameWidth / aspect;
            host.Width = frameWidth;
            host.Height = height;
            // ジャケ枠は内容サイズで確定。行の余白へ黒く伸ばさない。
            host.VerticalAlignment = VerticalAlignment.Top;
            host.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private static Stretch ResolveLocalStretch(Image image, BitmapSource local)
        {
            IValueConverter aspectConverter = GetAspectConverter(image);
            if (aspectConverter == null || local == null)
            {
                return Stretch.Uniform;
            }

            try
            {
                object result = aspectConverter.Convert(
                    local,
                    typeof(Stretch),
                    GetTargetAspect(image),
                    CultureInfo.CurrentCulture);
                if (result is Stretch stretch)
                {
                    return stretch;
                }
            }
            catch
            {
                // fall through
            }

            return Stretch.Uniform;
        }

        private static void SetLoading(Image image, bool loading)
        {
            UIElement indicator = GetLoadingIndicator(image);
            if (indicator != null)
            {
                indicator.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static bool IsCurrent(Image image, int generation) =>
            image != null
            && (int)image.GetValue(LoadGenerationProperty) == generation;

        private static BitmapSource LoadLocal(IValueConverter converter, string path, bool exists)
        {
            if (converter == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                object converted = converter.Convert(
                    path,
                    typeof(BitmapSource),
                    exists,
                    CultureInfo.CurrentCulture);
                return converted as BitmapSource;
            }
            catch
            {
                return null;
            }
        }
    }
}
