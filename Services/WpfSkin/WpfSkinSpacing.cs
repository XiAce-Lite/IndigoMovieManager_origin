using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// skin.json の margin / padding。数値（全辺同一）または "左,上,右,下" 文字列。
    /// </summary>
    public sealed class WpfSkinSpacing
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }

        public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

        public Thickness ToThickness() => new(Left, Top, Right, Bottom);

        public static WpfSkinSpacing Uniform(double value) =>
            new() { Left = value, Top = value, Right = value, Bottom = value };

        public static WpfSkinSpacing Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new WpfSkinSpacing();
            }

            string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length switch
            {
                1 when double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double uniform)
                    => Uniform(uniform),
                2 when TryParsePair(parts[0], parts[1], out double h, out double v)
                    => new WpfSkinSpacing { Left = h, Top = v, Right = h, Bottom = v },
                4 when TryParseQuad(parts, out WpfSkinSpacing quad) => quad,
                _ => new WpfSkinSpacing(),
            };
        }

        private static bool TryParsePair(string a, string b, out double first, out double second)
        {
            first = 0;
            second = 0;
            return double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out first)
                && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out second);
        }

        private static bool TryParseQuad(string[] parts, out WpfSkinSpacing spacing)
        {
            spacing = new WpfSkinSpacing();
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double left)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double top)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double right)
                || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double bottom))
            {
                return false;
            }

            spacing.Left = left;
            spacing.Top = top;
            spacing.Right = right;
            spacing.Bottom = bottom;
            return true;
        }
    }

    internal sealed class WpfSkinSpacingJsonConverter : JsonConverter<WpfSkinSpacing>
    {
        public override WpfSkinSpacing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.Number:
                    return WpfSkinSpacing.Uniform(reader.GetDouble());
                case JsonTokenType.String:
                    return WpfSkinSpacing.Parse(reader.GetString());
                case JsonTokenType.StartArray:
                    return ReadArray(ref reader);
                default:
                    throw new JsonException($"margin/padding に未対応の JSON トークンです: {reader.TokenType}");
            }
        }

        private static WpfSkinSpacing ReadArray(ref Utf8JsonReader reader)
        {
            var values = new List<double>(4);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException("margin/padding 配列は数値のみです。");
                }

                values.Add(reader.GetDouble());
            }

            return values.Count switch
            {
                0 => new WpfSkinSpacing(),
                1 => WpfSkinSpacing.Uniform(values[0]),
                2 => new WpfSkinSpacing
                {
                    Left = values[0],
                    Top = values[1],
                    Right = values[0],
                    Bottom = values[1],
                },
                4 => new WpfSkinSpacing
                {
                    Left = values[0],
                    Top = values[1],
                    Right = values[2],
                    Bottom = values[3],
                },
                _ => throw new JsonException("margin/padding 配列は 1 / 2 / 4 要素である必要があります。"),
            };
        }

        public override void Write(Utf8JsonWriter writer, WpfSkinSpacing value, JsonSerializerOptions options)
        {
            if (value == null || value.IsEmpty)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Left == value.Top && value.Top == value.Right && value.Right == value.Bottom)
            {
                writer.WriteNumberValue(value.Left);
                return;
            }

            writer.WriteStringValue(FormattableString.Invariant($"{value.Left},{value.Top},{value.Right},{value.Bottom}"));
        }
    }
}
