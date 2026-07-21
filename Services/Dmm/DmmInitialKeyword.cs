using System.IO;

namespace IndigoMovieManager.Services.Dmm
{
    internal static class DmmInitialKeyword
    {
        public static string FromMovieName(string movieName)
        {
            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(movieName);
            if (extracted.HasProductCode)
            {
                return extracted.ProductCode;
            }

            string body = Path.GetFileNameWithoutExtension(movieName ?? string.Empty);
            return string.IsNullOrWhiteSpace(body) ? movieName ?? string.Empty : body;
        }

        /// <summary>
        /// 手動検索向け。ファイル名から推定した品番表記の揺れ（xxxx-024 / xxxx024 等）を返す。
        /// </summary>
        public static IReadOnlyList<string> SuggestSearchVariants(string movieName)
        {
            var variants = new List<string>();
            void Add(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string trimmed = value.Trim();
                if (!variants.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    variants.Add(trimmed);
                }
            }

            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(movieName);
            if (extracted.HasProductCode)
            {
                Add(extracted.ProductCode);
                if (extracted.CidCandidates != null)
                {
                    foreach (string cid in extracted.CidCandidates)
                    {
                        Add(cid);
                    }
                }
            }
            else
            {
                Add(FromMovieName(movieName));
            }

            return variants;
        }
    }
}
