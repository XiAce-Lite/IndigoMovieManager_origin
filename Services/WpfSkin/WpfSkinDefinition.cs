using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IndigoMovieManager.Services.WpfSkin
{
    public sealed class WpfSkinDefinition
    {
        public string Name { get; set; } = "CardLarge";
        public string Type { get; set; } = "card";
        public WpfSkinThumbnail Thumbnail { get; set; } = new();
        public WpfSkinCard Card { get; set; } = new();
        public WpfSkinSurface Surface { get; set; } = new();
        public Dictionary<string, WpfSkinStyle> Styles { get; set; } = new();

        /// <summary>type が "list" のとき、1 行 1 アイテムの縦リスト表示にする。</summary>
        [JsonIgnore]
        public bool IsList => string.Equals(Type, "list", System.StringComparison.OrdinalIgnoreCase);
    }

    public sealed class WpfSkinSurface
    {
        // 既定は既存 Small タブ準拠（システムウィンドウ背景＝白）。
        public string Background { get; set; } = "#FFFFFF";
    }

    public sealed class WpfSkinThumbnail
    {
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 225;
        public int Columns { get; set; } = 1;
        public int Rows { get; set; } = 1;
        public double TargetAspect => Height > 0 ? (double)Width / Height : 16.0 / 9.0;
    }

    public sealed class WpfSkinCard
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Padding { get; set; } = 8;

        /// <summary>
        /// true のとき、カードを固定幅にせずコンテナ幅に追従させる。
        /// 1 カラムしか入らない幅ではウィンドウ幅まで広がり、複数カラム入る幅では
        /// 等幅で並ぶ（既定 Big / 5x10 タブと同じ挙動）。
        /// </summary>
        public bool Stretch { get; set; }

        [JsonConverter(typeof(WpfSkinSpacingJsonConverter))]
        public WpfSkinSpacing Margin { get; set; }
        // 既定はカード背景なし（Small タブ同様、リスト背景をそのまま使う）。
        public string Background { get; set; } = "";
        public WpfSkinNode Layout { get; set; } = new();
    }

    public sealed class WpfSkinStyle
    {
        public double FontSize { get; set; }
        public string FontFamily { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string Foreground { get; set; } = "";
        public string Background { get; set; } = "";
        public string Align { get; set; } = "";
        public bool Wrap { get; set; }
    }

    public sealed class WpfSkinNode
    {
        // ---- コンテナ ----
        /// <summary>stack / grid。children があるとき使用。未指定なら stack。</summary>
        public string Panel { get; set; } = "";

        public string Stack { get; set; } = "vertical";
        public List<WpfSkinNode> Children { get; set; }

        /// <summary>Grid 行定義: auto, *, 2*, 120 など。</summary>
        public List<string> Rows { get; set; }

        /// <summary>Grid 列定義: auto, *, 2*, 120 など。</summary>
        public List<string> Columns { get; set; }

        // ---- 要素 ----
        public string Type { get; set; } = "text";
        public string Field { get; set; } = "";
        public string Label { get; set; } = "";

        /// <summary>list 型のカラム見出し（ヘッダー行に表示）。</summary>
        public string Header { get; set; } = "";
        public string Format { get; set; } = "";
        // 以下のスタイル系は未指定（空 / 0）を既定とし、styles 参照や既定スタイルが効くようにする。
        public string Align { get; set; } = "";

        public double FontSize { get; set; }
        public string FontFamily { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string Foreground { get; set; } = "";
        public bool Wrap { get; set; }

        /// <summary>styles 辞書のキー。</summary>
        public string Style { get; set; } = "";

        // ---- Grid 配置 ----
        public int Row { get; set; }
        public int Col { get; set; }
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;

        // ---- 箱 ----
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? MinWidth { get; set; }
        public double? MaxWidth { get; set; }
        public double? MinHeight { get; set; }
        public double? MaxHeight { get; set; }
        [JsonConverter(typeof(WpfSkinSpacingJsonConverter))]
        public WpfSkinSpacing Margin { get; set; }
        [JsonConverter(typeof(WpfSkinSpacingJsonConverter))]
        public WpfSkinSpacing Padding { get; set; }
        public string VAlign { get; set; } = "";
        public string HAlign { get; set; } = "";
        public string Background { get; set; } = "";

        [JsonIgnore]
        public bool IsContainer => Children != null && Children.Count > 0;

        [JsonIgnore]
        public bool IsGrid =>
            IsContainer && string.Equals(ResolvePanel(), "grid", System.StringComparison.OrdinalIgnoreCase);

        public string ResolvePanel()
        {
            if (!string.IsNullOrWhiteSpace(Panel))
            {
                return Panel;
            }

            return "stack";
        }
    }
}
