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
    /// preferJacket 用。まずローカルサムネを出し、Comment1 の HTTP URL が取れたら差し替える。
    /// 列・行に関わらずジャケは枠全体へ Uniform・中央（非ストレッチ）。失敗時は C×R サムネのまま。
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

        public static readonly DependencyProperty LoadingIndicatorProperty =
            DependencyProperty.RegisterAttached(
                "LoadingIndicator",
                typeof(UIElement),
                typeof(PreferJacketImageBehavior),
                new PropertyMetadata(null));

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

        public static void SetLoadingIndicator(DependencyObject element, UIElement value) =>
            element.SetValue(LoadingIndicatorProperty, value);

        public static UIElement GetLoadingIndicator(DependencyObject element) =>
            (UIElement)element.GetValue(LoadingIndicatorProperty);

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
                // 同じジャケ表示中に LocalPath だけ更新 → リモート再取得不要
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

            // 常にローカルを先に表示（黒待ちを避ける）
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
            // 失敗時は既に出しているローカルのまま
        }

        private static void ApplyLocal(
            Image image,
            IValueConverter converter,
            string path,
            bool exists)
        {
            image.Source = LoadLocal(converter, path, exists);
            image.Stretch = Stretch.Uniform;
            image.SetValue(AppliedJacketUrlProperty, null);
            image.SetValue(ShowingRemoteProperty, false);
        }

        private static void ApplyRemote(Image image, BitmapSource remote, string url)
        {
            image.Source = remote;
            // 枠全体に対し縦横比維持・中央（Fill しない）
            image.Stretch = Stretch.Uniform;
            image.ClearValue(FrameworkElement.MaxWidthProperty);
            image.ClearValue(FrameworkElement.MaxHeightProperty);
            image.SetValue(AppliedJacketUrlProperty, url);
            image.SetValue(ShowingRemoteProperty, true);
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
