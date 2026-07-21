using System.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace IndigoMovieManager
{
    /// <summary>
    /// ドロワー内ナビゲーション行（アイコン + ラベル）。
    /// </summary>
    public class NavigationDrawerItem : INotifyPropertyChanged
    {
        private string _text = "";
        private bool _isEnabled = true;

        public string Text
        {
            get => _text;
            set
            {
                if (_text == value)
                {
                    return;
                }

                _text = value ?? string.Empty;
                OnPropertyChanged(nameof(Text));
            }
        }

        /// <summary>NavigationMenuIds、ファイルパス、action:* など。</summary>
        public string Id { get; init; } = "";

        public PackIconKind IconKind { get; init; } = PackIconKind.Circle;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static NavigationDrawerItem ForRecentFile(string path) =>
            new()
            {
                Text = path,
                Id = path,
                IconKind = PackIconKind.File,
            };
    }
}
