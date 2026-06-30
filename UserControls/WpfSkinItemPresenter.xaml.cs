using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndigoMovieManager.Services.WpfSkin;

namespace IndigoMovieManager.UserControls
{
    /// <summary>
    /// skin.json のカード定義を 1 件の MovieRecords 向けに描画する。
    /// </summary>
    public partial class WpfSkinItemPresenter : UserControl
    {
        public static readonly DependencyProperty SkinDefinitionProperty =
            DependencyProperty.Register(
                nameof(SkinDefinition),
                typeof(WpfSkinDefinition),
                typeof(WpfSkinItemPresenter),
                new PropertyMetadata(null, OnSkinDefinitionChanged));

        public WpfSkinDefinition SkinDefinition
        {
            get => (WpfSkinDefinition)GetValue(SkinDefinitionProperty);
            set => SetValue(SkinDefinitionProperty, value);
        }

        public WpfSkinItemPresenter()
        {
            InitializeComponent();
        }

        private static void OnSkinDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WpfSkinItemPresenter presenter)
            {
                presenter.RebuildLayout();
            }
        }

        private void RebuildLayout()
        {
            if (SkinDefinition == null)
            {
                LayoutHost.Content = null;
                return;
            }

            WpfSkinCard card = SkinDefinition.Card ?? new WpfSkinCard();
            double cardWidth = card.Width > 0 ? card.Width : SkinDefinition.Thumbnail.Width;

            CardBorder.Width = cardWidth;
            CardBorder.Padding = new Thickness(card.Padding);
            // margin が JSON に明示されていれば（0 でも）それを尊重する。
            // 未指定（null）のときだけ既定 2px。IsEmpty で判定すると "margin":0 が既定 2 に化ける。
            CardBorder.Margin = card.Margin != null
                ? card.Margin.ToThickness()
                : new Thickness(2);
            CardBorder.Background = ParseBrush(card.Background);

            if (card.Height > 0)
            {
                CardBorder.Height = card.Height;
            }
            else
            {
                CardBorder.ClearValue(FrameworkElement.HeightProperty);
            }

            LayoutHost.Content = WpfSkinLayoutBuilder.Build(card.Layout ?? new WpfSkinNode(), SkinDefinition);
        }

        private static Brush ParseBrush(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                var brush = (Brush)new BrushConverter().ConvertFromString(value);
                brush?.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
