using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;

namespace IndigoMovieManager
{
    public partial class DmmTagExcludeWindow : Window
    {
        private MessageBoxResult _closeStatus = MessageBoxResult.Cancel;

        public DmmTagExcludeWindow()
        {
            InitializeComponent();
            OwnedModalWindowHelper.ExcludeFromAltTab(this);
            PatternEditBox.Text = Properties.Settings.Default.DmmTagExcludePatterns ?? "";
            ContentRendered += (_, _) =>
            {
                _ = PatternEditBox.Focus();
                PatternEditBox.CaretIndex = PatternEditBox.Text.Length;
            };
        }

        public MessageBoxResult CloseStatus() => _closeStatus;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            if (btn.Name == "OK")
            {
                string normalized = DmmTagExcludePatternMatcher.NormalizeForStorage(PatternEditBox.Text);
                DmmTagExcludePatternMatcher.ParseResult parsed = DmmTagExcludePatternMatcher.Validate(normalized);
                if (!parsed.IsValid)
                {
                    var message = new StringBuilder();
                    message.AppendLine("次の行のパターンが不正です。修正してから保存してください。");
                    message.AppendLine();
                    foreach (string line in parsed.InvalidLines.Take(8))
                    {
                        message.AppendLine("・" + line);
                    }

                    if (parsed.InvalidLines.Count > 8)
                    {
                        message.AppendLine($"…他 {parsed.InvalidLines.Count - 8} 件");
                    }

                    MessageBox.Show(
                        this,
                        message.ToString(),
                        Assembly.GetExecutingAssembly().GetName().Name,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                AppSettingsPersistence.SaveDmmTagExcludePatterns(normalized);
                DmmTagExcludePatternMatcher.Shared.ReloadFrom(normalized);
                PatternEditBox.Text = normalized;
                _closeStatus = MessageBoxResult.OK;
                Hide();
                return;
            }

            _closeStatus = MessageBoxResult.Cancel;
            Hide();
        }
    }
}
