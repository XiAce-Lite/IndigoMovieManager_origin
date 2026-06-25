namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// マニュアルサムネイルプレビュー用フレーム抽出。FFmpeg を優先し、未設定時は OpenCV にフォールバックする。
    /// </summary>
    internal static class PreviewFrameExtractor
    {
        public static async Task<string> TryExtractToTempFileAsync(
            string movieFullPath,
            double positionMs,
            CancellationToken cts)
        {
            string ffmpegResult = await FfmpegPreviewFrameExtractor
                .TryExtractToTempFileAsync(movieFullPath, positionMs, cts)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(ffmpegResult))
            {
                return ffmpegResult;
            }

            return await OpenCvPreviewFrameExtractor
                .TryExtractToTempFileAsync(movieFullPath, positionMs, cts)
                .ConfigureAwait(false);
        }
    }
}
