using System.Windows;
using System.Windows.Controls;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;

namespace IndigoMovieManager
{
    /// <summary>新規 WPF スキン作成時のテンプレート選択。</summary>
    public partial class WpfSkinTemplatePickWindow : Window
    {
        /// <summary>既存スキンから複製するときのフォルダ名。構造テンプレ選択時は null。</summary>
        public string SelectedTemplateName { get; private set; }

        /// <summary>構造テンプレ選択時にセットされる。既存スキン選択時は null。</summary>
        public WpfSkinStructTemplate SelectedStructTemplate { get; private set; }

        public WpfSkinTemplatePickWindow(Window owner)
        {
            InitializeComponent();
            OwnedModalWindowHelper.ExcludeFromAltTab(this);
            Owner = owner;

            // 既存スキン一覧
            IReadOnlyList<WpfSkinTemplateCatalog.Entry> entries = WpfSkinTemplateCatalog.Available();
            TemplateList.ItemsSource = entries;
            if (entries.Count > 0)
            {
                TemplateList.SelectedIndex = 0;
            }

            // 構造テンプレ一覧
            StructList.ItemsSource = WpfSkinStructTemplateCatalog.All;
            if (WpfSkinStructTemplateCatalog.All.Count > 0)
            {
                StructList.SelectedIndex = 0;
            }

            OkButton.IsEnabled = entries.Count > 0 || WpfSkinStructTemplateCatalog.All.Count > 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabs.SelectedIndex == 0)
            {
                // 既存スキンタブ
                if (TemplateList.SelectedItem is not WpfSkinTemplateCatalog.Entry entry)
                {
                    return;
                }

                SelectedTemplateName = entry.FolderName;
                SelectedStructTemplate = null;
            }
            else
            {
                // 構造テンプレタブ
                if (StructList.SelectedItem is not WpfSkinStructTemplate tmpl)
                {
                    return;
                }

                SelectedStructTemplate = tmpl;
                SelectedTemplateName = null;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TemplateList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TemplateList.SelectedItem != null)
            {
                OkButton_Click(sender, e);
            }
        }

        private void StructList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (StructList.SelectedItem != null)
            {
                MainTabs.SelectedIndex = 1;
                OkButton_Click(sender, e);
            }
        }
    }
}
