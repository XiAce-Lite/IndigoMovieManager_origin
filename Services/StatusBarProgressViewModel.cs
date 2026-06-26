using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// ウィンドウ下部ステータスバーにバインドする進捗表示用 ViewModel。
    /// </summary>
    public sealed class StatusBarProgressViewModel : INotifyPropertyChanged
    {
        public const double DetailMaxWidth = 480d;
        public const double MessageFontSize = 12d;

        private bool _isActive;
        private string _statusText = "準備完了";
        private double _progressPercent;
        private bool _showProgress;
        private bool _showCancel;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value ?? "");
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetField(ref _progressPercent, value);
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set => SetField(ref _showProgress, value);
        }

        public bool ShowCancel
        {
            get => _showCancel;
            set => SetField(ref _showCancel, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
