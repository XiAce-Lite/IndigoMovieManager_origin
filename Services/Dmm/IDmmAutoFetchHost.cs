namespace IndigoMovieManager.Services.Dmm
{
    internal interface IDmmAutoFetchHost
    {
        bool IsManualFetchRunning { get; }

        void RunOnUi(Action action);

        Task RunOnUiAsync(Action action);

        MovieRecords FindMovieRecord(long movieId);

        void NotifyRecordUpdated(long movieId);

        void NotifyPendingCandidatesChanged();

        void ShowCompletionMessage(string message);

        /// <summary>一括取得など、完了をダイアログでも知らせたい場合。</summary>
        void ShowCompletionDialog(string title, string message);
    }
}
