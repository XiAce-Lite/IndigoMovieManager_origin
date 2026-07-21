using System.Windows;
using System.Windows.Controls;

namespace IndigoMovieManager
{
    public sealed class MetadataEditModel
    {
        public string Title { get; set; }
        public string Comment1 { get; set; }
        public string Comment2 { get; set; }
        public string Comment3 { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }

        public static MetadataEditModel FromMovie(MovieRecords movie)
        {
            return new MetadataEditModel
            {
                Title = movie?.Title ?? string.Empty,
                Comment1 = movie?.Comment1 ?? string.Empty,
                Comment2 = movie?.Comment2 ?? string.Empty,
                Comment3 = movie?.Comment3 ?? string.Empty,
                Artist = movie?.Artist ?? string.Empty,
                Genre = movie?.Genre ?? string.Empty,
            };
        }

        public void ApplyTo(MovieRecords movie)
        {
            if (movie == null)
            {
                return;
            }

            movie.Title = Title ?? string.Empty;
            movie.Comment1 = Comment1 ?? string.Empty;
            movie.Comment2 = Comment2 ?? string.Empty;
            movie.Comment3 = Comment3 ?? string.Empty;
            movie.Artist = Artist ?? string.Empty;
            movie.Genre = Genre ?? string.Empty;
        }
    }

    /// <summary>
    /// メタデータ（title / comment1–3 / artist / genre）編集ダイアログ。
    /// </summary>
    public partial class MetadataEditWindow : Window
    {
        private MessageBoxResult _closeStatus = MessageBoxResult.Cancel;

        public MetadataEditWindow()
        {
            InitializeComponent();
            ContentRendered += MetadataEditWindow_ContentRendered;
        }

        private void MetadataEditWindow_ContentRendered(object sender, EventArgs e)
        {
            _ = TitleBox.Focus();
            TitleBox.CaretIndex = TitleBox.Text?.Length ?? 0;
        }

        public MessageBoxResult CloseStatus() => _closeStatus;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _closeStatus = btn.Name switch
                {
                    "OK" => MessageBoxResult.OK,
                    "Cancel" => MessageBoxResult.Cancel,
                    _ => MessageBoxResult.Cancel,
                };
            }

            Hide();
        }
    }
}
