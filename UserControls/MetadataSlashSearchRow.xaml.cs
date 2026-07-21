using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// <c> / </c> 区切りメタデータ行。各セグメントをクリックすると検索する。
    /// </summary>
    public partial class MetadataSlashSearchRow : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(MetadataSlashSearchRow),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(MetadataSlashSearchRow),
                new PropertyMetadata(string.Empty, OnValueChanged));

        private bool _ctrlFlg;

        public MetadataSlashSearchRow()
        {
            InitializeComponent();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MetadataSlashSearchRow row)
            {
                row.LabelText.Text = e.NewValue as string ?? string.Empty;
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MetadataSlashSearchRow row)
            {
                row.RebuildSegments(e.NewValue as string ?? string.Empty);
            }
        }

        private void RebuildSegments(string value)
        {
            SegmentsPanel.Children.Clear();
            IReadOnlyList<string> segments = MetadataSlashSegments.Split(value);
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    SegmentsPanel.Children.Add(new TextBlock { Style = (Style)FindResource("MetadataSeparatorStyle") });
                }

                string segment = segments[i];
                var textBlock = new TextBlock
                {
                    Style = (Style)FindResource("MetadataSegmentStyle"),
                    Text = segment,
                    ToolTip = $"{segment}\nクリックで検索",
                    Tag = segment,
                };
                textBlock.MouseLeftButtonDown += Segment_MouseLeftButtonDown;
                SegmentsPanel.Children.Add(textBlock);
            }
        }

        private void MetadataSlashSearchRow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                _ctrlFlg = true;
            }
        }

        private void MetadataSlashSearchRow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                _ctrlFlg = false;
            }
        }

        private async void Segment_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBlock textBlock || textBlock.Tag is not string keyword)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            IMainWindowActions actions = MainWindowActionsHelper.GetActions(this);
            if (actions == null)
            {
                return;
            }

            string searchKeyword = _ctrlFlg
                ? (actions.SearchBox.Text ?? "") + " " + keyword
                : keyword;

            await actions.SearchByKeywordAsync(searchKeyword).ConfigureAwait(true);
            e.Handled = true;
        }
    }
}
