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

        /// <summary>
        /// 同じ SkinDefinition 参照のまま layout だけ変わったときに再構築する。
        /// </summary>
        public void RebuildLayoutNow() => RebuildLayout();

        /// <summary>デザイン時カード幅グリップ用。現在の見た目のカード幅。</summary>
        public double GetPreviewCardWidth()
        {
            if (!double.IsNaN(CardBorder.Width) && CardBorder.Width > 0)
            {
                return CardBorder.Width;
            }

            return ActualWidth > 0 ? ActualWidth : 0;
        }

        /// <summary>デザイン時カード幅グリップ用。レイアウト再構築なしで幅だけ変える。</summary>
        public void SetPreviewCardWidth(double width)
        {
            if (SkinDefinition?.Card?.Stretch == true)
            {
                return;
            }

            CardBorder.Width = Math.Max(80, width);
            CardBorder.HorizontalAlignment = HorizontalAlignment.Left;
            HorizontalAlignment = HorizontalAlignment.Left;
        }

        /// <summary>デザイン時カード高さグリップ用。</summary>
        public double GetPreviewCardHeight()
        {
            if (!double.IsNaN(CardBorder.Height) && CardBorder.Height > 0)
            {
                return CardBorder.Height;
            }

            return CardBorder.ActualHeight > 0 ? CardBorder.ActualHeight : ActualHeight;
        }

        /// <summary>デザイン時カード高さグリップ用。0 で自動に戻す。</summary>
        public void SetPreviewCardHeight(double height)
        {
            if (height <= 0)
            {
                CardBorder.ClearValue(FrameworkElement.HeightProperty);
                return;
            }

            CardBorder.Height = Math.Max(40, height);
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

            if (card.Stretch)
            {
                // 幅を固定せずコンテナに追従させる。Measure 時の希望幅は中身の自然幅
                // （≒ cardWidth）になるため WrapPanel のカラム数計算はそのまま機能し、
                // Arrange 時に StretchItems がスロット幅まで引き伸ばす。
                CardBorder.ClearValue(WidthProperty);
                CardBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                // UserControl 既定の HorizontalContentAlignment=Left だと、内側の
                // CardBorder/レイアウトが内容幅で左寄せにシュリンクラップされ、grid の
                // star 列が広がらずテキストが「…」になる。Stretch にして内部も全幅へ伸ばす。
                HorizontalContentAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                CardBorder.Width = cardWidth;
                CardBorder.HorizontalAlignment = HorizontalAlignment.Left;
                HorizontalContentAlignment = HorizontalAlignment.Left;
            }

            CardBorder.Padding = new Thickness(card.Padding);
            // margin が JSON に明示されていれば（0 でも）それを尊重する。
            // 未指定（null）のときだけ既定 2px。IsEmpty で判定すると "margin":0 が既定 2 に化ける。
            CardBorder.Margin = card.Margin != null
                ? card.Margin.ToThickness()
                : new Thickness(2);
            CardBorder.Background = WpfSkinColorResolver.ResolveBrush(card.Background, null, SkinDefinition);

            if (card.Height > 0)
            {
                CardBorder.Height = card.Height;
                VerticalContentAlignment = VerticalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Top;
            }
            else
            {
                CardBorder.ClearValue(FrameworkElement.HeightProperty);
                VerticalContentAlignment = VerticalAlignment.Top;
            }

            LayoutHost.Content = WpfSkinLayoutBuilder.Build(card.Layout ?? new WpfSkinNode(), SkinDefinition);
            if (LayoutHost.Content is FrameworkElement root && card.Height > 0)
            {
                root.VerticalAlignment = VerticalAlignment.Stretch;
                root.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
    }
}
