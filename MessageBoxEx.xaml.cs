using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// DialogWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MessageBoxEx : Window
    {
        private MessageBoxResult _closeStatus = MessageBoxResult.Cancel;

        public string DlogTitle = "";
        public string DlogMessage = "";
        public PackIconKind PackIconKind = PackIconKind.InfoBox;
        public bool UseCheckBox = false;
        public bool CheckBoxIsChecked = false;
        public string CheckBoxContent = "";
        public string Radio1Content = "";
        public string Radio2Content = "";
        public bool UseRadioButton = false;
        public bool Radio1IsChecked = true;
        public bool Radio2IsChecked = false;
        public bool OkOnly = false;
        /// <summary>true のとき Cancel にフォーカスし、Enter も Cancel 扱い（削除確認など）。</summary>
        public bool PreferCancelFocus = false;

        public MessageBoxEx(Window owner)
        {
            InitializeComponent();
            OwnedModalWindowHelper.ExcludeFromAltTab(this);
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ContentRendered += DialogWindowEx_ContentRendered;
        }

        private void DialogWindowEx_ContentRendered(object sender, EventArgs e)
        {
            Title = DlogTitle;
            dlogMessage.Text = DlogMessage;
            dlogIcon.Kind = PackIconKind;
            checkBox.Content = ToAccessContent(CheckBoxContent);
            checkBox.IsChecked = CheckBoxIsChecked;
            radioButton1.IsChecked = true;
            radioButton1.Content = ToAccessContent(Radio1Content);
            radioButton2.Content = ToAccessContent(Radio2Content);

            if (!UseCheckBox)
            {
                checkArea.Visibility = Visibility.Collapsed;
            }

            if (!UseRadioButton)
            {
                radioArea.Visibility = Visibility.Collapsed;
            }

            if (OkOnly)
            {
                Cancel.Visibility = Visibility.Collapsed;
                OK.Content = "閉じる(_C)";
                OK.IsCancel = true;
                Cancel.IsCancel = false;
                Dispatcher.BeginInvoke(
                    () => OK.Focus(),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (PreferCancelFocus)
            {
                OK.IsDefault = false;
                Cancel.IsDefault = true;
                Cancel.IsCancel = true;
                Dispatcher.BeginInvoke(
                    () => Cancel.Focus(),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        public MessageBoxResult CloseStatus() { return _closeStatus; }

        private void RadioButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is RadioButton radio)
            {
                radio.IsChecked = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.G:
                    if (TrySelectRadio(radioButton1))
                    {
                        e.Handled = true;
                    }
                    break;
                case Key.D:
                    if (TrySelectRadio(radioButton2))
                    {
                        e.Handled = true;
                    }
                    break;
                case Key.S:
                    if (TryToggleCheckBox())
                    {
                        e.Handled = true;
                    }
                    break;
            }
        }

        private bool TrySelectRadio(RadioButton radio)
        {
            if (!UseRadioButton || radioArea.Visibility != Visibility.Visible)
            {
                return false;
            }

            radio.IsChecked = true;
            radio.Focus();
            return true;
        }

        private bool TryToggleCheckBox()
        {
            if (!UseCheckBox || checkArea.Visibility != Visibility.Visible)
            {
                return false;
            }

            checkBox.IsChecked = checkBox.IsChecked != true;
            checkBox.Focus();
            return true;
        }

        private static object ToAccessContent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return new AccessText { Text = text };
        }

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
                CheckBoxIsChecked = (bool)checkBox.IsChecked;
                Radio1IsChecked = (bool)radioButton1.IsChecked;
                Radio2IsChecked = (bool)radioButton2.IsChecked;
            }
            Hide();
        }
    }
}
