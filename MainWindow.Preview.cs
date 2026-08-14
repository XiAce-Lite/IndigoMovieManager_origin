using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.SQLite;
namespace IndigoMovieManager
{
    public partial class MainWindow
    {
        #region マニュアルサムネイル用のプレイヤー関連

        private bool IsPlaying = false;
        /// <summary>
        /// 再生ボタンクリック時のイベントハンドラ
        /// パクリ元：https://resanaplaza.com/2023/06/24/%e3%80%90%e3%82%b5%e3%83%b3%e3%83%97%e3%83%ab%e6%ba%80%e8%bc%89%e3%80%91c%e3%81%a7%e5%8b%95%e7%94%bb%e5%86%8d%e7%94%9f%e3%81%97%e3%82%88%e3%81%86%e3%82%88%ef%bc%81%ef%bc%88mediaelement%ef%bc%89/
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!_manualPreview.IsOpen) { return; }

            PlayerArea.Visibility = Visibility.Visible;
            PlayerController.Visibility = Visibility.Visible;
            SetPreviewModeUi(_useLegacyPreviewFallback);
            ApplyManualPreviewTimerInterval();

            if (_useLegacyPreviewFallback)
            {
                IsPlaying = true;
                timer.Start();
                return;
            }

            if (!_isPreviewMediaOpened)
            {
                return;
            }

            if (_applyPendingStartOnPlay)
            {
                SetPreviewPositionMs(_pendingPreviewStartMs);
                uxTime.Text = _manualPreview.PositionText;
                _applyPendingStartOnPlay = false;
            }

            IsPlaying = true;
            uxPreviewImage.Volume = uxVolumeSlider.Value;
            uxPreviewImage.Play();
            uxTimeSlider.Value = GetPreviewPositionMs();
            timer.Start();
        }

        /// <summary>
        /// 一時停止ボタンクリック時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }
        }

        private void UxPreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_manualPreview.IsOpen) { return; }

            if (IsPlaying)
            {
                Pause_Click(sender, e);
                return;
            }

            Start_Click(sender, e);
        }

        /// <summary>
        /// ストップボタンクリック時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            CloseManualThumbnailPreview();
        }

        /// <summary>
        /// タイムラインスライダーのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UxTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_manualPreview.IsOpen) { return; }
            if (_isUpdatingSliderFromPlayer) { return; }

            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastUpdate = now - _lastSliderTime;

            if (timeSinceLastUpdate >= _timeSliderInterval)
            {
                SetPreviewPositionMs(uxTimeSlider.Value);
                _lastSliderTime = now;
                uxTime.Text = _manualPreview.PositionText;
            }
        }

        /// <summary>
        /// ボリュームスライダーのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UxVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (uxVolume != null)
            {
                uxVolume.Text = ((int)(uxVolumeSlider.Value * 100)).ToString();
            }

            if (_isPreviewMediaOpened)
            {
                uxPreviewImage.Volume = uxVolumeSlider.Value;
            }
        }

        /// <summary>
        /// キャプチャボタンのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            //QueueObj 作って、サムネ作成する。どのパネルか、秒数はどこか、差し替える画像はどれか。
            //その辺は、サムネ作成側の処理で判断。

            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
            if (mv == null) { return; }

            timer.Stop();
            IsPlaying = false;
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }

            QueueObj queueObj = ManualThumbnailCaptureFactory.Create(
                mv.Movie_Id,
                mv.Movie_Path,
                manualPos,
                _manualPreview.PositionSeconds);
            PopulateActiveListQueueLayout(queueObj);

            CloseManualThumbnailPreview();

            await EnqueueManualThumbnailWorkAsync(queueObj);
        }

        private async Task EnqueueManualThumbnailWorkAsync(QueueObj queueObj)
        {
            for (int i = 0; i < ManualThumbnailCaptureFactory.EnqueueRetryCount; i++)
            {
                if (TryEnqueueManualThumbnailWork(queueObj))
                {
                    return;
                }

                await Task.Delay(ManualThumbnailCaptureFactory.EnqueueRetryDelayMs).ConfigureAwait(true);
            }

            MessageBox.Show(
                this,
                ManualThumbnailCaptureFactory.BusyMessage,
                AppTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        public void DeleteBookmark(object sender, RoutedEventArgs e)
        {
            if (sender is Button deleteButton)
            {
                var item = deleteButton.DataContext as MovieRecords;
                DeleteBookmarkTable(MainVM.DbInfo.DBFullPath, item.Movie_Id);
                GetBookmarkTable();
                BookmarkList.Items.Refresh();
            }
        }

        private async void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            //QueueObj 作って、サムネ作成する。どのパネルか、秒数はどこか、差し替える画像はどれか。
            //その辺は、サムネ作成側の処理で判断。

            if (!IsMovieListActive) { return; }

            MovieRecords mv = null;
            if (_manualPreview.IsOpen)
            {
                mv = BookmarkSourceResolver.FindMovieRecordByPath(MainVM.MovieRecs, _manualPreview.MoviePath);
            }

            mv ??= GetSelectedMovie();
            if (mv == null) { return; }

            timer.Stop();
            IsPlaying = false;
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }

            MovieInfo mvi = new(mv.Movie_Path, true);        //Hashの取得が重いのでオプション付けた。ブックマークには不要。

            int pos = _manualPreview.PositionSeconds;
            string thumbBody = BookmarkCaptureNaming.BuildThumbBody(
                mv.Movie_Body,
                pos,
                mvi.FPS,
                DateTime.Now);
            string thumbFolder = BookmarkCaptureNaming.ResolveFolderOrDefault(
                MainVM.DbInfo.BookmarkFolder,
                MainVM.DbInfo.DBName);
            string thumbFileName = BookmarkCaptureNaming.BuildThumbFilePath(thumbFolder, thumbBody);
            if (!Path.Exists(thumbFolder))
            {
                Directory.CreateDirectory(thumbFolder);
            }

            await Task.Delay(10);
            //bookmark用サムネイル作成処理。通常と重複は多いんだけども。
            _ = CreateBookmarkThumbAsync(mv.Movie_Path, thumbFileName, pos);

            CloseManualThumbnailPreview();

            //Bookmarkテーブルへのレコード書き込み処理追加
            mvi.MovieName = thumbBody;
            mvi.MoviePath = BookmarkCaptureNaming.BuildThumbFileName(thumbBody);
            InsertBookmarkTable(MainVM.DbInfo.DBFullPath, mvi, mv.Movie_Path, mv.Hash);
            GetBookmarkTable();
            BookmarkList.Items.Refresh();
        }

        private async void ManualThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (!IsMovieListActive) { return; }

            MovieRecords mv = _contextMenuMovie ?? GetSelectedMovie();
            if (mv == null) { return; }

            if (ZipMediaKind.IsZipRecord(mv))
            {
                return;
            }

            int msec = 0;
            if (sender is MenuItem senderObj && senderObj.Name == "ManualThumbnail")
            {
                if (_contextMenuThumbClickValid
                    && _contextMenuThumbImage != null
                    && ThumbPanelHitResolver.TryResolveFromImageClick(
                        _contextMenuThumbClick,
                        _contextMenuThumbImage.ActualWidth,
                        _contextMenuThumbImage.ActualHeight,
                        PlayPositionResolver.GetThumbPathForEngine(mv, _currentSkinEngine),
                        ZipMediaKind.IsZipRecord(mv),
                        out int panelIndex,
                        out msec))
                {
                    manualPos = panelIndex;
                }
                else
                {
                    msec = GetPlayPosition(_currentSkinEngine, mv, ref manualPos);
                }
            }

            await _manualPreview.OpenAsync(mv.Movie_Path, msec);
            _pendingPreviewStartMs = _manualPreview.PositionMs;
            _applyPendingStartOnPlay = true;
            _useLegacyPreviewFallback = false;
            _isPreviewMediaOpened = false;
            uxPreviewFallbackImage.Source = null;
            uxPreviewImage.Stop();
            uxPreviewImage.Source = new Uri(mv.Movie_Path, UriKind.Absolute);
            uxPreviewImage.Volume = uxVolumeSlider.Value;

            uxTimeSlider.Maximum = Math.Max(_manualPreview.DurationMs, _manualPreview.PositionMs);
            uxTimeSlider.Value = _manualPreview.PositionMs;
            uxTime.Text = _manualPreview.PositionText;
            IsPlaying = false;
            PlayerArea.Visibility = Visibility.Visible;
            PlayerController.Visibility = Visibility.Visible;
            SetPreviewModeUi(useLegacyFallback: false);
            uxTimeSlider.Focus();
        }

        private void CloseManualThumbnailPreview()
        {
            timer.Stop();
            _manualPreview.CancelPending();
            _manualPreview.Close();
            _isPreviewMediaOpened = false;
            _pendingPreviewStartMs = 0d;
            _applyPendingStartOnPlay = false;
            _useLegacyPreviewFallback = false;
            uxPreviewImage.Stop();
            PlayerArea.Visibility = Visibility.Collapsed;
            PlayerController.Visibility = Visibility.Collapsed;
            uxPreviewImage.Visibility = Visibility.Collapsed;
            uxPreviewFallbackImage.Visibility = Visibility.Collapsed;
            uxPreviewImage.Source = null;
            uxPreviewFallbackImage.Source = null;
            IsPlaying = false;
        }

        private void ApplyManualPreviewTimerInterval()
        {
            timer.Interval = _useLegacyPreviewFallback
                ? _manualPreview.PlaybackInterval
                : TimeSpan.FromMilliseconds(100);
        }

        private void FR_Click(object sender, RoutedEventArgs e)
        {
            int tempSlider = PreviewPlaybackTiming.ClampSeekMs(
                (int)uxTimeSlider.Value,
                -100,
                0,
                (int)uxTimeSlider.Maximum);
            FF_FR(tempSlider);
        }
        private void FF_Click(object sender, RoutedEventArgs e)
        {
            int tempSlider = PreviewPlaybackTiming.ClampSeekMs(
                (int)uxTimeSlider.Value,
                100,
                0,
                (int)uxTimeSlider.Maximum);
            FF_FR(tempSlider);
        }
        private void FF_FR(int tempSlider)
        {
            uxTimeSlider.Value = tempSlider;
            SetPreviewPositionMs(tempSlider);
            uxTime.Text = _manualPreview.PositionText;
        }

        /// <summary>
        /// ドラッグしてなければ、再生中はスライダーを進めてプレビューを更新する。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isDragging || !_manualPreview.IsOpen)
            {
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            if (_useLegacyPreviewFallback)
            {
                double nextMs = _manualPreview.PositionMs + timer.Interval.TotalMilliseconds;
                if (_manualPreview.DurationMs > 0d && nextMs > _manualPreview.DurationMs)
                {
                    nextMs = _manualPreview.DurationMs;
                    IsPlaying = false;
                }

                _manualPreview.SetPositionMs(nextMs, schedulePreview: false);
                _isUpdatingSliderFromPlayer = true;
                uxTimeSlider.Value = _manualPreview.PositionMs;
                _isUpdatingSliderFromPlayer = false;
                uxTime.Text = _manualPreview.PositionText;
                _ = _manualPreview.RefreshPreviewAsync();
                return;
            }

            if (!_isPreviewMediaOpened)
            {
                return;
            }

            double currentMs = GetPreviewPositionMs();
            if (_manualPreview.DurationMs > 0d && currentMs > _manualPreview.DurationMs)
            {
                currentMs = _manualPreview.DurationMs;
            }

            _manualPreview.SetPositionMs(currentMs, schedulePreview: false);
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxTimeSlider_DragEnter(object sender, DragEventArgs e)
        {
            isDragging = true;
        }

        private void UxTimeSlider_DragLeave(object sender, DragEventArgs e)
        {
            isDragging = false;
            SetPreviewPositionMs(uxTimeSlider.Value);
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxPreviewImage_MediaOpened(object sender, RoutedEventArgs e)
        {
            _useLegacyPreviewFallback = false;
            _isPreviewMediaOpened = true;
            _manualPreview.SetPositionMs(_pendingPreviewStartMs, schedulePreview: false);
            uxPreviewImage.Volume = uxVolumeSlider.Value;
            SetPreviewModeUi(useLegacyFallback: false);

            if (uxPreviewImage.NaturalDuration.HasTimeSpan)
            {
                _manualPreview.SetPositionMs(Math.Min(_pendingPreviewStartMs, uxPreviewImage.NaturalDuration.TimeSpan.TotalMilliseconds), schedulePreview: false);
                uxTimeSlider.Maximum = uxPreviewImage.NaturalDuration.TimeSpan.TotalMilliseconds;
            }

            uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            uxPreviewImage.Play();
            uxPreviewImage.Pause();
            uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            uxTime.Text = _manualPreview.PositionText;
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
        }

        private void UxPreviewImage_MediaEnded(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
            timer.Stop();
            _manualPreview.SetPositionMs(_manualPreview.DurationMs, schedulePreview: false);
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxPreviewImage_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            IsPlaying = false;
            timer.Stop();
            _isPreviewMediaOpened = false;
            ActivateLegacyPreviewFallback();
        }

        private double GetPreviewPositionMs() =>
            _isPreviewMediaOpened
                ? uxPreviewImage.Position.TotalMilliseconds
                : _manualPreview.PositionMs;

        private void SetPreviewPositionMs(double positionMs)
        {
            _manualPreview.SetPositionMs(positionMs, schedulePreview: false);
            _pendingPreviewStartMs = _manualPreview.PositionMs;
            _applyPendingStartOnPlay = false;
            if (_useLegacyPreviewFallback)
            {
                _ = _manualPreview.RefreshPreviewAsync();
                return;
            }

            if (_isPreviewMediaOpened)
            {
                uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            }
        }

        private void ActivateLegacyPreviewFallback()
        {
            _useLegacyPreviewFallback = true;
            _applyPendingStartOnPlay = false;
            uxPreviewImage.Stop();
            SetPreviewModeUi(useLegacyFallback: true);
            ApplyManualPreviewTimerInterval();
            _ = _manualPreview.RefreshPreviewAsync();
        }

        private void SetPreviewModeUi(bool useLegacyFallback)
        {
            uxPreviewImage.Visibility = useLegacyFallback ? Visibility.Collapsed : Visibility.Visible;
            uxPreviewFallbackImage.Visibility = useLegacyFallback ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
