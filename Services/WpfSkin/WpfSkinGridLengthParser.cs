using System;
using System.Globalization;
using System.Windows;

namespace IndigoMovieManager.Services.WpfSkin
{
    internal static class WpfSkinGridLengthParser
    {
        public static GridLength Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GridLength.Auto;
            }

            value = value.Trim();
            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return GridLength.Auto;
            }

            if (value.EndsWith('*'))
            {
                string starPart = value[..^1].Trim();
                if (string.IsNullOrEmpty(starPart))
                {
                    return new GridLength(1, GridUnitType.Star);
                }

                if (double.TryParse(starPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double stars))
                {
                    return new GridLength(stars, GridUnitType.Star);
                }
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels))
            {
                return new GridLength(pixels, GridUnitType.Pixel);
            }

            return GridLength.Auto;
        }
    }
}
