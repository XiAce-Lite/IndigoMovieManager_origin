using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace IndigoMovieManager
{
    /// <summary>
    /// Settings.xaml の相互作用ロジック
    /// </summary>
    public partial class CommonSettingsWindow : Window
    {
        public CommonSettingsWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
            DefaultPlayerParam.ItemsSource = new string[]
            {
                "/start <ms>",
                "<file> player -seek pos=<ms>"
            };
            DefaultZipViewerParam.ItemsSource = new string[]
            {
                "<file>",
                "\"<file>\""
            };
            InitializeFfmpegHardwareDecodeCombo();
        }

        private void InitializeFfmpegHardwareDecodeCombo()
        {
            (string Value, string Label)[] items =
            [
                ("Off", "使用しない"),
                ("Auto", "自動"),
                ("Cuda", "NVIDIA CUDA"),
                ("Qsv", "Intel QSV"),
                ("D3d11va", "D3D11VA"),
                ("Dxva2", "DXVA2"),
            ];

            foreach ((string value, string label) in items)
            {
                FfmpegHardwareDecodeModeCombo.Items.Add(new ComboBoxItem
                {
                    Content = label,
                    Tag = value,
                });
            }

            string current = Properties.Settings.Default.FfmpegHardwareDecodeMode?.Trim() ?? "Off";
            foreach (ComboBoxItem item in FfmpegHardwareDecodeModeCombo.Items)
            {
                if (string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
                {
                    FfmpegHardwareDecodeModeCombo.SelectedItem = item;
                    break;
                }
            }

            if (FfmpegHardwareDecodeModeCombo.SelectedItem == null && FfmpegHardwareDecodeModeCombo.Items.Count > 0)
            {
                FfmpegHardwareDecodeModeCombo.SelectedIndex = 0;
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.AutoOpen = (bool)AutoOpen.IsChecked;
            Properties.Settings.Default.ConfirmExit = (bool)ConfirmExit.IsChecked;
            Properties.Settings.Default.DefaultPlayerPath = DefaultPlayerPath.Text;
            Properties.Settings.Default.DefaultPlayerParam = DefaultPlayerParam.Text;
            Properties.Settings.Default.DefaultZipViewerPath = DefaultZipViewerPath.Text;
            Properties.Settings.Default.DefaultZipViewerParam = DefaultZipViewerParam.Text;
            Properties.Settings.Default.RecentFilesCount = (int)slider.Value;
            Properties.Settings.Default.CheckExt = MediaExtensionSettings.NormalizeListForStorage(CheckExt.Text);
            if (FfmpegHardwareDecodeModeCombo.SelectedItem is ComboBoxItem hwItem
                && hwItem.Tag is string hwMode)
            {
                Properties.Settings.Default.FfmpegHardwareDecodeMode = hwMode;
                FfmpegHardwareDecodePolicy.InvalidateCache();
            }

            MediaExtensionSettings.EnsureRequiredExtensions();
            Properties.Settings.Default.Save();
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
                DefaultPlayerPath.Text = ofd.FileName;
            }
        }

        private void OpenDialogZipViewer_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                RestoreDirectory = true,
                Filter = "実行ファイル(*.exe)|*.exe|すべてのファイル(*.*)|*.*",
                FilterIndex = 1,
                Title = "ZIP画像ビューワー選択"
            };

            var result = ofd.ShowDialog();
            if (result == true)
            {
                DefaultZipViewerPath.Text = ofd.FileName;
            }
        }
    }
}
