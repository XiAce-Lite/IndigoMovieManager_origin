namespace IndigoMovieManager.Services
{
    /// <summary>
    /// メタデータ（title / comment1–3 / artist / genre）のクリップボード往復。
    /// </summary>
    internal static class MetadataClipboard
    {
        public const string ClipboardFormat = "IndigoMovieManager.Metadata.v1";

        public static string Serialize(MetadataEditModel model)
        {
            model ??= new MetadataEditModel();
            return string.Join("\n",
                ClipboardFormat,
                "Title=" + Escape(model.Title),
                "Comment1=" + Escape(model.Comment1),
                "Comment2=" + Escape(model.Comment2),
                "Comment3=" + Escape(model.Comment3),
                "Artist=" + Escape(model.Artist),
                "Genre=" + Escape(model.Genre));
        }

        public static bool TryDeserialize(string text, out MetadataEditModel model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n', StringSplitOptions.None);
            if (lines.Length == 0 ||
                !string.Equals(lines[0].Trim(), ClipboardFormat, StringComparison.Ordinal))
            {
                return false;
            }

            var result = new MetadataEditModel();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line[..eq];
                string value = Unescape(line[(eq + 1)..]);
                switch (key)
                {
                    case "Title":
                        result.Title = value;
                        break;
                    case "Comment1":
                        result.Comment1 = value;
                        break;
                    case "Comment2":
                        result.Comment2 = value;
                        break;
                    case "Comment3":
                        result.Comment3 = value;
                        break;
                    case "Artist":
                        result.Artist = value;
                        break;
                    case "Genre":
                        result.Genre = value;
                        break;
                }
            }

            model = result;
            return true;
        }

        private static string Escape(string value) =>
            (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    char next = value[++i];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        '\\' => '\\',
                        _ => next,
                    });
                    continue;
                }

                sb.Append(value[i]);
            }

            return sb.ToString();
        }
    }
}
