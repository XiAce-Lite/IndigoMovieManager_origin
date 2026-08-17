using IndigoMovieManager.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace IndigoMovieManager
{
    /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly ObservableCollection<PreGenThumbSkinSelection.SkinOption> _preGenSkinOptions = [];

        public SettingsWindow()
        {
            InitializeComponent();
            PlayerParam.ItemsSource = new string[]
            {
                "/start <ms>",
                "<file> player -seek pos=<ms>"
            };
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializePreGenThumbSkinList();
        }

        private void InitializePreGenThumbSkinList()
        {
            string storedKeys = DataContext is DatabaseSettings db
                ? db.PreGenThumbSkinKeys
                : string.Empty;
            bool enabled = DataContext is DatabaseSettings settings
                && settings.PreGenThumbsOnNewMovies;

            _preGenSkinOptions.Clear();
            foreach (PreGenThumbSkinSelection.SkinOption option in PreGenThumbSkinSelection.BuildOptionsFromDisk(
                         PreGenThumbSkinSelection.ParseStoredKeys(storedKeys)))
            {
                _preGenSkinOptions.Add(option);
            }

            PreGenThumbSkinList.ItemsSource = _preGenSkinOptions;
            PreGenThumbsOnNewMoviesCheck.IsChecked = enabled;
            UpdatePreGenThumbSkinListEnabled();
        }

        /// <summary>閉じる前に DataContext（DatabaseSettings）へ反映する。</summary>
        public void CommitPreGenThumbSelection()
        {
            if (DataContext is not DatabaseSettings db)
            {
                return;
            }

            db.PreGenThumbsOnNewMovies = PreGenThumbsOnNewMoviesCheck.IsChecked == true;
            db.PreGenThumbSkinKeys = PreGenThumbSkinSelection.FormatStoredKeys(
                _preGenSkinOptions.Where(o => o.IsChecked).Select(o => o.Key));
        }

        private void PreGenThumbsOnNewMoviesCheck_Changed(object sender, RoutedEventArgs e) =>
            UpdatePreGenThumbSkinListEnabled();

        private void UpdatePreGenThumbSkinListEnabled()
        {
            bool enabled = PreGenThumbsOnNewMoviesCheck.IsChecked == true;
            PreGenThumbSkinList.IsEnabled = enabled;
            PreGenThumbSelectAllButton.IsEnabled = enabled;
            PreGenThumbClearButton.IsEnabled = enabled;
        }

        private void PreGenThumbSelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (PreGenThumbSkinSelection.SkinOption option in _preGenSkinOptions)
            {
                option.IsChecked = true;
            }
        }

        private void PreGenThumbClearButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (PreGenThumbSkinSelection.SkinOption option in _preGenSkinOptions)
            {
                option.IsChecked = false;
            }
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenFolderDialog_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;

            if (!(item.Name is "OpenThumbFolder" or "OpenBookmarkFolder"))
            {
                return;
            }

            var dlgTitle = item.Name == "OpenThumbFolder" ? "サムネイルの保存先" : "ブックマークの保存先";
            var dlg = new OpenFolderDialog
            {
                Title = dlgTitle,
                Multiselect = false,
                AddToRecent = true,
            };

            var ret = dlg.ShowDialog();

            TextBox textBox = item.Name == "OpenThumbFolder" ? ThumbFolder : BookmarkFolder;
            if (ret == true)
            {
                textBox.Text = dlg.FolderName;
            }
        }

        private void OpenDialogPlayer_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                RestoreDirectory = true,
                Filter = "実行ファイル(*.exe)|*.exe|すべてのファイル(*.*)|*.*",
                FilterIndex = 1,
                Title = "既定のプレイヤー選択"
            };

            var result = ofd.ShowDialog();
            if (result == true)
            {
                PlayerPrg.Text = ofd.FileName;
            }
        }
    }
}
