using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// UserControl から MainWindow 機能へアクセスするための契約。
    /// </summary>
    public interface IMainWindowActions
    {
        ComboBox SearchBox { get; }
        TabControl Tabs { get; }
        Task SearchByKeywordAsync(string keyword);
        void PlayMovie_Click(object sender, RoutedEventArgs e);
        void DeleteBookmark(object sender, RoutedEventArgs e);
        void RefreshActiveList(int tabIndex);
        void RefreshExtDetail();
        void RequestDetailThumbnailRecreate();
        string DbFullPath { get; }
        void UpdateMovieColumn(long movieId, Data.MovieColumn column, object value);
    }
}
