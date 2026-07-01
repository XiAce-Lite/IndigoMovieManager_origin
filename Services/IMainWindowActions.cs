using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// UserControl から MainWindow 機能へアクセスするための契約。
    /// </summary>
    public interface IMainWindowActions
    {
        ComboBox SearchBox { get; }
        SkinEngine CurrentSkinEngine { get; }
        bool IsMovieListActive { get; }
        Task SearchByKeywordAsync(string keyword, bool addToHistory = true);
        void PlayMovie_Click(object sender, RoutedEventArgs e);
        void DeleteBookmark(object sender, RoutedEventArgs e);
        void RefreshActiveList(SkinEngine engine);
        void RefreshExtDetail();
        void RequestDetailThumbnailRecreate();
        string DbFullPath { get; }
        void UpdateMovieColumn(long movieId, Data.MovieColumn column, object value);
    }
}
