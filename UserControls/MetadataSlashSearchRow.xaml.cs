using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// <c> / </c> 区切りメタデータ行。
    /// <see cref="ClickSearchMode"/> が Comment3Brace のとき各語クリックで列指定 SQL 検索。
    /// None のときは表示のみ。
    /// </summary>
    public partial class MetadataSlashSearchRow : UserControl
    {
        public const string ModeNone = "None";
        public const string ModeComment3Brace = "Comment3Brace";

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

        public static readonly DependencyProperty ClickSearchModeProperty =
            DependencyProperty.Register(
                nameof(ClickSearchMode),
                typeof(string),
                typeof(MetadataSlashSearchRow),
                new PropertyMetadata(ModeComment3Brace, OnClickSearchModeChanged));

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

        /// <summary><see cref="ModeNone"/> または <see cref="ModeComment3Brace"/>。</summary>
        public string ClickSearchMode
        {
            get => (string)GetValue(ClickSearchModeProperty);
            set => SetValue(ClickSearchModeProperty, value);
        }

        private bool IsComment3Search =>
            string.Equals(ClickSearchMode, ModeComment3Brace, StringComparison.OrdinalIgnoreCase);

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

        private static void OnClickSearchModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MetadataSlashSearchRow row)
            {
                row.RebuildSegments(row.Value ?? string.Empty);
            }
        }

        private void RebuildSegments(string value)
        {
            SegmentsPanel.Children.Clear();
            IReadOnlyList<string> segments = MetadataSlashSegments.Split(value);
            bool searchable = IsComment3Search;
            Style segmentStyle = searchable
                ? (Style)FindResource("MetadataSegmentStyle")
                : (Style)FindResource("MetadataSegmentPlainStyle");

            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    SegmentsPanel.Children.Add(new TextBlock { Style = (Style)FindResource("MetadataSeparatorStyle") });
                }

                string segment = segments[i];
                var textBlock = new TextBlock
                {
                    Style = segmentStyle,
                    Text = segment,
                    Tag = segment,
                };
                if (searchable)
                {
                    textBlock.ToolTip = $"{segment}\nクリックで Comment3 検索";
                    textBlock.MouseLeftButtonDown += Segment_MouseLeftButtonDown;
                }

                SegmentsPanel.Children.Add(textBlock);
            }
        }

        private async void Segment_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsComment3Search)
            {
                return;
            }

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

            // Ctrl+クリック加算は無効（列指定 SQL 同士の結合を避ける）。
            string searchKeyword = BraceFieldSearchBuilder.BuildComment3Like(keyword);
            if (string.IsNullOrEmpty(searchKeyword))
            {
                return;
            }

            await actions.SearchByKeywordAsync(searchKeyword).ConfigureAwait(true);
            e.Handled = true;
        }
    }
}
