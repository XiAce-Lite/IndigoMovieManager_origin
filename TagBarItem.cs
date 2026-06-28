using System.ComponentModel;

namespace IndigoMovieManager
{
    public class TagBarItem : INotifyPropertyChanged
    {
        private long itemId;
        private long parentId;
        private long orderId;
        private long groupId;
        private string title = "";
        private string contents = "";

        public long Item_Id
        {
            get => itemId;
            set { itemId = value; OnPropertyChanged(nameof(Item_Id)); }
        }

        public long Parent_Id
        {
            get => parentId;
            set { parentId = value; OnPropertyChanged(nameof(Parent_Id)); }
        }

        public long Order_Id
        {
            get => orderId;
            set { orderId = value; OnPropertyChanged(nameof(Order_Id)); }
        }

        public long Group_Id
        {
            get => groupId;
            set { groupId = value; OnPropertyChanged(nameof(Group_Id)); }
        }

        public string Title
        {
            get => title;
            set { title = value ?? ""; OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(EffectiveContents)); }
        }

        public string Contents
        {
            get => contents;
            set { contents = value ?? ""; OnPropertyChanged(nameof(Contents)); OnPropertyChanged(nameof(EffectiveContents)); }
        }

        public string EffectiveContents =>
            !string.IsNullOrWhiteSpace(Contents) ? Contents.Trim() : (Title?.Trim() ?? "");

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
