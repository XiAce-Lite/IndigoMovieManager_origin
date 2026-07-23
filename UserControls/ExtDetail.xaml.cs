using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IndigoMovieManager.Converter;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// ExtDetail.xaml の相互作用ロジック
    /// </summary>
    public partial class ExtDetail : UserControl
    {
        /// <summary>ジャケ無し時に枠中央へ置く従来詳細サムネの表示幅（旧枠の横幅）。</summary>
        private const double LocalDetailThumbDisplayWidth = 160;

        /// <summary>旧枠 160×120 と同じ縦横比。</summary>
        private const double LocalDetailThumbDisplayHeight = 120;

        private readonly NoLockImageConverter _thumbConverter = new();
        private bool _ctrlFlg;
        private bool _showingLocalThumb;
        private bool _displayingRemoteJacket;
        private bool _frontJacketAvailable;
        private int _imageLoadGeneration;
        private MovieRecords _subscribedRecord;

        public ExtDetail()
        {
            InitializeComponent();
            DataContext = new MovieRecords();
            DataContextChanged += ExtDetail_DataContextChanged;
        }

        private void ExtDetail_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnsubscribeRecord(_subscribedRecord);
            _subscribedRecord = DataContext as MovieRecords;
            if (_subscribedRecord != null)
            {
                _subscribedRecord.PropertyChanged += Record_PropertyChanged;
            }

            _showingLocalThumb = false;
            UpdateCommentRowStyles();
            UpdateDetailImage();
        }

        private void Record_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MovieRecords.Comment1) or nameof(MovieRecords.Comment2))
            {
                UpdateCommentRowStyles();
            }

            if (e.PropertyName is nameof(MovieRecords.Comment1)
                or nameof(MovieRecords.ThumbDetail)
                or nameof(MovieRecords.IsExists))
            {
                UpdateDetailImage();
            }
        }

        private void UnsubscribeRecord(MovieRecords record)
        {
            if (record != null)
            {
                record.PropertyChanged -= Record_PropertyChanged;
            }
        }

        private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
                actions?.PlayMovie_Click(sender, e);
            }
        }

        private void ThumbnailImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1 || _displayingRemoteJacket)
            {
                return;
            }

            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            actions?.RequestDetailThumbnailRecreate();
        }

        public void Refresh()
        {
            ExtDetailTags.Items.Refresh();
            UpdateDetailImage();
        }

        private void JacketPrevButton_Click(object sender, RoutedEventArgs e)
        {
            _showingLocalThumb = false;
            UpdateDetailImage();
        }

        private void JacketNextButton_Click(object sender, RoutedEventArgs e)
        {
            // ジャケ表が取れているときだけ ▶ で詳細サムネへ切り替え
            if (!_frontJacketAvailable || DataContext is not MovieRecords record)
            {
                return;
            }

            _showingLocalThumb = true;
            ApplyThumbnailDisplay(record);
            UpdateJacketButtons();
        }

        private void UpdateCommentRowStyles()
        {
            if (DataContext is not MovieRecords record)
            {
                return;
            }

            ApplyCommentValueStyle(Comment1Value, record.Comment1);
            ApplyCommentValueStyle(Comment2Value, record.Comment2);
        }

        private static void ApplyCommentValueStyle(TextBlock textBlock, string value)
        {
            if (textBlock == null)
            {
                return;
            }

            if (DmmJacketUrls.IsHttpUrl(value))
            {
                textBlock.TextDecorations = TextDecorations.Underline;
                textBlock.ToolTip = "クリックでブラウザを開く";
            }
            else
            {
                textBlock.TextDecorations = null;
                textBlock.ToolTip = string.IsNullOrWhiteSpace(value)
                    ? null
                    : "クリックで検索";
            }
        }

        private void UpdateDetailImage()
        {
            _ = UpdateDetailImageAsync();
        }

        private async Task UpdateDetailImageAsync()
        {
            if (DetailImage == null)
            {
                return;
            }

            int generation = Interlocked.Increment(ref _imageLoadGeneration);

            if (DataContext is not MovieRecords record)
            {
                ApplyLocalThumbnailLayout();
                DetailImage.Source = null;
                _displayingRemoteJacket = false;
                _frontJacketAvailable = false;
                _showingLocalThumb = false;
                UpdateJacketButtons();
                return;
            }

            string frontUrl = DmmJacketUrls.GetFrontUrl(record);
            if (string.IsNullOrEmpty(frontUrl))
            {
                _showingLocalThumb = false;
                _frontJacketAvailable = false;
                ApplyThumbnailDisplay(record);
                UpdateJacketButtons();
                return;
            }

            // ▶ で詳細サムネ表示中なら、ジャケ再読込せずサムネを維持
            if (_showingLocalThumb)
            {
                ApplyThumbnailDisplay(record);
                UpdateJacketButtons();
                return;
            }

            BitmapSource frontImage = await LoadRemoteImageAsync(frontUrl).ConfigureAwait(true);
            if (generation != _imageLoadGeneration)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (generation != _imageLoadGeneration || DataContext is not MovieRecords current)
                {
                    return;
                }

                if (frontImage == null)
                {
                    _showingLocalThumb = false;
                    _frontJacketAvailable = false;
                    ApplyThumbnailDisplay(current);
                    UpdateJacketButtons();
                    return;
                }

                _frontJacketAvailable = true;
                ApplyJacketImage(frontImage);
                UpdateJacketButtons();
            });
        }

        private void ApplyJacketImage(BitmapSource image)
        {
            ApplyJacketImageLayout();
            DetailImage.Source = image;
            _displayingRemoteJacket = true;
        }

        private void ApplyThumbnailDisplay(MovieRecords record)
        {
            ApplyLocalThumbnailLayout();
            DetailImage.Source = LoadLocalThumbnail(record);
            _displayingRemoteJacket = false;
        }

        /// <summary>ジャケは枠いっぱいに Uniform。</summary>
        private void ApplyJacketImageLayout()
        {
            DetailImage.ClearValue(FrameworkElement.WidthProperty);
            DetailImage.ClearValue(FrameworkElement.HeightProperty);
            DetailImage.Stretch = Stretch.Uniform;
            LabelExtDetail.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            LabelExtDetail.VerticalContentAlignment = VerticalAlignment.Stretch;
        }

        /// <summary>従来詳細サムネは横160のまま枠中央に配置（枠いっぱいに拡大しない）。</summary>
        private void ApplyLocalThumbnailLayout()
        {
            DetailImage.Width = LocalDetailThumbDisplayWidth;
            DetailImage.Height = LocalDetailThumbDisplayHeight;
            DetailImage.Stretch = Stretch.Uniform;
            LabelExtDetail.HorizontalContentAlignment = HorizontalAlignment.Center;
            LabelExtDetail.VerticalContentAlignment = VerticalAlignment.Center;
        }

        private void UpdateJacketButtons()
        {
            bool canFlip = _frontJacketAvailable;

            if (JacketPrevButton != null)
            {
                JacketPrevButton.IsEnabled = canFlip && _showingLocalThumb;
            }

            if (JacketNextButton != null)
            {
                JacketNextButton.IsEnabled = canFlip && !_showingLocalThumb;
            }
        }

        private Task<BitmapSource> LoadRemoteImageAsync(string url)
        {
            string resolvedUrl = DmmJacketUrls.ResolveUsableJacketUrl(url);
            if (string.IsNullOrEmpty(resolvedUrl))
            {
                return Task.FromResult<BitmapSource>(null);
            }

            if (!DmmJacketUrls.IsHttpUrl(resolvedUrl))
            {
                return Task.FromResult<BitmapSource>(null);
            }

            var tcs = new TaskCompletionSource<BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bool completed = false;

                    void Finish(BitmapSource result)
                    {
                        if (completed)
                        {
                            return;
                        }

                        completed = true;
                        tcs.TrySetResult(result);
                    }

                    bitmap.DownloadFailed += (_, _) => Finish(null);
                    bitmap.DecodeFailed += (_, _) => Finish(null);
                    bitmap.DownloadCompleted += (_, _) =>
                    {
                        Uri loadedUri = bitmap.UriSource;
                        if (DmmJacketUrls.IsPlaceholderJacketUri(loadedUri))
                        {
                            Finish(null);
                            return;
                        }

                        if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                        {
                            bitmap.Freeze();
                            Finish(bitmap);
                        }
                        else
                        {
                            Finish(null);
                        }
                    };

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(resolvedUrl.Trim(), UriKind.Absolute);
                    bitmap.EndInit();

                    if (!bitmap.IsDownloading)
                    {
                        if (DmmJacketUrls.IsPlaceholderJacketUri(bitmap.UriSource))
                        {
                            Finish(null);
                        }
                        else if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                        {
                            bitmap.Freeze();
                            Finish(bitmap);
                        }
                        else
                        {
                            Finish(null);
                        }
                    }
                }
                catch
                {
                    tcs.TrySetResult(null);
                }
            });

            return tcs.Task;
        }

        private BitmapSource LoadLocalThumbnail(MovieRecords record)
        {
            object converted = _thumbConverter.Convert(
                record.ThumbDetail,
                typeof(BitmapSource),
                record.IsExists,
                CultureInfo.CurrentCulture);
            return converted as BitmapSource;
        }

        private void CommentRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string value)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (DmmJacketUrls.IsHttpUrl(value))
            {
                OpenUrlInBrowser(value);
                e.Handled = true;
                return;
            }

            MetadataRow_MouseLeftButtonDown(sender, e);
        }

        private static void OpenUrlInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url.Trim()) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var item = (Hyperlink)sender;
            if (item != null)
            {
                MovieRecords mv = item.DataContext as MovieRecords;
                if (Path.Exists(mv.Movie_Path))
                {
                    Process.Start("explorer.exe", $"/select,{mv.Movie_Path}");
                }
            }
        }

        private async void FileNameLink_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MovieRecords record)
            {
                IMainWindowActions actions = MainWindowActionsHelper.GetActions(this)
                    ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (actions != null)
                {
                    var quoted = $"\"{record.Movie_Body}\"";
                    await actions.SearchByKeywordAsync(quoted).ConfigureAwait(true);
                }
            }
        }

        private async void Ext_Click(object sender, RoutedEventArgs e)
        {
            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            var item = (Hyperlink)sender;
            if (actions != null && item != null)
            {
                MovieRecords mv = item.DataContext as MovieRecords;
                await actions.SearchByKeywordAsync(mv.Ext).ConfigureAwait(true);
            }
        }

        private void MetadataPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                _ctrlFlg = true;
            }
        }

        private void MetadataPanel_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                _ctrlFlg = false;
            }
        }

        private async void MetadataRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string keyword)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null)
            {
                return;
            }

            string searchKeyword = _ctrlFlg
                ? (actions.SearchBox.Text ?? "") + " " + keyword
                : keyword;

            await actions.SearchByKeywordAsync(searchKeyword).ConfigureAwait(true);
            e.Handled = true;
        }
    }
}
