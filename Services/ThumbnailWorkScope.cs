namespace IndigoMovieManager.Services
{
    /// <summary>
    /// サムネイル作成バッチ（Parallel.ForEachAsync）用のキャンセル。
    /// プロセッサループ本体は止めず、DB 切替時に実行中バッチだけ止める。
    /// </summary>
    internal sealed class ThumbnailWorkScope
    {
        private CancellationTokenSource _batchCts = new();

        public CancellationToken Token => _batchCts.Token;

        public void CancelBatch()
        {
            try
            {
                _batchCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _batchCts.Dispose();
            _batchCts = new CancellationTokenSource();
        }
    }
}
