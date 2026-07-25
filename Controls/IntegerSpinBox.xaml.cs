using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IndigoMovieManager.Controls
{
    /// <summary>整数スピン（TextBox + 上下ボタン）。</summary>
    public partial class IntegerSpinBox : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(int),
                typeof(IntegerSpinBox),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(IntegerSpinBox), new PropertyMetadata(0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(IntegerSpinBox), new PropertyMetadata(int.MaxValue));

        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register(nameof(Hint), typeof(string), typeof(IntegerSpinBox), new PropertyMetadata(""));

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ValueChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int>),
                typeof(IntegerSpinBox));

        private bool _suppress;

        public IntegerSpinBox()
        {
            InitializeComponent();
            SyncTextFromValue();
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<int> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not IntegerSpinBox box)
            {
                return;
            }

            int clamped = Math.Clamp((int)e.NewValue, box.Minimum, box.Maximum);
            if (clamped != (int)e.NewValue)
            {
                box.Value = clamped;
                return;
            }

            box.SyncTextFromValue();
            box.RaiseEvent(new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, clamped, ValueChangedEvent));
        }

        private void SyncTextFromValue()
        {
            if (_suppress)
            {
                return;
            }

            _suppress = true;
            ValueBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            _suppress = false;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEnabled)
            {
                return;
            }

            Value = Math.Min(Maximum, Value + 1);
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEnabled)
            {
                return;
            }

            Value = Math.Max(Minimum, Value - 1);
        }

        private void ValueBox_LostFocus(object sender, RoutedEventArgs e) => CommitText();

        private void ValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                UpButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                DownButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void CommitText()
        {
            if (_suppress)
            {
                return;
            }

            if (int.TryParse(ValueBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                Value = Math.Clamp(parsed, Minimum, Maximum);
            }
            else
            {
                SyncTextFromValue();
            }
        }
    }
}
